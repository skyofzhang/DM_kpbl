#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class DiagMaterialCompare
{
    public static void Execute()
    {
        // 默认KpblUnit的材质
        var kpbl = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/KpblUnit.prefab");
        if (kpbl != null)
        {
            var renderer = kpbl.GetComponentInChildren<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                var mat = renderer.sharedMaterial;
                Debug.Log($"[MatCmp] KpblUnit: shader={mat.shader.name}, " +
                    $"smoothness={GetFloat(mat, "_Smoothness")}, metallic={GetFloat(mat, "_Metallic")}, " +
                    $"baseColor={GetColor(mat, "_BaseColor")}, path={AssetDatabase.GetAssetPath(mat)}");
            }
        }

        // 每个gift材质
        string root = "Assets/Models/Kpbl/5个礼物召唤的水豚单位";
        var dirs = new string[] { "卡皮巴拉礼物单位2", "卡皮巴拉礼物单位3", "卡皮巴拉礼物单位4", "卡皮巴拉礼物单位5", "卡皮巴拉礼物单位6" };
        for (int i = 0; i < 5; i++)
        {
            int tier = i + 2;
            string matPath = $"{root}/{dirs[i]}/mat_gift{tier}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) continue;

            Debug.Log($"[MatCmp] Gift{tier}: shader={mat.shader.name}, " +
                $"smoothness={GetFloat(mat, "_Smoothness")}, metallic={GetFloat(mat, "_Metallic")}, " +
                $"baseColor={GetColor(mat, "_BaseColor")}, " +
                $"hasBaseMap={mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null}");

            // 列出所有float属性看看有没有其他影响光泽的属性
            var so = new SerializedObject(mat);
            var savedProps = so.FindProperty("m_SavedProperties");
            var floats = savedProps.FindPropertyRelative("m_Floats");
            string floatList = "";
            for (int j = 0; j < floats.arraySize; j++)
            {
                var entry = floats.GetArrayElementAtIndex(j);
                var key = entry.FindPropertyRelative("first");
                var val = entry.FindPropertyRelative("second");
                floatList += $"  {key.stringValue}={val.floatValue}";
            }
            Debug.Log($"[MatCmp] Gift{tier} all floats:{floatList}");
        }

        Debug.Log("[MatCmp] === DONE ===");
    }

    static string GetFloat(Material mat, string prop)
    {
        return mat.HasProperty(prop) ? mat.GetFloat(prop).ToString("F2") : "N/A";
    }

    static string GetColor(Material mat, string prop)
    {
        if (!mat.HasProperty(prop)) return "N/A";
        var c = mat.GetColor(prop);
        return $"({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})";
    }
}
#endif
