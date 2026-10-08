using UnityEngine;

namespace PrGame
{
    [RequireComponent(typeof(Camera))]
    public sealed class SeatedView : MonoBehaviour
    {
        [Range(0, 1)] public float atmosphere = .35f;
        public Light deskLamp;
        public float defaultFieldOfView = 58f;
        public float focusFieldOfView = 48f;
        [Range(15f, 55f)] public float zoomFieldOfView = 32f;
        [Range(1f, 20f)] public float zoomResponse = 7f;
        [Range(.1f, 5f)] public float lookSensitivity = 1.8f;
        [Tooltip("Visible OS screen fill on the limiting axis; 0.92 covers 84.64% of a 16:9 view.")]
        [Range(.5f, .96f)] public float screenViewportFraction = .92f;
        public bool IsLooking { get; private set; }
        public bool IsZooming { get; private set; }
        public bool IsFocused { get; private set; }
        public bool CanInteractWithComputer { get; private set; }
        Camera view;
        Vector3 seatedPosition;
        Quaternion seatedRotation;
        float yaw, pitch, lampIntensity, elapsed;
        bool waitForMouseRelease, lookReleased, hasApplicationFocus, skipLookFrame;
        PortfolioDesktop desktop;
        MonitorSurface monitor;
        Font helpFont;
        struct ViewInput
        {
            public Vector2 look;
            public bool leftPressed, leftHeld, rightHeld, escapePressed, computerPressed, homePressed, atmospherePressed;
        }
#if UNITY_EDITOR
        public bool UseVerificationInput { get; set; }
        // Drive the same input path without moving or capturing the user's mouse during QA.
        public void VerifyFrame(Vector2 look, float deltaTime, bool rightHeld=false, bool leftPressed=false,
            bool escapePressed=false, bool computerPressed=false, bool homePressed=false, bool appFocused=true)
        {
            TickView(new ViewInput { look=look, rightHeld=rightHeld, leftPressed=leftPressed, leftHeld=leftPressed,
                escapePressed=escapePressed, computerPressed=computerPressed, homePressed=homePressed }, deltaTime, appFocused);
        }
#endif

        void Awake()
        {
            view = GetComponent<Camera>();
            seatedPosition = transform.position;
            seatedRotation = transform.rotation;
            lampIntensity = deskLamp ? deskLamp.intensity : 1f;
            desktop = FindAnyObjectByType<PortfolioDesktop>();
            monitor = FindAnyObjectByType<MonitorSurface>();
            helpFont = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
        }

        void OnEnable()
        {
            hasApplicationFocus = Application.isFocused || Application.isBatchMode;
            lookReleased = false;
            SetLooking(hasApplicationFocus);
        }

        public void EnterComputer()
        {
            if (!monitor || !monitor.screenCollider) return;
            SetLooking(false);
            IsZooming = false;
            IsFocused = true;
            // The approach click must never also click an OS button.
            waitForMouseRelease = true;
            SetInteraction(false);
        }

        public void LeaveComputer()
        {
            IsFocused = false;
            IsZooming = false;
            SetInteraction(false);
            SetLooking(isActiveAndEnabled && hasApplicationFocus && !lookReleased);
        }

        // Frame only the usable OS display, not the bezel or projecting light shields.
        public bool TryGetFocusPose(out Vector3 position, out Quaternion rotation)
        {
            position = seatedPosition; rotation = seatedRotation;
            if (!monitor || !monitor.screenCollider) return false;
            var screen = monitor.screenCollider.transform;
            var renderer = screen.GetComponent<Renderer>();
            if (!renderer) return false;
            var bounds = renderer.bounds;
            float tangent = Mathf.Tan(focusFieldOfView * Mathf.Deg2Rad * .5f);
            float distance = Mathf.Max(bounds.size.x / (2f * tangent * view.aspect * screenViewportFraction),
                bounds.size.y / (2f * tangent * screenViewportFraction));
            distance = Mathf.Max(distance, view.nearClipPlane + .15f);
            rotation = Quaternion.LookRotation(screen.forward, screen.up);
            position = bounds.center - screen.forward * distance;
            return true;
        }

        void Update()
        {
#if UNITY_EDITOR
            if (UseVerificationInput) return;
#endif
            // Unity Editor Esc and OS focus changes may release capture outside our input path.
            if (IsLooking && !Application.isBatchMode && Cursor.lockState != CursorLockMode.Locked)
                lookReleased = true;
            TickView(new ViewInput {
                look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")),
                leftPressed = Input.GetMouseButtonDown(0), leftHeld = Input.GetMouseButton(0), rightHeld = Input.GetMouseButton(1),
                escapePressed = Input.GetKeyDown(KeyCode.Escape), computerPressed = Input.GetKeyDown(KeyCode.F),
                homePressed = Input.GetKeyDown(KeyCode.Home), atmospherePressed = Input.GetKeyDown(KeyCode.H)
            }, Time.unscaledDeltaTime, Application.isFocused || Application.isBatchMode);
        }

