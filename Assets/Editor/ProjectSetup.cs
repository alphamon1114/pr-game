using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrGame.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("PR Game/Create initial desktop scene")]
        public static void CreateScene()
        {
            const string scenePath = "Assets/Scenes/Desktop.unity";
            if (File.Exists(scenePath))
                throw new InvalidOperationException("Desktop scene already exists; refusing to overwrite it.");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1600, 900);
            File.WriteAllText("Assets/Settings/DesktopTheme.tss", "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset("Assets/Settings/DesktopTheme.tss");
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Settings/DesktopTheme.tss");
            panel.themeStyleSheet = theme;
            AssetDatabase.CreateAsset(panel, "Assets/Settings/DesktopPanel.asset");
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.gameObject.tag = "MainCamera";
            var screen = new GameObject("Portfolio Desktop");
            var document = screen.AddComponent<UIDocument>();
            document.panelSettings = panel;
            screen.AddComponent<PortfolioDesktop>();
            PlayerSettings.productName = "PR GAME";
            PlayerSettings.companyName = "Personal Projects";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("PR_GAME_SETUP_OK");
        }
    }
}
