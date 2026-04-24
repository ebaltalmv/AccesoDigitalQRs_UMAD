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
        private ObservableCollection<Usuario> _usuarios;

        [ObservableProperty]
        private ObservableCollection<Rol> _roles;

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
        private Rol _rol = null!;

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
        private bool _isMatriculaValid = true; // Por defecto es válido para visitantes

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
            Usuarios = new ObservableCollection<Usuario>();
            Roles = new ObservableCollection<Rol>();
            _dataContext = dataContext;
        }

        public async Task GetUsuariosAsync()
        {
            Usuarios.Clear();
            var usuariosFromDb = await _dataContext.Usuarios.Include(u => u.Rol).AsNoTracking().ToListAsync();
            foreach (var usuario in usuariosFromDb)
            {
                Usuarios.Add(usuario);
            }
        }

        public async Task GetRolesAsync()
        {
            Roles.Clear();
            Roles = new(await _dataContext.Roles.AsNoTracking().ToListAsync());
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

        partial void OnRolChanged(Rol value)
        {
            ValidateFields();
        }

        private void ValidateFields()
        {
            IsNombreValid = !string.IsNullOrWhiteSpace(NombreCompleto);

            // Validar Matrícula (Solo si no es Visitante, o si es Visitante pero escribió algo)
            if (Rol != null && Rol.NombreRol == "Visitante" && string.IsNullOrEmpty(Matricula))
            {
                IsMatriculaValid = true; // Visitante sin matrícula es válido
            }
            else
            {
                // Exactamente 8 dígitos numéricos
                IsMatriculaValid = !string.IsNullOrWhiteSpace(Matricula) && Regex.IsMatch(Matricula, @"^\d{8}$");
            }

            // Validar Correo
            bool formatoBaseValido = !string.IsNullOrWhiteSpace(Correo) && Regex.IsMatch(Correo, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (formatoBaseValido && Rol != null && Rol.NombreRol != "Visitante")
            {
                // Si es institucional (no Visitante), debe terminar en dominio específico
                IsCorreoValid = Correo.EndsWith("@umad.edu.mx", StringComparison.OrdinalIgnoreCase) ||
                                Correo.EndsWith("@madero.edu.mx", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                IsCorreoValid = formatoBaseValido;
            }

            // Validar Contraseña (letras, caracteres y numeros, aceptando mayusculas y minusculas)
            // Regex: Mínimo 8 caracteres, al menos 1 mayúscula, 1 minúscula, 1 número y 1 carácter especial
            IsContrasenaValid = !string.IsNullOrWhiteSpace(Contrasena) &&
                                Regex.IsMatch(Contrasena, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$");

            IsRolValid = Rol != null;
        }

        [RelayCommand]
        public async Task SaveUsuario()
        {
            var foundUsuario = await _dataContext.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Correo == this.Correo);
            if (foundUsuario != null)
            {
                await EditUsuario(foundUsuario);
                await App.Current!.MainPage!.DisplayAlert("Exito en la edición", "El registro se ha editado exitosamente", "OK");
                await App.Current!.MainPage!.Navigation.PopAsync();
                return;
            }
            await CreateUsuario();
            await App.Current!.MainPage!.DisplayAlert("Éxito", "Usuario registrado exitosamente", "OK");
            await App.Current!.MainPage!.Navigation.PopAsync();
        }

        private async Task CreateUsuario()
        {
            Usuario usuario = new()
            {
                Matricula = this.Matricula,
                NombreCompleto = this.NombreCompleto,
                Correo = this.Correo,
                Contrasena = this.Contrasena,
                IdRol = this.Rol.IdRol // Solo usar el Id para evitar conflictos de tracking
            };

            await _dataContext.Usuarios.AddAsync(usuario);
            await _dataContext.SaveChangesAsync();
        }

        private async Task EditUsuario(Usuario foundUsuario)
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

        public void LoadUsuarioForEdition(Usuario usuario)
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
        public async Task GoToEditPage(Usuario usuario)
        {
            await Shell.Current.GoToAsync("AddUsuarioPage", new Dictionary<string, object> { ["Usuario"] = usuario });
        }

        [RelayCommand]
        public async Task DeleteUsuario(Usuario usuario)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("¿Estas seguro de que quieres eliminar este usuario?", "Cancelar", "Eliminar");
            if (userAnswer == "Cancelar") return;

            var tracked = _dataContext.ChangeTracker.Entries<Usuario>()
                            .FirstOrDefault(e => e.Entity.IdUsuario == usuario.IdUsuario)?.Entity;

            var entityToDelete = tracked ?? await _dataContext.Usuarios.FindAsync(usuario.IdUsuario);

            if (entityToDelete != null)
            {
                _dataContext.Usuarios.Remove(entityToDelete);
                await _dataContext.SaveChangesAsync();
                Usuarios = new(await _dataContext.Usuarios.Include(u => u.Rol).AsNoTracking().ToListAsync());
            }
        }

        public async Task Initialize(Usuario usuario)
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
                await App.Current!.MainPage!.DisplayAlert("Error", "Ingresa correo y contraseña", "OK");
                return;
            }

            var usuario = await _dataContext.Usuarios
                .Include(u => u.Rol)
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Correo == Correo && u.Contrasena == Contrasena);

            if (usuario != null)
            {
                // Iniciar sesión exitoso
                App.UsuarioActual = usuario;

                // Cambiamos la raíz de la aplicación a AppShell para cargar el menú principal
                App.Current!.MainPage = new AppShell();
            }
            else
            {
                await App.Current!.MainPage!.DisplayAlert("Error", "Credenciales incorrectas", "OK");
            }
        }

        [RelayCommand]
        public async Task IrARegistro()
        {
            // Navegar a la página de registro desde el Login
            // Como estamos en NavigationPage, usamos Navigation.PushAsync
            var registroView = Application.Current!.Handler!.MauiContext!.Services.GetService<Views.RegistroView>();
            await Application.Current.MainPage!.Navigation.PushAsync(registroView);
        }
    }
}
