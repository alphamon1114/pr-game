using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame
{
    public sealed class DesktopWindow : VisualElement
    {
        public VisualElement Content { get; }
        public bool Maximized { get; private set; }
        public bool Minimized { get; private set; }
        public bool IsPointerGestureActive => dragging;
        public int PointerGestureEdges => edges;
        public event Action Focused, Changed, Closed;
        readonly Func<Vector2> desktopSize;
        Rect normalRect;
        Rect currentRect;
        bool dragging;
        Vector2 pointerStart;
        Rect gestureRect;
        int edges;
        const float MinWidth=360,MinHeight=270;
        public DesktopWindow(string id,string title,string icon,Rect bounds,Func<Vector2> desktopSize)
        {
            this.desktopSize=desktopSize;name="Window-"+id;AddToClassList("window");normalRect=bounds;SetRect(bounds);
            var bar=PortfolioDesktop.El(this,"titlebar");PortfolioDesktop.Icon(bar,icon);
            PortfolioDesktop.Text(bar,title,"window-title");
            PortfolioDesktop.IconBtn(bar,"minus","최소화",Minimize,"",id+"-minimize");
            PortfolioDesktop.IconBtn(bar,"square","최대화 / 복원",ToggleMaximize,"",id+"-maximize");
            PortfolioDesktop.IconBtn(bar,"x","닫기",Close,"close-button",id+"-close");
            Content=PortfolioDesktop.El(this,"window-content",id+"-content");
            RegisterCallback<PointerDownEvent>(_=>{BringToFront();Focused?.Invoke();},TrickleDown.TrickleDown);
            bar.RegisterCallback<PointerDownEvent>(evt=>
            {
                if(evt.button!=0 || HasButtonAncestor(evt.target as VisualElement,bar))return;
                if(evt.clickCount==2){ToggleMaximize();return;}
                if(Maximized)
                {
                    ToggleMaximize();var restored=CurrentRect();
                    restored.x=Mathf.Clamp(evt.position.x-restored.width/2,0,desktopSize().x-restored.width);restored.y=12;SetRect(restored);
                }
                BeginGesture(evt,0);
            });
            foreach(int mask in new[]{1,2,4,8,5,6,9,10})
            {
                int value=mask;var handle=PortfolioDesktop.El(this,"resize-edge edge-"+mask);handle.name=id+"-resize-"+mask;
                handle.RegisterCallback<PointerDownEvent>(evt=>{if(evt.button==0&&!Maximized)BeginGesture(evt,value);});
            }
            RegisterCallback<PointerMoveEvent>(MoveGesture);
            RegisterCallback<PointerUpEvent>(evt=>{if(dragging){dragging=false;this.ReleasePointer(evt.pointerId);normalRect=CurrentRect();Changed?.Invoke();evt.StopPropagation();}});
            RegisterCallback<PointerCaptureOutEvent>(_=>dragging=false);
            RegisterCallback<GeometryChangedEvent>(_=>EnableInClassList("compact",resolvedStyle.width<620));
        }
        static bool HasButtonAncestor(VisualElement el,VisualElement stop){for(;el!=null&&el!=stop;el=el.parent)if(el is Button)return true;return false;}
        Rect CurrentRect()=>currentRect;
        void SetRect(Rect rect){currentRect=rect;style.left=rect.x;style.top=rect.y;style.width=rect.width;style.height=rect.height;}
        void BeginGesture(PointerDownEvent evt,int resizeEdges)
        {
            dragging=true;edges=resizeEdges;pointerStart=evt.position;gestureRect=CurrentRect();this.CapturePointer(evt.pointerId);evt.StopPropagation();
        }
        void MoveGesture(PointerMoveEvent evt)
        {
            if(!dragging)return;
            var delta=(Vector2)evt.position-pointerStart;var r=gestureRect;var size=desktopSize();
            if(edges==0){r.x=Mathf.Clamp(r.x+delta.x,0,Mathf.Max(0,size.x-r.width));r.y=Mathf.Clamp(r.y+delta.y,0,Mathf.Max(0,size.y-r.height));}
            else
            {
                if((edges&1)!=0){float x=Mathf.Clamp(r.x+delta.x,0,r.xMax-MinWidth);r.width=r.xMax-x;r.x=x;}
                if((edges&2)!=0)r.width=Mathf.Clamp(r.width+delta.x,MinWidth,size.x-r.x);
                if((edges&4)!=0){float y=Mathf.Clamp(r.y+delta.y,0,r.yMax-MinHeight);r.height=r.yMax-y;r.y=y;}
                if((edges&8)!=0)r.height=Mathf.Clamp(r.height+delta.y,MinHeight,size.y-r.y);
            }
            SetRect(r);evt.StopPropagation();
        }
        public void ClampToDesktop()
        {
            var size=desktopSize();if(float.IsNaN(size.x)||size.x<MinWidth)return;
            if(Maximized){SetRect(new Rect(0,0,size.x,size.y));return;}
            var r=CurrentRect();if(float.IsNaN(r.width))r=normalRect;
            r.width=Mathf.Clamp(r.width,MinWidth,size.x);r.height=Mathf.Clamp(r.height,MinHeight,size.y);
            r.x=Mathf.Clamp(r.x,0,size.x-r.width);r.y=Mathf.Clamp(r.y,0,size.y-r.height);SetRect(r);
        }
        public void ToggleMaximize()
        {
            if(!Maximized){normalRect=CurrentRect();Maximized=true;EnableInClassList("maximized",true);ClampToDesktop();}
            else{Maximized=false;EnableInClassList("maximized",false);SetRect(normalRect);ClampToDesktop();}
            Changed?.Invoke();
        }
        public void Snap(bool right)
        {
            if(Maximized)ToggleMaximize();var s=desktopSize();SetRect(new Rect(right?s.x/2:0,90,s.x/2,s.y-90));normalRect=CurrentRect();Changed?.Invoke();
        }
        public void Minimize(){Minimized=true;style.display=DisplayStyle.None;Changed?.Invoke();}
        public void Show(){Minimized=false;style.display=DisplayStyle.Flex;BringToFront();Focused?.Invoke();Changed?.Invoke();}
        public void Close(){RemoveFromHierarchy();Closed?.Invoke();}
    }
}
