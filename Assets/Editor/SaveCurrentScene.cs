using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SaveCurrentScene
{
    public static string Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.isDirty)
            return "Scene is not dirty, no save needed. Path: " + scene.path;

        EditorSceneManager.SaveScene(scene);
        return "Saved scene: " + scene.path;
    }
}
