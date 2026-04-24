using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SharedResources.Data;
using SharedResources.Models;
using System.Collections.ObjectModel;

namespace Acceso_UMAD_QRs.ViewModels
{
    [QueryProperty(nameof(Usuario), "Usuario")]
    public partial class TokenAccesoViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<TokenAcceso> _tokens;

        [ObservableProperty]
        private ObservableCollection<Usuario> _usuarios;

        [ObservableProperty]
        private string _hashQr = string.Empty;

        [ObservableProperty]
        private DateTime _fechaExpiracion = DateTime.Now.AddDays(1);

        [ObservableProperty]
        private bool _activo = true;

        [ObservableProperty]
        private int _diasVigencia = 1;

        [ObservableProperty]
        private Usuario _usuario = null!;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isHashValid = false;

        public bool IsFormValid => IsHashValid && Usuario != null;

        // --- Propiedades para "Mi QR" ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EsAprobado))]
        [NotifyPropertyChangedFor(nameof(EsPendiente))]
        [NotifyPropertyChangedFor(nameof(EsExpirado))]
        private string _estadoUsuario = "Aprobado";

        public bool EsAprobado => EstadoUsuario == "Aprobado";
        public bool EsPendiente => EstadoUsuario == "Pendiente";
        public bool EsExpirado => EstadoUsuario == "Expirado";

        // --- Propiedades para "Aprobaciones" ---
        // Lista de usuarios sin token activo
        [ObservableProperty]
        private ObservableCollection<Usuario> _solicitudesPendientes;

        private readonly UmadDbContext _dataContext;

        public TokenAccesoViewModel(UmadDbContext dataContext)
        {
            Tokens = new ObservableCollection<TokenAcceso>();
            Usuarios = new ObservableCollection<Usuario>();
            SolicitudesPendientes = new ObservableCollection<Usuario>();
            _dataContext = dataContext;
        }

        public async Task GetTokensAsync()
        {
            Tokens.Clear();
            var tokensFromDb = await _dataContext.TokensAcceso.Include(t => t.Usuario).AsNoTracking().ToListAsync();
            foreach (var token in tokensFromDb)
            {
                Tokens.Add(token);
            }
        }

        public async Task GetUsuariosAsync()
        {
            Usuarios.Clear();
            Usuarios = new(await _dataContext.Usuarios.AsNoTracking().ToListAsync());
        }

        partial void OnHashQrChanged(string value)
        {
            IsHashValid = !string.IsNullOrWhiteSpace(value);
        }

        [RelayCommand]
        public async Task SaveToken()
        {
            var foundToken = await _dataContext.TokensAcceso.AsNoTracking().SingleOrDefaultAsync(t => t.HashQr == this.HashQr);
            if (foundToken != null)
            {
                await EditToken(foundToken);
                await Shell.Current.DisplayAlertAsync("Exito en la edición", "El registro se ha editado exitosamente", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }
            await CreateToken();
            await Shell.Current.GoToAsync("..");
        }

        private async Task CreateToken()
        {
            TokenAcceso token = new()
            {
                HashQr = this.HashQr,
                FechaExpiracion = this.FechaExpiracion,
                Activo = this.Activo,
                IdUsuario = this.Usuario.IdUsuario
            };

            await _dataContext.TokensAcceso.AddAsync(token);
            await _dataContext.SaveChangesAsync();
        }

        private async Task EditToken(TokenAcceso foundToken)
        {
            foundToken.HashQr = this.HashQr;
            foundToken.FechaExpiracion = this.FechaExpiracion;
            foundToken.Activo = this.Activo;
            foundToken.IdUsuario = this.Usuario.IdUsuario;

            _dataContext.TokensAcceso.Update(foundToken);
            await _dataContext.SaveChangesAsync();
        }

        public void LoadTokenForEdition(TokenAcceso token)
        {
            this.HashQr = token.HashQr;
            this.FechaExpiracion = token.FechaExpiracion;
            this.Activo = token.Activo;
            this.Usuario = this.Usuarios.FirstOrDefault(u => u.IdUsuario == token.IdUsuario)!;
        }

        [RelayCommand]
        public async Task GoToAddPage()
        {
            await Shell.Current.GoToAsync("AddTokenAccesoPage");
        }

        [RelayCommand]
        public async Task GoToEditPage(TokenAcceso token)
        {
            await Shell.Current.GoToAsync("AddTokenAccesoPage", new Dictionary<string, object> { ["TokenAcceso"] = token });
        }

        [RelayCommand]
        public async Task DeleteToken(TokenAcceso token)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("¿Estas seguro de que quieres eliminar este token?", "Cancelar", "Eliminar");
            if (userAnswer == "Cancelar") return;

            var tracked = _dataContext.ChangeTracker.Entries<TokenAcceso>()
                            .FirstOrDefault(e => e.Entity.IdToken == token.IdToken)?.Entity;

            var entityToDelete = tracked ?? await _dataContext.TokensAcceso.FindAsync(token.IdToken);

            if (entityToDelete != null)
            {
                _dataContext.TokensAcceso.Remove(entityToDelete);
                await _dataContext.SaveChangesAsync();
                Tokens = new(await _dataContext.TokensAcceso.Include(t => t.Usuario).AsNoTracking().ToListAsync());
            }
        }

