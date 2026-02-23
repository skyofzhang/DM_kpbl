using UnityEngine;
using UnityEditor;
using CapybaraDuel.Systems;

public class UpdateOrangeRange
{
    public static string Execute()
    {
        var orange = GameObject.Find("Orange");
        if (orange == null)
            return "ERROR: Orange not found";

        var oc = orange.GetComponent<OrangeController>();
        if (oc == null)
            return "ERROR: OrangeController not found";

        // Use SerializedObject to update private serialized fields
        var so = new SerializedObject(oc);
        var minProp = so.FindProperty("positionRangeMin");
        var maxProp = so.FindProperty("positionRangeMax");

        if (minProp == null || maxProp == null)
            return "ERROR: Properties not found";

        float oldMin = minProp.floatValue;
        float oldMax = maxProp.floatValue;

        minProp.floatValue = -100f;
        maxProp.floatValue = 100f;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(oc);

        return $"OK: positionRangeMin {oldMin} -> -100, positionRangeMax {oldMax} -> 100";
    }
}
