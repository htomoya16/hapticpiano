#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PC1CurveImporter
{
    private static string DataRootPath =>
        Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "docs", "data", "PC1-hand-kinematics"));

    [MenuItem("Tools/Import PC1 Curve")]
    public static void ImportPc1Curves()
    {
        const string folderRoot = "Assets/Settings";
        const string folderHandModel = "Assets/Settings/HandModel";
        const string assetPath = "Assets/Settings/HandModel/PC1CurveSet.asset";

        // フォルダが無い場合は作成しておく
        if (!AssetDatabase.IsValidFolder(folderRoot))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }

        if (!AssetDatabase.IsValidFolder(folderHandModel))
        {
            AssetDatabase.CreateFolder(folderRoot, "HandModel");
        }

        var curveSet = AssetDatabase.LoadAssetAtPath<PC1CurveSet>(assetPath);
        if (curveSet == null)
        {
            curveSet = ScriptableObject.CreateInstance<PC1CurveSet>();
            AssetDatabase.CreateAsset(curveSet, assetPath);
        }

        curveSet.mcpIndex = LoadCurve("PC1_MCP/PC1_MCP_Index_phase.csv");
        curveSet.mcpMiddle = LoadCurve("PC1_MCP/PC1_MCP_Middle_phase.csv");
        curveSet.mcpRing = LoadCurve("PC1_MCP/PC1_MCP_Ring_phase.csv");
        curveSet.mcpPinky = LoadCurve("PC1_MCP/PC1_MCP_Little_phase.csv");

        curveSet.pipIndex = LoadCurve("PC1_PIP/PC1_PIP_Index_phase.csv");
        curveSet.pipMiddle = LoadCurve("PC1_PIP/PC1_PIP_Middle_phase.csv");
        curveSet.pipRing = LoadCurve("PC1_PIP/PC1_PIP_Ring_phase.csv");
        curveSet.pipPinky = LoadCurve("PC1_PIP/PC1_PIP_Little_phase.csv");

        EditorUtility.SetDirty(curveSet);
        AssetDatabase.SaveAssets();

        Debug.Log("PC1 curves imported into " + assetPath);
    }

    private static AnimationCurve LoadCurve(string relativeCsvPath)
    {
        var fullPath = Path.Combine(DataRootPath, relativeCsvPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning("PC1 CSV not found: " + fullPath);
            return new AnimationCurve();
        }

        var keys = new List<Keyframe>();
        using (var reader = new StreamReader(fullPath))
        {
            string line;
            var isFirstLine = true;

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (isFirstLine)
                {
                    isFirstLine = false;
                    var lowered = line.ToLowerInvariant();
                    if (lowered.Contains("phase") && lowered.Contains("pc1"))
                        continue;
                }

                var parts = line.Split(',');
                if (parts.Length < 2)
                    continue;

                if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var phase))
                    continue;

                if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pc1))
                    continue;

                keys.Add(new Keyframe(phase, pc1));
            }
        }

        var curve = new AnimationCurve(keys.ToArray());

        // キー間を滑らかにつなぐために、すべてのキーの接線をスムーズ化する
        for (var i = 0; i < curve.length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }

        curve.preWrapMode = WrapMode.ClampForever;
        curve.postWrapMode = WrapMode.ClampForever;

        return curve;
    }
}
#endif
