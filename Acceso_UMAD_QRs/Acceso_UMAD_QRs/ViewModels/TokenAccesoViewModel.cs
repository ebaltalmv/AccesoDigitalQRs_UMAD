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
        private ObservableCollection<TokenAccesoModel> _tokens;

        [ObservableProperty]
        private ObservableCollection<UsuarioModel> _usuarios;

        [ObservableProperty]
        private string _hashQr = string.Empty;

        [ObservableProperty]
        private DateTime _fechaExpiracion = DateTime.Now.AddDays(1);

        [ObservableProperty]
        private bool _activo = true;

        [ObservableProperty]
        private int _diasVigencia = 1;

        [ObservableProperty]
        private UsuarioModel _usuario = null!;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isHashValid = false;

        public bool IsFormValid => IsHashValid && Usuario != null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EsAprobado))]
        [NotifyPropertyChangedFor(nameof(EsPendiente))]
        [NotifyPropertyChangedFor(nameof(EsExpirado))]
        private string _estadoUsuario = "Aprobado";

        public bool EsAprobado => EstadoUsuario == "Aprobado";
        public bool EsPendiente => EstadoUsuario == "Pendiente";
        public bool EsExpirado => EstadoUsuario == "Expirado";

        [ObservableProperty]
        private ObservableCollection<UsuarioModel> _solicitudesPendientes;

        private readonly UmadDbContext _dataContext;


        public TokenAccesoViewModel(UmadDbContext dataContext)
        {
            Tokens = new ObservableCollection<TokenAccesoModel>();
            Usuarios = new ObservableCollection<UsuarioModel>();
            SolicitudesPendientes = new ObservableCollection<UsuarioModel>();
            _dataContext = dataContext;
        }

        public async Task GetTokensAsync()
        {
            try
            {
                Tokens.Clear();
                var tokensFromDb = await _dataContext.TokensAcceso.Include(t => t.Usuario).AsNoTracking().ToListAsync();
                foreach (var token in tokensFromDb)
                {
                    Tokens.Add(token);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Conexión", $"No se pudieron obtener los tokens: {ex.Message}", "OK");
            }
        }

        public async Task GetUsuariosAsync()
        {
            try
            {
                Usuarios.Clear();
                Usuarios = new(await _dataContext.Usuarios.AsNoTracking().ToListAsync());
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Conexión", $"No se pudieron obtener los usuarios: {ex.Message}", "OK");
            }
        }

        partial void OnHashQrChanged(string value)
        {
            IsHashValid = !string.IsNullOrWhiteSpace(value);
        }

        [RelayCommand]
        public async Task SaveToken()
        {
            try
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
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Base de Datos", $"No se pudo guardar el token: {ex.Message}", "OK");
            }
        }

        private async Task CreateToken()
        {
            try
            {
                TokenAccesoModel token = new()
                {
                    HashQr = this.HashQr,
                    FechaExpiracion = this.FechaExpiracion,
                    Activo = this.Activo,
                    IdUsuario = this.Usuario.IdUsuario
                };

                await _dataContext.TokensAcceso.AddAsync(token);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Base de Datos", $"No se pudo crear el token: {ex.Message}", "OK");
            }
        }

        private async Task EditToken(TokenAccesoModel foundToken)
        {
            try
            {
                foundToken.HashQr = this.HashQr;
                foundToken.FechaExpiracion = this.FechaExpiracion;
                foundToken.Activo = this.Activo;
                foundToken.IdUsuario = this.Usuario.IdUsuario;

                _dataContext.TokensAcceso.Update(foundToken);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Base de Datos", $"No se pudo editar el token: {ex.Message}", "OK");
            }
        }

        public void LoadTokenForEdition(TokenAccesoModel token)
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
        public async Task GoToEditPage(TokenAccesoModel token)
        {
            await Shell.Current.GoToAsync("AddTokenAccesoPage", new Dictionary<string, object> { ["TokenAcceso"] = token });
        }

        [RelayCommand]
        public async Task DeleteToken(TokenAccesoModel token)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("¿Estas seguro de que quieres eliminar este token?", "Cancelar", "Eliminar");
            if (userAnswer == "Cancelar") return;

            try
            {
                var tracked = _dataContext.ChangeTracker.Entries<TokenAccesoModel>()
                                .FirstOrDefault(e => e.Entity.IdToken == token.IdToken)?.Entity;

                var entityToDelete = tracked ?? await _dataContext.TokensAcceso.FindAsync(token.IdToken);

                if (entityToDelete != null)
                {
                    _dataContext.TokensAcceso.Remove(entityToDelete);
                    await _dataContext.SaveChangesAsync();
                    Tokens = new(await _dataContext.TokensAcceso.Include(t => t.Usuario).AsNoTracking().ToListAsync());
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Base de Datos", $"No se pudo eliminar el token: {ex.Message}", "OK");
            }
        }

        public async Task Initialize(TokenAccesoModel token)
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
            try
            {
                SolicitudesPendientes.Clear();
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
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Conexión", $"No se pudieron cargar las solicitudes: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        private async Task IrAGenerarToken(UsuarioModel user)
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

            this.HashQr = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
            this.FechaExpiracion = DiasVigencia > 0 ? DateTime.Now.AddDays(DiasVigencia) : DateTime.Now.AddYears(10);
            this.Activo = true;

            await CreateToken();

            await Shell.Current.DisplayAlertAsync("Éxito", $"El Token fue generado y asignado con éxito a {Usuario.NombreCompleto}.", "OK");

            await CargarSolicitudesPendientes();
            await Shell.Current.GoToAsync("..");
        }
    }
}