        public async Task Initialize(TokenAcceso token)
        {
            await GetUsuariosAsync();
            await GetTokensAsync();
            await CargarSolicitudesPendientes();
            CargarMiToken();

            if (token != null)
            {
                LoadTokenForEdition(token);
            }
        }

        private void CargarMiToken()
        {
            if (App.UsuarioActual != null)
            {
                var miToken = Tokens.OrderByDescending(t => t.FechaExpiracion).FirstOrDefault(t => t.IdUsuario == App.UsuarioActual.IdUsuario);
                if (miToken != null)
                {
                    this.HashQr = miToken.HashQr;
                    this.FechaExpiracion = miToken.FechaExpiracion;
                    this.EstadoUsuario = miToken.Activo && miToken.FechaExpiracion > DateTime.Now ? "Aprobado" : "Expirado";
                }
                else
                {
                    this.EstadoUsuario = "Pendiente";
                }
            }
        }

        public async Task CargarSolicitudesPendientes()
        {
            SolicitudesPendientes.Clear();
            // Buscar usuarios que no tienen tokens activos
            var usuariosSinToken = await _dataContext.Usuarios
                .Include(u => u.Rol)
                .Where(u => !u.Tokens.Any(t => t.Activo && t.FechaExpiracion > DateTime.Now))
                .AsNoTracking()
                .ToListAsync();

            foreach (var u in usuariosSinToken)
            {
                SolicitudesPendientes.Add(u);
            }
        }

        [RelayCommand]
        private async Task IrAGenerarToken(Usuario user)
        {
            await Shell.Current.GoToAsync(nameof(Views.GeneradorAccesoView), new Dictionary<string, object>
            {
                { "Usuario", user }
            });
        }

        [RelayCommand]
        private async Task SolicitarRenovacion()
        {
            await Shell.Current.DisplayAlertAsync("Solicitud", "Tu solicitud de renovación ha sido enviada.", "OK");
            this.EstadoUsuario = "Pendiente";
        }
        [RelayCommand]
        private async Task GenerarToken()
        {
            if (this.Usuario == null)
            {
                await Shell.Current.DisplayAlertAsync("Error", "No se ha seleccionado un usuario para generar el token.", "OK");
                return;
            }

            this.HashQr = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(); // Hash aleatorio

            // Si son 0 días, asumimos Permanente (ej. 10 años)
            this.FechaExpiracion = DiasVigencia > 0 ? DateTime.Now.AddDays(DiasVigencia) : DateTime.Now.AddYears(10);
            this.Activo = true;

            await CreateToken();

            await Shell.Current.DisplayAlertAsync("Éxito", $"El Token fue generado y asignado con éxito a {Usuario.NombreCompleto}.", "OK");

            // Refrescar lista de aprobaciones y regresar
            await CargarSolicitudesPendientes();
            await Shell.Current.GoToAsync("..");
        }
    }
}
