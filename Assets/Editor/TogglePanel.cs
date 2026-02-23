using UnityEngine;
using UnityEditor;

public class TogglePanel
{
    public static string Execute()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return "Canvas not found";

        // Hide MainMenuPanel
        var hideObj = canvas.transform.Find("MainMenuPanel");
        if (hideObj != null) hideObj.gameObject.SetActive(false);

        // Show GameUIPanel (search inactive children)
        foreach (Transform child in canvas.transform)
        {
            if (child.name == "GameUIPanel")
            {
                child.gameObject.SetActive(true);
                return "GameUIPanel activated, MainMenuPanel hidden";
            }
        }

        return "GameUIPanel not found";
    }
}
