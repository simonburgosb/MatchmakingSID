using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Sid2026.Matchmaking
{
    /// <summary>
    /// Interfaz responsive construida en tiempo de ejecución para evitar dependencias de prefabs.
    /// </summary>
    public sealed class MatchmakingUI : MonoBehaviour
    {
        private static readonly Color32 Background = new Color32(7, 14, 27, 255);
        private static readonly Color32 Surface = new Color32(14, 25, 44, 255);
        private static readonly Color32 SurfaceRaised = new Color32(21, 35, 58, 255);
        private static readonly Color32 Primary = new Color32(44, 201, 180, 255);
        private static readonly Color32 Accent = new Color32(72, 125, 255, 255);
        private static readonly Color32 Danger = new Color32(239, 91, 112, 255);
        private static readonly Color32 TextMain = new Color32(237, 244, 255, 255);
        private static readonly Color32 TextMuted = new Color32(143, 160, 188, 255);

        private FirebaseMatchmakingService service;
        private GameObject authPanel;
        private GameObject mainPanel;
        private GameObject warningPanel;
        private Text warningText;
        private InputField nameInput;
        private InputField emailInput;
        private InputField passwordInput;
        private Text authStatus;
        private Text currentUserText;
        private Text footerStatus;
        private Text requestsNavLabel;
        private RectTransform onlineContent;
        private RectTransform requestsContent;
        private RectTransform friendsContent;
        private readonly List<GameObject> pages = new List<GameObject>();
        private readonly List<Button> navigationButtons = new List<Button>();
        private Text matchmakingStatus;
        private Text matchmakingDetail;
        private Button matchmakingButton;
        private Button leaveMatchButton;
        private GameObject matchCard;
        private Text opponentText;
        private GameObject toast;
        private Text toastText;
        private Coroutine toastRoutine;
        private bool queued;
        private MatchData currentMatch;
        private IReadOnlyList<PresenceData> latestOnlineUsers = new List<PresenceData>();
        private IReadOnlyList<FriendData> latestFriends = new List<FriendData>();
        private IReadOnlyList<FriendRequestData> latestRequests = new List<FriendRequestData>();

        public void Build(string ownerFullName)
        {
            Canvas canvas = RuntimeUiFactory.CreateCanvas(transform);
            Image background = RuntimeUiFactory.Panel("Background", canvas.transform, Background,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            BuildHeader(background.transform, ownerFullName);
            BuildWarning(background.transform);
            BuildAuthentication(background.transform);
            BuildMainApplication(background.transform);
            BuildToast(background.transform);

            warningPanel.SetActive(false);
            mainPanel.SetActive(false);
            authPanel.SetActive(true);
        }

        public void Bind(FirebaseMatchmakingService firebaseService)
        {
            service = firebaseService;
            service.StatusChanged += HandleStatus;
            service.ConfigurationWarning += HandleConfigurationWarning;
            service.SessionChanged += HandleSession;
            service.OnlineUsersChanged += HandleOnlineUsers;
            service.FriendRequestsChanged += HandleFriendRequests;
            service.FriendsChanged += HandleFriends;
            service.FriendPresenceNotification += HandleFriendPresenceNotification;
            service.QueueStateChanged += HandleQueueState;
            service.MatchChanged += HandleMatch;
        }

        private void BuildHeader(Transform parent, string ownerFullName)
        {
            Image header = RuntimeUiFactory.Panel("Header", parent, new Color32(10, 19, 34, 255),
                new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -84f), Vector2.zero);
            Image brandMark = RuntimeUiFactory.Panel("BrandMark", header.transform, Primary,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, -23f), new Vector2(76f, 23f));
            Text mark = RuntimeUiFactory.Label("Mark", brandMark.transform, "M", 27, Background,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            Text title = RuntimeUiFactory.Label("Title", header.transform, "MATCHROOM", 27, TextMain,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(92f, 0f);
            title.rectTransform.offsetMax = Vector2.zero;

            Text owner = RuntimeUiFactory.Label("Owner", header.transform,
                "ACTIVIDAD 6  •  " + ownerFullName.ToUpperInvariant(), 18, TextMuted,
                TextAnchor.MiddleRight, FontStyle.Bold);
            owner.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            owner.rectTransform.anchorMax = Vector2.one;
            owner.rectTransform.offsetMin = Vector2.zero;
            owner.rectTransform.offsetMax = new Vector2(-34f, 0f);
        }

        private void BuildWarning(Transform parent)
        {
            Image panel = RuntimeUiFactory.Panel("ConfigurationWarning", parent, new Color32(92, 49, 24, 255),
                new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -148f), new Vector2(-20f, -94f));
            warningPanel = panel.gameObject;
            warningText = RuntimeUiFactory.Label("WarningText", panel.transform, string.Empty, 16,
                new Color32(255, 218, 171, 255), TextAnchor.MiddleCenter, FontStyle.Bold);
            warningText.rectTransform.offsetMin = new Vector2(16f, 4f);
            warningText.rectTransform.offsetMax = new Vector2(-16f, -4f);
        }

        private void BuildAuthentication(Transform parent)
        {
            authPanel = RuntimeUiFactory.Rect("Authentication", parent, Vector2.zero, Vector2.one,
                new Vector2(0f, 0f), new Vector2(0f, -84f)).gameObject;

            Image card = RuntimeUiFactory.Panel("AuthCard", authPanel.transform, Surface,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330f, -300f),
                new Vector2(330f, 300f));

            Text eyebrow = RuntimeUiFactory.Label("Eyebrow", card.transform, "FIREBASE  •  TIEMPO REAL", 15,
                Primary, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetRect(eyebrow.rectTransform, 44f, -65f, -44f, -30f, true);

            Text title = RuntimeUiFactory.Label("Title", card.transform, "Encuentra tu próxima partida", 31,
                TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetRect(title.rectTransform, 44f, -121f, -44f, -66f, true);

            Text subtitle = RuntimeUiFactory.Label("Subtitle", card.transform,
                "Regístrate o inicia sesión para ver usuarios conectados.", 18, TextMuted, TextAnchor.MiddleLeft);
            SetRect(subtitle.rectTransform, 44f, -162f, -44f, -122f, true);

            nameInput = RuntimeUiFactory.Input("FullName", card.transform, "Nombre completo");
            SetRect(nameInput.GetComponent<RectTransform>(), 44f, -238f, -44f, -182f, true);
            emailInput = RuntimeUiFactory.Input("Email", card.transform, "Correo electrónico");
            emailInput.contentType = InputField.ContentType.EmailAddress;
            emailInput.keyboardType = TouchScreenKeyboardType.EmailAddress;
            SetRect(emailInput.GetComponent<RectTransform>(), 44f, -306f, -44f, -250f, true);
            passwordInput = RuntimeUiFactory.Input("Password", card.transform, "Contraseña (mínimo 6 caracteres)", true);
            SetRect(passwordInput.GetComponent<RectTransform>(), 44f, -374f, -44f, -318f, true);

            Button login = RuntimeUiFactory.Button("Login", card.transform, "INICIAR SESIÓN", Accent, Color.white, 18);
            SetRect(login.GetComponent<RectTransform>(), 44f, -446f, -8f, -390f, true, 0.5f);
            Button register = RuntimeUiFactory.Button("Register", card.transform, "CREAR CUENTA", Primary, Background, 18);
            register.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1f);
            register.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            register.GetComponent<RectTransform>().offsetMin = new Vector2(8f, -446f);
            register.GetComponent<RectTransform>().offsetMax = new Vector2(-44f, -390f);

            login.onClick.AddListener(() => service?.SignIn(emailInput.text, passwordInput.text));
            register.onClick.AddListener(() => service?.Register(nameInput.text, emailInput.text, passwordInput.text));

            authStatus = RuntimeUiFactory.Label("Status", card.transform, "Inicializando Firebase…", 17,
                TextMuted, TextAnchor.MiddleCenter);
            SetRect(authStatus.rectTransform, 44f, -530f, -44f, -466f, true);
        }

        private void BuildMainApplication(Transform parent)
        {
            mainPanel = RuntimeUiFactory.Rect("MainApplication", parent, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(0f, -84f)).gameObject;

            Image sidebar = RuntimeUiFactory.Panel("Sidebar", mainPanel.transform, new Color32(10, 20, 36, 255),
                Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(270f, 0f));

            Text onlineCaption = RuntimeUiFactory.Label("SignedInLabel", sidebar.transform, "SESIÓN ACTIVA", 14,
                Primary, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetRect(onlineCaption.rectTransform, 28f, -49f, -22f, -20f, true);
            currentUserText = RuntimeUiFactory.Label("CurrentUser", sidebar.transform, "Usuario", 22, TextMain,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            SetRect(currentUserText.rectTransform, 28f, -92f, -22f, -50f, true);

            CreateNavigationButton(sidebar.transform, "Usuarios en línea", 0, 132f);
            CreateNavigationButton(sidebar.transform, "Solicitudes", 1, 194f);
            requestsNavLabel = navigationButtons[1].GetComponentInChildren<Text>();
            CreateNavigationButton(sidebar.transform, "Mis amigos", 2, 256f);
            CreateNavigationButton(sidebar.transform, "Matchmaking", 3, 318f);

            Button signOut = RuntimeUiFactory.Button("SignOut", sidebar.transform, "CERRAR SESIÓN",
                new Color32(32, 47, 69, 255), TextMuted, 16);
            signOut.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            signOut.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0f);
            signOut.GetComponent<RectTransform>().offsetMin = new Vector2(24f, 28f);
            signOut.GetComponent<RectTransform>().offsetMax = new Vector2(-24f, 80f);
            signOut.onClick.AddListener(() => service?.SignOut());

            RectTransform pageHost = RuntimeUiFactory.Rect("Pages", mainPanel.transform, Vector2.zero, Vector2.one,
                new Vector2(294f, 56f), new Vector2(-26f, -24f));
            onlineContent = CreateListPage(pageHost, "Usuarios conectados", "Envía una solicitud y empieza a formar tu equipo.");
            requestsContent = CreateListPage(pageHost, "Buzón de solicitudes", "Acepta o rechaza invitaciones de otros usuarios.");
            friendsContent = CreateListPage(pageHost, "Mis amigos", "Recibirás una notificación cuando cambie su conexión.");
            CreateMatchmakingPage(pageHost);

            footerStatus = RuntimeUiFactory.Label("FooterStatus", mainPanel.transform, "Listo", 15, TextMuted,
                TextAnchor.MiddleLeft);
            footerStatus.rectTransform.anchorMin = new Vector2(0f, 0f);
            footerStatus.rectTransform.anchorMax = new Vector2(1f, 0f);
            footerStatus.rectTransform.offsetMin = new Vector2(294f, 13f);
            footerStatus.rectTransform.offsetMax = new Vector2(-26f, 47f);

            ShowPage(0);
        }

        private void CreateNavigationButton(Transform parent, string label, int index, float top)
        {
            Button button = RuntimeUiFactory.Button("Nav" + index, parent, label,
                index == 0 ? new Color32(28, 55, 72, 255) : new Color32(10, 20, 36, 255),
                index == 0 ? Primary : TextMuted, 18);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(18f, -top - 52f);
            rect.offsetMax = new Vector2(-18f, -top);
            int capturedIndex = index;
            button.onClick.AddListener(() => ShowPage(capturedIndex));
            navigationButtons.Add(button);
        }

        private RectTransform CreateListPage(Transform parent, string titleValue, string subtitleValue)
        {
            RectTransform page = RuntimeUiFactory.Rect("Page" + pages.Count, parent, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            pages.Add(page.gameObject);

            Text title = RuntimeUiFactory.Label("Title", page, titleValue, 32, TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetRect(title.rectTransform, 4f, -52f, -4f, -4f, true);
            Text subtitle = RuntimeUiFactory.Label("Subtitle", page, subtitleValue, 18, TextMuted, TextAnchor.MiddleLeft);
            SetRect(subtitle.rectTransform, 4f, -94f, -4f, -54f, true);

            RectTransform listFrame = RuntimeUiFactory.Rect("ListFrame", page, Vector2.zero, Vector2.one,
                new Vector2(0f, 0f), new Vector2(0f, -112f));
            ScrollRect scrollRect;
            return RuntimeUiFactory.ScrollContent("Scroll", listFrame, out scrollRect);
        }

        private void CreateMatchmakingPage(Transform parent)
        {
            RectTransform page = RuntimeUiFactory.Rect("PageMatchmaking", parent, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            pages.Add(page.gameObject);

            Text title = RuntimeUiFactory.Label("Title", page, "Matchmaking", 32, TextMain,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            SetRect(title.rectTransform, 4f, -52f, -4f, -4f, true);
            Text subtitle = RuntimeUiFactory.Label("Subtitle", page,
                "Entra a la cola; los clientes conectados forman parejas automáticamente.", 18, TextMuted,
                TextAnchor.MiddleLeft);
            SetRect(subtitle.rectTransform, 4f, -94f, -4f, -54f, true);

            Image card = RuntimeUiFactory.Panel("QueueCard", page, Surface, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-360f, -220f), new Vector2(360f, 190f));
            Image icon = RuntimeUiFactory.Panel("Icon", card.transform, new Color32(22, 69, 76, 255),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-48f, -112f), new Vector2(48f, -16f));
            Text iconText = RuntimeUiFactory.Label("IconText", icon.transform, "↔", 38, Primary,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            matchmakingStatus = RuntimeUiFactory.Label("Status", card.transform, "Listo para buscar", 28,
                TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(matchmakingStatus.rectTransform, 32f, -174f, -32f, -120f, true);
            matchmakingDetail = RuntimeUiFactory.Label("Detail", card.transform,
                "Cuando otro usuario entre a la cola, Firebase creará un match para ambos.", 17,
                TextMuted, TextAnchor.MiddleCenter);
            SetRect(matchmakingDetail.rectTransform, 70f, -234f, -70f, -178f, true);

            matchmakingButton = RuntimeUiFactory.Button("QueueButton", card.transform, "BUSCAR PARTIDA",
                Primary, Background, 19);
            SetRect(matchmakingButton.GetComponent<RectTransform>(), 120f, -316f, -120f, -254f, true);
            matchmakingButton.onClick.AddListener(ToggleQueue);

            matchCard = RuntimeUiFactory.Panel("MatchFound", card.transform, new Color32(20, 54, 66, 255),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 18f), new Vector2(-40f, 132f)).gameObject;
            opponentText = RuntimeUiFactory.Label("Opponent", matchCard.transform, "Rival: —", 20,
                TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            opponentText.rectTransform.offsetMin = new Vector2(22f, 12f);
            opponentText.rectTransform.offsetMax = new Vector2(-190f, -12f);
            leaveMatchButton = RuntimeUiFactory.Button("LeaveMatch", matchCard.transform, "FINALIZAR",
                Danger, Color.white, 16);
            leaveMatchButton.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0.5f);
            leaveMatchButton.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            leaveMatchButton.GetComponent<RectTransform>().offsetMin = new Vector2(-174f, -25f);
            leaveMatchButton.GetComponent<RectTransform>().offsetMax = new Vector2(-18f, 25f);
            leaveMatchButton.onClick.AddListener(() => service?.LeaveMatch());
            matchCard.SetActive(false);
        }

        private void BuildToast(Transform parent)
        {
            Image panel = RuntimeUiFactory.Panel("Toast", parent, new Color32(25, 52, 70, 250),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-330f, -166f),
                new Vector2(330f, -104f));
            toast = panel.gameObject;
            toastText = RuntimeUiFactory.Label("ToastText", panel.transform, string.Empty, 18, TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            toastText.rectTransform.offsetMin = new Vector2(18f, 5f);
            toastText.rectTransform.offsetMax = new Vector2(-18f, -5f);
            toast.SetActive(false);
        }

        private void HandleStatus(string status)
        {
            if (authStatus != null) authStatus.text = status;
            if (footerStatus != null) footerStatus.text = status;
        }

        private void HandleConfigurationWarning(string message)
        {
            warningText.text = message;
            warningPanel.SetActive(true);
            ShowToast("Revisa la configuración de Firebase", false, 6f);
        }

        private void HandleSession(bool signedIn, string displayName)
        {
            authPanel.SetActive(!signedIn);
            mainPanel.SetActive(signedIn);
            if (signedIn)
            {
                currentUserText.text = displayName;
                ShowPage(0);
            }
            else
            {
                passwordInput.text = string.Empty;
                queued = false;
                currentMatch = null;
            }
        }

        private void HandleOnlineUsers(IReadOnlyList<PresenceData> users)
        {
            latestOnlineUsers = users ?? new List<PresenceData>();
            RenderOnlineUsers();
        }

        private void HandleFriendRequests(IReadOnlyList<FriendRequestData> requests)
        {
            latestRequests = requests ?? new List<FriendRequestData>();
            requestsNavLabel.text = latestRequests.Count > 0 ? "Solicitudes  (" + latestRequests.Count + ")" : "Solicitudes";
            ClearChildren(requestsContent);
            if (latestRequests.Count == 0)
            {
                AddEmptyState(requestsContent, "No tienes solicitudes pendientes.");
                return;
            }

            foreach (FriendRequestData request in latestRequests) AddRequestRow(request);
            RefreshList(requestsContent);
        }

        private void HandleFriends(IReadOnlyList<FriendData> friends)
        {
            latestFriends = friends ?? new List<FriendData>();
            RenderFriends();
            RenderOnlineUsers();
        }

        private void HandleFriendPresenceNotification(string friendName, bool online)
        {
            ShowToast(friendName + (online ? " se conectó" : " se desconectó"), online, 4f);
        }

        private void HandleQueueState(bool isQueued, string detail)
        {
            queued = isQueued;
            if (currentMatch != null) return;
            matchmakingStatus.text = isQueued ? "Buscando partida…" : "Listo para buscar";
            matchmakingDetail.text = detail;
            matchmakingButton.GetComponentInChildren<Text>().text = isQueued ? "CANCELAR BÚSQUEDA" : "BUSCAR PARTIDA";
            matchmakingButton.colors = BuildButtonColors(isQueued ? Danger : Primary);
        }

        private void HandleMatch(MatchData match)
        {
            currentMatch = match;
            bool found = match != null;
            matchCard.SetActive(found);
            matchmakingButton.gameObject.SetActive(!found);
            if (!found) return;

            queued = false;
            matchmakingStatus.text = "¡Partida encontrada!";
            matchmakingDetail.text = "La asignación quedó almacenada en Realtime Database para los dos clientes.";
            opponentText.text = "Rival: " + match.OpponentName(service.CurrentUid) + "\nSala: " + match.matchId;
            ShowPage(3);
            ShowToast("Match encontrado con " + match.OpponentName(service.CurrentUid), true, 5f);
        }

        private void RenderOnlineUsers()
        {
            if (onlineContent == null) return;
            ClearChildren(onlineContent);
            if (latestOnlineUsers.Count == 0)
            {
                AddEmptyState(onlineContent, "Todavía no hay usuarios conectados.");
                RefreshList(onlineContent);
                return;
            }

            HashSet<string> friendIds = new HashSet<string>(latestFriends.Select(friend => friend.uid));
            foreach (PresenceData user in latestOnlineUsers)
            {
                bool isSelf = service != null && user.uid == service.CurrentUid;
                bool isFriend = friendIds.Contains(user.uid);
                AddOnlineUserRow(user, isSelf, isFriend);
            }
            RefreshList(onlineContent);
        }

        private void RenderFriends()
        {
            if (friendsContent == null) return;
            ClearChildren(friendsContent);
            if (latestFriends.Count == 0)
            {
                AddEmptyState(friendsContent, "Aún no has agregado amigos.");
                return;
            }
            foreach (FriendData friend in latestFriends) AddFriendRow(friend);
            RefreshList(friendsContent);
        }

        private void AddOnlineUserRow(PresenceData user, bool isSelf, bool isFriend)
        {
            Image row = RuntimeUiFactory.Panel("User_" + user.uid, onlineContent, SurfaceRaised,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RuntimeUiFactory.Row(row.gameObject, 82f);
            AddAvatar(row.transform, user.displayName, true);
            AddRowTexts(row.transform, user.displayName, isSelf ? "Tú · conectado" : "En línea ahora");

            string buttonLabel = isSelf ? "TU PERFIL" : (isFriend ? "AMIGO" : "AGREGAR");
            Button action = AddRowButton(row.transform, buttonLabel, isFriend || isSelf ? Surface : Accent);
            action.interactable = !isSelf && !isFriend;
            action.onClick.AddListener(() => service?.SendFriendRequest(user.uid, user.displayName));
        }

        private void AddRequestRow(FriendRequestData request)
        {
            Image row = RuntimeUiFactory.Panel("Request_" + request.senderUid, requestsContent, SurfaceRaised,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RuntimeUiFactory.Row(row.gameObject, 92f);
            AddAvatar(row.transform, request.senderName, true);
            AddRowTexts(row.transform, request.senderName, "Quiere agregarte como amigo");

            Button reject = RuntimeUiFactory.Button("Reject", row.transform, "RECHAZAR", Surface, TextMuted, 14);
            reject.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0.5f);
            reject.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            reject.GetComponent<RectTransform>().offsetMin = new Vector2(-306f, -23f);
            reject.GetComponent<RectTransform>().offsetMax = new Vector2(-166f, 23f);
            reject.onClick.AddListener(() => service?.RejectFriendRequest(request));

            Button accept = RuntimeUiFactory.Button("Accept", row.transform, "ACEPTAR", Primary, Background, 14);
            accept.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0.5f);
            accept.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0.5f);
            accept.GetComponent<RectTransform>().offsetMin = new Vector2(-150f, -23f);
            accept.GetComponent<RectTransform>().offsetMax = new Vector2(-18f, 23f);
            accept.onClick.AddListener(() => service?.AcceptFriendRequest(request));
        }

        private void AddFriendRow(FriendData friend)
        {
            Image row = RuntimeUiFactory.Panel("Friend_" + friend.uid, friendsContent, SurfaceRaised,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RuntimeUiFactory.Row(row.gameObject, 82f);
            AddAvatar(row.transform, friend.displayName, friend.isOnline);
            AddRowTexts(row.transform, friend.displayName, friend.isOnline ? "En línea" : "Desconectado");

            Text state = RuntimeUiFactory.Label("State", row.transform, friend.isOnline ? "●  DISPONIBLE" : "●  OFFLINE",
                14, friend.isOnline ? Primary : TextMuted, TextAnchor.MiddleRight, FontStyle.Bold);
            state.rectTransform.anchorMin = new Vector2(1f, 0f);
            state.rectTransform.anchorMax = Vector2.one;
            state.rectTransform.offsetMin = new Vector2(-220f, 0f);
            state.rectTransform.offsetMax = new Vector2(-22f, 0f);
        }

        private static void AddAvatar(Transform parent, string displayName, bool online)
        {
            Image avatar = RuntimeUiFactory.Panel("Avatar", parent,
                online ? new Color32(28, 84, 83, 255) : new Color32(45, 55, 72, 255),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -27f), new Vector2(72f, 27f));
            string initial = string.IsNullOrWhiteSpace(displayName) ? "?" : displayName.Trim().Substring(0, 1).ToUpperInvariant();
            Text initialText = RuntimeUiFactory.Label("Initial", avatar.transform, initial, 23,
                online ? Primary : TextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private static void AddRowTexts(Transform parent, string titleValue, string subtitleValue)
        {
            Text title = RuntimeUiFactory.Label("Name", parent, titleValue, 20, TextMain,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(90f, -2f);
            title.rectTransform.offsetMax = new Vector2(-330f, -8f);

            Text subtitle = RuntimeUiFactory.Label("Subtitle", parent, subtitleValue, 15, TextMuted,
                TextAnchor.MiddleLeft);
            subtitle.rectTransform.anchorMin = Vector2.zero;
            subtitle.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            subtitle.rectTransform.offsetMin = new Vector2(90f, 8f);
            subtitle.rectTransform.offsetMax = new Vector2(-330f, 2f);
        }

        private static Button AddRowButton(Transform parent, string label, Color color)
        {
            Button button = RuntimeUiFactory.Button("Action", parent, label, color,
                color == Primary ? Background : TextMain, 14);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-174f, -24f);
            rect.offsetMax = new Vector2(-18f, 24f);
            return button;
        }

        private static void AddEmptyState(Transform content, string message)
        {
            Image row = RuntimeUiFactory.Panel("Empty", content, new Color32(15, 27, 47, 255),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RuntimeUiFactory.Row(row.gameObject, 110f);
            Text text = RuntimeUiFactory.Label("Message", row.transform, message, 18, TextMuted,
                TextAnchor.MiddleCenter, FontStyle.Italic);
            text.rectTransform.offsetMin = new Vector2(20f, 10f);
            text.rectTransform.offsetMax = new Vector2(-20f, -10f);
        }

        private void ToggleQueue()
        {
            if (service == null) return;
            if (queued) service.CancelMatchmaking();
            else service.EnterMatchmaking();
        }

        private void ShowPage(int index)
        {
            for (int i = 0; i < pages.Count; i++) pages[i].SetActive(i == index);
            for (int i = 0; i < navigationButtons.Count; i++)
            {
                bool selected = i == index;
                Color background = selected ? new Color32(28, 55, 72, 255) : new Color32(10, 20, 36, 255);
                navigationButtons[i].colors = BuildButtonColors(background);
                navigationButtons[i].GetComponentInChildren<Text>().color = selected ? Primary : TextMuted;
            }
        }

        private void ShowToast(string message, bool positive, float duration)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toast.GetComponent<Image>().color = positive
                ? new Color32(21, 77, 72, 250)
                : new Color32(93, 48, 53, 250);
            toastText.text = message;
            toast.SetActive(true);
            toastRoutine = StartCoroutine(HideToastAfter(duration));
        }

        private IEnumerator HideToastAfter(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            toast.SetActive(false);
            toastRoutine = null;
        }

        private static ColorBlock BuildButtonColors(Color color)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.35f);
            return colors;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static void SetRect(RectTransform rect, float left, float bottom, float right, float top,
            bool anchoredTop, float maxAnchorX = 1f)
        {
            rect.anchorMin = anchoredTop ? new Vector2(0f, 1f) : Vector2.zero;
            rect.anchorMax = anchoredTop ? new Vector2(maxAnchorX, 1f) : Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
        private static void RefreshList(RectTransform content)
        {
            if (content == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            RectTransform viewport = content.parent as RectTransform;

            if (viewport != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);

            ScrollRect scrollRect = content.GetComponentInParent<ScrollRect>();

            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;

            Canvas.ForceUpdateCanvases();
        }
        private void OnDestroy()
        {
            if (service == null) return;
            service.StatusChanged -= HandleStatus;
            service.ConfigurationWarning -= HandleConfigurationWarning;
            service.SessionChanged -= HandleSession;
            service.OnlineUsersChanged -= HandleOnlineUsers;
            service.FriendRequestsChanged -= HandleFriendRequests;
            service.FriendsChanged -= HandleFriends;
            service.FriendPresenceNotification -= HandleFriendPresenceNotification;
            service.QueueStateChanged -= HandleQueueState;
            service.MatchChanged -= HandleMatch;
        }
    }
}
