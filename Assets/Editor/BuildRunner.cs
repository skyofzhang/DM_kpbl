#if UNITY_EDITOR
using UnityEditor;

namespace CapybaraDuel.Editor
{
    public static class BuildRunner
    {
        public static string Execute()
        {
            BuildTool.BuildWindows();
            return "Build started - check Unity console for results";
        }
    }
}
#endif
