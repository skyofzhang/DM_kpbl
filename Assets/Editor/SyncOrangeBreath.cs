#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 同步橘子呼吸/微晃增强参数到场景序列化值
/// </summary>
public static class SyncOrangeBreath
{
    public static void Execute()
    {
        var orange = GameObject.Find("Orange");
        if (orange == null) { Debug.LogError("[SyncOrangeBreath] Orange not found"); return; }
        var ctrl = orange.GetComponent<CapybaraDuel.Systems.OrangeController>();
        if (ctrl == null) { Debug.LogError("[SyncOrangeBreath] OrangeController not found"); return; }

        var so = new SerializedObject(ctrl);

        // 呼吸增强
        SetFloat(so, "breathScale", 0.03f);
        SetFloat(so, "breathFrequency", 0.5f);
        // 微晃增强
        SetFloat(so, "swayAngle", 5f);
        SetFloat(so, "swayFrequency", 0.35f);
        // 反推疯狂自转
        SetFloat(so, "frenzySpinSpeed", 1080f);
        SetFloat(so, "frenzyDuration", 2f);
        SetFloat(so, "reversalSpeedThreshold", 0.15f);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ctrl);
        Debug.Log("[SyncOrangeBreath] DONE: breathScale=0.03, swayAngle=5, frenzy synced");
    }

    static void SetFloat(SerializedObject so, string name, float val)
    {
        var p = so.FindProperty(name);
        if (p != null)
        {
            float old = p.floatValue;
            p.floatValue = val;
            if (Mathf.Abs(old - val) > 0.001f)
                Debug.Log($"  {name}: {old:F3} -> {val:F3}");
        }
    }
}
#endif
