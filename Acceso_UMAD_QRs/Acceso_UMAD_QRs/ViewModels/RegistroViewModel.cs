using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SharedResources.Data;
using SharedResources.Models;
using System.Collections.ObjectModel;

namespace Acceso_UMAD_QRs.ViewModels
{
    public partial class RegistroViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<Registro> _registros;

        [ObservableProperty]
        private ObservableCollection<Usuario> _usuarios;

        [ObservableProperty]
        private string _puntoAcceso = string.Empty;

        [ObservableProperty]
        private DateTime _fechaHora = DateTime.Now;

        [ObservableProperty]
        private Usuario _usuario = null!;

        // Propiedades para SetupPuntoAccesoView
        [ObservableProperty]
        private ObservableCollection<string> _puntosDeAcceso = new() { "Entrada Principal", "Estacionamiento Norte", "Edificio Central", "Biblioteca" };

        [ObservableProperty]
        private string _puntoSeleccionado;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isPuntoAccesoValid = false;

        // Propiedades del Escáner
        [ObservableProperty]
        private int _estadoLectura = 0;

        [ObservableProperty]
        private string _mensajeResultado = "Apunta la cámara al código QR del usuario.";

        public bool IsFormValid => IsPuntoAccesoValid && Usuario != null;

        private readonly UmadDbContext _dataContext;

        public RegistroViewModel(UmadDbContext dataContext)
        {
            Registros = new ObservableCollection<Registro>();
            Usuarios = new ObservableCollection<Usuario>();
            _dataContext = dataContext;
        }

        public async Task GetRegistrosAsync()
        {
            Registros.Clear();
            var registrosFromDb = await _dataContext.Registros.Include(r => r.Usuario).AsNoTracking().ToListAsync();
            foreach (var registro in registrosFromDb)
            {
                Registros.Add(registro);
            }
        }

        public async Task GetUsuariosAsync()
        {
            Usuarios.Clear();
            Usuarios = new(await _dataContext.Usuarios.AsNoTracking().ToListAsync());
        }

        partial void OnPuntoAccesoChanged(string value)
        {
            IsPuntoAccesoValid = !string.IsNullOrWhiteSpace(value);
        }

        [RelayCommand]
        public async Task SaveRegistro()
        {
            // Asumiendo que es nuevo o se está editando uno existente basado en una condición
            // Aquí simplemente creamos uno nuevo para simplificar, o lo editamos si guardamos el Id en el VM
            await CreateRegistro();
            await Shell.Current.GoToAsync("..");
        }

        private async Task CreateRegistro()
        {
            Registro registro = new()
            {
                PuntoAcceso = this.PuntoAcceso,
                FechaHora = this.FechaHora,
                IdUsuario = this.Usuario.IdUsuario
            };

            await _dataContext.Registros.AddAsync(registro);
            await _dataContext.SaveChangesAsync();
        }

        public void LoadRegistroForEdition(Registro registro)
        {
            this.PuntoAcceso = registro.PuntoAcceso;
            this.FechaHora = registro.FechaHora;
            this.Usuario = this.Usuarios.FirstOrDefault(u => u.IdUsuario == registro.IdUsuario)!;
        }

        [RelayCommand]
        public async Task GoToAddPage()
        {
            await Shell.Current.GoToAsync("AddRegistroPage");
        }

        [RelayCommand]
        public async Task GoToEditPage(Registro registro)
        {
            await Shell.Current.GoToAsync("AddRegistroPage", new Dictionary<string, object> { ["Registro"] = registro });
        }

        [RelayCommand]
        public async Task DeleteRegistro(Registro registro)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("¿Estas seguro de que quieres eliminar este registro?", "Cancelar", "Eliminar");
            if (userAnswer == "Cancelar") return;

            var tracked = _dataContext.ChangeTracker.Entries<Registro>()
                            .FirstOrDefault(e => e.Entity.IdRegistro == registro.IdRegistro)?.Entity;

            var entityToDelete = tracked ?? await _dataContext.Registros.FindAsync(registro.IdRegistro);

            if (entityToDelete != null)
            {
                _dataContext.Registros.Remove(entityToDelete);
                await _dataContext.SaveChangesAsync();
                Registros = new(await _dataContext.Registros.Include(r => r.Usuario).AsNoTracking().ToListAsync());
            }
        }

        public async Task Initialize(Registro registro)
        {
            await GetUsuariosAsync();
            await GetRegistrosAsync();
            if (registro != null)
            {
                LoadRegistroForEdition(registro);
            }
        }

        [RelayCommand]
        private async Task SimularAccesoValido()
        {
            EstadoLectura = 1;
            MensajeResultado = "ACCESO PERMITIDO\nToken Válido.";

            // Registrar el acceso en la BD
            if (Usuarios.Any())
            {
                this.Usuario = Usuarios.First();
                this.PuntoAcceso = Preferences.Default.Get("PuntoAccesoActual", "Entrada Desconocida");
                this.FechaHora = DateTime.Now;
                await CreateRegistro();
                await GetRegistrosAsync();
            }

            await Task.Delay(3000);
            RestablecerEscaner();
        }

        [RelayCommand]
        private async Task SimularAccesoInvalido()
        {
            EstadoLectura = 2;
            MensajeResultado = "ACCESO DENEGADO\nToken Expirado o Inválido.";
            await Task.Delay(3000);
            RestablecerEscaner();
        }

        [RelayCommand]
        private async Task IniciarTurno()
        {
            if (string.IsNullOrEmpty(PuntoSeleccionado))
            {
                await Shell.Current.DisplayAlertAsync("Error", "Selecciona un punto de acceso primero.", "OK");
                return;
            }

            // Guardar localmente para que el escáner lo sepa
            Preferences.Default.Set("PuntoAccesoActual", PuntoSeleccionado);

            await Shell.Current.DisplayAlertAsync("Turno Iniciado", $"Registrando accesos en: {PuntoSeleccionado}", "OK");
            await Shell.Current.GoToAsync(nameof(Views.EscanerView));
        }

        public async Task ProcesarLecturaQR(string hashQr)
        {
            var token = await _dataContext.TokensAcceso
                                          .Include(t => t.Usuario)
                                          .FirstOrDefaultAsync(t => t.HashQr == hashQr);

            if (token != null && token.Activo && token.FechaExpiracion > DateTime.Now)
            {
                EstadoLectura = 1;
                MensajeResultado = $"ACCESO PERMITIDO\n{token.Usuario.NombreCompleto}";

                this.Usuario = token.Usuario;
                this.PuntoAcceso = Preferences.Default.Get("PuntoAccesoActual", "Entrada Desconocida");
                this.FechaHora = DateTime.Now;
                await CreateRegistro();
                await GetRegistrosAsync();
            }
            else
            {
                EstadoLectura = 2;
                MensajeResultado = "ACCESO DENEGADO\nToken Expirado o Inválido.";
            }

            await Task.Delay(3000);
            RestablecerEscaner();
        }

        private void RestablecerEscaner()
        {
            EstadoLectura = 0;
            MensajeResultado = "Apunta la cámara al código QR del usuario.";
        }
    }
}
