using UnityEngine;

/// <summary>
/// シンプルな線形モデル（curl を 0〜1 で受け取り、
/// 各指の MCP/PIP/DIP を同じ量だけ曲げる）を定義するプロファイル。
/// もともとの HandVisualFromCurl の等分モデルに相当する。
/// </summary>
[CreateAssetMenu(menuName = "HapticPiano/Kinematics/Linear")]
public class LinearHandKinematicsProfile : HandKinematicsProfile
{
    [Header("Max Angles (deg)")]
    public float thumbMaxAngle = 220f;
    public float indexMaxAngle = 220f;
    public float middleMaxAngle = 220f;
    public float ringMaxAngle = 220f;
    public float pinkyMaxAngle = 220f;

    public override void Evaluate(
        HandFinger finger,
        float curl,
        out float mcpDeg,
        out float pipDeg,
        out float dipDeg)
    {
        float c = Mathf.Clamp01(curl);
        float maxAngle = GetMaxAngle(finger);

        // 3 関節（MCP/PIP/DIP）に均等に分配する。
        float total = maxAngle * c;
        float perJoint = total / 3f;

        mcpDeg = perJoint;
        pipDeg = perJoint;
        dipDeg = perJoint;
    }

    private float GetMaxAngle(HandFinger finger)
    {
        switch (finger)
        {
            case HandFinger.Thumb:  return thumbMaxAngle;
            case HandFinger.Index:  return indexMaxAngle;
            case HandFinger.Middle: return middleMaxAngle;
            case HandFinger.Ring:   return ringMaxAngle;
            case HandFinger.Pinky:  return pinkyMaxAngle;
            default:                return indexMaxAngle;
        }
    }
}

