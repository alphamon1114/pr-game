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
        [Range(.5f, .9f)] public float monitorViewportFraction = .8f;
        public bool IsLooking { get; private set; }
        public bool IsFocused { get; private set; }
        public bool CanInteractWithComputer { get; private set; }
        Camera view;
        Vector3 seatedPosition;
        Quaternion seatedRotation;
        float yaw, pitch, lampIntensity, elapsed;
        bool waitForMouseRelease;
        PortfolioDesktop desktop;
        MonitorSurface monitor;
        Renderer[] monitorFrame;
        Font helpFont;

        void Awake()
        {
            view = GetComponent<Camera>();
            seatedPosition = transform.position;
            seatedRotation = transform.rotation;
            lampIntensity = deskLamp ? deskLamp.intensity : 1f;
            desktop = FindAnyObjectByType<PortfolioDesktop>();
            monitor = FindAnyObjectByType<MonitorSurface>();
            var model = GameObject.Find("Refined / Monitor");
            monitorFrame = model ? System.Array.FindAll(model.GetComponentsInChildren<Renderer>(),
                r=>r.name=="Monitor housing" || r.name.Contains("light shield")) : new Renderer[0];
            helpFont = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
        }

        public void EnterComputer()
        {
            if (!monitor || !monitor.screenCollider) return;
            StopLooking();
            IsFocused = true;
            // The approach click must never also click an OS button.
            waitForMouseRelease = Input.GetMouseButton(0) || Input.GetMouseButton(1);
            SetInteraction(false);
        }

        public void LeaveComputer()
        {
            IsFocused = false;
            SetInteraction(false);
            StopLooking();
        }

        // Fit the physical screen in perspective, including wide/portrait displays.
        public bool TryGetFocusPose(out Vector3 position, out Quaternion rotation)
        {
            position = seatedPosition; rotation = seatedRotation;
            if (!monitor || !monitor.screenCollider) return false;
            var screen = monitor.screenCollider.transform;
            var renderer = screen.GetComponent<Renderer>();
            if (!renderer) return false;
            var bounds = renderer.bounds;
            foreach (var part in monitorFrame) if(part) bounds.Encapsulate(part.bounds);
            float tangent = Mathf.Tan(focusFieldOfView * Mathf.Deg2Rad * .5f);
            // Include the light shields, whose front edges project larger than the glass.
            float distance = Mathf.Max(bounds.size.x / (2f * tangent * view.aspect * monitorViewportFraction),
                bounds.size.y / (2f * tangent * monitorViewportFraction));
            distance = Mathf.Max(distance, view.nearClipPlane + .15f);
            rotation = Quaternion.LookRotation(screen.forward, screen.up);
            position = new Vector3(bounds.center.x,bounds.center.y,bounds.min.z) - screen.forward * distance;
            return true;
        }

        void Update()
        {
            bool typing = desktop && desktop.IsTyping;
            if (IsFocused && Input.GetKeyDown(KeyCode.Escape)) LeaveComputer();
            else if (!typing && Input.GetKeyDown(KeyCode.F))
            {
                if (IsFocused) LeaveComputer(); else EnterComputer();
            }
            else if (!IsFocused && !IsLooking && Input.GetMouseButtonDown(0) &&
                monitor && monitor.IsPointerOverMonitor(Input.mousePosition)) EnterComputer();

            if (!typing && Input.GetKeyDown(KeyCode.Home))
            {
                LeaveComputer(); yaw = pitch = 0;
            }
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (Input.GetMouseButtonDown(1) && (!IsFocused || alt))
            {
                if (IsFocused) LeaveComputer();
                IsLooking = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (!Input.GetMouseButton(1)) StopLooking();
            if (IsLooking)
            {
                yaw = Mathf.Repeat(yaw + Input.GetAxisRaw("Mouse X") * 1.8f + 180f, 360f) - 180f;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 1.8f, -65f, 70f);
            }
            if (!typing && Input.GetKeyDown(KeyCode.H)) atmosphere = atmosphere > 0 ? 0 : .25f;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) waitForMouseRelease = false;

            var targetPosition = seatedPosition;
            var targetRotation = seatedRotation * Quaternion.Euler(pitch, yaw, 0);
            if (IsFocused) TryGetFocusPose(out targetPosition, out targetRotation);
            float blend = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
            float targetFov = IsFocused ? focusFieldOfView : defaultFieldOfView;
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, targetFov, blend);
            SetInteraction(IsFocused && !waitForMouseRelease &&
                Vector3.Distance(transform.position, targetPosition) < .002f &&
                Quaternion.Angle(transform.rotation, targetRotation) < .2f && Mathf.Abs(view.fieldOfView-targetFov) < .1f);

            elapsed += Time.deltaTime;
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

        void StopLooking()
        {
            if (!IsLooking) return;
            IsLooking = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        void OnApplicationFocus(bool hasFocus) { if (!hasFocus) { LeaveComputer(); StopLooking(); } }
        void OnDisable() { LeaveComputer(); if (deskLamp) deskLamp.intensity = lampIntensity; }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { font = helpFont, fontSize = Mathf.Max(12, Screen.height / 65), alignment = TextAnchor.MiddleCenter };
            string text = IsFocused ? "컴퓨터 조작 중     ·     ESC  방 둘러보기" : "오른쪽 마우스를 누른 채 움직여 둘러보기     ·     모니터 클릭 / F  컴퓨터 사용     ·     HOME  정면";
            var rect = new Rect(0, Screen.height-36, Screen.width, 28);
            style.normal.textColor = new Color(0, 0, 0, .85f);
            GUI.Label(new Rect(rect.x+1, rect.y+1, rect.width, rect.height), text, style);
            style.normal.textColor = new Color(.90f, .92f, .94f, .9f);
            GUI.Label(rect, text, style);
        }
    }
}
