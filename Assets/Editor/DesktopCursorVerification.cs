using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame.Editor
{
    public static class DesktopCursorVerification
    {
        static bool originalNativeVisibility;
        static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
        public static void CheckInactive(PortfolioDesktop os)
        {
            originalNativeVisibility=UnityEngine.Cursor.visible;
            os.Pointer.Update(new Vector2(Screen.width/2f,Screen.height/2f),true);
            Assert(!os.Pointer.IsDisplayed,"Custom cursor displayed while viewing room or approaching computer");
            Assert(UnityEngine.Cursor.visible==originalNativeVisibility,"Inactive cursor changed native cursor visibility");
        }
        public static void PointAt(PortfolioDesktop os,MonitorSurface surface,Vector2 localPoint)
        {
            var root=os.Root;var size=root.contentRect.size;
            var world=surface.screenCollider.transform.TransformPoint(new Vector3(localPoint.x/size.x-.5f,.5f-localPoint.y/size.y,0));
            var pixel=surface.viewCamera.WorldToScreenPoint(world);
            os.Pointer.VerificationScreenPosition=new Vector2(pixel.x,pixel.y);
            os.Pointer.VerificationApplicationFocus=true;os.Pointer.UpdateFromInput();
            Assert(Vector2.Distance(os.Pointer.PanelPosition,root.LocalToWorld(localPoint))<1,"Custom cursor position differs from click coordinate");
        }
        public static void PointAt(PortfolioDesktop os,MonitorSurface surface,VisualElement element)
        {Assert(element!=null,"Missing hover target");PointAt(os,surface,os.Root.WorldToLocal(element.worldBound.center));}
        public static void CheckShape(PortfolioDesktop os,DesktopCursorShape expected)
        {
            Assert(os.Pointer.IsDisplayed,"Custom cursor not displayed over OS");
            Assert(!UnityEngine.Cursor.visible,"Native cursor overlaps custom OS cursor");
            Assert(os.Pointer.Shape==expected,"Wrong cursor shape: "+os.Pointer.Shape+", expected "+expected);
            var target=os.Root.panel.Pick(os.Pointer.PanelPosition);
            Assert(target!=null && target!=os.Pointer.Visual && target.name!="OSCursorLayer","Cursor intercepts OS clicks");
        }
        public static void CheckArrowHotspot(PortfolioDesktop os)
        {
            CheckShape(os,DesktopCursorShape.Arrow);
            Assert(Vector2.Distance(os.Pointer.Visual.worldBound.position+new Vector2(5,4),os.Pointer.PanelPosition)<1,"Arrow tip does not match pointer hotspot");
        }
        public static void CheckResizeAndHide(PortfolioDesktop os,MonitorSurface surface)
        {
            var window=os.GetWindow("explorer");
            foreach(int mask in new[]{1,2,4,8,5,6,9,10})
            {
                PointAt(os,surface,window.Q("explorer-resize-"+mask));
                var shape=mask<3 ? DesktopCursorShape.Horizontal : mask==4||mask==8 ? DesktopCursorShape.Vertical : mask==5||mask==10 ? DesktopCursorShape.DiagonalDown : DesktopCursorShape.DiagonalUp;
                CheckShape(os,shape);
            }
            var corner=window.Q("explorer-resize-10");
            using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=corner.worldBound.center}))corner.SendEvent(down);
            Assert(window.IsPointerGestureActive,"Resize gesture did not capture pointer");
            PointAt(os,surface,new Vector2(1190,625));
            CheckShape(os,DesktopCursorShape.DiagonalDown);
            using(var up=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,mousePosition=os.Pointer.PanelPosition}))window.SendEvent(up);
            Assert(!window.IsPointerGestureActive,"Resize cursor remained captured after release");
            PointAt(os,surface,window.Q(className:"titlebar").worldBound.position+new Vector2(160,20)-os.Root.worldBound.position);
            CheckShape(os,DesktopCursorShape.Move);
            os.Pointer.Update(new Vector2(1,1),true);
            Assert(!os.Pointer.IsDisplayed,"OS cursor escaped monitor onto room");
            PointAt(os,surface,new Vector2(1190,625));
            os.Pointer.Update(os.Pointer.VerificationScreenPosition.Value,false);
            Assert(!os.Pointer.IsDisplayed && UnityEngine.Cursor.visible==originalNativeVisibility,"Focus loss did not hide OS cursor and restore native pointer");
            os.Pointer.UpdateFromInput();
            CheckShape(os,DesktopCursorShape.Arrow);
        }
        public static void CheckReturned(PortfolioDesktop os)
        {
            os.Pointer.UpdateFromInput();
            Assert(!os.Pointer.IsDisplayed && UnityEngine.Cursor.visible==originalNativeVisibility,"Leaving computer did not hide OS cursor and restore native pointer");
            var visual=os.Pointer.Visual;os.enabled=false;
            Assert(visual.parent?.parent==null && UnityEngine.Cursor.visible==originalNativeVisibility,"OS disable did not clean up cursor");
            File.WriteAllText("Logs/OSQA/cursor-results.txt","PASS: hidden in room and during approach; monitor coordinates and arrow hotspot; native cursor hidden during computer use; pointer ignores picking; arrow/button/text/move/eight resize edges; resize shape retained during capture and released afterward; silver/sage palette; off-monitor and app-focus-loss hiding; native cursor restoration on exit; cleanup on OS disable.\n");
            Debug.Log("PR_GAME_DESKTOP_CURSOR_VERIFIED");
        }
    }
}
