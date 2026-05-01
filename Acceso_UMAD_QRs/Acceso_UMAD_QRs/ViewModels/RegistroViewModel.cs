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
        private ObservableCollection<RegistroModel> _registros;

        [ObservableProperty]
        private ObservableCollection<UsuarioModel> _usuarios;

        [ObservableProperty]
        private string _puntoAcceso = string.Empty;

        [ObservableProperty]
        private DateTime _fechaHora = DateTime.Now;

        [ObservableProperty]
        private UsuarioModel _usuario = null!;

        [ObservableProperty]
        private ObservableCollection<string> _puntosDeAcceso = new() { "Entrada Principal", "Estacionamiento Norte", "Edificio Central", "Biblioteca" };

        [ObservableProperty]
        private ObservableCollection<string> _filtrosPuntoAcceso = new() { "Todas las locaciones", "Entrada Principal", "Estacionamiento Norte", "Edificio Central", "Biblioteca" };

        [ObservableProperty]
        private string _filtroBusqueda = string.Empty;

        [ObservableProperty]
        private string _filtroLocacion = "Todas las locaciones";

        [ObservableProperty]
        private string _puntoSeleccionado;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isPuntoAccesoValid = false;

        [ObservableProperty]
        private int _estadoLectura = 0;

        [ObservableProperty]
        private string _mensajeResultado = "Apunta la cámara al código QR del usuario.";

        public bool IsFormValid => IsPuntoAccesoValid && Usuario != null;

        private readonly UmadDbContext _dataContext;

        /// <summary>
        /// Inicializa una nueva instancia de la clase RegistroViewModel utilizando el contexto de base de datos
        /// especificado.
        /// </summary>
        /// <param name="dataContext">El contexto de base de datos que se utilizará para acceder y administrar los datos de la aplicación. No puede ser nulo.</param>
        public RegistroViewModel(UmadDbContext dataContext)
        {
            Registros = new ObservableCollection<RegistroModel>();
            Usuarios = new ObservableCollection<UsuarioModel>();
            _dataContext = dataContext;
        }

        [RelayCommand]
        public async Task FiltrarHistorial()
        {
            try
            {
                Registros.Clear();
                var query = _dataContext.Registros.Include(r => r.Usuario).AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(FiltroBusqueda))
                {
                    var filtro = FiltroBusqueda.ToLower();
                    query = query.Where(r => r.Usuario.NombreCompleto.ToLower().Contains(filtro) || (r.Usuario.Matricula != null && r.Usuario.Matricula.Contains(filtro)));
                }

                if (!string.IsNullOrEmpty(FiltroLocacion) && FiltroLocacion != "Todas las locaciones")
                {
                    query = query.Where(r => r.PuntoAcceso == FiltroLocacion);
                }

                var registrosFiltrados = await query.ToListAsync();
                foreach (var registro in registrosFiltrados)
                {
                    Registros.Add(registro);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Conexión", $"No se pudo filtrar el historial: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Asíncronamente actualiza la colección de registros con los datos más recientes obtenidos de la base de
        /// datos.
        /// </summary>
        /// <remarks>Limpia la colección existente antes de cargar los nuevos registros. Utiliza una
        /// consulta sin seguimiento para mejorar el rendimiento en escenarios de solo lectura.</remarks>
        /// <returns>Una tarea que representa la operación asincrónica.</returns>
        public async Task GetRegistrosAsync()
        {
            try
            {
                Registros.Clear();
                var registrosFromDb = await _dataContext.Registros.Include(r => r.Usuario).AsNoTracking().ToListAsync();
                foreach (var registro in registrosFromDb)
                {
                    Registros.Add(registro);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Conexión", $"No se pudieron obtener los registros: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Recupera de forma asíncrona la lista de usuarios de la fuente de datos y actualiza la colección Usuarios.
        /// </summary>
        /// <remarks>Este método borra la colección Usuarios existente antes de cargar los datos más recientes.
        /// La operación no realiza un seguimiento de los cambios en las entidades recuperadas.</remarks>
        /// <returns>Una tarea que representa la operación asíncrona.</returns>
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

        /// <summary>
        /// Gestiona los cambios en el valor de PuntoAcceso y actualiza el estado de validación correspondiente.
        /// </summary>
        /// <param name="value">El nuevo valor asignado a PuntoAcceso. Puede ser nulo o contener espacios en blanco.</param>
        partial void OnPuntoAccesoChanged(string value)
        {
            IsPuntoAccesoValid = !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Guarda un nuevo registro y navega a la pantalla anterior de forma asíncrona.
        /// </summary>
        /// <remarks>Este método debe invocarse desde el contexto de la interfaz de usuario. Utiliza
        /// navegación basada en Shell para regresar a la pantalla anterior tras completar el guardado.</remarks>
        /// <returns>Una tarea que representa la operación asincrónica de guardar el registro y navegar.</returns>
        [RelayCommand]
        public async Task SaveRegistro()
        {
            await CreateRegistro();
            await Shell.Current.GoToAsync("..");
        }

        /// <summary>
        /// Crea un nuevo registro en la base de datos utilizando los valores actuales de punto de acceso, fecha y
        /// usuario.
        /// </summary>
        /// <remarks>Este método debe llamarse cuando se desee persistir un nuevo registro asociado al
        /// usuario y punto de acceso actuales. El método realiza la operación de forma asincrónica y guarda los cambios
        /// en el contexto de datos.</remarks>
        /// <returns>Una tarea que representa la operación asincrónica de creación del registro.</returns>
        private async Task CreateRegistro()
        {
            try
            {
                RegistroModel registro = new()
                {
                    PuntoAcceso = this.PuntoAcceso,
                    FechaHora = this.FechaHora,
                    IdUsuario = this.Usuario.IdUsuario
                };

                await _dataContext.Registros.AddAsync(registro);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Base de Datos", $"No se pudo crear el registro: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Carga los datos del registro especificado en la instancia actual para su edición.
        /// </summary>
        /// <param name="registro">El registro cuyos datos se cargarán para edición. No puede ser null.</param>
        public void LoadRegistroForEdition(RegistroModel registro)
        {
            this.PuntoAcceso = registro.PuntoAcceso;
            this.FechaHora = registro.FechaHora;
            this.Usuario = this.Usuarios.FirstOrDefault(u => u.IdUsuario == registro.IdUsuario)!;
        }

        /// <summary>
        /// Navega de forma asíncrona a la página para agregar un nuevo registro.
        /// </summary>
        /// <remarks>Utiliza la navegación de Shell para mostrar la página 'AddRegistroPage'. Puede
        /// utilizarse en comandos de interfaz de usuario para iniciar el flujo de creación de registros.</remarks>
        /// <returns>Una tarea que representa la operación de navegación asíncrona.</returns>
        [RelayCommand]
        public async Task GoToAddPage()
        {
            await Shell.Current.GoToAsync("AddRegistroPage");
        }

        /// <summary>
        /// Navega de forma asíncrona a la página de edición de registros, pasando el registro especificado como
        /// parámetro de navegación.
        /// </summary>
        /// <remarks>La página de destino debe estar preparada para recibir el parámetro 'Registro' a
        /// través del diccionario de navegación.</remarks>
        /// <param name="registro">El registro que se va a editar. No puede ser nulo.</param>
        /// <returns>Una tarea que representa la operación de navegación asíncrona.</returns>
        [RelayCommand]
        public async Task GoToEditPage(RegistroModel registro)
        {
            await Shell.Current.GoToAsync("AddRegistroPage", new Dictionary<string, object> { ["Registro"] = registro });
        }

        /// <summary>
        /// Elimina de forma asíncrona un registro existente tras solicitar confirmación al usuario.
        /// </summary>
        /// <remarks>Si el usuario cancela la operación, el registro no se elimina. Tras la eliminación,
        /// la colección de registros se actualiza para reflejar los cambios.</remarks>
        /// <param name="registro">El registro que se va a eliminar. No puede ser nulo y debe contener un identificador válido.</param>
        /// <returns>Una tarea que representa la operación de eliminación asincrónica.</returns>
        [RelayCommand]
        public async Task DeleteRegistro(RegistroModel registro)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("¿Estas seguro de que quieres eliminar este registro?", "Cancelar", "Eliminar");
            if (userAnswer == "Cancelar") return;

            try
            {
                var tracked = _dataContext.ChangeTracker.Entries<RegistroModel>()
                                .FirstOrDefault(e => e.Entity.IdRegistro == registro.IdRegistro)?.Entity;

                var entityToDelete = tracked ?? await _dataContext.Registros.FindAsync(registro.IdRegistro);

                if (entityToDelete != null)
                {
                    _dataContext.Registros.Remove(entityToDelete);
                    await _dataContext.SaveChangesAsync();
                    Registros = new(await _dataContext.Registros.Include(r => r.Usuario).AsNoTracking().ToListAsync());
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Base de Datos", $"No se pudo eliminar el registro: {ex.Message}", "OK");
            }
        }

        /// <summary>
        /// Inicializa los datos necesarios para la edición o visualización de registros de usuario.
        /// </summary>
        /// <param name="registro">El modelo de registro que se va a cargar para edición. Puede ser nulo para inicializar sin cargar un
        /// registro específico.</param>
        /// <returns>Una tarea que representa la operación de inicialización asincrónica.</returns>
        public async Task Initialize(RegistroModel registro)
        {
            await GetUsuariosAsync();
            await GetRegistrosAsync();
            if (registro != null)
            {
                LoadRegistroForEdition(registro);
            }
        }

        /// <summary>
        /// Simula un acceso válido actualizando el estado de lectura, mostrando un mensaje de acceso permitido y
        /// registrando el acceso del primer usuario disponible.
        /// </summary>
        /// <remarks>Utilice este método para probar el flujo de acceso permitido en el sistema sin
        /// requerir un token real. El método selecciona el primer usuario disponible y registra el acceso con la
        /// información de punto de acceso y la hora actual. Tras una breve pausa, restablece el escáner para permitir
        /// nuevas lecturas.</remarks>
        /// <returns>Una tarea que representa la operación asincrónica de simulación de acceso válido.</returns>
        [RelayCommand]
        private async Task SimularAccesoValido()
        {
            EstadoLectura = 1;
            MensajeResultado = "ACCESO PERMITIDO\nToken Válido.";

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

        /// <summary>
        /// Simula un intento de acceso no autorizado mostrando un mensaje de error y restableciendo el escáner tras una
        /// breve pausa.
        /// </summary>
        /// <remarks>Utilice este método para probar el comportamiento de la interfaz de usuario ante
        /// accesos denegados por token expirado o inválido. El método actualiza el estado y el mensaje de resultado
        /// antes de restablecer el escáner.</remarks>
        /// <returns>Una tarea que representa la operación asíncrona de simulación de acceso inválido.</returns>
        [RelayCommand]
        private async Task SimularAccesoInvalido()
        {
            EstadoLectura = 2;
            MensajeResultado = "ACCESO DENEGADO\nToken Expirado o Inválido.";
            await Task.Delay(3000);
            RestablecerEscaner();
        }

        /// <summary>
        /// Inicia un nuevo turno registrando el punto de acceso seleccionado y navega a la vista de escaneo.
        /// </summary>
        /// <remarks>Muestra mensajes de alerta al usuario si no se ha seleccionado un punto de acceso o
        /// al iniciar el turno correctamente. Navega a la vista de escaneo tras el registro exitoso del punto de
        /// acceso.</remarks>
        /// <returns>Una tarea que representa la operación asincrónica de inicio de turno.</returns>
        [RelayCommand]
        private async Task IniciarTurno()
        {
            if (string.IsNullOrEmpty(PuntoSeleccionado))
            {
                await Shell.Current.DisplayAlertAsync("Error", "Selecciona un punto de acceso primero.", "OK");
                return;
            }

            Preferences.Default.Set("PuntoAccesoActual", PuntoSeleccionado);

            await Shell.Current.DisplayAlertAsync("Turno Iniciado", $"Registrando accesos en: {PuntoSeleccionado}", "OK");
            await Shell.Current.GoToAsync(nameof(Views.EscanerView));
        }

        /// <summary>
        /// Procesa una lectura de código QR para validar el acceso del usuario asociado al token correspondiente.
        /// </summary>
        /// <remarks>Actualiza el estado de la lectura y el mensaje de resultado según la validez del
        /// token. Si el acceso es permitido, también actualiza la información del usuario y del punto de acceso, y
        /// registra el evento. El escáner se restablece automáticamente tras el procesamiento.</remarks>
        /// <param name="hashQr">El valor hash del código QR escaneado que se utilizará para buscar y validar el token de acceso. No puede
        /// ser nulo.</param>
        /// <returns>Una tarea que representa la operación asincrónica de procesamiento de la lectura del código QR.</returns>
        public async Task ProcesarLecturaQR(string hashQr)
        {
            try
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
            }
            catch (Exception ex)
            {
                EstadoLectura = 2;
                MensajeResultado = "ERROR DE CONEXIÓN\nVerifica la base de datos.";
                Console.WriteLine(ex.Message);
            }

            await Task.Delay(3000);
            RestablecerEscaner();
        }

        /// <summary>
        /// Restablece el estado del escáner para iniciar una nueva lectura de código QR.
        /// </summary>
        /// <remarks>Utilice este método para preparar el escáner antes de comenzar una nueva operación de
        /// lectura. Restablece el estado interno y muestra un mensaje de instrucción al usuario.</remarks>
        private void RestablecerEscaner()
        {
            EstadoLectura = 0;
            MensajeResultado = "Apunta la cámara al código QR del usuario.";
        }
    }
}
