using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SharedResources.Data;
using SharedResources.Models;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Acceso_UMAD_QRs.ViewModels
{
    public partial class UsuarioViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<UsuarioModel> _usuarios;

        [ObservableProperty]
        private ObservableCollection<RolModel> _roles;

        [ObservableProperty]
        private string _matricula = string.Empty;

        [ObservableProperty]
        private string _nombreCompleto = string.Empty;

        [ObservableProperty]
        private string _correo = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private string _contrasena = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private RolModel _rol = null!;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isNombreValid = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        [NotifyPropertyChangedFor(nameof(EsCorreoInvalido))]
        private bool _isCorreoValid = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        [NotifyPropertyChangedFor(nameof(EsMatriculaInvalida))]
        private bool _isMatriculaValid = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        [NotifyPropertyChangedFor(nameof(EsContrasenaInvalida))]
        private bool _isContrasenaValid = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isRolValid = false;

        public bool EsCorreoInvalido => !IsCorreoValid && !string.IsNullOrEmpty(Correo);
        public bool EsMatriculaInvalida => !IsMatriculaValid && !string.IsNullOrEmpty(Matricula);
        public bool EsContrasenaInvalida => !IsContrasenaValid && !string.IsNullOrEmpty(Contrasena);

        public bool IsFormValid => IsNombreValid && IsCorreoValid && IsContrasenaValid && IsRolValid && IsMatriculaValid;

        private readonly UmadDbContext _dataContext;

        public UsuarioViewModel(UmadDbContext dataContext)
        {
            Usuarios = new ObservableCollection<UsuarioModel>();
            Roles = new ObservableCollection<RolModel>();
            _dataContext = dataContext;
        }

        public async Task GetUsuariosAsync()
        {
            try
            {
                Usuarios.Clear();
                var usuariosFromDb = await _dataContext.Usuarios.Include(u => u.Rol).AsNoTracking().ToListAsync();
                foreach (var usuario in usuariosFromDb)
                {
                    Usuarios.Add(usuario);
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page != null)
                    await Application.Current.Windows[0].Page!.DisplayAlertAsync("Error de Conexión", $"No se pudieron obtener los usuarios: {ex.Message}", "OK");
            }
        }

        public async Task GetRolesAsync()
        {
            try
            {
                Roles.Clear();
                Roles = new(await _dataContext.Roles.AsNoTracking().ToListAsync());
            }
            catch (Exception ex)
            {
                if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page != null)
                    await Application.Current.Windows[0].Page!.DisplayAlertAsync("Error de Conexión", $"No se pudieron obtener los roles: {ex.Message}", "OK");
            }
        }

        partial void OnNombreCompletoChanged(string value)
        {
            ValidateFields();
        }

        partial void OnCorreoChanged(string value)
        {
            ValidateFields();
        }

        partial void OnMatriculaChanged(string value)
        {
            ValidateFields();
        }

        partial void OnContrasenaChanged(string value)
        {
            ValidateFields();
        }

        partial void OnRolChanged(RolModel value)
        {
            ValidateFields();
        }

        private void ValidateFields()
        {
            IsNombreValid = !string.IsNullOrWhiteSpace(NombreCompleto);

            if (Rol != null && Rol.NombreRol == "Visitante" && string.IsNullOrEmpty(Matricula))
            {
                IsMatriculaValid = true;
            }
            else
            {
                IsMatriculaValid = !string.IsNullOrWhiteSpace(Matricula) && Regex.IsMatch(Matricula, @"^\d{8}$");
            }

            bool formatoBaseValido = !string.IsNullOrWhiteSpace(Correo) && Regex.IsMatch(Correo, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (formatoBaseValido && Rol != null && Rol.NombreRol != "Visitante")
            {
                IsCorreoValid = Correo.EndsWith("@umad.edu.mx", StringComparison.OrdinalIgnoreCase) ||
                                Correo.EndsWith("@madero.edu.mx", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                IsCorreoValid = formatoBaseValido;
            }

            IsContrasenaValid = !string.IsNullOrWhiteSpace(Contrasena) &&
                                Regex.IsMatch(Contrasena, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$");

            IsRolValid = Rol != null;
        }

        [RelayCommand]
        public async Task SaveUsuario()
        {
            try
            {
                var foundUsuario = await _dataContext.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Correo == this.Correo);
                if (foundUsuario != null)
                {
                    await EditUsuario(foundUsuario);
                    await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Exito en la edición", "El registro se ha editado exitosamente", "OK");
                    await Application.Current!.Windows[0].Page!.Navigation.PopAsync();
                    return;
                }
                await CreateUsuario();
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Éxito", "Usuario registrado exitosamente", "OK");
                await Application.Current!.Windows[0].Page!.Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error de Base de Datos", $"No se pudo guardar el usuario: {ex.Message}", "OK");
            }
        }

        private async Task CreateUsuario()
        {
            UsuarioModel usuario = new()
            {
                Matricula = this.Matricula,
                NombreCompleto = this.NombreCompleto,
                Correo = this.Correo,
                Contrasena = this.Contrasena,
                IdRol = this.Rol.IdRol
            };

            await _dataContext.Usuarios.AddAsync(usuario);
            await _dataContext.SaveChangesAsync();
        }

        private async Task EditUsuario(UsuarioModel foundUsuario)
        {
            foundUsuario.Matricula = this.Matricula;
            foundUsuario.NombreCompleto = this.NombreCompleto;
            if (!string.IsNullOrEmpty(this.Contrasena))
            {
                foundUsuario.Contrasena = this.Contrasena;
            }
            foundUsuario.IdRol = this.Rol.IdRol;

            _dataContext.Usuarios.Update(foundUsuario);
            await _dataContext.SaveChangesAsync();
        }

        public void LoadUsuarioForEdition(UsuarioModel usuario)
        {
            this.Matricula = usuario.Matricula ?? string.Empty;
            this.NombreCompleto = usuario.NombreCompleto;
            this.Correo = usuario.Correo;
            this.Rol = this.Roles.FirstOrDefault(r => r.IdRol == usuario.IdRol)!;
        }

        [RelayCommand]
        public async Task GoToAddPage()
        {
            await Shell.Current.GoToAsync("AddUsuarioPage");
        }

        [RelayCommand]
        public async Task GoToEditPage(UsuarioModel usuario)
        {
            await Shell.Current.GoToAsync("AddUsuarioPage", new Dictionary<string, object> { ["Usuario"] = usuario });
        }

        [RelayCommand]
        public async Task DeleteUsuario(UsuarioModel usuario)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("¿Estas seguro de que quieres eliminar este usuario?", "Cancelar", "Eliminar");
            if (userAnswer == "Cancelar") return;

            try
            {
                var tracked = _dataContext.ChangeTracker.Entries<UsuarioModel>()
                                .FirstOrDefault(e => e.Entity.IdUsuario == usuario.IdUsuario)?.Entity;

                var entityToDelete = tracked ?? await _dataContext.Usuarios.FindAsync(usuario.IdUsuario);

                if (entityToDelete != null)
                {
                    _dataContext.Usuarios.Remove(entityToDelete);
                    await _dataContext.SaveChangesAsync();
                    Usuarios = new(await _dataContext.Usuarios.Include(u => u.Rol).AsNoTracking().ToListAsync());
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Base de Datos", $"No se pudo eliminar el usuario: {ex.Message}", "OK");
            }
        }

        public async Task Initialize(UsuarioModel usuario)
        {
            await GetRolesAsync();
            await GetUsuariosAsync();
            if (usuario != null)
            {
                LoadUsuarioForEdition(usuario);
            }
        }

        [RelayCommand]
        public async Task IniciarSesion()
        {
            if (string.IsNullOrWhiteSpace(Correo) || string.IsNullOrWhiteSpace(Contrasena))
            {
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error", "Ingresa correo y contraseña", "OK");
                return;
            }

            try
            {
                var usuario = await _dataContext.Usuarios
                    .Include(u => u.Rol)
                    .AsNoTracking()
                    .SingleOrDefaultAsync(u => u.Correo == Correo && u.Contrasena == Contrasena);

                if (usuario != null)
                {
                    App.UsuarioActual = usuario;

                    Application.Current!.Windows[0].Page = new AppShell();
                }
                else
                {
                    await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error", "Credenciales incorrectas", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error de Conexión", $"Problema al conectar con la base de datos: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        public async Task IrARegistro()
        {
            var registroView = Application.Current!.Handler!.MauiContext!.Services.GetService<Views.RegistroView>();
            await Application.Current!.Windows[0].Page!.Navigation.PushAsync(registroView);
        }
    }
}