        void TickView(ViewInput input, float deltaTime, bool appFocused)
        {
            if (hasApplicationFocus != appFocused) OnApplicationFocus(appFocused);
            bool wasLooking = IsLooking;
            bool typing = desktop && desktop.IsTyping;
            if (appFocused && input.escapePressed)
            {
                if (IsFocused) LeaveComputer();
                else { lookReleased = true; SetLooking(false); }
            }
            else if (appFocused && !typing && input.computerPressed)
            {
                lookReleased = false;
                if (IsFocused) LeaveComputer(); else EnterComputer();
            }
            else if (appFocused && !IsFocused && input.leftPressed)
            {
                // A recapture click only resumes looking; it must not also enter the computer.
                if (lookReleased) { lookReleased = false; wasLooking = false; }
                else if (!RoomDoor.TryInteract(view) && monitor && monitor.IsPointerOverMonitor(new Vector2(Screen.width*.5f, Screen.height*.5f))) EnterComputer();
            }
            if (appFocused && !typing && input.homePressed)
            {
                lookReleased = false; LeaveComputer(); yaw = pitch = 0; wasLooking = false;
            }
            SetLooking(appFocused && !IsFocused && !lookReleased);
            IsZooming = IsLooking && input.rightHeld;
            if (IsLooking && wasLooking && !skipLookFrame)
            {
                // Lower angular sensitivity as the view narrows, keeping zoomed inspection steady.
                float sensitivity = lookSensitivity * Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f) /
                    Mathf.Tan(defaultFieldOfView*Mathf.Deg2Rad*.5f);
                yaw = Mathf.Repeat(yaw + input.look.x * sensitivity + 180f, 360f) - 180f;
                pitch = Mathf.Clamp(pitch - input.look.y * sensitivity, -65f, 70f);
            }
            skipLookFrame = false;
            if (appFocused && !typing && input.atmospherePressed) atmosphere = atmosphere > 0 ? 0 : .25f;
            if (!input.leftHeld && !input.rightHeld) waitForMouseRelease = false;

            var targetPosition = seatedPosition;
            var targetRotation = seatedRotation * Quaternion.Euler(pitch, yaw, 0);
            if (IsFocused) TryGetFocusPose(out targetPosition, out targetRotation);
            float blend = 1f - Mathf.Exp(-10f * deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
            float targetFov = IsFocused ? focusFieldOfView : IsZooming ? Mathf.Min(zoomFieldOfView,defaultFieldOfView) : defaultFieldOfView;
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, targetFov, IsFocused ? blend : 1f-Mathf.Exp(-zoomResponse*deltaTime));
            SetInteraction(appFocused && IsFocused && !waitForMouseRelease &&
                Vector3.Distance(transform.position, targetPosition) < .002f &&
                Quaternion.Angle(transform.rotation, targetRotation) < .2f && Mathf.Abs(view.fieldOfView-targetFov) < .1f);

            elapsed += deltaTime;
            if (deskLamp)
            {
                float phase = elapsed % 43f;
                float dip = phase > 39f && phase < 41f ? Mathf.Sin((phase - 39f) * Mathf.PI / 2f) : 0;
                deskLamp.intensity = lampIntensity * (1f - dip * atmosphere);
            }
        }

        void SetInteraction(bool active)
        {
            if (CanInteractWithComputer == active) return;
            CanInteractWithComputer = active;
            if (!active && desktop) desktop.SuspendInput();
        }

        void SetLooking(bool active)
        {
            if (IsLooking == active) return;
            IsLooking = active;
            if (active) skipLookFrame = true;
            // Batch QA must never capture the desktop pointer.
            if (!Application.isBatchMode) Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !active;
        }
        void OnApplicationFocus(bool hasFocus)
        {
            hasApplicationFocus = hasFocus;
            if (!hasFocus) { lookReleased = true; LeaveComputer(); }
        }
        void OnDisable()
        {
            LeaveComputer(); SetLooking(false);
            if (view) view.fieldOfView = defaultFieldOfView;
            if (deskLamp) deskLamp.intensity = lampIntensity;
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { font = helpFont, fontSize = Mathf.Max(12, Screen.height / 65), alignment = TextAnchor.MiddleCenter };
            string text = IsFocused ? "컴퓨터 조작 중     ·     ESC  방 둘러보기" :
                !IsLooking ? "화면 클릭  둘러보기 재개     ·     F  컴퓨터 사용" :
                "마우스로 둘러보기     ·     우클릭 누르기  확대     ·     문 / 모니터를 보고 클릭     ·     F  컴퓨터 사용     ·     ESC  마우스 해제";
            var rect = new Rect(0, Screen.height-36, Screen.width, 28);
            style.normal.textColor = new Color(0, 0, 0, .85f);
            GUI.Label(new Rect(rect.x+1, rect.y+1, rect.width, rect.height), text, style);
            style.normal.textColor = new Color(.90f, .92f, .94f, .9f);
            GUI.Label(rect, text, style);
        }
    }
}
