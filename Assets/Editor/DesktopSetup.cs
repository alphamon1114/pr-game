using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrGame.Editor
{
    [InitializeOnLoad]
    public static class DesktopSetup
    {
        static DesktopSetup(){EditorApplication.delayCall+=()=>{PrepareIcons();FitOpenScene();};}
        static void FitOpenScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var surface=Object.FindAnyObjectByType<MonitorSurface>();
            if(!surface)return;
            bool wasDirty=surface.gameObject.scene.isDirty;
            if(surface.FitToDisplay())
            {
                EditorUtility.SetDirty(surface.screenCollider.transform);
                EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
                // Save only a previously clean scene, never unrelated unsaved user edits.
                if(!wasDirty)EditorSceneManager.SaveScene(surface.gameObject.scene);
            }
        }
        [MenuItem("PR Game/Fit OS screen to monitor")]
        public static void Fit(){FitOpenScene();}
        public static void PrepareAndVerify()
        {
            EditorSceneManager.OpenScene(DeskRoomBuilder.ScenePath);
            FitOpenScene();
            PrepareIcons();AssetDatabase.SaveAssets();DesktopVerification.Run();
        }
        static void PrepareIcons()
        {
            if(!AssetDatabase.IsValidFolder("Assets/Resources/Desktop/Icons"))return;
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Resources/Desktop/Icons"}))
            {
                var importer=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if(!importer || (importer.textureCompression==TextureImporterCompression.Uncompressed && importer.alphaIsTransparency && !importer.mipmapEnabled))continue;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
            }
        }
    }
}
