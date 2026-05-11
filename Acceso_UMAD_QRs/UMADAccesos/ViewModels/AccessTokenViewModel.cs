using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SharedResources.Data;
using SharedResources.Models;
using System.Collections.ObjectModel;

namespace UMADAccesos.ViewModels
{
    [QueryProperty(nameof(User), "User")]
    public partial class AccessTokenViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<AccessTokenModel> _tokens;

        [ObservableProperty]
        private ObservableCollection<UserModel> _users;

        [ObservableProperty]
        private string _qrHash = string.Empty;

        [ObservableProperty]
        private DateTime _expirationDate = DateTime.Now.AddDays(1);

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private int _validityDays = 1;

        [ObservableProperty]
        private UserModel _user = new UserModel();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsFormValid))]
        private bool _isHashValid = false;

        public bool IsFormValid => IsHashValid && User != null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsApproved))]
        [NotifyPropertyChangedFor(nameof(IsPending))]
        [NotifyPropertyChangedFor(nameof(IsExpired))]
        private string _userStatus = "Approved";

        public bool IsApproved => UserStatus == "Approved";
        public bool IsPending => UserStatus == "Pending";
        public bool IsExpired => UserStatus == "Expired";

        [ObservableProperty]
        private ObservableCollection<UserModel> _pendingRequests;

        private readonly UmadDbContext _dataContext;


        public AccessTokenViewModel(UmadDbContext dataContext)
        {
            Tokens = new ObservableCollection<AccessTokenModel>();
            Users = new ObservableCollection<UserModel>();
            PendingRequests = new ObservableCollection<UserModel>();
            _dataContext = dataContext;
        }

        public async Task GetTokensAsync()
        {
            try
            {
                Tokens.Clear();
                var tokensFromDb = await _dataContext.AccessTokens.Include(t => t.User).AsNoTracking().ToListAsync();
                foreach (var token in tokensFromDb)
                {
                    Tokens.Add(token);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Connection Error", $"Could not fetch tokens: {ex.Message}", "OK");
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
                await Shell.Current.DisplayAlertAsync("Connection Error", $"Could not fetch users: {ex.Message}", "OK");
            }
        }

        partial void OnQrHashChanged(string value)
        {
            IsHashValid = !string.IsNullOrWhiteSpace(value);
        }

        [RelayCommand]
        public async Task SaveToken()
        {
            try
            {
                var foundToken = await _dataContext.AccessTokens.AsNoTracking().SingleOrDefaultAsync(t => t.QrHash == this.QrHash);
                if (foundToken != null)
                {
                    await EditToken(foundToken);
                    await Shell.Current.DisplayAlertAsync("Edit Success", "The record has been edited successfully", "OK");
                    await Shell.Current.GoToAsync("..");
                    return;
                }
                await CreateToken();
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Database Error", $"Could not save token: {ex.Message}", "OK");
            }
        }

        private async Task CreateToken()
        {
            try
            {
                AccessTokenModel token = new()
                {
                    QrHash = this.QrHash,
                    ExpirationDate = this.ExpirationDate,
                    IsActive = this.IsActive,
                    IdUser = this.User.IdUser
                };

                await _dataContext.AccessTokens.AddAsync(token);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Database Error", $"Could not create token: {ex.Message}", "OK");
            }
        }

        private async Task EditToken(AccessTokenModel foundToken)
        {
            try
            {
                foundToken.QrHash = this.QrHash;
                foundToken.ExpirationDate = this.ExpirationDate;
                foundToken.IsActive = this.IsActive;
                foundToken.IdUser = this.User.IdUser;

                _dataContext.AccessTokens.Update(foundToken);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Database Error", $"Could not edit token: {ex.Message}", "OK");
            }
        }

        public void LoadTokenForEdition(AccessTokenModel token)
        {
            this.QrHash = token.QrHash;
            this.ExpirationDate = token.ExpirationDate;
            this.IsActive = token.IsActive;
            this.User = this.Users.FirstOrDefault(u => u.IdUser == token.IdUser)!;
        }

        [RelayCommand]
        public async Task GoToAddPage()
        {
            await Shell.Current.GoToAsync("AddAccessTokenPage");
        }

        [RelayCommand]
        public async Task GoToEditPage(AccessTokenModel token)
        {
            await Shell.Current.GoToAsync("AddAccessTokenPage", new Dictionary<string, object> { ["AccessToken"] = token });
        }

        [RelayCommand]
        public async Task DeleteToken(AccessTokenModel token)
        {
            string userAnswer = await Shell.Current.DisplayActionSheetAsync("Are you sure you want to delete this token?", "Cancel", "Delete");
            if (userAnswer == "Cancel") return;

            try
            {
                var tracked = _dataContext.ChangeTracker.Entries<AccessTokenModel>()
                                .FirstOrDefault(e => e.Entity.IdToken == token.IdToken)?.Entity;

                var entityToDelete = tracked ?? await _dataContext.AccessTokens.FindAsync(token.IdToken);

                if (entityToDelete != null)
                {
                    _dataContext.AccessTokens.Remove(entityToDelete);
                    await _dataContext.SaveChangesAsync();
                    Tokens = new(await _dataContext.AccessTokens.Include(t => t.User).AsNoTracking().ToListAsync());
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Database Error", $"Could not delete token: {ex.Message}", "OK");
            }
        }

        public async Task Initialize(AccessTokenModel token)
        {
            await GetUsersAsync();
            await GetTokensAsync();
            await LoadPendingRequests();
            LoadMyToken();

            if (token != null)
            {
                LoadTokenForEdition(token);
            }
        }

        private void LoadMyToken()
        {
            if (App.CurrentUser != null)
            {
                var myToken = Tokens.OrderByDescending(t => t.ExpirationDate).FirstOrDefault(t => t.IdUser == App.CurrentUser.IdUser);
                if (myToken != null)
                {
                    this.QrHash = myToken.QrHash;
                    this.ExpirationDate = myToken.ExpirationDate;
                    this.UserStatus = myToken.IsActive && myToken.ExpirationDate > DateTime.Now ? "Approved" : "Expired";
                }
                else
                {
                    this.UserStatus = "Pending";
                }
            }
        }

        public async Task LoadPendingRequests()
        {
            try
            {
                PendingRequests.Clear();
                var usersWithoutToken = await _dataContext.Users
                    .Include(u => u.Role)
                    .Where(u => !u.Tokens.Any(t => t.IsActive && t.ExpirationDate > DateTime.Now))
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var u in usersWithoutToken)
                {
                    PendingRequests.Add(u);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Connection Error", $"Could not load pending requests: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        private async Task GoToGenerateToken(UserModel user)
        {
            await Shell.Current.GoToAsync(nameof(Views.AccessGeneratorView), new Dictionary<string, object>
            {
                { "User", user }
            });
        }

        [RelayCommand]
        private async Task RequestRenewal()
        {
            await Shell.Current.DisplayAlertAsync("Request", "Your renewal request has been sent.", "OK");
            this.UserStatus = "Pending";
        }

        [RelayCommand]
        private async Task GenerateToken()
        {
            if (this.User == null)
            {
                await Shell.Current.DisplayAlertAsync("Error", "No user has been selected to generate the token.", "OK");
                return;
            }

            this.QrHash = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
            this.ExpirationDate = ValidityDays > 0 ? DateTime.Now.AddDays(ValidityDays) : DateTime.Now.AddYears(10);
            this.IsActive = true;

            await CreateToken();

            await Shell.Current.DisplayAlertAsync("Success", $"Token generated and assigned successfully to {User.FullName}.", "OK");

            await LoadPendingRequests();
            await Shell.Current.GoToAsync("..");
        }
    }
}
