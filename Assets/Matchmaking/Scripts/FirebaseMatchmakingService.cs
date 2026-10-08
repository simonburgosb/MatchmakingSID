using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using UnityEngine;

namespace Sid2026.Matchmaking
{
    /// <summary>
    /// Capa de acceso a Firebase. Mantiene toda la lógica de red separada de la UI.
    /// </summary>
    public sealed class FirebaseMatchmakingService : MonoBehaviour
    {
        public event Action<string> StatusChanged;
        public event Action<string> ConfigurationWarning;
        public event Action<bool, string> SessionChanged;
        public event Action<IReadOnlyList<PresenceData>> OnlineUsersChanged;
        public event Action<IReadOnlyList<FriendRequestData>> FriendRequestsChanged;
        public event Action<IReadOnlyList<FriendData>> FriendsChanged;
        public event Action<string, bool> FriendPresenceNotification;
        public event Action<bool, string> QueueStateChanged;
        public event Action<MatchData> MatchChanged;

        public string CurrentUid => auth != null && auth.CurrentUser != null ? auth.CurrentUser.UserId : string.Empty;
        public string CurrentDisplayName => auth != null && auth.CurrentUser != null
            ? ResolveDisplayName(auth.CurrentUser)
            : string.Empty;
        public bool IsSignedIn => auth != null && auth.CurrentUser != null;

        private FirebaseApp app;
        private FirebaseAuth auth;
        private FirebaseDatabase database;
        private DatabaseReference root;
        private DatabaseReference connectedReference;
        private DatabaseReference statusCollectionReference;
        private DatabaseReference requestsReference;
        private DatabaseReference friendsReference;
        private DatabaseReference queueReference;
        private DatabaseReference userMatchReference;

        private readonly Dictionary<string, PresenceData> presenceByUid = new Dictionary<string, PresenceData>();
        private readonly Dictionary<string, FriendData> friendsByUid = new Dictionary<string, FriendData>();
        private readonly Dictionary<string, bool> knownFriendStates = new Dictionary<string, bool>();

        private string databaseUrl;
        private string activeUid;
        private string activeMatchId;
        private MatchData activeMatch;
        private bool firebaseReady;
        private bool isCreatingMatch;
        private bool isQueued;

        public async void Initialize(string requestedDatabaseUrl)
        {
            databaseUrl = NormalizeDatabaseUrl(requestedDatabaseUrl);
            StatusChanged?.Invoke("Conectando con Firebase…");

            try
            {
                DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (dependencyStatus != DependencyStatus.Available)
                {
                    StatusChanged?.Invoke("No se pudieron resolver las dependencias de Firebase: " + dependencyStatus);
                    return;
                }

                app = FirebaseApp.DefaultInstance;
                auth = FirebaseAuth.DefaultInstance;
                database = string.IsNullOrWhiteSpace(databaseUrl)
                    ? FirebaseDatabase.DefaultInstance
                    : FirebaseDatabase.GetInstance(databaseUrl);
                root = database.RootReference;
                firebaseReady = true;

                ValidateConfiguration();
                auth.StateChanged += HandleAuthStateChanged;

                if (auth.CurrentUser != null)
                {
                    BeginSession(auth.CurrentUser);
                }
                else
                {
                    StatusChanged?.Invoke("Firebase listo. Inicia sesión o crea una cuenta.");
                    SessionChanged?.Invoke(false, string.Empty);
                }
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("No fue posible iniciar Firebase: " + FriendlyException(exception));
            }
        }

        public async void Register(string fullName, string email, string password)
        {
            if (!CanAuthenticate(fullName, email, password, true)) return;
            StatusChanged?.Invoke("Creando cuenta…");

            try
            {
                AuthResult result = await auth.CreateUserWithEmailAndPasswordAsync(email.Trim(), password);
                FirebaseUser user = result.User;
                Firebase.Auth.UserProfile profile = new Firebase.Auth.UserProfile { DisplayName = fullName.Trim() };
                await user.UpdateUserProfileAsync(profile);

                Dictionary<string, object> userData = new Dictionary<string, object>
                {
                    { "uid", user.UserId },
                    { "displayName", fullName.Trim() },
                    { "email", email.Trim() },
                    { "createdAt", ServerValue.Timestamp }
                };
                await root.Child("users").Child(user.UserId).SetValueAsync(userData);
                BeginSession(user);
                StatusChanged?.Invoke("Cuenta creada correctamente.");
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("No se pudo crear la cuenta: " + FriendlyAuthException(exception));
            }
        }

