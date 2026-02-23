#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 确保场景中 Orange 的 OrangeController 序列化值与代码默认值一致
/// 特别是呼吸/微晃/反推自转等新增字段
/// </summary>
public static class UpdateOrangeValues
{
    public static void Execute()
    {
        var orange = GameObject.Find("Orange");
        if (orange == null)
        {
            Debug.LogError("[UpdateOrangeValues] Orange not found in scene");
            return;
        }

        var ctrl = orange.GetComponent<CapybaraDuel.Systems.OrangeController>();
        if (ctrl == null)
        {
            Debug.LogError("[UpdateOrangeValues] OrangeController component not found");
            return;
        }

        var so = new SerializedObject(ctrl);

        // 读取并打印当前值
        Debug.Log($"[UpdateOrangeValues] === Current Orange Values ===");

        string[] fields = {
            "enableRotation",
            "enableIdleSway", "swayAngle", "swayFrequency",
            "enableBreathing", "breathScale", "breathFrequency",
            "enableForceWobble", "wobbleMaxAmplitude", "wobbleFrequency",
            "enableReversalFrenzy", "frenzySpinSpeed", "frenzyDuration", "reversalSpeedThreshold",
            "baseSpinSpeed", "moveSpinMultiplier"
        };

        foreach (var f in fields)
        {
            var prop = so.FindProperty(f);
            if (prop == null)
            {
                Debug.LogWarning($"  {f} = NOT FOUND (new field, will use code default)");
                continue;
            }

            string val = prop.propertyType switch
            {
                SerializedPropertyType.Boolean => prop.boolValue.ToString(),
                SerializedPropertyType.Float => prop.floatValue.ToString("F4"),
                SerializedPropertyType.Integer => prop.intValue.ToString(),
                _ => prop.propertyType.ToString()
            };
            Debug.Log($"  {f} = {val}");
        }

        // 确保关键布尔值是启用的
        SetBool(so, "enableRotation", true);
        SetBool(so, "enableIdleSway", true);
        SetBool(so, "enableBreathing", true);
        SetBool(so, "enableForceWobble", true);
        SetBool(so, "enableReversalFrenzy", true);

        // 确保呼吸/微晃参数不为0
        SetFloatMin(so, "swayAngle", 3f);
        SetFloatMin(so, "swayFrequency", 0.4f);
        SetFloatMin(so, "breathScale", 0.015f);
        SetFloatMin(so, "breathFrequency", 0.5f);

        // 反推自转参数（新字段，场景中默认为0需要设置）
        SetFloatMin(so, "frenzySpinSpeed", 1080f);
        SetFloatMin(so, "frenzyDuration", 2f);
        SetFloatMin(so, "reversalSpeedThreshold", 0.15f);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ctrl);
        Debug.Log("[UpdateOrangeValues] === DONE ===");
    }

    static void SetBool(SerializedObject so, string name, bool val)
    {
        var p = so.FindProperty(name);
        if (p != null && p.boolValue != val)
        {
            Debug.Log($"  [FIX] {name}: {p.boolValue} -> {val}");
            p.boolValue = val;
        }
    }

    static void SetFloatMin(SerializedObject so, string name, float minVal)
    {
        var p = so.FindProperty(name);
        if (p != null && p.floatValue < minVal * 0.1f) // 如果值接近0说明是新字段未初始化
        {
            Debug.Log($"  [FIX] {name}: {p.floatValue} -> {minVal}");
            p.floatValue = minVal;
        }
    }
}
#endif
