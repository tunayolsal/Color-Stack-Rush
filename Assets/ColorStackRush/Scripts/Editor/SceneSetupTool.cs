using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColorStackRush.EditorTools
{
    /// <summary>
    /// One-click scene setup: adds the GameBootstrapper to the open scene.
    /// The bootstrapper builds everything else (player, world, UI, camera rig)
    /// at runtime, so this is the only editor step required.
    /// </summary>
    public static class SceneSetupTool
    {
        [MenuItem("Tools/Color Stack Rush/Setup Scene")]
        public static void SetupScene()
        {
            if (Object.FindFirstObjectByType<GameBootstrapper>() != null)
            {
                EditorUtility.DisplayDialog("Color Stack Rush",
                    "This scene is already set up. Press Play!", "OK");
                return;
            }

            var go = new GameObject("ColorStackRush");
            go.AddComponent<GameBootstrapper>();
            Undo.RegisterCreatedObjectUndo(go, "Setup Color Stack Rush");

            EditorSceneManager.MarkSceneDirty(go.scene);
            EditorSceneManager.SaveOpenScenes();

            EditorUtility.DisplayDialog("Color Stack Rush",
                "Scene ready! Press Play to run the game.\n\n" +
                "Steer with mouse drag / A-D keys in the editor,\n" +
                "swipe on device.", "Let's go!");
        }

        [MenuItem("Tools/Color Stack Rush/Delete Save Data")]
        public static void DeleteSave()
        {
            SaveManager.DeleteAll();
            Debug.Log("[Color Stack Rush] Save data deleted.");
        }
    }
}
