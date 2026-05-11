using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SharedResources.Data;
using SharedResources.Models;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace UMADAccesos.ViewModels
{
    public partial class UserViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<UserModel> _users;

        [ObservableProperty]
        private ObservableCollection<RoleModel> _roles;

        [ObservableProperty]
        private string _studentId = string.Empty;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private string _password = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private RoleModel _role = new RoleModel();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isNameValid = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        [NotifyPropertyChangedFor(nameof(IsEmailInvalid))]
        private bool _isEmailValid = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        [NotifyPropertyChangedFor(nameof(IsStudentIdInvalid))]
        private bool _isStudentIdValid = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        [NotifyPropertyChangedFor(nameof(IsPasswordInvalid))]
        private bool _isPasswordValid = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isRoleValid = false;

        public bool IsEmailInvalid => !IsEmailValid && !string.IsNullOrEmpty(Email);
        public bool IsStudentIdInvalid => !IsStudentIdValid && !string.IsNullOrEmpty(StudentId);
        public bool IsPasswordInvalid => !IsPasswordValid && !string.IsNullOrEmpty(Password);

        public bool IsFormValid => IsNameValid && IsEmailValid && IsPasswordValid && IsRoleValid && IsStudentIdValid;

        private readonly UmadDbContext _dataContext;

        public UserViewModel(UmadDbContext dataContext)
        {
            Users = new ObservableCollection<UserModel>();
            Roles = new ObservableCollection<RoleModel>();
            _dataContext = dataContext;
        }

        public async Task GetUsersAsync()
        {
            try
            {
                Users.Clear();
                var usersFromDb = await _dataContext.Users.Include(u => u.Role).AsNoTracking().ToListAsync();
                foreach (var user in usersFromDb)
                {
                    Users.Add(user);
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page != null)
                    await Application.Current.Windows[0].Page!.DisplayAlertAsync("Connection Error", $"Could not fetch users: {ex.Message}", "OK");
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
                    await Application.Current.Windows[0].Page!.DisplayAlertAsync("Connection Error", $"Could not fetch roles: {ex.Message}", "OK");
            }
        }

        partial void OnFullNameChanged(string value)
        {
            ValidateFields();
        }

        partial void OnEmailChanged(string value)
        {
            ValidateFields();
        }

        partial void OnStudentIdChanged(string value)
        {
            ValidateFields();
        }

        partial void OnPasswordChanged(string value)
        {
            ValidateFields();
        }

        partial void OnRoleChanged(RoleModel value)
        {
            ValidateFields();
        }

        private void ValidateFields()
        {
            IsNameValid = !string.IsNullOrWhiteSpace(FullName);

            if (Role != null && Role.RoleName == "Visitante" && string.IsNullOrEmpty(StudentId))
            {
                IsStudentIdValid = true;
            }
            else
            {
                IsStudentIdValid = !string.IsNullOrWhiteSpace(StudentId) && Regex.IsMatch(StudentId, @"^\d{8}$");
            }

            bool isBaseFormatValid = !string.IsNullOrWhiteSpace(Email) && Regex.IsMatch(Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (isBaseFormatValid && Role != null && Role.RoleName != "Visitante")
            {
                IsEmailValid = Email.EndsWith("@umad.edu.mx", StringComparison.OrdinalIgnoreCase) ||
                                Email.EndsWith("@madero.edu.mx", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                IsEmailValid = isBaseFormatValid;
            }

            IsPasswordValid = !string.IsNullOrWhiteSpace(Password) &&
                                Regex.IsMatch(Password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$");

            IsRoleValid = Role != null;
        }

        [RelayCommand]
        public async Task SaveUser()
        {
            try
            {
                var foundUser = await _dataContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == this.Email);
                if (foundUser != null)
                {
                    await EditUser(foundUser);
                    await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Edit Success", "The record has been edited successfully", "OK");
                    await Application.Current!.Windows[0].Page!.Navigation.PopAsync();
                    return;
                }
                await CreateUser();
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Success", "User registered successfully", "OK");
                await Application.Current!.Windows[0].Page!.Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Database Error", $"Could not save user: {ex.Message}", "OK");
            }
        }

        private async Task CreateUser()
        {
            UserModel user = new()
            {
                StudentId = this.StudentId,
                FullName = this.FullName,
                Email = this.Email,
                Password = this.Password,
                IdRole = this.Role.IdRole
            };

            await _dataContext.Users.AddAsync(user);
            await _dataContext.SaveChangesAsync();
        }

        private async Task EditUser(UserModel foundUser)
        {
            foundUser.StudentId = this.StudentId;
            foundUser.FullName = this.FullName;
            if (!string.IsNullOrEmpty(this.Password))
            {
                foundUser.Password = this.Password;
            }
            foundUser.IdRole = this.Role.IdRole;

            _dataContext.Users.Update(foundUser);
            await _dataContext.SaveChangesAsync();
        }

        public void LoadUserForEdition(UserModel user)
        {
            this.StudentId = user.StudentId ?? string.Empty;
            this.FullName = user.FullName;
            this.Email = user.Email;
            this.Role = this.Roles.FirstOrDefault(r => r.IdRole == user.IdRole)!;
        }

        [RelayCommand]
        public async Task GoToAddPage()
        {
            await Shell.Current.GoToAsync("AddUserPage");
        }

        [RelayCommand]
        public async Task GoToEditPage(UserModel user)
        {
            await Shell.Current.GoToAsync("AddUserPage", new Dictionary<string, object> { ["User"] = user });
        }

        [RelayCommand]
        public async Task DeleteUser(UserModel user)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("Are you sure you want to delete this user?", "Cancel", "Delete");
            if (userAnswer == "Cancel") return;

            try
            {
                var tracked = _dataContext.ChangeTracker.Entries<UserModel>()
                                .FirstOrDefault(e => e.Entity.IdUser == user.IdUser)?.Entity;

                var entityToDelete = tracked ?? await _dataContext.Users.FindAsync(user.IdUser);

                if (entityToDelete != null)
                {
                    _dataContext.Users.Remove(entityToDelete);
                    await _dataContext.SaveChangesAsync();
                    Users = new(await _dataContext.Users.Include(u => u.Role).AsNoTracking().ToListAsync());
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Database Error", $"Could not delete user: {ex.Message}", "OK");
            }
        }

        public async Task Initialize(UserModel user)
        {
            await GetRolesAsync();
            await GetUsersAsync();
            if (user != null)
            {
                LoadUserForEdition(user);
            }
        }

        [RelayCommand]
        public async Task Login()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error", "Enter email and password", "OK");
                return;
            }

            try
            {
                var user = await _dataContext.Users
                    .Include(u => u.Role)
                    .AsNoTracking()
                    .SingleOrDefaultAsync(u => u.Email == Email && u.Password == Password);

                if (user != null)
                {
                    App.CurrentUser = user;

                    Application.Current!.Windows[0].Page = new AppShell();
                }
                else
                {
                    await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Error", "Invalid credentials", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current!.Windows[0].Page!.DisplayAlertAsync("Connection Error", $"Problem connecting to database: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        public async Task GoToSignUp()
        {
            var signUpView = Application.Current!.Handler!.MauiContext!.Services.GetService<Views.SignUpView>();
            await Application.Current!.Windows[0].Page!.Navigation.PushAsync(signUpView);
        }
    }
}
