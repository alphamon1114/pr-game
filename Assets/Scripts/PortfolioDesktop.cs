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
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.backgroundColor = ColorOf("#20242D");

            var desk = Box(root, "#775A48");
            desk.style.position = Position.Absolute;
            desk.style.bottom = 0;
            desk.style.width = Length.Percent(100);
            desk.style.height = Length.Percent(24);

            var monitor = Box(root, "#101319");
            monitor.name = "Monitor";
            monitor.style.width = Length.Percent(82);
            monitor.style.height = Length.Percent(72);
            monitor.style.paddingLeft = monitor.style.paddingRight = 16;
            monitor.style.paddingTop = 16;
            monitor.style.paddingBottom = 25;
            monitor.style.borderTopLeftRadius = monitor.style.borderTopRightRadius = 18;
            monitor.style.borderBottomLeftRadius = monitor.style.borderBottomRightRadius = 18;

            desktop = Box(monitor, "#243D51");
            desktop.name = "Desktop";
            desktop.style.flexGrow = 1;
            desktop.style.paddingLeft = desktop.style.paddingRight = 24;
            desktop.style.paddingTop = 18;
            desktop.style.overflow = Overflow.Hidden;
            var title = Text(desktop, "PERSONAL DESKTOP", 24);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            Text(desktop, "Open a folder. Take a look around.", 14);

            var folders = new VisualElement();
            folders.style.flexDirection = FlexDirection.Row;
            folders.style.flexWrap = Wrap.Wrap;
            folders.style.marginTop = 22;
            desktop.Add(folders);
            Folder(folders, "About me", "ABOUT ME", "Your profile will live here.\n\nName, nickname and a short introduction can be added later.");
            Folder(folders, "Music", "MUSIC", "DRAGON PONY\nNOTD\nOWL CITY\n\nFavorite tracks and personal notes will be added here.");
            Folder(folders, "Games", "FAVORITE GAMES", "XBOX\nForza Horizon series\n\nNINTENDO / Wii\nSuper Paper Mario\n\nPLAYSTATION\nCrash Bandicoot 3: Warped");
            Folder(folders, "My projects", "MY PROJECTS", "Big Shot Fighter / MiniWar\nDigimon asymmetric PvP\nFPS Simulation\nRace\n\nScreenshots, development notes and verified play links will be added here.");

            var taskbar = Box(desktop, "#172938");
            taskbar.style.position = Position.Absolute;
            taskbar.style.bottom = taskbar.style.left = taskbar.style.right = 0;
            taskbar.style.height = 36;
            taskbar.style.justifyContent = Justify.Center;
            taskbar.style.paddingLeft = 18;
            Text(taskbar, "PR OS  /  Desktop", 13);

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
            heading = Text(bar, "", 20);
            heading.style.color = ColorOf("#20364A");
            heading.style.flexGrow = 1;
            var close = new Button(() => window.style.display = DisplayStyle.None) { text = "Close" };
            close.name = "CloseWindow";
            bar.Add(close);
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 16;
            window.Add(scroll);
            body = Text(scroll, "", 18);
            body.style.color = ColorOf("#20364A");
            body.style.whiteSpace = WhiteSpace.Normal;
            window.style.display = DisplayStyle.None;

            var stand = Box(root, "#141820");
            stand.style.width = 90;
            stand.style.height = 28;
            var foot = Box(root, "#101319");
            foot.style.width = 220;
            foot.style.height = 12;
            var keyboard = Box(root, "#303540");
            keyboard.style.width = Length.Percent(34);
            keyboard.style.height = 30;
            keyboard.style.marginTop = 15;
            keyboard.style.alignItems = Align.Center;
            Text(keyboard, "[  ][  ][  ][  ][  ][  ][  ][  ][  ][  ]", 14);
        }

        private void Folder(VisualElement parent, string label, string title, string content)
        {
            var button = new Button(() => OpenFile(title, content)) { text = "+  " + label };
            button.name = label.Replace(" ", "");
            button.style.width = 145;
            button.style.height = 62;
            button.style.marginRight = 12;
            button.style.marginBottom = 12;
            button.style.fontSize = 17;
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
