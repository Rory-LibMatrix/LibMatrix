using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArcaneLibs;
using ArcaneLibs.Collections;
using ArcaneLibs.Extensions;
using LibMatrix.EventTypes.Spec.State;
using LibMatrix.Filters;
using LibMatrix.Responses;

namespace LibMatrix.HomeserverEmulator.Services;

public class UserStore {
    public ConcurrentBag<User> _users = new();
    private readonly RoomStore _roomStore;

    public UserStore(HSEConfiguration config, RoomStore roomStore) {
        _roomStore = roomStore;
        if (config.StoreData) {
            var path = Path.Combine(config.DataStoragePath, "users");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            foreach (var file in Directory.GetFiles(path)) {
                var user = JsonSerializer.Deserialize<User>(File.ReadAllText(file));
                if (user is not null) _users.Add(user);
            }

            Console.WriteLine($"Loaded {_users.Count} users from disk");
        }
        else {
            Console.WriteLine("Data storage is disabled, not loading users from disk");
        }
    }

    public async Task<User?> GetUserById(string userId, bool createIfNotExists = false) {
        if (_users.Any(x => x.UserId == userId))
            return _users.First(x => x.UserId == userId);

        if (!createIfNotExists)
            return null;

        return await CreateUser(userId);
    }

    public async Task<User?> GetUserByToken(string token, bool createIfNotExists = false, string? serverName = null) {
        if (_users.Any(x => x.AccessTokens.ContainsKey(token)))
            return _users.First(x => x.AccessTokens.ContainsKey(token));

        if (!createIfNotExists)
            return null;
        if (string.IsNullOrWhiteSpace(serverName)) throw new NullReferenceException("Server name was not passed");
        var uid = $"@{Guid.NewGuid().ToString()}:{serverName}";
        return await CreateUser(uid);
    }

    public async Task<User> CreateUser(string userId, Dictionary<string, object>? profile = null) {
        profile ??= new();
        if (!profile.ContainsKey("displayname")) profile.Add("displayname", userId.Split(":")[0]);
        if (!profile.ContainsKey("avatar_url")) profile.Add("avatar_url", null);
        var user = new User() {
            UserId = userId,
            AccountData = new() {
                new StateEventResponse() {
                    Type = "im.vector.analytics",
                    RawContent = new JsonObject() {
                        ["pseudonymousAnalyticsOptIn"] = false
                    },
                },
                new StateEventResponse() {
                    Type = "im.vector.web.settings",
                    RawContent = new JsonObject() {
                        ["developerMode"] = true
                    }
                },
            }
        };
        user.Profile.AddRange(profile);
        _users.Add(user);
        if (!_roomStore._rooms.IsEmpty)
            foreach (var item in Random.Shared.GetItems(_roomStore._rooms.ToArray(), Math.Min(_roomStore._rooms.Count, 400))) {
                item.AddUser(userId);
            }

        int random = Random.Shared.Next(10);
        for (int i = 0; i < random; i++) {
            var room = _roomStore.CreateRoom(new());
            room.AddUser(userId);
        }

        return user;
    }

    public class User : NotifyPropertyChanged {
        public User() {
            AccessTokens = new();
            Filters = new();
            Profile = new();
            AccountData = new();
            RoomKeys = new();
        }

        private CancellationTokenSource _debounceCts = new();
        private string _userId;
        private ObservableDictionary<string, SessionInfo> _accessTokens;
        private ObservableDictionary<string, SyncFilter> _filters;
        private ObservableDictionary<string, object> _profile;
        private ObservableCollection<StateEventResponse> _accountData;
        private ObservableDictionary<string, RoomKeysResponse> _roomKeys;

        public string UserId {
            get => _userId;
            set => SetField(ref _userId, value);
        }

        public ObservableDictionary<string, SessionInfo> AccessTokens {
            get => _accessTokens;
            set {
                if (value == _accessTokens) return;
                _accessTokens = new(value);
                _accessTokens.CollectionChanged += async (sender, args) => await SaveDebounced();
                OnPropertyChanged();
            }
        }

        public ObservableDictionary<string, SyncFilter> Filters {
            get => _filters;
            set {
                if (value == _filters) return;
                _filters = new(value);
                _filters.CollectionChanged += async (sender, args) => await SaveDebounced();
                OnPropertyChanged();
            }
        }

        public ObservableDictionary<string, object> Profile {
            get => _profile;
            set {
                if (value == _profile) return;
                _profile = new(value);
                _profile.CollectionChanged += async (sender, args) => await SaveDebounced();
                OnPropertyChanged();
            }
        }

        public ObservableCollection<StateEventResponse> AccountData {
            get => _accountData;
            set {
                if (value == _accountData) return;
                _accountData = new(value);
                _accountData.CollectionChanged += async (sender, args) => await SaveDebounced();
                OnPropertyChanged();
            }
        }

        public ObservableDictionary<string, RoomKeysResponse> RoomKeys {
            get => _roomKeys;
            set {
                if (value == _roomKeys) return;
                _roomKeys = new(value);
                _roomKeys.CollectionChanged += async (sender, args) => await SaveDebounced();
                OnPropertyChanged();
            }
        }

        public async Task SaveDebounced() {
            if (!HSEConfiguration.Current.StoreData) return;
            _debounceCts.Cancel();
            _debounceCts = new CancellationTokenSource();
            try {
                await Task.Delay(250, _debounceCts.Token);
                var path = Path.Combine(HSEConfiguration.Current.DataStoragePath, "users", $"{_userId}.json");
                Console.WriteLine($"Saving user {_userId} to {path}!");
                await File.WriteAllTextAsync(path, this.ToJson(ignoreNull: true));
            }
            catch (TaskCanceledException) { }
            catch (InvalidOperationException) { } // We don't care about 100% data safety, this usually happens when something is updated while serialising
        }

        public class SessionInfo {
            public string DeviceId { get; set; } = Guid.NewGuid().ToString();
            public Dictionary<string, UserSyncState> SyncStates { get; set; } = new();

            public class UserSyncState {
                public Dictionary<string, SyncRoomPosition> RoomPositions { get; set; } = new();
                public string FilterId { get; set; }
                public DateTime SyncStateCreated { get; set; } = DateTime.Now;

                public class SyncRoomPosition {
                    public int TimelinePosition { get; set; }
                    public int StatePosition { get; set; }
                    public int AccountDataPosition { get; set; }
                }
            }
        }

        public LoginResponse Login() {
            var session = new SessionInfo();
            AccessTokens.Add(Guid.NewGuid().ToString(), session);
            SaveDebounced();
            return new LoginResponse() {
                AccessToken = AccessTokens.Keys.Last(),
                DeviceId = session.DeviceId,
                UserId = UserId
            };
        }
    }
}