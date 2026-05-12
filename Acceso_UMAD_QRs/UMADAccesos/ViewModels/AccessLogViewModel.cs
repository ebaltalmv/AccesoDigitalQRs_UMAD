using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SharedResources.Data;
using SharedResources.Models;
using System.Collections.ObjectModel;

namespace UMADAccesos.ViewModels
{
    public partial class AccessLogViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<AccessLogModel> _accessLogs;

        [ObservableProperty]
        private ObservableCollection<UserModel> _users;

        [ObservableProperty]
        private string _accessPoint = string.Empty;

        [ObservableProperty]
        private DateTime _timestamp = DateTime.Now;

        [ObservableProperty]
        private UserModel _user = new UserModel();

        [ObservableProperty]
        private ObservableCollection<string> _accessPoints = new() { "Entrada Principal", "Estacionamiento Norte", "Edificio Central", "Biblioteca" };

        [ObservableProperty]
        private ObservableCollection<string> _locationFilters = new() { "Todas las Ubicaciones", "Entrada Principal", "Estacionamiento Norte", "Edificio Central", "Biblioteca" };

        [ObservableProperty]
        private string _searchFilter = string.Empty;

        [ObservableProperty]
        private string _locationFilter = "Todas las Ubicaciones";

        [ObservableProperty]
        private string _selectedPoint = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isAccessPointValid = false;

        [ObservableProperty]
        private int _scanState = 0;

        [ObservableProperty]
        private string _resultMessage = "Apunta la cámara al código QR del usuario.";

        public bool IsFormValid => IsAccessPointValid && User != null;

        private readonly UmadDbContext _dataContext;

        public AccessLogViewModel(UmadDbContext dataContext)
        {
            AccessLogs = new ObservableCollection<AccessLogModel>();
            Users = new ObservableCollection<UserModel>();
            _dataContext = dataContext;
        }

        [RelayCommand]
        public async Task FilterHistory()
        {
            try
            {
                AccessLogs.Clear();
                var query = _dataContext.AccessLogs.Include(r => r.User).AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(SearchFilter))
                {
                    var filter = SearchFilter.ToLower();
                    query = query.Where(r => r.User.FullName.ToLower().Contains(filter) || (r.User.StudentId != null && r.User.StudentId.Contains(filter)));
                }

                if (!string.IsNullOrEmpty(LocationFilter) && LocationFilter != "Todas las Ubicaciones")
                {
                    query = query.Where(r => r.AccessPoint == LocationFilter);
                }

                var filteredLogs = await query.ToListAsync();
                foreach (var log in filteredLogs)
                {
                    AccessLogs.Add(log);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Conexión", $"No se pudo filtrar el historial: {ex.Message}", "Aceptar");
            }
        }

