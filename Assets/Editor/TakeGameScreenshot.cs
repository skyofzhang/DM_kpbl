using UnityEngine;

public class TakeGameScreenshot
{
    public static string Execute()
    {
        string path = @"C:\Users\Administrator\Desktop\反馈\auto_test\game_live.png";
        ScreenCapture.CaptureScreenshot(path);
        return "Screenshot saved to: " + path;
    }
}
