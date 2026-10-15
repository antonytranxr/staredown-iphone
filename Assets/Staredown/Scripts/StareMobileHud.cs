using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Staredown
{
    /// <summary>Phone controls stay above the arena; camera permission is requested only by a tap.</summary>
    public sealed class StareMobileHud : MonoBehaviour
    {
        static readonly Color Ink = new Color(.035f, .065f, .095f, .96f);
        static readonly Color Surface = new Color(.075f, .12f, .16f, .97f);
        static readonly Color Cyan = new Color(.25f, .94f, 1f);
        static readonly Color Muted = new Color(.67f, .77f, .82f);
        StareMobileSession session;
        Canvas canvas;
        RectTransform safe, home, roomBar, cameraPanel, dancePicker;
        Text roomLabel, roomStatus, homeStatus, caption, movementHint, lookHint, cameraStatus, cameraPrivacy, cameraTag;
        Text cameraToggleLabel, startLabel, fillLabel;
        RawImage preview;
        InputField code;
        Button practice, host, join, leave, start, fill, dance, cameraToggle, calibrate;
        StareMobileJoystick joystick;
        StareMobileLookPad lookPad;
        Font regular, bold;
        Sprite rounded;
        Vector2 lastScreen;
        Rect lastSafe;
        public RawImage CameraPreview => preview;
        public string ActiveCaption => caption != null ? caption.text : "";
        public bool HomeVisible => home != null && home.gameObject.activeSelf;
        public int MovePointerId => joystick != null ? joystick.PointerId : int.MinValue;
        public int LookPointerId => lookPad != null ? lookPad.PointerId : int.MinValue;
        public StareMobileJoystick MovementControl => joystick;
        public StareMobileLookPad LookControl => lookPad;

        public void Initialize(StareMobileSession owner)
        {
            if (canvas != null) return;
            session = owner;
            regular = Resources.Load<Font>("UI/Lato-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bold = Resources.Load<Font>("UI/Lato-SemiBold") ?? regular;
            rounded = RoundedSprite();
            var root = new GameObject("Mobile overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            safe = LayoutRect("Safe area", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            EnsureEvents();
            BuildLook(); BuildHome(); BuildRoom(); BuildCamera(); BuildControls();
            UpdateSafeArea(); Update();
        }

        void EnsureEvents()
        {
            if (EventSystem.current != null) return;
            var events = new GameObject("Mobile touch events", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        void BuildLook()
        {
            var area = LayoutRect("Swipe to look", safe, new Vector2(.43f, 0), new Vector2(1, .70f), Vector2.zero, Vector2.zero);
            var image = area.gameObject.AddComponent<Image>(); image.color = Color.clear;
            lookPad = area.gameObject.AddComponent<StareMobileLookPad>(); lookPad.Initialize(session, canvas);
        }

        void BuildHome()
        {
            home = Box("Join the arena", safe, new Vector2(0, .5f), new Vector2(34, 0), new Vector2(480, 598), new Vector2(0, .5f), Ink);
            Label("STAREDOWN", home, new Vector2(28, -28), new Vector2(424, 48), 39, Color.white, true);
            Label("PHONE × VR", home, new Vector2(30, -84), new Vector2(420, 26), 18, Cyan, true);
            Label("Keep your eyes open longer\nthan your opponent.", home, new Vector2(30, -137), new Vector2(420, 82), 28, Color.white, true);
            Label("Enable your front camera.\nLook forward with eyes open.", home, new Vector2(30, -229), new Vector2(420, 65), 22, Muted);
            practice = ButtonElement("Practice with bots", home, new Vector2(28, -321), new Vector2(424, 60), () => session.Practice(), true);
            host = ButtonElement("Create a room", home, new Vector2(28, -395), new Vector2(424, 60), () => session.Host());
            var field = Box("Four digit room code", home, new Vector2(0, 1), new Vector2(28, -472), new Vector2(266, 60), new Vector2(0, 1), Surface);
            code = field.gameObject.AddComponent<InputField>(); code.characterLimit = StareRelayNetwork.CodeLength;
            code.contentType = InputField.ContentType.IntegerNumber; code.keyboardType = TouchScreenKeyboardType.NumberPad;
            var value = Label("", field, new Vector2(18, -7), new Vector2(230, 47), 28, Color.white, true);
            var placeholder = Label("Room code", field, new Vector2(18, -7), new Vector2(230, 47), 24, Muted);
            code.textComponent = value; code.placeholder = placeholder; code.targetGraphic = field.GetComponent<Image>();
            join = ButtonElement("Join", home, new Vector2(308, -472), new Vector2(144, 60), () => session.Join(code.text), true);
            homeStatus = Label("4 digits · play together from any Wi-Fi", home, new Vector2(29, -545), new Vector2(423, 44), 18, Muted);
        }

        void BuildRoom()
        {
            roomBar = Box("Room toolbar", safe, new Vector2(0, 1), new Vector2(26, -22), new Vector2(770, 88), new Vector2(0, 1), Ink);
            roomLabel = Label("", roomBar, new Vector2(18, -12), new Vector2(308, 33), 27, Color.white, true);
            roomStatus = Label("", roomBar, new Vector2(18, -49), new Vector2(344, 28), 18, Muted);
            fill = ButtonElement("Bots: on", roomBar, new Vector2(384, -18), new Vector2(135, 52), () => session.Act("bot-fill"));
            fillLabel = fill.GetComponentInChildren<Text>();
            start = ButtonElement("Start", roomBar, new Vector2(530, -18), new Vector2(102, 52), () => session.StartMatch(), true);
            startLabel = start.GetComponentInChildren<Text>();
            leave = ButtonElement("Leave", roomBar, new Vector2(643, -18), new Vector2(106, 52), () => session.Leave());
            var center = Box("Match caption", safe, new Vector2(.5f, 1), new Vector2(0, -125), new Vector2(590, 60), new Vector2(.5f, 1), Ink);
            caption = Label("", center, new Vector2(12, -9), new Vector2(566, 42), 27, Color.white, true, TextAnchor.MiddleCenter);
        }

        void BuildCamera()
        {
            cameraPanel = Box("Front camera", safe, Vector2.one, new Vector2(-26, -22), new Vector2(300, 365), Vector2.one, Ink);
            cameraTag = Label("FRONT CAMERA", cameraPanel, new Vector2(16, -11), new Vector2(268, 29), 18, Cyan, true);
            var frame = Box("Camera frame", cameraPanel, new Vector2(0, 1), new Vector2(14, -46), new Vector2(272, 155), new Vector2(0, 1), Surface);
            var imageRect = LayoutRect("Front facing preview", frame, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
            preview = imageRect.gameObject.AddComponent<RawImage>(); preview.color = Color.white; preview.raycastTarget = false;
            // Camera processing supplies a corrected upright texture; preserve its aspect ratio.
            imageRect.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            cameraStatus = Label("Camera is off", cameraPanel, new Vector2(16, -209), new Vector2(268, 59), 18, Color.white);
            cameraPrivacy = Label("Preview only. Enable to detect blinks.", cameraPanel, new Vector2(16, -273), new Vector2(268, 35), 15, Muted);
            cameraToggle = ButtonElement("Enable camera", cameraPanel, new Vector2(14, -318), new Vector2(174, 34), () => session.Act("camera"), true);
            cameraToggleLabel = cameraToggle.GetComponentInChildren<Text>(); cameraToggleLabel.fontSize = 17;
            calibrate = ButtonElement("Reset", cameraPanel, new Vector2(198, -318), new Vector2(88, 34), () => session.Act("calibrate"));
            calibrate.GetComponentInChildren<Text>().fontSize = 17;
        }

        void BuildControls()
        {
            var pad = Box("Movement joystick", safe, Vector2.zero, new Vector2(76, 63), new Vector2(166, 166), new Vector2(0, 0), new Color(.07f, .15f, .21f, .75f));
            var knob = Box("Joystick thumb", pad, new Vector2(.5f, .5f), Vector2.zero, new Vector2(64, 64), new Vector2(.5f, .5f), Cyan);
            knob.GetComponent<Image>().raycastTarget = false;
            joystick = pad.gameObject.AddComponent<StareMobileJoystick>(); joystick.Initialize(session, knob, 59);
            movementHint = Label("", safe, new Vector2(27, -10), new Vector2(490, 34), 18, Color.white);
            movementHint.rectTransform.anchorMin = movementHint.rectTransform.anchorMax = Vector2.zero;
            movementHint.rectTransform.pivot = Vector2.zero; movementHint.rectTransform.anchoredPosition = new Vector2(29, 16);
            dance = ButtonElement("Dance", safe, new Vector2(-28, 23), new Vector2(156, 55), () => dancePicker.gameObject.SetActive(!dancePicker.gameObject.activeSelf));
            var rect = dance.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(1, 0);
            dancePicker = Box("Quick dances", safe, new Vector2(1, 0), new Vector2(-28, 92), new Vector2(262, 265), new Vector2(1, 0), Ink);
            string[] options = { "Hip hop", "Charleston", "Lindy hop", "Fancy footwork", "Stop dancing" };
            for (int i = 0; i < options.Length; i++)
            {
                int choice = i == 4 ? -1 : i;
                var button = ButtonElement(options[i], dancePicker, new Vector2(12, -12 - i * 49), new Vector2(238, 41), () => { session.Act("dance:" + choice); dancePicker.gameObject.SetActive(false); });
                button.GetComponentInChildren<Text>().fontSize = 20;
            }
            dancePicker.gameObject.SetActive(false);
            lookHint = Label("Swipe the arena to look", safe, new Vector2(-465, 25), new Vector2(270, 34), 18, Muted, false, TextAnchor.MiddleRight);
            var hint = lookHint.rectTransform; hint.anchorMin = hint.anchorMax = new Vector2(1, 0); hint.pivot = Vector2.zero;
        }

        void Update()
        {
            if (session == null || canvas == null) return;
            UpdateSafeArea();
            bool connected = session.Connected;
            home.gameObject.SetActive(!connected); roomBar.gameObject.SetActive(connected);
            homeStatus.text = string.IsNullOrEmpty(session.Status) ? "4 digits · play together from any Wi-Fi" : session.Status;
            lookPad.gameObject.SetActive(connected);
            lookHint.gameObject.SetActive(connected);
            joystick.gameObject.SetActive(connected && session.CanMove);
            dance.gameObject.SetActive(connected); dance.interactable = session.CanDance;
            if (!connected || !session.CanDance) dancePicker.gameObject.SetActive(false);
            practice.interactable = host.interactable = !session.Busy;
            join.interactable = !session.Busy && StareRelayNetwork.ValidCode(code.text);
            leave.interactable = !session.Busy;
            var state = session.State ?? new ArenaState();
            bool lobby = state.phase == (int)MatchPhase.Lobby;
            roomLabel.text = string.IsNullOrEmpty(session.RoomCode) ? "PRACTICE" : "ROOM " + session.RoomCode;
            roomStatus.text = session.Busy ? session.Status : state.fighters.Count(f => !f.bot) + " players · " + state.fighters.Count(f => f.bot) + " bots";
            start.gameObject.SetActive(session.Hosting && (lobby || state.phase == (int)MatchPhase.Champion));
            start.interactable = !session.Busy && state.fighters.Length >= 2 && state.fighters.All(f => f.ready && f.valid);
            startLabel.text = state.phase == (int)MatchPhase.Champion ? "Again" : "Start";
            fill.gameObject.SetActive(session.Hosting && lobby); fill.interactable = !session.Busy;
            fillLabel.text = state.botFill ? "Bots: on" : "Humans only";
            var local = state.fighters.FirstOrDefault(f => f.id == session.LocalId);
            caption.text = MatchCaption(state, local, session.Status);
            caption.transform.parent.gameObject.SetActive(connected && !string.IsNullOrEmpty(caption.text));
            movementHint.text = !connected ? "" : session.CanMove ? (local != null && !local.alive ? "Move outside the competitor fields" : "Left thumb to move") : "Your position is held during the stare";
            var camera = session.MobileCamera;
            bool running = camera != null && camera.Running;
            bool live = running && connected && !string.IsNullOrEmpty(session.RoomCode);
            cameraTag.text = live ? "● LIVE · FRONT CAMERA" : running ? "FRONT CAMERA · PREVIEW" : "FRONT CAMERA";
            cameraStatus.text = camera != null ? camera.Status : "Camera unavailable on this device";
            cameraPrivacy.text = live ? "Your face is shared with this room." : running ? "Preview only until you join a room." : "Enable camera to detect your blinks.";
            cameraToggleLabel.text = running ? "Camera off" : "Enable camera";
            calibrate.interactable = running;
            preview.texture = camera != null ? camera.Preview : null;
            preview.enabled = running && preview.texture != null;
            if (preview.texture != null) preview.GetComponent<AspectRatioFitter>().aspectRatio = (float)preview.texture.width / Mathf.Max(1, preview.texture.height);
        }

        public static string MatchCaption(ArenaState state, Fighter local, string status)
        {
            if (state == null || local == null) return status ?? "";
            if (state.phase == (int)MatchPhase.Champion)
            {
                var winner = state.fighters.FirstOrDefault(f => f.id == state.winner);
                return state.winner == local.id ? "You are the champion!" : (winner != null ? winner.name + " wins!" : "Match complete");
            }
            if (!local.alive) return "You're out · explore outside the fields";
            bool active = local.pair >= 0 && local.pair < state.pairs.Length && state.pairs[local.pair].winner < 0;
            if (active && state.trackingPaused) return "Tracking paused · look at your camera";
            if (active && state.phase == (int)MatchPhase.Countdown) return "Blink now · starts in " + Mathf.CeilToInt(state.remaining);
            if (active && state.phase == (int)MatchPhase.Duel) return "Keep your eyes open";
            if (state.phase == (int)MatchPhase.Lobby) return !local.ready || !local.valid ? "Look forward with eyes open to calibrate" : "Ready · explore while the host prepares";
            return "You advanced · next round soon";
        }

        void UpdateSafeArea()
        {
            var screen = new Vector2(Screen.width, Screen.height); var inset = Screen.safeArea;
            if (screen == lastScreen && inset == lastSafe) return;
            lastScreen = screen; lastSafe = inset;
            safe.anchorMin = new Vector2(inset.x / Mathf.Max(1, screen.x), inset.y / Mathf.Max(1, screen.y));
            safe.anchorMax = new Vector2(inset.xMax / Mathf.Max(1, screen.x), inset.yMax / Mathf.Max(1, screen.y));
        }

        RectTransform LayoutRect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high;
            return rect;
        }
        RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, Color color)
        {
            var rect = LayoutRect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero); rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = rounded; image.type = Image.Type.Sliced; image.color = color;
            return rect;
        }
        Text Label(string content, Transform parent, Vector2 position, Vector2 size, int points, Color color, bool heavy = false, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var rect = LayoutRect(string.IsNullOrEmpty(content) ? "Label" : content, parent, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = rect.gameObject.AddComponent<Text>(); text.text = content; text.font = heavy ? bold : regular; text.fontSize = points;
            text.color = color; text.alignment = alignment; text.raycastTarget = false; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
        Button ButtonElement(string label, Transform parent, Vector2 position, Vector2 size, System.Action clicked, bool primary = false)
        {
            var rect = Box(label, parent, new Vector2(0, 1), position, size, new Vector2(0, 1), primary ? Cyan : Surface);
            var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => clicked());
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(.8f, 1, 1); colors.pressedColor = new Color(.6f, .83f, .86f); colors.disabledColor = new Color(.5f, .5f, .5f, .6f); button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(label, rect, Vector2.zero, size, 24, primary ? Ink : Color.white, true, TextAnchor.MiddleCenter);
            return button;
        }
        Sprite RoundedSprite()
        {
            const int width = 32; const float radius = 8;
            var texture = new Texture2D(width, width, TextureFormat.RGBA32, false) { name = "Mobile rounded surface" };
            var pixels = new Color[width * width];
            for (int y = 0; y < width; y++) for (int x = 0; x < width; x++)
            {
                var delta = new Vector2(Mathf.Max(radius - x - .5f, x + .5f - (width - radius)), Mathf.Max(radius - y - .5f, y + .5f - (width - radius)));
                float edge = new Vector2(Mathf.Max(0, delta.x), Mathf.Max(0, delta.y)).magnitude - radius;
                pixels[y * width + x] = new Color(1, 1, 1, Mathf.Clamp01(.5f - edge));
            }
            texture.SetPixels(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, width), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * radius);
        }
        void OnDisable() { joystick?.Release(); lookPad?.Release(); }
        void OnDestroy() { if (rounded != null) { Destroy(rounded.texture); Destroy(rounded); } }
    }

    /// <summary>The first finger owns movement until release; another finger can independently look.</summary>
    public sealed class StareMobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
    {
        StareMobileSession session; RectTransform knob, rect; float radius;
        public int PointerId { get; private set; } = int.MinValue;
        public void Initialize(StareMobileSession owner, RectTransform thumb, float reach) { session = owner; knob = thumb; rect = GetComponent<RectTransform>(); radius = reach; }
        public bool Begin(int id, Vector2 local)
        {
            if (PointerId != int.MinValue || session == null || !session.CanMove) return false;
            PointerId = id; Move(id, local); return true;
        }
        public void Move(int id, Vector2 local)
        {
            if (id != PointerId || session == null) return;
            var value = Vector2.ClampMagnitude(local / radius, 1); session.MoveInput = value; knob.anchoredPosition = value * radius;
        }
        public void End(int id) { if (id == PointerId) Release(); }
        public void Release() { PointerId = int.MinValue; if (session != null) session.MoveInput = Vector2.zero; if (knob != null) knob.anchoredPosition = Vector2.zero; }
        Vector2 Local(PointerEventData e) { Vector2 point; RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out point); return point - rect.rect.center; }
        public void OnPointerDown(PointerEventData e) { Begin(e.pointerId, Local(e)); }
        public void OnDrag(PointerEventData e) { Move(e.pointerId, Local(e)); }
        public void OnPointerUp(PointerEventData e) { End(e.pointerId); }
        public void OnCancel(BaseEventData e) { Release(); }
        void OnDisable() { Release(); }
    }

    public sealed class StareMobileLookPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
    {
        StareMobileSession session; Canvas canvas; Vector2 last;
        public int PointerId { get; private set; } = int.MinValue;
        public void Initialize(StareMobileSession owner, Canvas overlay) { session = owner; canvas = overlay; }
        public bool Begin(int id, Vector2 position)
        {
            if (PointerId != int.MinValue || session == null || !session.Connected) return false;
            PointerId = id; last = position; return true;
        }
        public void Move(int id, Vector2 position)
        {
            if (id != PointerId || session == null) return;
            session.LookInput += (position - last) * (.14f / Mathf.Max(.01f, canvas.scaleFactor)); last = position;
        }
        public void End(int id) { if (id == PointerId) Release(); }
        public void Release() { PointerId = int.MinValue; if (session != null) session.LookInput = Vector2.zero; }
        public void OnPointerDown(PointerEventData e) { Begin(e.pointerId, e.position); }
        public void OnDrag(PointerEventData e) { Move(e.pointerId, e.position); }
        public void OnPointerUp(PointerEventData e) { End(e.pointerId); }
        public void OnCancel(BaseEventData e) { Release(); }
        void OnDisable() { Release(); }
    }
}
