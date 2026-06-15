using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Linq;

public class SceneSwitcherWindow : EditorWindow
{
    private Vector2 scrollPos;

    [MenuItem("Tools/Scene Switcher")] // Shortcut: Ctrl + S (Cmd + S on Mac)
    public static void ShowWindow()
    {
        var window = GetWindow<SceneSwitcherWindow>("Scene Switcher");
        window.minSize = new Vector2(250, 200);
    }

    private void OnGUI()
    {
        GUILayout.Label("Scene Switcher", EditorStyles.boldLabel);

        if (GUILayout.Button("Refresh Scenes"))
        {
            Repaint();
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));

        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        foreach (var scenePath in scenes)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (GUILayout.Button(sceneName))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);
                }
            }
        }

        EditorGUILayout.EndScrollView();
        GUILayout.Space(10);

        if (GUILayout.Button("Clear PlayerPrefs", GUILayout.Height(30)))
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("PlayerPrefs Cleared!");
        }
    }
}
