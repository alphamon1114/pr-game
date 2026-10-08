using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame
{
    // Web pixels and pointer events share this exact rectangle inside the OS panel.
    public sealed class WebBrowserView : VisualElement
    {
        readonly WebBrowserPage page;
        readonly Func<bool> canInteract;
        readonly Image image;
        bool ownsIme;
        IMECompositionMode previousIme;
        string composition="";
        public WebBrowserPage Page=>page;
        public bool HasKeyboardFocus=>panel?.focusController?.focusedElement==this;
        public DesktopCursorShape CursorShape=>page.Cursor=="Hand"?DesktopCursorShape.Link:
            page.Cursor=="IBeam"?DesktopCursorShape.Text:DesktopCursorShape.Arrow;
        public WebBrowserView(WebBrowserPage page,Func<bool> canInteract)
        {
            this.page=page;this.canInteract=canInteract;name="LiveWebView";focusable=true;tabIndex=0;AddToClassList("live-web-view");
            image=new Image{pickingMode=PickingMode.Ignore,scaleMode=ScaleMode.StretchToFill};image.AddToClassList("fill");Add(image);
            RegisterCallback<PointerDownEvent>(e=>
            {
                if(!canInteract()||e.button>2)return;Focus();this.CapturePointer(e.pointerId);
                Input.compositionCursorPos=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
                SendPointer("down",e);e.StopPropagation();
            });
            RegisterCallback<PointerUpEvent>(e=>
            {
                if(!canInteract())return;SendPointer("up",e);this.ReleasePointer(e.pointerId);e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e=>{if(canInteract()){SendPointer("move",e);e.StopPropagation();}});
            RegisterCallback<PointerLeaveEvent>(e=>{if(!this.HasPointerCapture(e.pointerId))SendPointer("move",e,true);});
            RegisterCallback<WheelEvent>(e=>
            {
                if(!canInteract())return;var p=ToPixel(e.mousePosition);
                page.Send(new WebCommand{type="wheel",x=p.x,y=p.y,dx=Mathf.RoundToInt(-e.delta.x*40),dy=Mathf.RoundToInt(-e.delta.y*40),modifiers=Modifiers(e.modifiers,0)});
                e.StopPropagation();
            });
            RegisterCallback<KeyDownEvent>(e=>SendKey(e,true));RegisterCallback<KeyUpEvent>(e=>SendKey(e,false));
            RegisterCallback<FocusInEvent>(_=>
            {
                if(!canInteract())return;
                previousIme=Input.imeCompositionMode;Input.imeCompositionMode=IMECompositionMode.On;ownsIme=true;
                page.Send(new WebCommand{type="focus",flag=true});
            });
            RegisterCallback<FocusOutEvent>(_=>ReleaseInput());
            RegisterCallback<DetachFromPanelEvent>(_=>ReleaseInput());
            RegisterCallback<GeometryChangedEvent>(_=>Resize());
        }
        public void Resize()
        {
            if(contentRect.width>1&&contentRect.height>1)page.Resize(Mathf.RoundToInt(contentRect.width*1.25f),Mathf.RoundToInt(contentRect.height*1.25f));
        }
        public void Tick(bool visible)
        {
            page.SetVisible(visible);
            if(!visible||!canInteract())
            {if(HasKeyboardFocus)Blur();ReleaseInput();return;}
            if(page.ReadFrame())image.image=page.Texture;
            if(!HasKeyboardFocus)return;
            string committed=new string(Input.inputString.Where(c=>!char.IsControl(c)).ToArray());
            if(committed.Length>0)CommitText(committed);
            string next=Input.compositionString;
            if(composition!=next){composition=next;page.Send(new WebCommand{type="composition",text=next});}
        }
        public void CommitText(string text){if(canInteract())page.Send(new WebCommand{type="text",text=text});}
        public void ReleaseInput()
        {
            if(!ownsIme)return;ownsIme=false;Input.imeCompositionMode=previousIme;composition="";
            page.Send(new WebCommand{type="focus",flag=false});
            if(this.HasPointerCapture(PointerId.mousePointerId))this.ReleasePointer(PointerId.mousePointerId);
        }
        Vector2Int ToPixel(Vector2 position)
        {
            var local=this.WorldToLocal(position);
            return new Vector2Int(Mathf.RoundToInt(local.x/Mathf.Max(1,contentRect.width)*page.Width),Mathf.RoundToInt(local.y/Mathf.Max(1,contentRect.height)*page.Height));
        }
        void SendPointer(string type,IPointerEvent e,bool leave=false)
        {
            var p=ToPixel(e.position);
            page.Send(new WebCommand{type=type,x=p.x,y=p.y,button=e.button==1?2:e.button==2?1:0,count=Mathf.Max(1,e.clickCount),modifiers=Modifiers(e.modifiers,e.pressedButtons),flag=leave});
        }
        static int Modifiers(EventModifiers modifiers,int buttons)
        {
            int result=0;
            if((modifiers&EventModifiers.Shift)!=0)result|=2;if((modifiers&EventModifiers.Control)!=0)result|=4;if((modifiers&EventModifiers.Alt)!=0)result|=8;
            if((buttons&1)!=0)result|=16;if((buttons&4)!=0)result|=32;if((buttons&2)!=0)result|=64;return result;
        }
        void SendKey(IKeyboardEvent e,bool down)
        {
            if(!canInteract()||!HasKeyboardFocus)return;
            int key=WindowsKey(e.keyCode);
            if(key>0&&(Input.compositionString.Length==0||e.ctrlKey||e.altKey))
                page.Send(new WebCommand{type=down?"keyDown":"keyUp",key=key,modifiers=Modifiers(e.modifiers,0)});
            (e as EventBase)?.StopPropagation();
        }
        static int WindowsKey(KeyCode code)
        {
            if(code>=KeyCode.A&&code<=KeyCode.Z)return 65+(code-KeyCode.A);
            if(code>=KeyCode.Alpha0&&code<=KeyCode.Alpha9)return (int)code;
            if(code>=KeyCode.F1&&code<=KeyCode.F12)return 112+(code-KeyCode.F1);
            switch(code)
            {
                case KeyCode.Backspace:return 8;case KeyCode.Tab:return 9;case KeyCode.Return:case KeyCode.KeypadEnter:return 13;
                case KeyCode.LeftShift:case KeyCode.RightShift:return 16;case KeyCode.LeftControl:case KeyCode.RightControl:return 17;
                case KeyCode.LeftAlt:case KeyCode.RightAlt:return 18;case KeyCode.Escape:return 27;case KeyCode.Space:return 32;
                case KeyCode.PageUp:return 33;case KeyCode.PageDown:return 34;case KeyCode.End:return 35;case KeyCode.Home:return 36;
                case KeyCode.LeftArrow:return 37;case KeyCode.UpArrow:return 38;case KeyCode.RightArrow:return 39;case KeyCode.DownArrow:return 40;
                case KeyCode.Insert:return 45;case KeyCode.Delete:return 46;case KeyCode.Semicolon:return 186;case KeyCode.Equals:return 187;
                case KeyCode.Comma:return 188;case KeyCode.Minus:return 189;case KeyCode.Period:return 190;case KeyCode.Slash:return 191;
                case KeyCode.BackQuote:return 192;case KeyCode.LeftBracket:return 219;case KeyCode.Backslash:return 220;case KeyCode.RightBracket:return 221;case KeyCode.Quote:return 222;
                default:return 0;
            }
        }
    }
}
