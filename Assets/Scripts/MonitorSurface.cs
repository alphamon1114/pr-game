using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

namespace PrGame
{
    // UI stays in a texture; only rays that hit the physical screen can operate it.
    [DefaultExecutionOrder(-100)]
    [ExecuteAlways]
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
            FitToDisplay();
            if (!Application.isPlaying) return;
            document = GetComponent<UIDocument>();
            originalPanel = document.panelSettings;
            if (!originalPanel) return;
            runtimePanel = Instantiate(originalPanel);
            runtimePanel.targetTexture = screenTexture;
            runtimePanel.referenceResolution = new Vector2Int(1280,720);
            runtimePanel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            runtimePanel.clearColor = true;
            runtimePanel.colorClearValue = Color.black;
            runtimePanel.SetScreenToPanelSpaceFunction(ScreenToPanel);
            document.panelSettings = runtimePanel;
        }

        // Refit against the current imported mesh, including after a model reimport.
        // This desk's display is axis-aligned; retain a true 16:9 image within its glass.
        public bool FitToDisplay()
        {
            if (!screenCollider) return false;
            var model = GameObject.Find("Refined / Monitor");
            if (!model) return false;
            var glass = model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name=="Screen glass");
            if (!glass) return false;
            var bounds=glass.bounds;
            float width=bounds.size.x*.996f, height=bounds.size.y*.996f;
            float aspect=screenTexture ? (float)screenTexture.width/screenTexture.height : 16f/9f;
            width=Mathf.Min(width,height*aspect);height=width/aspect;
            var screen=screenCollider.transform;
            var position=new Vector3(bounds.center.x,bounds.center.y,bounds.min.z-.0005f);
            var parentScale=screen.parent ? screen.parent.lossyScale : Vector3.one;
            var scale=new Vector3(width/parentScale.x,height/parentScale.y,1/parentScale.z);
            bool changed=Vector3.Distance(screen.position,position)>.000001f || Vector3.Distance(screen.localScale,scale)>.000001f || glass.enabled;
            screen.rotation=Quaternion.identity;
            screen.position=position;
            screen.localScale=scale;
            glass.enabled=false;
            return changed;
        }

        public bool IsPointerOverMonitor(Vector2 bottomLeftScreenPosition)
        {
            if(!viewCamera || !screenCollider)return false;
            return Physics.Raycast(viewCamera.ScreenPointToRay(bottomLeftScreenPosition),out var hit,5f) && hit.collider==screenCollider;
        }

        public Vector2 ScreenToPanel(Vector2 position)
        {
            if (!viewCamera || !screenCollider || !screenTexture ||
                (TryGetComponent<PortfolioDesktop>(out var desktop) && !desktop.AcceptsScreenInput) ||
                (seatedView && !seatedView.CanInteractWithComputer))
                return new Vector2(-10000, -10000);
            // UI Toolkit supplies top-left screen coordinates; Camera expects bottom-left.
            var ray = viewCamera.ScreenPointToRay(new Vector3(position.x, Screen.height - position.y));
            if (!Physics.Raycast(ray, out var hit, 5f) || hit.collider != screenCollider)
            {
                // An active drag may leave the screen. Continue on the same plane so it
                // clamps naturally and still receives its pointer-up, without accepting new clicks.
                if(document && document.rootVisualElement.panel?.GetCapturingElement(PointerId.mousePointerId)!=null)
                {
                    var transform=screenCollider.transform;var plane=new Plane(transform.forward,transform.position);
                    if(plane.Raycast(ray,out float distance))
                    {
                        var local=transform.InverseTransformPoint(ray.GetPoint(distance));
                        return new Vector2((local.x+.5f)*screenTexture.width,(.5f-local.y)*screenTexture.height);
                    }
                }
                return new Vector2(-10000, -10000);
            }
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
                runtimePanel=null;
            }
        }
    }
}
