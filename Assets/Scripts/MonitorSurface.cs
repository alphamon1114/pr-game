using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame
{
    // UI stays in a texture; only rays that hit the physical screen can operate it.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class MonitorSurface : MonoBehaviour
    {
        public Camera viewCamera;
        public MeshCollider screenCollider;
        public RenderTexture screenTexture;
        public SeatedView seatedView;
        private PanelSettings originalPanel;
        private PanelSettings runtimePanel;
        private UIDocument document;

        private void OnEnable()
        {
            document = GetComponent<UIDocument>();
            originalPanel = document.panelSettings;
            runtimePanel = Instantiate(originalPanel);
            runtimePanel.targetTexture = screenTexture;
            runtimePanel.SetScreenToPanelSpaceFunction(ScreenToPanel);
            document.panelSettings = runtimePanel;
        }

        public Vector2 ScreenToPanel(Vector2 position)
        {
            if (!viewCamera || !screenCollider || !screenTexture ||
                (seatedView && seatedView.IsLooking))
                return new Vector2(-10000, -10000);
            // UI Toolkit supplies top-left screen coordinates; Camera expects bottom-left.
            var ray = viewCamera.ScreenPointToRay(new Vector3(position.x, Screen.height - position.y));
            if (!Physics.Raycast(ray, out var hit, 5f) || hit.collider != screenCollider)
                return new Vector2(-10000, -10000);
            return new Vector2(hit.textureCoord.x * screenTexture.width,
                (1f - hit.textureCoord.y) * screenTexture.height);
        }

        private void OnDisable()
        {
            if (runtimePanel)
            {
                runtimePanel.SetScreenToPanelSpaceFunction(null);
                if (document) document.panelSettings = originalPanel;
                Destroy(runtimePanel);
            }
        }
    }
}
