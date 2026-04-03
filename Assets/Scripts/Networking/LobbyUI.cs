// LobbyUI.cs
// CYBERNOMAD -- PLAGA '44
// World-space lobby UI: Create/Join room, player list, ready status.
// Uses UnityEngine.UI (legacy UI) -- same as SplashScreen.cs pattern.
// No external dependencies. NetworkManager must exist in scene.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plaga44.Networking
{
    /// <summary>
    /// Tracks one player entry visible in the lobby list.
    /// </summary>
    [Serializable]
    public struct LobbyPlayerEntry
    {
        public int    PlayerId;
        public string DisplayName;
        public bool   IsReady;
        public bool   IsLocal;
    }

    /// <summary>
    /// World-space lobby panel. Attach to any GameObject.
    /// The panel spawns its own Canvas and builds the UI at runtime.
    ///
    /// Layout:
    ///   [PLAGA '44 -- LOBBY]
    ///   Room ID: [______] [Create] [Join]
    ///   --------------------------------
    ///   Players:
    ///     > P0 (You)  [READY]
    ///     > P1        [ --- ]
    ///   --------------------------------
    ///   [READY UP]          [LEAVE]
    ///   Status: Waiting for players...
    /// </summary>
    public sealed class LobbyUI : MonoBehaviour
    {
        // ---- Inspector ----
        [Header("Placement")]
        [Tooltip("Distance in front of the player camera when auto-placed.")]
        public float PlacementDistance = 1.5f;

        [Tooltip("Height offset from camera position.")]
        public float HeightOffset = -0.1f;

        [Tooltip("World-space canvas scale.")]
        public float CanvasScale = 0.001f;

        [Tooltip("Canvas width in pixels (UI units).")]
        public float CanvasWidth = 600f;

        [Tooltip("Canvas height in pixels (UI units).")]
        public float CanvasHeight = 500f;

        [Header("Appearance")]
        public Color BackgroundColor  = new Color(0.05f, 0.05f, 0.08f, 0.97f);
        public Color AccentColor      = new Color(0.8f, 0.15f, 0.1f, 1f);   // red
        public Color TextColor        = Color.white;
        public Color ReadyColor       = new Color(0.2f, 0.85f, 0.3f, 1f);
        public Color NotReadyColor    = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("Behaviour")]
        [Tooltip("Show/hide lobby on Start. Can also toggle at runtime via ShowLobby()/HideLobby().")]
        public bool ShowOnStart = true;

        // ---- Private: UI refs ----
        private Canvas        _canvas;
        private RectTransform _canvasRect;
        private InputField    _roomIdInput;
        private Button        _createBtn;
        private Button        _joinBtn;
        private Button        _readyBtn;
        private Button        _leaveBtn;
        private Text          _statusText;
        private Transform     _playerListRoot;

        // ---- Private: state ----
        private readonly List<LobbyPlayerEntry> _players = new List<LobbyPlayerEntry>();
        private readonly List<GameObject>       _playerRows = new List<GameObject>();
        private bool   _localReady = false;
        private string _currentRoomId = "";

        private Font _font;

        // ---- Lifecycle ----
        private void Start()
        {
            _font = Font.CreateDynamicFontFromOSFont("Consolas", 14);

            BuildCanvas();
            BuildUI();
            RegisterNetworkEvents();

            if (ShowOnStart)
                ShowLobby();
            else
                HideLobby();
        }

        private void OnDestroy()
        {
            UnregisterNetworkEvents();
        }

        private void Update()
        {
            // If canvas is visible, billboard it toward the camera.
            if (_canvas != null && _canvas.gameObject.activeSelf)
                FaceCamera();
        }

        // ---- Public API ----
        public void ShowLobby()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            PlaceInFrontOfCamera();
        }

        public void HideLobby()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        public void AddOrUpdatePlayer(LobbyPlayerEntry entry)
        {
            int idx = _players.FindIndex(p => p.PlayerId == entry.PlayerId);
            if (idx >= 0)
                _players[idx] = entry;
            else
                _players.Add(entry);

            RefreshPlayerList();
        }

        public void RemovePlayer(int playerId)
        {
            _players.RemoveAll(p => p.PlayerId == playerId);
            RefreshPlayerList();
        }

        public void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = "Status: " + message;
        }

        // ---- Button handlers ----
        private void OnCreateClicked()
        {
            string roomId = GetRoomId();
            SetStatus($"Creating room '{roomId}'...");
            DisableConnectionButtons();

            NetworkManager.Instance.Connect(roomId, success =>
            {
                if (success)
                {
                    SetStatus($"Room '{roomId}' created. Waiting for players...");
                    AddLocalPlayer();
                }
                else
                {
                    SetStatus("Failed to create room.");
                    EnableConnectionButtons();
                }
            });
        }

        private void OnJoinClicked()
        {
            string roomId = GetRoomId();
            SetStatus($"Joining room '{roomId}'...");
            DisableConnectionButtons();

            NetworkManager.Instance.Connect(roomId, success =>
            {
                if (success)
                {
                    SetStatus($"Joined room '{roomId}'.");
                    AddLocalPlayer();
                }
                else
                {
                    SetStatus("Failed to join room.");
                    EnableConnectionButtons();
                }
            });
        }

        private void OnReadyClicked()
        {
            _localReady = !_localReady;

            int localId = NetworkManager.Instance != null ? NetworkManager.Instance.LocalPlayerId : 0;
            int idx = _players.FindIndex(p => p.PlayerId == localId);
            if (idx >= 0)
            {
                var entry = _players[idx];
                entry.IsReady = _localReady;
                _players[idx] = entry;
            }

            RefreshPlayerList();
            UpdateReadyButton();
            SetStatus(_localReady ? "Marked as ready." : "Cancelled ready.");
        }

        private void OnLeaveClicked()
        {
            if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
                NetworkManager.Instance.Disconnect();

            _players.Clear();
            RefreshPlayerList();
            _localReady = false;
            UpdateReadyButton();
            EnableConnectionButtons();
            SetStatus("Left room.");
        }

        // ---- Network event handlers ----
        private void RegisterNetworkEvents()
        {
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.OnConnected      += HandleConnected;
            NetworkManager.Instance.OnDisconnected   += HandleDisconnected;
            NetworkManager.Instance.OnPlayerJoined   += HandlePlayerJoined;
            NetworkManager.Instance.OnPlayerLeft     += HandlePlayerLeft;
        }

        private void UnregisterNetworkEvents()
        {
            if (NetworkManager.Instance == null) return;
            NetworkManager.Instance.OnConnected      -= HandleConnected;
            NetworkManager.Instance.OnDisconnected   -= HandleDisconnected;
            NetworkManager.Instance.OnPlayerJoined   -= HandlePlayerJoined;
            NetworkManager.Instance.OnPlayerLeft     -= HandlePlayerLeft;
        }

        private void HandleConnected(int localPlayerId)
        {
            SetStatus("Connected.");
        }

        private void HandleDisconnected()
        {
            _players.Clear();
            RefreshPlayerList();
            SetStatus("Disconnected.");
            EnableConnectionButtons();
        }

        private void HandlePlayerJoined(int playerId)
        {
            int localId = NetworkManager.Instance != null ? NetworkManager.Instance.LocalPlayerId : -1;
            if (playerId == localId) return; // local already added

            var entry = new LobbyPlayerEntry
            {
                PlayerId    = playerId,
                DisplayName = $"Player {playerId}",
                IsReady     = false,
                IsLocal     = false
            };
            AddOrUpdatePlayer(entry);
            SetStatus($"Player {playerId} joined.");
        }

        private void HandlePlayerLeft(int playerId)
        {
            RemovePlayer(playerId);
            SetStatus($"Player {playerId} left.");
        }

        // ---- Helpers ----
        private string GetRoomId()
        {
            string id = _roomIdInput != null ? _roomIdInput.text.Trim() : "";
            if (string.IsNullOrEmpty(id))
                id = NetworkManager.Instance != null ? NetworkManager.Instance.defaultRoomId : "plaga44";
            _currentRoomId = id;
            return id;
        }

        private void AddLocalPlayer()
        {
            int localId = NetworkManager.Instance != null ? NetworkManager.Instance.LocalPlayerId : 0;
            var entry = new LobbyPlayerEntry
            {
                PlayerId    = localId,
                DisplayName = $"Player {localId} (You)",
                IsReady     = false,
                IsLocal     = true
            };
            AddOrUpdatePlayer(entry);
        }

        private void DisableConnectionButtons()
        {
            if (_createBtn != null) _createBtn.interactable = false;
            if (_joinBtn   != null) _joinBtn.interactable   = false;
        }

        private void EnableConnectionButtons()
        {
            if (_createBtn != null) _createBtn.interactable = true;
            if (_joinBtn   != null) _joinBtn.interactable   = true;
        }

        private void UpdateReadyButton()
        {
            if (_readyBtn == null) return;
            var label = _readyBtn.GetComponentInChildren<Text>();
            if (label != null)
                label.text = _localReady ? "CANCEL READY" : "READY UP";

            var btnImage = _readyBtn.GetComponent<Image>();
            if (btnImage != null)
                btnImage.color = _localReady ? ReadyColor : AccentColor;
        }

        private void RefreshPlayerList()
        {
            if (_playerListRoot == null) return;

            // Destroy old rows
            foreach (var row in _playerRows)
                if (row != null) Destroy(row);
            _playerRows.Clear();

            float rowH = 30f;
            float y = 0f;

            foreach (var player in _players)
            {
                var rowGO = new GameObject($"PlayerRow_{player.PlayerId}");
                rowGO.transform.SetParent(_playerListRoot, false);

                var rowRect = rowGO.AddComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot     = new Vector2(0.5f, 1f);
                rowRect.sizeDelta = new Vector2(0f, rowH);
                rowRect.anchoredPosition = new Vector2(0f, -y);

                // Name label
                var nameGO   = new GameObject("Name");
                nameGO.transform.SetParent(rowGO.transform, false);
                var nameTxt  = nameGO.AddComponent<Text>();
                nameTxt.text = $"  > {player.DisplayName}";
                nameTxt.font      = _font;
                nameTxt.fontSize  = 13;
                nameTxt.color     = player.IsLocal ? AccentColor : TextColor;
                nameTxt.alignment = TextAnchor.MiddleLeft;
                nameTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                var nameRect = nameGO.GetComponent<RectTransform>();
                nameRect.anchorMin        = new Vector2(0f, 0f);
                nameRect.anchorMax        = new Vector2(0.7f, 1f);
                nameRect.offsetMin        = Vector2.zero;
                nameRect.offsetMax        = Vector2.zero;

                // Ready label
                var rdyGO  = new GameObject("Ready");
                rdyGO.transform.SetParent(rowGO.transform, false);
                var rdyTxt = rdyGO.AddComponent<Text>();
                rdyTxt.text      = player.IsReady ? "[READY]" : "[ --- ]";
                rdyTxt.font      = _font;
                rdyTxt.fontSize  = 13;
                rdyTxt.color     = player.IsReady ? ReadyColor : NotReadyColor;
                rdyTxt.alignment = TextAnchor.MiddleRight;
                rdyTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                var rdyRect = rdyGO.GetComponent<RectTransform>();
                rdyRect.anchorMin = new Vector2(0.7f, 0f);
                rdyRect.anchorMax = new Vector2(1f, 1f);
                rdyRect.offsetMin = Vector2.zero;
                rdyRect.offsetMax = new Vector2(-8f, 0f);

                _playerRows.Add(rowGO);
                y += rowH;
            }

            // Resize list root to fit
            var listRect = _playerListRoot.GetComponent<RectTransform>();
            if (listRect != null)
                listRect.sizeDelta = new Vector2(listRect.sizeDelta.x, Mathf.Max(y, 30f));
        }

        private void FaceCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            _canvas.transform.LookAt(_canvas.transform.position + cam.transform.forward);
        }

        private void PlaceInFrontOfCamera()
        {
            var cam = Camera.main;
            if (cam == null || _canvas == null) return;

            Vector3 pos = cam.transform.position
                + cam.transform.forward * PlacementDistance
                + Vector3.up * HeightOffset;
            _canvas.transform.position = pos;
            FaceCamera();
        }

        // ---- UI Construction ----
        private void BuildCanvas()
        {
            var go = new GameObject("[LobbyCanvas]");
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode  = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100;

            go.AddComponent<GraphicRaycaster>();

            _canvasRect = _canvas.GetComponent<RectTransform>();
            _canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            _canvasRect.localScale = Vector3.one * CanvasScale;
        }

        private void BuildUI()
        {
            float pad = 16f;
            float y   = -pad;  // current vertical cursor from top

            // -- Background panel --
            MakeImage(_canvas.transform, "Background", BackgroundColor,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // -- Title --
            var title = MakeText(_canvas.transform, "Title",
                "PLAGA '44 -- LOBBY", 18, AccentColor, TextAnchor.UpperCenter);
            SetRect(title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -48f), new Vector2(0f, 0f));

            y -= 40f;

            // -- Separator --
            MakeSeparator(_canvas.transform, y);
            y -= 6f;

            // -- Room ID row --
            float rowY = y;
            _roomIdInput = MakeInputField(_canvas.transform, "RoomInput",
                "Room ID...", 13, rowY, -pad, CanvasWidth * 0.5f - pad * 2f);

            _createBtn = MakeButton(_canvas.transform, "CreateBtn", "CREATE",
                13, rowY, pad + CanvasWidth * 0.5f, 80f);
            _createBtn.onClick.AddListener(OnCreateClicked);

            _joinBtn = MakeButton(_canvas.transform, "JoinBtn", "JOIN",
                13, rowY, pad + CanvasWidth * 0.5f + 90f, 70f);
            _joinBtn.onClick.AddListener(OnJoinClicked);

            y -= 36f;

            // -- Separator --
            MakeSeparator(_canvas.transform, y);
            y -= 10f;

            // -- "Players:" label --
            var playersLabel = MakeText(_canvas.transform, "PlayersLabel",
                "Players:", 13, TextColor, TextAnchor.UpperLeft);
            SetRect(playersLabel.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, y - 20f), new Vector2(0f, y));

            y -= 24f;

            // -- Player list root --
            var listGO = new GameObject("PlayerList");
            listGO.transform.SetParent(_canvas.transform, false);
            _playerListRoot = listGO.transform;
            var listRect = listGO.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 1f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.pivot     = new Vector2(0.5f, 1f);
            listRect.offsetMin = new Vector2(pad, 0f);
            listRect.offsetMax = new Vector2(-pad, 0f);
            listRect.anchoredPosition = new Vector2(0f, y);
            listRect.sizeDelta = new Vector2(0f, 120f);

            y -= 130f;

            // -- Separator --
            MakeSeparator(_canvas.transform, y);
            y -= 6f;

            // -- Ready + Leave buttons --
            _readyBtn = MakeButton(_canvas.transform, "ReadyBtn", "READY UP",
                14, y, pad, 120f);
            _readyBtn.onClick.AddListener(OnReadyClicked);
            _readyBtn.GetComponent<Image>().color = AccentColor;

            _leaveBtn = MakeButton(_canvas.transform, "LeaveBtn", "LEAVE",
                14, y, CanvasWidth - pad - 80f, 80f);
            _leaveBtn.onClick.AddListener(OnLeaveClicked);
            _leaveBtn.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);

            y -= 46f;

            // -- Status text --
            _statusText = MakeText(_canvas.transform, "StatusText",
                "Status: Not connected.", 11, NotReadyColor, TextAnchor.LowerLeft);
            SetRect(_statusText.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 8f), new Vector2(0f, 28f));
        }

        // ---- Factory helpers ----
        private Image MakeImage(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img  = go.AddComponent<Image>();
            img.color = color;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return img;
        }

        private Text MakeText(Transform parent, string name, string content,
            int fontSize, Color color, TextAnchor alignment)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.text      = content;
            txt.font      = _font;
            txt.fontSize  = fontSize;
            txt.color     = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow   = VerticalWrapMode.Overflow;
            return txt;
        }

        private void MakeSeparator(Transform parent, float anchoredY)
        {
            var sep = MakeImage(parent, "Sep",
                new Color(1f, 1f, 1f, 0.15f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(8f, 0f), new Vector2(-8f, 0f));
            var r = sep.GetComponent<RectTransform>();
            r.anchoredPosition = new Vector2(0f, anchoredY);
            r.sizeDelta        = new Vector2(0f, 1f);
        }

        private InputField MakeInputField(Transform parent, string name,
            string placeholder, int fontSize, float anchoredY, float x, float width)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img  = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin        = new Vector2(0f, 1f);
            rect.anchorMax        = new Vector2(0f, 1f);
            rect.pivot            = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, anchoredY);
            rect.sizeDelta        = new Vector2(width, 28f);

            // Placeholder text
            var phGO  = new GameObject("Placeholder");
            phGO.transform.SetParent(go.transform, false);
            var phTxt = phGO.AddComponent<Text>();
            phTxt.text     = placeholder;
            phTxt.font     = _font;
            phTxt.fontSize = fontSize;
            phTxt.color    = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            phTxt.alignment = TextAnchor.MiddleLeft;
            FillRect(phTxt.transform, 4f);

            // Input text
            var txtGO  = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var inTxt  = txtGO.AddComponent<Text>();
            inTxt.font     = _font;
            inTxt.fontSize = fontSize;
            inTxt.color    = TextColor;
            inTxt.alignment = TextAnchor.MiddleLeft;
            FillRect(inTxt.transform, 4f);

            var field = go.AddComponent<InputField>();
            field.textComponent   = inTxt;
            field.placeholder     = phTxt;
            field.characterLimit  = 32;

            return field;
        }

        private Button MakeButton(Transform parent, string name, string label,
            int fontSize, float anchoredY, float x, float width)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img  = go.AddComponent<Image>();
            img.color = AccentColor;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin        = new Vector2(0f, 1f);
            rect.anchorMax        = new Vector2(0f, 1f);
            rect.pivot            = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, anchoredY);
            rect.sizeDelta        = new Vector2(width, 30f);

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var txt   = lblGO.AddComponent<Text>();
            txt.text      = label;
            txt.font      = _font;
            txt.fontSize  = fontSize;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            FillRect(txt.transform, 0f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor     = new Color(0.7f, 0.7f, 0.7f);
            btn.colors = colors;

            return btn;
        }

        private void SetRect(Transform t, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var r = t.GetComponent<RectTransform>();
            if (r == null) return;
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.offsetMin = offsetMin;
            r.offsetMax = offsetMax;
        }

        private void FillRect(Transform t, float padding)
        {
            var r = t.GetComponent<RectTransform>();
            if (r == null) return;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(padding, padding);
            r.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
