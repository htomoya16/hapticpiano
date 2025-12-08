using UnityEngine;

public enum HandFinger
{
    Thumb = 0,
    Index = 1,
    Middle = 2,
    Ring = 3,
    Pinky = 4,
}

/// <summary>
/// curl 値（0〜1）から MCP/PIP/DIP の角度（deg）を計算するための抽象プロファイル。
/// 線形モデルや PC1 ベースモデルを ScriptableObject として差し替える。
/// </summary>
public abstract class HandKinematicsProfile : ScriptableObject
{
    /// <summary>
    /// finger/curl/curlPrev に対して MCP/PIP/DIP の角度（deg）を計算する。
    /// </summary>
    public abstract void Evaluate(
        HandFinger finger,
        float curl,
        float previousCurl,
        out float mcpDeg,
        out float pipDeg,
        out float dipDeg);
}
