using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PortfolioDesktop : MonoBehaviour
    {
        private VisualElement desktop;
        private VisualElement window;
        private Label heading;
        private Label body;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.backgroundColor = ColorOf("#172F3B");

            desktop = Box(root, "#172F3B");
            desktop.name = "Desktop";
            desktop.style.flexGrow = 1;
            desktop.style.paddingLeft = desktop.style.paddingRight = 56;
            desktop.style.paddingTop = 42;
            desktop.style.overflow = Overflow.Hidden;
            Text(desktop, "PR OS    /    LOCAL SESSION", 17);
            var title = Text(desktop, "Personal space.", 60);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            Text(desktop, "Music, games, and things I make.", 22);

            var folders = new VisualElement();
            folders.style.flexDirection = FlexDirection.Row;
            folders.style.flexWrap = Wrap.Wrap;
            folders.style.marginTop = 52;
            desktop.Add(folders);
            Folder(folders, "About me", "ABOUT ME", "Your profile will live here.\n\nName, nickname and a short introduction can be added later.");
            Folder(folders, "Music", "MUSIC", "DRAGON PONY\nNOTD\nOWL CITY\n\nFavorite tracks and personal notes will be added here.");
            Folder(folders, "Games", "FAVORITE GAMES", "XBOX\nForza Horizon series\n\nNINTENDO / Wii\nSuper Paper Mario\n\nPLAYSTATION\nCrash Bandicoot 3: Warped");
            Folder(folders, "My projects", "MY PROJECTS", "Big Shot Fighter / MiniWar\nDigimon asymmetric PvP\nFPS Simulation\nRace\n\nScreenshots, development notes and verified play links will be added here.");

            var taskbar = Box(desktop, "#172938");
            taskbar.style.position = Position.Absolute;
            taskbar.style.bottom = taskbar.style.left = taskbar.style.right = 0;
            taskbar.style.height = 44;
            taskbar.style.justifyContent = Justify.Center;
            taskbar.style.paddingLeft = 18;
            Text(taskbar, "DESKTOP    /    4 FOLDERS                                      OFFLINE", 16);

            window = Box(desktop, "#EDF0F3");
            window.name = "FileWindow";
            window.style.position = Position.Absolute;
            window.style.left = Length.Percent(18);
            window.style.top = Length.Percent(12);
            window.style.width = Length.Percent(76);
            window.style.height = Length.Percent(76);
            window.style.paddingLeft = window.style.paddingRight = 20;
            window.style.paddingTop = window.style.paddingBottom = 15;
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            window.Add(bar);
            heading = Text(bar, "", 26);
            heading.style.color = ColorOf("#20364A");
            heading.style.flexGrow = 1;
            var close = new Button(() => window.style.display = DisplayStyle.None) { text = "Close" };
            close.name = "CloseWindow";
            bar.Add(close);
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 16;
            window.Add(scroll);
            body = Text(scroll, "", 25);
            body.style.color = ColorOf("#20364A");
            body.style.whiteSpace = WhiteSpace.Normal;
            window.style.display = DisplayStyle.None;

        }

        private void Folder(VisualElement parent, string label, string title, string content)
        {
            var button = new Button(() => OpenFile(title, content)) { text = "+  " + label };
            button.name = label.Replace(" ", "");
            button.style.width = 245;
            button.style.height = 100;
            button.style.marginRight = 18;
            button.style.marginBottom = 12;
            button.style.fontSize = 25;
            button.style.backgroundColor = ColorOf("#E3BB80");
            button.style.color = ColorOf("#253840");
            parent.Add(button);
        }

        public void OpenFile(string title, string content)
        {
            heading.text = title;
            body.text = content;
            window.style.display = DisplayStyle.Flex;
            window.BringToFront();
        }

        private static VisualElement Box(VisualElement parent, string color)
        {
            var element = new VisualElement();
            element.style.backgroundColor = ColorOf(color);
            parent.Add(element);
            return element;
        }

        private static Label Text(VisualElement parent, string value, int size)
        {
            var label = new Label(value);
            label.style.fontSize = size;
            label.style.color = ColorOf("#E8EDF4");
            parent.Add(label);
            return label;
        }

        private static Color ColorOf(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }
    }
}
