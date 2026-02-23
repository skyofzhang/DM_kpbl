using UnityEngine;
using UnityEditor;

public class RestorePanel
{
    public static string Execute()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return "Canvas not found";

        foreach (Transform child in canvas.transform)
        {
            if (child.name == "MainMenuPanel")
                child.gameObject.SetActive(true);
            if (child.name == "GameUIPanel")
                child.gameObject.SetActive(false);
        }

        return "MainMenuPanel restored, GameUIPanel hidden";
    }
}
