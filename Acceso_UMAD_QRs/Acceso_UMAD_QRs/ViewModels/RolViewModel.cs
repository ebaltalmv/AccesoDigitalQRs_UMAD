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
        private ObservableCollection<RolModel> _roles;

        [ObservableProperty]
        private string _nombreRol = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isNombreValid = false;

        public bool IsFormValid => IsNombreValid;

        private readonly UmadDbContext _dataContext;

        /// <summary>
        /// Inicializa una nueva instancia de la clase RolViewModel utilizando el contexto de datos especificado.
        /// </summary>
        /// <param name="dataContext">El contexto de base de datos que se utilizará para acceder y administrar los datos relacionados con roles. No puede ser nulo.</param>
        public RolViewModel(UmadDbContext dataContext)
        {
            Roles = new ObservableCollection<RolModel>();
            _dataContext = dataContext;
        }

        /// <summary>
        /// Recupera de forma asíncrona todos los roles de la fuente de datos y actualiza la colección local.
        /// </summary>
        /// <remarks>La colección local se vacía antes de rellenarse con los roles más recientes de la
        /// fuente de datos. Se recomienda esperar a que finalice este método para asegurar que los roles se cargue completamente antes de continuar con el procesamiento.</remarks>
        /// <returns>Una tarea que representa la operación asíncrona.</returns>
        public async Task GetRolesAsync()
        {
            Roles.Clear();
            var rolesFromDb = await _dataContext.Roles.AsNoTracking().ToListAsync();
            foreach (var rol in rolesFromDb)
            {
                Roles.Add(rol);
            }
        }

        /// <summary>
        /// Se invoca cuando cambia el valor del nombre del rol para realizar acciones adicionales, como la validación.
        /// </summary>
        /// <param name="value">El nuevo valor asignado al nombre del rol. No puede ser nulo.</param>
        partial void OnNombreRolChanged(string value)
        {
            ValidateNombre(value);
        }

        /// <summary>
        /// Valida el valor proporcionado para determinar si el nombre es válido.
        /// </summary>
        /// <remarks>Establece la propiedad IsNombreValid en función de si el valor no es nulo, vacío ni
        /// contiene solo espacios en blanco.</remarks>
        /// <param name="value">El valor del nombre que se va a validar. Puede ser nulo o una cadena vacía.</param>
        private void ValidateNombre(string value)
        {
            IsNombreValid = !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Guarda un rol nuevo o actualiza uno existente según el nombre especificado.
        /// </summary>
        /// <remarks>Si ya existe un rol con el mismo nombre, se actualiza el registro existente y se
        /// muestra una notificación de éxito. Si no existe, se crea un nuevo rol. Al finalizar, la navegación regresa a
        /// la pantalla anterior.</remarks>
        /// <returns>Una tarea que representa la operación asincrónica de guardar o editar el rol.</returns>
        [RelayCommand]
        public async Task SaveRol()
        {
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

        /// <summary>
        /// Crea un nuevo rol en el contexto de datos utilizando el valor actual de NombreRol.
        /// </summary>
        /// <remarks>Este método agrega un nuevo rol al almacén de datos subyacente y guarda los cambios
        /// de forma asíncrona. Diseñado para uso interno dentro de la clase.</remarks>
        /// <returns>Una tarea que representa la operación asíncrona.</returns>
        private async Task CreateRol()
        {
            RolModel rol = new()
            {
                NombreRol = this.NombreRol
            };

            await _dataContext.Roles.AddAsync(rol);
            await _dataContext.SaveChangesAsync();
        }

        /// <summary>
        /// Actualiza la entidad de rol especificada con el nombre de rol actual y guarda los cambios de forma asíncrona.
        /// </summary>
        /// <param name="foundRol">La entidad de rol que se va a actualizar. No debe ser nula y debe estar vinculada al contexto de datos.</param>
        /// <returns>Una tarea que representa la operación de guardado asíncrono.</returns>
        private async Task EditRol(RolModel foundRol)
        {
            foundRol.NombreRol = this.NombreRol;

            _dataContext.Roles.Update(foundRol);
            await _dataContext.SaveChangesAsync();
        }

        /// <summary>
        /// Carga los datos del rol especificado en la instancia actual para su edición.
        /// </summary>
        /// <param name="rol">El modelo de rol que contiene los datos que se van a cargar. No puede ser nulo.</param>
        public void LoadRolForEdition(RolModel rol)
        {
            this.NombreRol = rol.NombreRol;
        }


        [RelayCommand]
        public async Task GoToAddPage()
        {
            await Shell.Current.GoToAsync("AddRolPage");
        }

        [RelayCommand]
        public async Task GoToEditPage(RolModel rol)
        {
            await Shell.Current.GoToAsync("AddRolPage", new Dictionary<string, object> { ["Rol"] = rol });
        }

        [RelayCommand]
        public async Task DeleteRol(RolModel rol)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("¿Estas seguro de que quieres eliminar este rol?", "Cancelar", "Eliminar");
            if (userAnswer == "Cancelar") return;

            var tracked = _dataContext.ChangeTracker.Entries<RolModel>()
                            .FirstOrDefault(e => e.Entity.IdRol == rol.IdRol)?.Entity;

            var entityToDelete = tracked ?? await _dataContext.Roles.FindAsync(rol.IdRol);

            if (entityToDelete != null)
            {
                _dataContext.Roles.Remove(entityToDelete);
                await _dataContext.SaveChangesAsync();
                Roles = new(await _dataContext.Roles.AsNoTracking().ToListAsync());
            }
        }

        public async Task Initialize(RolModel rol)
        {
            await GetRolesAsync();
            if (rol != null)
            {
                LoadRolForEdition(rol);
            }
        }
    }
}
