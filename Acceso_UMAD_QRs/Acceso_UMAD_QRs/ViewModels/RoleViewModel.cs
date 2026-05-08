using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SharedResources.Data;
using SharedResources.Models;
using System.Collections.ObjectModel;

namespace Acceso_UMAD_QRs.ViewModels
{
    public partial class RoleViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<RoleModel> _roles;

        [ObservableProperty]
        private string _roleName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isNameValid = false;

        public bool IsFormValid => IsNameValid;

        private readonly UmadDbContext _dataContext;

        public RoleViewModel(UmadDbContext dataContext)
        {
            Roles = new ObservableCollection<RoleModel>();
            _dataContext = dataContext;
        }

        public async Task GetRolesAsync()
        {
            try
            {
                Roles.Clear();
                var rolesFromDb = await _dataContext.Roles.AsNoTracking().ToListAsync();
                foreach (var role in rolesFromDb)
                {
                    Roles.Add(role);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Connection Error", $"Could not fetch roles: {ex.Message}", "OK");
            }
        }

        partial void OnRoleNameChanged(string value)
        {
            ValidateName(value);
        }

        private void ValidateName(string value)
        {
            IsNameValid = !string.IsNullOrWhiteSpace(value);
        }

        [RelayCommand]
        public async Task SaveRole()
        {
            try
            {
                var foundRole = await _dataContext.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.RoleName == this.RoleName);
                if (foundRole != null)
                {
                    await EditRole(foundRole);
                    await Shell.Current.DisplayAlertAsync("Edit Success", "The record has been edited successfully", "OK");
                    await Shell.Current.GoToAsync("..");
                    return;
                }
                await CreateRole();
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Database Error", $"Could not save role: {ex.Message}", "OK");
            }
        }

        private async Task CreateRole()
        {
            try
            {
                RoleModel role = new()
                {
                    RoleName = this.RoleName
                };

                await _dataContext.Roles.AddAsync(role);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Database Error", $"Could not create role: {ex.Message}", "OK");
            }
        }

        private async Task EditRole(RoleModel foundRole)
        {
            try
            {
                foundRole.RoleName = this.RoleName;

                _dataContext.Roles.Update(foundRole);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Database Error", $"Could not edit role: {ex.Message}", "OK");
            }
        }

        public void LoadRoleForEdition(RoleModel role)
        {
            this.RoleName = role.RoleName;
        }

        [RelayCommand]
        public async Task GoToAddPage()
        {
            await Shell.Current.GoToAsync("AddRolePage");
        }

        [RelayCommand]
        public async Task GoToEditPage(RoleModel role)
        {
            await Shell.Current.GoToAsync("AddRolePage", new Dictionary<string, object> { ["Role"] = role });
        }

        [RelayCommand]
        public async Task DeleteRole(RoleModel role)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("Are you sure you want to delete this role?", "Cancel", "Delete");
            if (userAnswer == "Cancel") return;

            try
            {
                var tracked = _dataContext.ChangeTracker.Entries<RoleModel>()
                                .FirstOrDefault(e => e.Entity.IdRole == role.IdRole)?.Entity;

                var entityToDelete = tracked ?? await _dataContext.Roles.FindAsync(role.IdRole);

                if (entityToDelete != null)
                {
                    _dataContext.Roles.Remove(entityToDelete);
                    await _dataContext.SaveChangesAsync();
                    Roles = new(await _dataContext.Roles.AsNoTracking().ToListAsync());
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Database Error", $"Could not delete role: {ex.Message}", "OK");
            }
        }

        public async Task Initialize(RoleModel role)
        {
            await GetRolesAsync();
            if (role != null)
            {
                LoadRoleForEdition(role);
            }
        }
    }
}
