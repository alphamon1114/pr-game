using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PrGame.Editor
{
    public static class RoomViewVerification
    {
        static Vector3 seatedPosition;
        static Quaternion seatedRotation;
        static void Assert(bool ok,string message) { if(!ok)throw new Exception(message); }
        public static void CheckRoom(MonitorSurface surface)
        {
            var camera=surface.viewCamera;var view=surface.seatedView;
            seatedPosition=camera.transform.position;seatedRotation=camera.transform.rotation;
            Assert(!view.IsFocused && !view.CanInteractWithComputer,"Room should start outside computer mode");
            Assert(surface.ScreenToPanel(new Vector2(Screen.width/2f,Screen.height/2f)).x<0,"Room mode accepted OS input");
            var room=GameObject.Find("Furnished night room");Assert(room,"Furnished room missing");
            var renderers=room.GetComponentsInChildren<Renderer>();
            Vector3 Center(string name)=>renderers.First(r=>r.name==name).bounds.center;
            var desk=Center("Desk walnut top");var window=Center("Window night pane");
            Assert(window.x>desk.x+2 && window.z<desk.z,"Window must be on the right wall from the desk");
            Assert(Center("Fridge body").x<desk.x && Center("Wardrobe carcass").x>desk.x,"Desk neighbors do not match plan");
            Assert(Center("Bed timber frame").x>desk.x && Center("Bed timber frame").z<desk.z-2,"Bed must be behind on the right");
            Assert(Center("Door leaf").x<-2 && Center("Door leaf").z<-1,"Door must be at rear left");
            Assert(renderers.Any(r=>r.name.StartsWith("T50")),"T50 chair missing");
            Assert(Shader.Find("PRGame/Room PBR").isSupported && Shader.Find("Hidden/PRGame/NightLighting").isSupported,"Room shader unsupported");
            Capture(camera,"room-seated");
            var pos=camera.transform.position;var rot=camera.transform.rotation;float fov=camera.fieldOfView;int mask=camera.cullingMask;
            try
            {
                camera.fieldOfView=70;camera.transform.LookAt(new Vector3(1.7f,1,-1.18f));Capture(camera,"room-right-wall");
                camera.cullingMask=-1;camera.transform.position=new Vector3(-1.74f,1.8f,-1.79f);
                camera.transform.LookAt(new Vector3(.25f,1.04f,.55f));camera.fieldOfView=76;Capture(camera,"room-overview");
                camera.transform.position=new Vector3(.5f,1.35f,-.15f);camera.transform.LookAt(new Vector3(-1.30f,1,-1.78f));camera.fieldOfView=70;Capture(camera,"room-door");
                var ceiling=renderers.First(r=>r.name=="Architecture ceiling");bool enabled=ceiling.enabled;
                try
                {
                    ceiling.enabled=false;camera.transform.position=new Vector3(0,6,-.25f);
                    camera.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
                    camera.orthographic=true;camera.orthographicSize=2.35f;Capture(camera,"room-plan");
                }
                finally {ceiling.enabled=enabled;camera.orthographic=false;}
            }
            finally {camera.transform.SetPositionAndRotation(pos,rot);camera.fieldOfView=fov;camera.cullingMask=mask;}
            Debug.Log("ROOM_LAYOUT_PASS: north desk, left fridge, right wardrobe, east window, southeast bed, southwest door, T50 chair");
        }
        public static void CheckFocused(MonitorSurface surface)
        {
            var camera=surface.viewCamera;var view=surface.seatedView;
            Assert(view.IsFocused && view.CanInteractWithComputer,"Approach transition did not enable computer input");
            Assert(Vector3.Distance(camera.transform.position,seatedPosition)>.1f,"Camera did not physically approach screen");
            var pos=camera.transform.position;var rot=camera.transform.rotation;float aspect=camera.aspect,fov=camera.fieldOfView;
            try
            {
                foreach(float ratio in new[]{16f/9f,4f/3f,21f/9f,9f/16f})
                {
                    camera.aspect=ratio;camera.fieldOfView=view.focusFieldOfView;
                    Assert(view.TryGetFocusPose(out var p,out var q),"No focus pose");camera.transform.SetPositionAndRotation(p,q);
                    var display=surface.screenCollider.transform;
                    var a=camera.WorldToViewportPoint(display.TransformPoint(new Vector3(-.5f,-.5f,0)));
                    var b=camera.WorldToViewportPoint(display.TransformPoint(new Vector3(.5f,.5f,0)));
                    Assert(Mathf.Abs(Mathf.Max(b.x-a.x,b.y-a.y)-.92f)<.005f,"Usable OS screen does not fill 92% of limiting axis at "+ratio);
                    Assert(a.x>.039f&&a.y>.039f&&b.x<.961f&&b.y<.961f,"Usable OS screen clipped at "+ratio);
                    if(Mathf.Abs(ratio-16f/9f)<.01f)
                    {
                        float area=(b.x-a.x)*(b.y-a.y);
                        Assert(area>=.80f && Mathf.Abs(area-.8464f)<.005f,"OS display must occupy at least 80% of the 16:9 viewport area");
                        Debug.Log("OS_DISPLAY_AREA_PASS: "+(area*100).ToString("F2")+" percent of the 16:9 viewport, excluding bezel and shields");
                        Capture(camera,"monitor-focus");
                    }
                }
            }
            finally {camera.aspect=aspect;camera.fieldOfView=fov;camera.transform.SetPositionAndRotation(pos,rot);}
            Debug.Log("ROOM_FOCUS_PASS: usable display fills 92% of limiting axis at 16:9, 4:3, 21:9, 9:16; 84.64% viewport area at 16:9; physical approach; input enabled only after transition");
        }
        public static void CheckReturned(MonitorSurface surface,PortfolioDesktop desktop)
        {
            Assert(!surface.seatedView.IsFocused && !surface.seatedView.CanInteractWithComputer,"Computer interaction remains active");
            Assert(!desktop.IsTyping,"Text field retained focus after leaving computer");
            Assert(Vector3.Distance(surface.viewCamera.transform.position,seatedPosition)<.005f,"Camera did not return to seated position");
            Assert(Quaternion.Angle(surface.viewCamera.transform.rotation,seatedRotation)<.3f,"Room orientation was not restored");
            Assert(surface.ScreenToPanel(new Vector2(Screen.width/2f,Screen.height/2f)).x<0,"Room mode accepted pointer");
            Assert(desktop.GetWindow("notes")!=null,"Looking away closed note window");
            File.WriteAllText("Logs/OSQA/room-results.txt","PASS: room starts outside computer mode; floor-plan landmarks and east/right window; supported PBR/contact-shadow shaders; physical approach; usable OS display fills 92% of limiting axis at 16:9, 4:3, 21:9 and 9:16; 84.64% viewport area at 16:9 (bezel/shields excluded); gated OS input; return to seat; typing focus released; note window preserved.\n");
            Debug.Log("PR_GAME_ROOM_VIEW_VERIFIED");
        }
        static void Capture(Camera camera,string name)
        {
            var target=new RenderTexture(1600,900,24);var previous=camera.targetTexture;var active=RenderTexture.active;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();File.WriteAllBytes("Logs/OSQA/"+name+".png",texture.EncodeToPNG());
            }
            finally {camera.targetTexture=previous;RenderTexture.active=active;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);}
        }
    }
}
