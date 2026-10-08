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
        public static void CheckFreeLook(MonitorSurface surface)
        {
            var view=surface.seatedView;var camera=surface.viewCamera;
            var origin=camera.transform.position;var front=camera.transform.rotation;
            const float dt=1f/60f;view.UseVerificationInput=true;
            void Frames(int count,bool zoom=false){for(int i=0;i<count;i++)view.VerifyFrame(Vector2.zero,dt,rightHeld:zoom);}
            try
            {
                view.VerifyFrame(Vector2.zero,dt,homePressed:true);Frames(90);
                Assert(view.IsLooking&&!Cursor.visible,"Room did not capture free look without a mouse button");
                for(int i=0;i<15;i++)view.VerifyFrame(new Vector2(1,.3f),dt);
                Frames(45);var turned=camera.transform.rotation;
                Assert(Quaternion.Angle(front,turned)>20,"Mouse motion without RMB did not turn the camera");
                Assert(!view.IsZooming&&Mathf.Abs(camera.fieldOfView-view.defaultFieldOfView)<.1f,"Free look unexpectedly zoomed");
                Capture(camera,"room-free-look");
                view.VerifyFrame(Vector2.zero,dt,rightHeld:true);
                Assert(view.IsZooming&&camera.fieldOfView<view.defaultFieldOfView&&camera.fieldOfView>view.zoomFieldOfView,"RMB zoom snapped or failed to start");
                Frames(90,true);
                Assert(Mathf.Abs(camera.fieldOfView-view.zoomFieldOfView)<.1f,"Held RMB did not reach inspection zoom");
                Assert(Vector3.Distance(camera.transform.position,origin)<.001f&&Quaternion.Angle(camera.transform.rotation,turned)<.1f,"Zoom moved the seat or changed aim");
                Assert(!view.CanInteractWithComputer,"Inspection zoom enabled OS input");Capture(camera,"room-hold-zoom");
                view.VerifyFrame(Vector2.zero,dt);
                Assert(!view.IsZooming&&camera.fieldOfView>view.zoomFieldOfView&&camera.fieldOfView<view.defaultFieldOfView,"Zoom release did not ease back");Frames(90);
                Assert(Mathf.Abs(camera.fieldOfView-view.defaultFieldOfView)<.1f,"Zoom release failed to restore FOV");

                view.VerifyFrame(Vector2.zero,dt,homePressed:true);Frames(90);
                view.VerifyFrame(Vector2.zero,dt,escapePressed:true);
                Assert(!view.IsLooking&&Cursor.visible,"Esc did not release the room cursor");
                view.VerifyFrame(new Vector2(50,50),dt,rightHeld:true);Frames(30);
                Assert(!view.IsZooming&&Quaternion.Angle(camera.transform.rotation,front)<.1f,"Released pointer still moved or zoomed room");
                view.VerifyFrame(new Vector2(100,100),dt,leftPressed:true);Frames(30);
                Assert(view.IsLooking&&!view.IsFocused&&Quaternion.Angle(camera.transform.rotation,front)<.1f,"Recapture click entered computer or jumped camera");

                Frames(60,true);view.VerifyFrame(Vector2.zero,dt,rightHeld:true,computerPressed:true);Frames(120,true);
                Assert(view.IsFocused&&!view.IsLooking&&!view.IsZooming&&!view.CanInteractWithComputer,"Zoom-to-computer transition leaked held input");
                view.VerifyFrame(Vector2.zero,dt);Frames(5);
                Assert(view.CanInteractWithComputer,"Computer did not unlock after the approach button was released");
                var focused=camera.transform.rotation;
                for(int i=0;i<30;i++)view.VerifyFrame(new Vector2(20,20),dt,rightHeld:true);
                Assert(!view.IsZooming&&Mathf.Abs(camera.fieldOfView-view.focusFieldOfView)<.1f&&Quaternion.Angle(focused,camera.transform.rotation)<.1f,"OS right click or mouse input changed camera");
                view.VerifyFrame(new Vector2(100,100),dt,escapePressed:true);Frames(90);
                Assert(view.IsLooking&&!view.IsFocused&&!view.CanInteractWithComputer&&Quaternion.Angle(front,camera.transform.rotation)<.1f,"Leaving computer failed to resume room look cleanly");

                Frames(30,true);view.VerifyFrame(new Vector2(40,40),dt,rightHeld:true,appFocused:false);
                Assert(!view.IsLooking&&!view.IsZooming&&Cursor.visible,"Focus loss retained zoom or cursor capture");
                view.VerifyFrame(new Vector2(40,40),dt);
                Assert(!view.IsLooking,"Focus regain recaptured the desktop without a click");
                view.VerifyFrame(Vector2.zero,dt,leftPressed:true);Frames(90);
                Assert(view.IsLooking&&!Cursor.visible&&Quaternion.Angle(front,camera.transform.rotation)<.1f,"Focus recapture changed aim");
                view.enabled=false;
                Assert(!view.IsLooking&&!view.IsZooming&&Cursor.visible&&Mathf.Abs(camera.fieldOfView-view.defaultFieldOfView)<.1f,"Disabling view retained capture or zoom");
                view.enabled=true;view.VerifyFrame(Vector2.zero,dt,homePressed:true);Frames(90);
                File.WriteAllText("Logs/OSQA/free-look-results.txt","PASS: default mouse look without RMB; smooth held RMB 58-to-32-degree zoom and release; fixed seat and aim; zoom blocks OS input; Esc cursor release; click recapture without entry or jump; held-button gate during computer approach; fixed computer camera and OS RMB behavior; focus loss/regain; disable cleanup.\n");
                Debug.Log("PR_GAME_FREE_LOOK_VERIFIED");
            }
            finally {view.UseVerificationInput=false;}
        }
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
            Assert(surface.seatedView.IsLooking&&!Cursor.visible,"Room free look did not resume after computer use");
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