        public async void SignIn(string email, string password)
        {
            if (!CanAuthenticate(string.Empty, email, password, false)) return;
            StatusChanged?.Invoke("Iniciando sesión…");

            try
            {
                AuthResult result = await auth.SignInWithEmailAndPasswordAsync(email.Trim(), password);
                BeginSession(result.User);
                StatusChanged?.Invoke("Sesión iniciada.");
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("No se pudo iniciar sesión: " + FriendlyAuthException(exception));
            }
        }

        public async void SignOut()
        {
            if (!IsSignedIn) return;
            string uid = CurrentUid;
            try
            {
                await SetPresence(uid, ResolveDisplayName(auth.CurrentUser), false);
                await root.Child("matchmaking").Child("queue").Child(uid).RemoveValueAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("No se pudo actualizar el estado antes de salir: " + exception.Message);
            }

            auth.SignOut();
        }

        public async void SendFriendRequest(string receiverUid, string receiverName)
        {
            if (!IsSignedIn || receiverUid == CurrentUid || friendsByUid.ContainsKey(receiverUid)) return;

            try
            {
                Dictionary<string, object> request = new Dictionary<string, object>
                {
                    { "senderUid", CurrentUid },
                    { "senderName", CurrentDisplayName },
                    { "sentAt", ServerValue.Timestamp }
                };
                await root.Child("friendRequests").Child(receiverUid).Child(CurrentUid).SetValueAsync(request);
                StatusChanged?.Invoke("Solicitud enviada a " + receiverName + ".");
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("No se pudo enviar la solicitud: " + FriendlyException(exception));
            }
        }

        public async void AcceptFriendRequest(FriendRequestData request)
        {
            if (!IsSignedIn || request == null || string.IsNullOrEmpty(request.senderUid)) return;

            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "friends/" + CurrentUid + "/" + request.senderUid + "/uid", request.senderUid },
                { "friends/" + CurrentUid + "/" + request.senderUid + "/displayName", request.senderName },
                { "friends/" + CurrentUid + "/" + request.senderUid + "/friendsSince", ServerValue.Timestamp },
                { "friends/" + request.senderUid + "/" + CurrentUid + "/uid", CurrentUid },
                { "friends/" + request.senderUid + "/" + CurrentUid + "/displayName", CurrentDisplayName },
                { "friends/" + request.senderUid + "/" + CurrentUid + "/friendsSince", ServerValue.Timestamp },
                { "friendRequests/" + CurrentUid + "/" + request.senderUid, null }
            };

