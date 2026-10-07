using UnityEngine;

namespace PrGame
{
    [RequireComponent(typeof(Camera))]
    public sealed class SeatedView : MonoBehaviour
    {
        [Range(0, 1)] public float atmosphere = 0.35f;
        public Light deskLamp;
        public float defaultFieldOfView = 47f;
        public float focusFieldOfView = 34f;
        public bool IsLooking { get; private set; }
        private Camera view;
        private Quaternion initialRotation;
        private float yaw;
        private float pitch;
        private float lampIntensity;
        private float elapsed;
        private bool focused;
        private PortfolioDesktop desktop;
        private MonitorSurface monitor;

        private void Awake()
        {
            view = GetComponent<Camera>();
            initialRotation = transform.localRotation;
            lampIntensity = deskLamp ? deskLamp.intensity : 1f;
            desktop=FindAnyObjectByType<PortfolioDesktop>();
            monitor=FindAnyObjectByType<MonitorSurface>();
        }

        private void Update()
        {
            bool typing=desktop && desktop.IsTyping;
            if (!typing && Input.GetKeyDown(KeyCode.Home))
            {
                yaw = pitch = 0;
                focused = false;
                StopLooking();
            }
            else if (Input.GetMouseButtonDown(1) &&
                (Input.GetKey(KeyCode.LeftAlt) || !monitor || !monitor.IsPointerOverMonitor(Input.mousePosition)))
            {
                IsLooking = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (!Input.GetMouseButton(1)) StopLooking();
            if (IsLooking)
            {
                yaw = Mathf.Repeat(yaw + Input.GetAxisRaw("Mouse X") * 1.8f + 180f, 360f) - 180f;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 1.8f, -50f, 65f);
            }
            if (!typing && Input.GetKeyDown(KeyCode.F)) { focused = !focused; yaw = pitch = 0; }
            if (!typing && Input.GetKeyDown(KeyCode.H)) atmosphere = atmosphere > 0 ? 0 : 0.35f;
            transform.localRotation = Quaternion.Slerp(transform.localRotation,
                initialRotation * Quaternion.Euler(pitch, yaw, 0), 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, focused ? focusFieldOfView : defaultFieldOfView,
                1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            elapsed += Time.deltaTime;
            if (deskLamp)
            {
                // One slow dip per 43 seconds, no rapid strobe or sudden sound.
                float phase = elapsed % 43f;
                float dip = phase > 39f && phase < 41f ? Mathf.Sin((phase - 39f) * Mathf.PI / 2f) : 0;
                deskLamp.intensity = lampIntensity * (1f - dip * atmosphere);
            }
        }

        private void StopLooking()
        {
            if (!IsLooking) return;
            IsLooking = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) StopLooking(); }
        private void OnDisable() { StopLooking(); if (deskLamp) deskLamp.intensity = lampIntensity; }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Screen.height / 65), alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(0.85f, 0.85f, 0.8f, 0.8f);
            GUI.Label(new Rect(0, Screen.height - 34, Screen.width, 26),
                "DOUBLE CLICK  open     /     ALT + RIGHT MOUSE  look     /     F  focus     /     HOME  reset     /     H  atmosphere " +
                (atmosphere > 0 ? "on" : "off"), style);
        }
    }
}
