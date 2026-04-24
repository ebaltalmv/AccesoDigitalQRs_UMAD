using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SharedResources.Data;
using SharedResources.Models;
using System.Collections.ObjectModel;

namespace Acceso_UMAD_QRs.ViewModels
{
    public partial class RolViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<Rol> _roles;

        [ObservableProperty]
        private string _nombreRol = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isNombreValid = false;

        public bool IsFormValid => IsNombreValid;

        private readonly UmadDbContext _dataContext;

        public RolViewModel(UmadDbContext dataContext)
        {
            Roles = new ObservableCollection<Rol>();
            _dataContext = dataContext;
        }

        public async Task GetRolesAsync()
        {
            Roles.Clear();
            var rolesFromDb = await _dataContext.Roles.AsNoTracking().ToListAsync();
            foreach (var rol in rolesFromDb)
            {
                Roles.Add(rol);
            }
        }

        partial void OnNombreRolChanged(string value)
        {
            ValidateNombre(value);
        }

        private void ValidateNombre(string value)
        {
            IsNombreValid = !string.IsNullOrWhiteSpace(value);
        }

        [RelayCommand]
        public async Task SaveRol()
        {
            // Busca si ya existe un rol con ese nombre para editarlo (o se podría buscar por Id)
            var foundRol = await _dataContext.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.NombreRol == this.NombreRol);
            if (foundRol != null)
            {
                await EditRol(foundRol);
                await Shell.Current.DisplayAlertAsync("Exito en la edición", "El registro se ha editado exitosamente", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }
            await CreateRol();
            await Shell.Current.GoToAsync("..");
        }

        private async Task CreateRol()
        {
            Rol rol = new()
            {
                NombreRol = this.NombreRol
            };

            await _dataContext.Roles.AddAsync(rol);
            await _dataContext.SaveChangesAsync();
        }

        private async Task EditRol(Rol foundRol)
        {
            foundRol.NombreRol = this.NombreRol;

            _dataContext.Roles.Update(foundRol);
            await _dataContext.SaveChangesAsync();
        }

        public void LoadRolForEdition(Rol rol)
        {
            this.NombreRol = rol.NombreRol;
        }

        [RelayCommand]
        public async Task GoToAddPage()
        {
            await Shell.Current.GoToAsync("AddRolPage");
        }

        [RelayCommand]
        public async Task GoToEditPage(Rol rol)
        {
            await Shell.Current.GoToAsync("AddRolPage", new Dictionary<string, object> { ["Rol"] = rol });
        }

        [RelayCommand]
        public async Task DeleteRol(Rol rol)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("¿Estas seguro de que quieres eliminar este rol?", "Cancelar", "Eliminar");
            if (userAnswer == "Cancelar") return;

            var tracked = _dataContext.ChangeTracker.Entries<Rol>()
                            .FirstOrDefault(e => e.Entity.IdRol == rol.IdRol)?.Entity;

            var entityToDelete = tracked ?? await _dataContext.Roles.FindAsync(rol.IdRol);

            if (entityToDelete != null)
            {
                _dataContext.Roles.Remove(entityToDelete);
                await _dataContext.SaveChangesAsync();
                Roles = new(await _dataContext.Roles.AsNoTracking().ToListAsync());
            }
        }

        public async Task Initialize(Rol rol)
        {
            await GetRolesAsync();
            if (rol != null)
            {
                LoadRolForEdition(rol);
            }
        }
    }
}