            try
            {
                await root.UpdateChildrenAsync(updates);
                StatusChanged?.Invoke(request.senderName + " ahora está en tu lista de amigos.");
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("No se pudo aceptar la solicitud: " + FriendlyException(exception));
            }
        }

        public async void RejectFriendRequest(FriendRequestData request)
        {
            if (!IsSignedIn || request == null || string.IsNullOrEmpty(request.senderUid)) return;
            try
            {
                await root.Child("friendRequests").Child(CurrentUid).Child(request.senderUid).RemoveValueAsync();
                StatusChanged?.Invoke("Solicitud rechazada.");
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("No se pudo rechazar la solicitud: " + FriendlyException(exception));
            }
        }

        public async void EnterMatchmaking()
        {
            if (!IsSignedIn || isQueued || activeMatch != null) return;
            try
            {
                Dictionary<string, object> player = new Dictionary<string, object>
                {
                    { "uid", CurrentUid },
                    { "displayName", CurrentDisplayName },
                    { "joinedAt", ServerValue.Timestamp }
                };
                DatabaseReference ownQueue = root.Child("matchmaking").Child("queue").Child(CurrentUid);
                await ownQueue.OnDisconnect().RemoveValue();
                await ownQueue.SetValueAsync(player);
                isQueued = true;
                QueueStateChanged?.Invoke(true, "Buscando otro jugador…");
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("No se pudo entrar a la cola: " + FriendlyException(exception));
            }
        }

        public async void CancelMatchmaking()
        {
            if (!IsSignedIn) return;
            try
            {
                await root.Child("matchmaking").Child("queue").Child(CurrentUid).RemoveValueAsync();
                isQueued = false;
                QueueStateChanged?.Invoke(false, "Búsqueda cancelada.");
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("No se pudo cancelar la búsqueda: " + FriendlyException(exception));
            }
        }

        public async void LeaveMatch()
        {
            if (!IsSignedIn || activeMatch == null) return;

            MatchData matchToClose = activeMatch;
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "matchmaking/userMatches/" + matchToClose.player1Uid, null },
                { "matchmaking/userMatches/" + matchToClose.player2Uid, null },
                { "matchmaking/matches/" + matchToClose.matchId + "/status", "closed" }
            };

            try
            {
                await root.UpdateChildrenAsync(updates);
                activeMatch = null;
                activeMatchId = string.Empty;
                MatchChanged?.Invoke(null);
                QueueStateChanged?.Invoke(false, "Partida finalizada.");
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("No se pudo cerrar la partida: " + FriendlyException(exception));
            }
        }

        private bool CanAuthenticate(string fullName, string email, string password, bool registering)
        {
            if (!firebaseReady)
            {
                StatusChanged?.Invoke("Firebase todavía no está listo.");
                return false;
            }

            if (registering && string.IsNullOrWhiteSpace(fullName))
            {
                StatusChanged?.Invoke("Escribe tu nombre completo.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                StatusChanged?.Invoke("Completa el correo y la contraseña.");
                return false;
            }

            if (password.Length < 6)
            {
                StatusChanged?.Invoke("La contraseña debe tener al menos 6 caracteres.");
                return false;
            }

            return true;
        }

        private void HandleAuthStateChanged(object sender, EventArgs eventArgs)
        {
            if (auth.CurrentUser == null)
            {
                if (!string.IsNullOrEmpty(activeUid)) EndSession();
                return;
            }

            if (activeUid != auth.CurrentUser.UserId) BeginSession(auth.CurrentUser);
        }

        private async void BeginSession(FirebaseUser user)
        {
            if (user == null || root == null) return;
            if (!string.IsNullOrEmpty(activeUid) && activeUid != user.UserId) DetachRealtimeListeners();

            activeUid = user.UserId;
            string displayName = ResolveDisplayName(user);
            SessionChanged?.Invoke(true, displayName);
            AttachRealtimeListeners(user.UserId);

            try
            {
                await EnsureUserProfile(user, displayName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("No se pudo completar el perfil: " + exception.Message);
            }
        }

        private async Task EnsureUserProfile(FirebaseUser user, string displayName)
        {
            DataSnapshot snapshot = await root.Child("users").Child(user.UserId).GetValueAsync();
            if (snapshot.Exists) return;

            Dictionary<string, object> userData = new Dictionary<string, object>
            {
                { "uid", user.UserId },
                { "displayName", displayName },
                { "email", user.Email ?? string.Empty },
                { "createdAt", ServerValue.Timestamp }
            };
            await root.Child("users").Child(user.UserId).SetValueAsync(userData);
        }

        private void AttachRealtimeListeners(string uid)
        {
            DetachRealtimeListeners();

            connectedReference = database.GetReference(".info/connected");
            statusCollectionReference = root.Child("status");
            requestsReference = root.Child("friendRequests").Child(uid);
            friendsReference = root.Child("friends").Child(uid);
            queueReference = root.Child("matchmaking").Child("queue");
            userMatchReference = root.Child("matchmaking").Child("userMatches").Child(uid);

            connectedReference.ValueChanged += HandleConnectionChanged;
            statusCollectionReference.ValueChanged += HandlePresenceChanged;
            requestsReference.ValueChanged += HandleRequestsChanged;
            friendsReference.ValueChanged += HandleFriendsChanged;
            queueReference.ValueChanged += HandleQueueChanged;
            userMatchReference.ValueChanged += HandleUserMatchChanged;
        }

        private void DetachRealtimeListeners()
        {
            if (connectedReference != null) connectedReference.ValueChanged -= HandleConnectionChanged;
            if (statusCollectionReference != null) statusCollectionReference.ValueChanged -= HandlePresenceChanged;
            if (requestsReference != null) requestsReference.ValueChanged -= HandleRequestsChanged;
            if (friendsReference != null) friendsReference.ValueChanged -= HandleFriendsChanged;
            if (queueReference != null) queueReference.ValueChanged -= HandleQueueChanged;
            if (userMatchReference != null) userMatchReference.ValueChanged -= HandleUserMatchChanged;

            connectedReference = null;
            statusCollectionReference = null;
            requestsReference = null;
            friendsReference = null;
            queueReference = null;
            userMatchReference = null;
        }

        private async void HandleConnectionChanged(object sender, ValueChangedEventArgs eventArgs)
        {
            if (eventArgs.DatabaseError != null || !IsSignedIn) return;
            bool connected = eventArgs.Snapshot.Value != null && Convert.ToBoolean(eventArgs.Snapshot.Value);
            if (!connected) return;

            try
            {
                await SetPresence(CurrentUid, CurrentDisplayName, true);
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("Error al actualizar la presencia: " + FriendlyException(exception));
            }
        }

        private async Task SetPresence(string uid, string displayName, bool online)
        {
            DatabaseReference statusReference = root.Child("status").Child(uid);
            Dictionary<string, object> offlineData = PresenceDictionary(uid, displayName, false);

            if (online)
            {
                await statusReference.OnDisconnect().SetValue(offlineData);
                await statusReference.SetValueAsync(PresenceDictionary(uid, displayName, true));
            }
            else
            {
                await statusReference.OnDisconnect().Cancel();
                await statusReference.SetValueAsync(offlineData);
            }
        }

        private static Dictionary<string, object> PresenceDictionary(string uid, string displayName, bool online)
        {
            return new Dictionary<string, object>
            {
                { "uid", uid },
                { "displayName", displayName },
                { "state", online ? "online" : "offline" },
                { "lastChanged", ServerValue.Timestamp }
            };
        }

        private void HandlePresenceChanged(object sender, ValueChangedEventArgs eventArgs)
        {
            if (eventArgs.DatabaseError != null)
            {
                StatusChanged?.Invoke("Error de presencia: " + eventArgs.DatabaseError.Message);
                return;
            }

            Dictionary<string, PresenceData> updated = new Dictionary<string, PresenceData>();
            foreach (DataSnapshot child in eventArgs.Snapshot.Children)
            {
                PresenceData presence = ReadPresence(child);
                if (!string.IsNullOrEmpty(presence.uid)) updated[presence.uid] = presence;
            }

            foreach (KeyValuePair<string, FriendData> friendPair in friendsByUid)
            {
                bool online = updated.ContainsKey(friendPair.Key) && updated[friendPair.Key].IsOnline;
                bool previous;
                if (knownFriendStates.TryGetValue(friendPair.Key, out previous) && previous != online)
                {
                    FriendPresenceNotification?.Invoke(friendPair.Value.displayName, online);
                }
                knownFriendStates[friendPair.Key] = online;
                friendPair.Value.isOnline = online;
            }

            presenceByUid.Clear();
            foreach (KeyValuePair<string, PresenceData> pair in updated) presenceByUid[pair.Key] = pair.Value;

            OnlineUsersChanged?.Invoke(updated.Values.Where(p => p.IsOnline)
                .OrderBy(p => p.displayName, StringComparer.OrdinalIgnoreCase).ToList());
            PublishFriends();
        }

        private void HandleRequestsChanged(object sender, ValueChangedEventArgs eventArgs)
        {
            if (eventArgs.DatabaseError != null) return;
            List<FriendRequestData> requests = new List<FriendRequestData>();
            foreach (DataSnapshot child in eventArgs.Snapshot.Children)
            {
                requests.Add(new FriendRequestData
                {
                    senderUid = StringValue(child, "senderUid", child.Key),
                    senderName = StringValue(child, "senderName", "Usuario"),
                    sentAt = LongValue(child, "sentAt")
                });
            }
            FriendRequestsChanged?.Invoke(requests.OrderByDescending(r => r.sentAt).ToList());
        }

        private void HandleFriendsChanged(object sender, ValueChangedEventArgs eventArgs)
        {
            if (eventArgs.DatabaseError != null) return;

            Dictionary<string, FriendData> updated = new Dictionary<string, FriendData>();
            foreach (DataSnapshot child in eventArgs.Snapshot.Children)
            {
                string uid = StringValue(child, "uid", child.Key);
                FriendData friend = new FriendData
                {
                    uid = uid,
                    displayName = StringValue(child, "displayName", "Usuario"),
                    friendsSince = LongValue(child, "friendsSince"),
                    isOnline = presenceByUid.ContainsKey(uid) && presenceByUid[uid].IsOnline
                };
                updated[uid] = friend;
                if (!knownFriendStates.ContainsKey(uid)) knownFriendStates[uid] = friend.isOnline;
            }

            foreach (string removedUid in friendsByUid.Keys.Where(k => !updated.ContainsKey(k)).ToList())
            {
                knownFriendStates.Remove(removedUid);
            }

            friendsByUid.Clear();
            foreach (KeyValuePair<string, FriendData> pair in updated) friendsByUid[pair.Key] = pair.Value;
            PublishFriends();
        }

        private void PublishFriends()
        {
            FriendsChanged?.Invoke(friendsByUid.Values
                .OrderByDescending(f => f.isOnline)
                .ThenBy(f => f.displayName, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        private void HandleQueueChanged(object sender, ValueChangedEventArgs eventArgs)
        {
            if (eventArgs.DatabaseError != null || !IsSignedIn) return;

            List<QueuePlayerData> queue = new List<QueuePlayerData>();
            foreach (DataSnapshot child in eventArgs.Snapshot.Children)
            {
                queue.Add(new QueuePlayerData
                {
                    uid = StringValue(child, "uid", child.Key),
                    displayName = StringValue(child, "displayName", "Jugador"),
                    joinedAt = LongValue(child, "joinedAt")
                });
            }
            queue = queue.OrderBy(player => player.joinedAt).ThenBy(player => player.uid, StringComparer.Ordinal).ToList();

            int ownIndex = queue.FindIndex(player => player.uid == CurrentUid);
            isQueued = ownIndex >= 0;
            if (isQueued && activeMatch == null)
            {
                QueueStateChanged?.Invoke(true, "En cola · " + queue.Count + (queue.Count == 1 ? " jugador" : " jugadores"));
            }

            if (ownIndex >= 0 && ownIndex % 2 == 0 && ownIndex + 1 < queue.Count && !isCreatingMatch && activeMatch == null)
            {
                TryCreateMatch(queue[ownIndex], queue[ownIndex + 1]);
            }
        }

        private async void TryCreateMatch(QueuePlayerData first, QueuePlayerData second)
        {
            isCreatingMatch = true;
            string firstUid = string.CompareOrdinal(first.uid, second.uid) <= 0 ? first.uid : second.uid;
            string secondUid = firstUid == first.uid ? second.uid : first.uid;
            string matchId = firstUid + "_" + secondUid;

            Dictionary<string, object> match = new Dictionary<string, object>
            {
                { "matchId", matchId },
                { "player1Uid", first.uid },
                { "player1Name", first.displayName },
                { "player2Uid", second.uid },
                { "player2Name", second.displayName },
                { "status", "ready" },
                { "createdAt", ServerValue.Timestamp }
            };
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "matchmaking/matches/" + matchId, match },
                { "matchmaking/userMatches/" + first.uid, matchId },
                { "matchmaking/userMatches/" + second.uid, matchId },
                { "matchmaking/queue/" + first.uid, null },
                { "matchmaking/queue/" + second.uid, null }
            };

            try
            {
                await root.UpdateChildrenAsync(updates);
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("No se pudo crear el match: " + FriendlyException(exception));
            }
            finally
            {
                isCreatingMatch = false;
            }
        }

        private async void HandleUserMatchChanged(object sender, ValueChangedEventArgs eventArgs)
        {
            if (eventArgs.DatabaseError != null || !IsSignedIn) return;
            string matchId = eventArgs.Snapshot.Value != null ? eventArgs.Snapshot.Value.ToString() : string.Empty;

            if (string.IsNullOrEmpty(matchId))
            {
                if (activeMatch != null)
                {
                    activeMatch = null;
                    activeMatchId = string.Empty;
                    MatchChanged?.Invoke(null);
                }
                return;
            }

            if (matchId == activeMatchId && activeMatch != null) return;
            try
            {
                DataSnapshot snapshot = await root.Child("matchmaking").Child("matches").Child(matchId).GetValueAsync();
                if (!snapshot.Exists) return;
                activeMatchId = matchId;
                activeMatch = ReadMatch(snapshot);
                isQueued = false;
                QueueStateChanged?.Invoke(false, "¡Partida encontrada!");
                MatchChanged?.Invoke(activeMatch);
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke("No se pudo abrir el match: " + FriendlyException(exception));
            }
        }

        private void EndSession()
        {
            DetachRealtimeListeners();
            activeUid = string.Empty;
            activeMatchId = string.Empty;
            activeMatch = null;
            isQueued = false;
            presenceByUid.Clear();
            friendsByUid.Clear();
            knownFriendStates.Clear();
            SessionChanged?.Invoke(false, string.Empty);
            MatchChanged?.Invoke(null);
            StatusChanged?.Invoke("Sesión cerrada.");
        }

        private void ValidateConfiguration()
        {
            string projectId = app.Options.ProjectId ?? string.Empty;
            if (!string.IsNullOrEmpty(databaseUrl) && !string.IsNullOrEmpty(projectId) &&
                databaseUrl.IndexOf(projectId, StringComparison.OrdinalIgnoreCase) < 0)
            {
                ConfigurationWarning?.Invoke(
                    "CONFIGURACIÓN INCOMPATIBLE: google-services.json pertenece a '" + projectId +
                    "', pero la URL indicada apunta a otro proyecto. Reemplaza el archivo por el descargado desde esa misma base de datos.");
            }
        }

        private static PresenceData ReadPresence(DataSnapshot snapshot)
        {
            return new PresenceData
            {
                uid = StringValue(snapshot, "uid", snapshot.Key),
                displayName = StringValue(snapshot, "displayName", "Usuario"),
                state = StringValue(snapshot, "state", "offline"),
                lastChanged = LongValue(snapshot, "lastChanged")
            };
        }

        private static MatchData ReadMatch(DataSnapshot snapshot)
        {
            return new MatchData
            {
                matchId = StringValue(snapshot, "matchId", snapshot.Key),
                player1Uid = StringValue(snapshot, "player1Uid", string.Empty),
                player1Name = StringValue(snapshot, "player1Name", "Jugador 1"),
                player2Uid = StringValue(snapshot, "player2Uid", string.Empty),
                player2Name = StringValue(snapshot, "player2Name", "Jugador 2"),
                status = StringValue(snapshot, "status", "ready"),
                createdAt = LongValue(snapshot, "createdAt")
            };
        }

        private static string StringValue(DataSnapshot snapshot, string child, string fallback)
        {
            object value = snapshot.Child(child).Value;
            return value == null || string.IsNullOrEmpty(value.ToString()) ? fallback : value.ToString();
        }

        private static long LongValue(DataSnapshot snapshot, string child)
        {
            object value = snapshot.Child(child).Value;
            if (value == null) return 0L;
            try { return Convert.ToInt64(value); }
            catch { return 0L; }
        }

        private static string ResolveDisplayName(FirebaseUser user)
        {
            if (user == null) return "Usuario";
            if (!string.IsNullOrWhiteSpace(user.DisplayName)) return user.DisplayName;
            if (!string.IsNullOrWhiteSpace(user.Email)) return user.Email.Split('@')[0];
            return "Usuario";
        }

        private static string NormalizeDatabaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            return url.Trim().TrimEnd('/') + "/";
        }

        private static string FriendlyAuthException(Exception exception)
        {
            FirebaseException firebaseException = FindFirebaseException(exception);
            if (firebaseException == null) return FriendlyException(exception);

            AuthError error = (AuthError)firebaseException.ErrorCode;
            switch (error)
            {
                case AuthError.InvalidEmail: return "el correo no es válido.";
                case AuthError.WrongPassword: return "la contraseña es incorrecta.";
                case AuthError.UserNotFound: return "no existe una cuenta con ese correo.";
                case AuthError.EmailAlreadyInUse: return "ese correo ya está registrado.";
                case AuthError.WeakPassword: return "la contraseña es demasiado débil.";
                case AuthError.NetworkRequestFailed: return "no hay conexión con Firebase.";
                default: return firebaseException.Message;
            }
        }

        private static FirebaseException FindFirebaseException(Exception exception)
        {
            if (exception is FirebaseException direct) return direct;
            AggregateException aggregate = exception as AggregateException;
            if (aggregate != null)
            {
                foreach (Exception inner in aggregate.Flatten().InnerExceptions)
                {
                    FirebaseException firebase = inner as FirebaseException;
                    if (firebase != null) return firebase;
                }
            }
            return exception.InnerException as FirebaseException;
        }

        private static string FriendlyException(Exception exception)
        {
            if (exception == null) return "error desconocido.";
            AggregateException aggregate = exception as AggregateException;
            if (aggregate != null && aggregate.Flatten().InnerExceptions.Count > 0)
                return aggregate.Flatten().InnerExceptions[0].Message;
            return exception.InnerException != null ? exception.InnerException.Message : exception.Message;
        }

        private void OnDestroy()
        {
            DetachRealtimeListeners();
            if (auth != null) auth.StateChanged -= HandleAuthStateChanged;
        }
    }
}