        public async Task GetAccessLogsAsync()
        {
            try
            {
                AccessLogs.Clear();
                var logsFromDb = await _dataContext.AccessLogs.Include(r => r.User).AsNoTracking().ToListAsync();
                foreach (var log in logsFromDb)
                {
                    AccessLogs.Add(log);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Conexión", $"No se pudieron obtener los registros de acceso: {ex.Message}", "Aceptar");
            }
        }

        public async Task GetUsersAsync()
        {
            try
            {
                Users.Clear();
                Users = new(await _dataContext.Users.AsNoTracking().ToListAsync());
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Conexión", $"No se pudieron obtener los usuarios: {ex.Message}", "Aceptar");
            }
        }

        partial void OnAccessPointChanged(string value)
        {
            IsAccessPointValid = !string.IsNullOrWhiteSpace(value);
        }

        [RelayCommand]
        public async Task SaveAccessLog()
        {
            await CreateAccessLog();
            await Shell.Current.GoToAsync("..");
        }

        private async Task CreateAccessLog()
        {
            try
            {
                AccessLogModel log = new()
                {
                    AccessPoint = this.AccessPoint,
                    Timestamp = this.Timestamp,
                    IdUser = this.User.IdUser
                };

                await _dataContext.AccessLogs.AddAsync(log);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Base de Datos", $"No se pudo crear el registro de acceso: {ex.Message}", "Aceptar");
            }
        }

        public void LoadAccessLogForEdition(AccessLogModel log)
        {
            this.AccessPoint = log.AccessPoint;
            this.Timestamp = log.Timestamp;
            this.User = this.Users.FirstOrDefault(u => u.IdUser == log.IdUser)!;
        }

        [RelayCommand]
        public async Task GoToAddPage()
        {
            await Shell.Current.GoToAsync("AddAccessLogPage");
        }

        [RelayCommand]
        public async Task GoToEditPage(AccessLogModel log)
        {
            await Shell.Current.GoToAsync("AddAccessLogPage", new Dictionary<string, object> { ["AccessLog"] = log });
        }

        [RelayCommand]
        public async Task DeleteAccessLog(AccessLogModel log)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("¿Estás seguro de que quieres eliminar este registro?", "Cancelar", "Eliminar");
            if (userAnswer == "Cancel") return;

            try
            {
                var tracked = _dataContext.ChangeTracker.Entries<AccessLogModel>()
                                .FirstOrDefault(e => e.Entity.IdLog == log.IdLog)?.Entity;

                var entityToDelete = tracked ?? await _dataContext.AccessLogs.FindAsync(log.IdLog);

                if (entityToDelete != null)
                {
                    _dataContext.AccessLogs.Remove(entityToDelete);
                    await _dataContext.SaveChangesAsync();
                    AccessLogs = new(await _dataContext.AccessLogs.Include(r => r.User).AsNoTracking().ToListAsync());
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error de Base de Datos", $"No se pudo eliminar el registro: {ex.Message}", "Aceptar");
            }
        }

        public async Task Initialize(AccessLogModel log)
        {
            await GetUsersAsync();
            await GetAccessLogsAsync();
            if (log != null)
            {
                LoadAccessLogForEdition(log);
            }
        }

        [RelayCommand]
        private async Task SimulateValidAccess()
        {
            ScanState = 1;
            ResultMessage = "ACCESO CONCEDIDO\nToken Válido.";

            if (Users.Any())
            {
                this.User = Users.First();
                this.AccessPoint = Preferences.Default.Get("CurrentAccessPoint", "Punto Desconocido");
                this.Timestamp = DateTime.Now;
                await CreateAccessLog();
                await GetAccessLogsAsync();
            }

            await Task.Delay(3000);
            ResetScanner();
        }

        [RelayCommand]
        private async Task SimulateInvalidAccess()
        {
            ScanState = 2;
            ResultMessage = "ACCESO DENEGADO\nToken Expirado o Inválido.";
            await Task.Delay(3000);
            ResetScanner();
        }

        [RelayCommand]
        private async Task StartShift()
        {
            if (string.IsNullOrEmpty(SelectedPoint))
            {
                await Shell.Current.DisplayAlertAsync("Error", "Selecciona un punto de acceso primero.", "Aceptar");
                return;
            }

            Preferences.Default.Set("CurrentAccessPoint", SelectedPoint);

            await Shell.Current.DisplayAlertAsync("Turno Iniciado", $"Registrando acceso en: {SelectedPoint}", "Aceptar");
            await Shell.Current.GoToAsync(nameof(Views.ScannerView));
        }

        public async Task ProcessQRScan(string qrHash)
        {
            try
            {
                var token = await _dataContext.AccessTokens
                                              .Include(t => t.User)
                                              .FirstOrDefaultAsync(t => t.QrHash == qrHash);

                if (token != null && token.IsActive && token.ExpirationDate > DateTime.Now)
                {
                    ScanState = 1;
                    ResultMessage = $"ACCESO CONCEDIDO\n{token.User.FullName}";

                    this.User = token.User;
                    this.AccessPoint = Preferences.Default.Get("CurrentAccessPoint", "Punto Desconocido");
                    this.Timestamp = DateTime.Now;
                    await CreateAccessLog();
                    await GetAccessLogsAsync();
                }
                else
                {
                    ScanState = 2;
                    ResultMessage = "ACCESO DENEGADO\nToken Expirado o Inválido.";
                }
            }
            catch (Exception ex)
            {
                ScanState = 2;
                ResultMessage = "ERROR DE CONEXIÓN\nVerifica la base de datos.";
                Console.WriteLine(ex.Message);
            }

            await Task.Delay(3000);
            ResetScanner();
        }

        private void ResetScanner()
        {
            ScanState = 0;
            ResultMessage = "Apunta la cámara al código QR del usuario.";
        }
    }
}
