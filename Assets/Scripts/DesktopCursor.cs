using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame
{
    public enum DesktopCursorShape { Arrow, Link, Text, Move, Horizontal, Vertical, DiagonalDown, DiagonalUp }

    // Painted into the monitor texture so the pointer belongs to the in-game OS.
    public sealed class DesktopCursor : IDisposable
    {
        readonly VisualElement root, layer;
        readonly CursorGlyph glyph;
        readonly MonitorSurface surface;
        bool ownsNativeCursor, previousNativeVisible;
        public bool IsDisplayed { get; private set; }
        public DesktopCursorShape Shape => glyph.Shape;
        public bool IsSage => glyph.IsSage;
        public Vector2 PanelPosition { get; private set; }
        public VisualElement Visual => glyph;
#if UNITY_EDITOR
        // Deterministic editor QA without moving the user's actual mouse.
        public Vector2? VerificationScreenPosition { get; set; }
        public bool? VerificationApplicationFocus { get; set; }
#endif

        public DesktopCursor(VisualElement root, MonitorSurface surface)
        {
            this.root=root;this.surface=surface;
            layer=new VisualElement { name="OSCursorLayer", pickingMode=PickingMode.Ignore };
            layer.style.position=Position.Absolute;
            layer.style.left=layer.style.right=layer.style.top=layer.style.bottom=0;
            layer.style.overflow=Overflow.Hidden;
            glyph=new CursorGlyph();layer.Add(glyph);root.Add(layer);
            layer.style.display=DisplayStyle.None;
        }

        public void UpdateFromInput()
        {
            Vector2 pointer=Input.mousePosition;bool focused=Application.isFocused;
#if UNITY_EDITOR
            pointer=VerificationScreenPosition ?? pointer;
            focused=VerificationApplicationFocus ?? focused;
#endif
            Update(pointer,focused);
        }

        public void Update(Vector2 screenPosition, bool applicationFocused)
        {
            bool active=applicationFocused && surface && surface.seatedView && surface.seatedView.CanInteractWithComputer;
            if(active)
            {
                if(!ownsNativeCursor){previousNativeVisible=UnityEngine.Cursor.visible;ownsNativeCursor=true;}
                UnityEngine.Cursor.visible=false;
            }
            else ReleaseNativeCursor();

            bool visible=active && root.panel!=null && surface.IsPointerOverMonitor(screenPosition);
            if(visible)
            {
                var point=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(screenPosition.x,Screen.height-screenPosition.y));
                var local=root.WorldToLocal(point);
                visible=root.contentRect.Contains(local);
                if(visible)
                {
                    PanelPosition=point;
                    var shape=ResolveShape(root.panel.Pick(point),root.panel.GetCapturingElement(PointerId.mousePointerId));
                    glyph.SetAppearance(shape,root.ClassListContains("sage"));
                    glyph.style.left=local.x-glyph.Hotspot.x;
                    glyph.style.top=local.y-glyph.Hotspot.y;
                    layer.BringToFront();
                }
            }
            IsDisplayed=visible;layer.style.display=visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static DesktopCursorShape ResizeShape(int edges)
        {
            if(edges==1 || edges==2)return DesktopCursorShape.Horizontal;
            if(edges==4 || edges==8)return DesktopCursorShape.Vertical;
            if(edges==5 || edges==10)return DesktopCursorShape.DiagonalDown;
            if(edges==6 || edges==9)return DesktopCursorShape.DiagonalUp;
            return DesktopCursorShape.Move;
        }
        static DesktopCursorShape ResolveShape(VisualElement target, IEventHandler captured)
        {
            if(captured is DesktopWindow window && window.IsPointerGestureActive)return ResizeShape(window.PointerGestureEdges);
            for(var element=target;element!=null;element=element.parent)
            {
                if(!element.enabledInHierarchy)return DesktopCursorShape.Arrow;
                if(element.ClassListContains("resize-edge") && int.TryParse(element.name.Substring(element.name.LastIndexOf('-')+1),out int edges))return ResizeShape(edges);
                if(element is TextField field && !field.isReadOnly)return DesktopCursorShape.Text;
                if(element is Button || element is Toggle || element is DropdownField)return DesktopCursorShape.Link;
                if(element.ClassListContains("titlebar"))return DesktopCursorShape.Move;
            }
            return DesktopCursorShape.Arrow;
        }

        void ReleaseNativeCursor()
        {
            if(!ownsNativeCursor)return;
            UnityEngine.Cursor.visible=surface && surface.seatedView && surface.seatedView.IsLooking ? false : previousNativeVisible;
            ownsNativeCursor=false;
        }
        public void Dispose(){IsDisplayed=false;layer.RemoveFromHierarchy();ReleaseNativeCursor();}

        sealed class CursorGlyph : VisualElement
        {
            public DesktopCursorShape Shape { get; private set; }
            public bool IsSage { get; private set; }
            public Vector2 Hotspot => Shape==DesktopCursorShape.Arrow ? new Vector2(5,4) :
                Shape==DesktopCursorShape.Link ? new Vector2(15,4) : new Vector2(18,18);
            static readonly Vector2[] Arrow={new Vector2(5,4),new Vector2(5,28),new Vector2(11,23),new Vector2(16,33),new Vector2(22,30),new Vector2(16,20),new Vector2(28,19)};
            public CursorGlyph()
            {
                name="OSCursor";pickingMode=PickingMode.Ignore;focusable=false;
                style.position=Position.Absolute;style.width=38;style.height=40;
                generateVisualContent+=Draw;
            }
            public void SetAppearance(DesktopCursorShape shape,bool sage)
            {
                if(Shape==shape && IsSage==sage)return;
                Shape=shape;IsSage=sage;MarkDirtyRepaint();
            }
            void Draw(MeshGenerationContext context)
            {
                var p=context.painter2D;p.lineCap=LineCap.Round;p.lineJoin=LineJoin.Round;
                var outline=new Color(.13f,.19f,.26f);
                var fill=IsSage ? new Color(.95f,.97f,.90f) : new Color(.96f,.98f,1f);
                var accent=IsSage ? new Color(.65f,.79f,.55f) : new Color(.36f,.57f,.83f);
                if(Shape==DesktopCursorShape.Arrow)
                {
                    Polygon(p,Arrow,new Vector2(1,2),new Color(0,0,0,.23f),Color.clear,0);
                    Polygon(p,Arrow,Vector2.zero,fill,outline,1.5f);
                    Stroke(p,accent,2.3f,new Vector2(15,24),new Vector2(18,29));
                }
                else if(Shape==DesktopCursorShape.Link)
                {
                    // Rounded fingertip, compact palm and a small theme-colored cuff.
                    p.BeginPath();p.MoveTo(new Vector2(12,22));p.LineTo(new Vector2(12,7));
                    p.BezierCurveTo(new Vector2(12,2),new Vector2(18,2),new Vector2(18,7));
                    p.LineTo(new Vector2(18,16));p.BezierCurveTo(new Vector2(21,13),new Vector2(24,15),new Vector2(24,18));
                    p.BezierCurveTo(new Vector2(27,15),new Vector2(30,18),new Vector2(29,22));
                    p.LineTo(new Vector2(27,30));p.BezierCurveTo(new Vector2(25,35),new Vector2(16,35),new Vector2(13,31));
                    p.LineTo(new Vector2(6,23));p.BezierCurveTo(new Vector2(3,19),new Vector2(7,17),new Vector2(10,20));
                    p.ClosePath();p.fillColor=fill;p.Fill();p.strokeColor=outline;p.lineWidth=1.5f;p.Stroke();
                    Stroke(p,accent,2.3f,new Vector2(17,30),new Vector2(24,30));
                }
                else
                {
                    // Dark halo and a light center stay readable on both OS themes.
                    for(int pass=0;pass<2;pass++)
                    {
                        Color color=pass==0 ? outline : fill;float width=pass==0 ? 4.5f : 2f;
                        if(Shape==DesktopCursorShape.Text)
                        {
                            Stroke(p,color,width,new Vector2(18,6),new Vector2(18,30));
                            Stroke(p,color,width,new Vector2(13,6),new Vector2(23,6));
                            Stroke(p,color,width,new Vector2(13,30),new Vector2(23,30));
                        }
                        else
                        {
                            if(Shape==DesktopCursorShape.Horizontal || Shape==DesktopCursorShape.Move)DoubleArrow(p,color,width,new Vector2(5,18),new Vector2(31,18));
                            if(Shape==DesktopCursorShape.Vertical || Shape==DesktopCursorShape.Move)DoubleArrow(p,color,width,new Vector2(18,5),new Vector2(18,31));
                            if(Shape==DesktopCursorShape.DiagonalDown)DoubleArrow(p,color,width,new Vector2(8,8),new Vector2(28,28));
                            if(Shape==DesktopCursorShape.DiagonalUp)DoubleArrow(p,color,width,new Vector2(8,28),new Vector2(28,8));
                        }
                    }
                    Stroke(p,accent,2.3f,new Vector2(18,16),new Vector2(18,20));
                }
            }
            static void Polygon(Painter2D p,Vector2[] points,Vector2 offset,Color fill,Color outline,float width)
            {
                p.BeginPath();p.MoveTo(points[0]+offset);for(int i=1;i<points.Length;i++)p.LineTo(points[i]+offset);p.ClosePath();
                p.fillColor=fill;p.Fill();if(width>0){p.strokeColor=outline;p.lineWidth=width;p.Stroke();}
            }
            static void Stroke(Painter2D p,Color color,float width,params Vector2[] points)
            {
                p.BeginPath();p.MoveTo(points[0]);for(int i=1;i<points.Length;i++)p.LineTo(points[i]);p.strokeColor=color;p.lineWidth=width;p.Stroke();
            }
            static void DoubleArrow(Painter2D p,Color color,float width,Vector2 a,Vector2 b)
            {
                var direction=(b-a).normalized;var side=new Vector2(-direction.y,direction.x);
                Stroke(p,color,width,a,b);
                Stroke(p,color,width,a+direction*5+side*5,a,a+direction*5-side*5);
                Stroke(p,color,width,b-direction*5+side*5,b,b-direction*5-side*5);
            }
        }
    }
}
