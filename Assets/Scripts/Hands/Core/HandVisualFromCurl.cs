using System.Collections;
using UnityEngine;

/// <summary>
/// HandVisualFromCurl で共有するパラメータプリセット。
/// （HandCurlTracker は生値を渡す前提のため、主にビジュアル用）
/// </summary>
[CreateAssetMenu(menuName = "HapticPiano/HandModelPreset")]
public class HandModelPreset : ScriptableObject
{
    [Header("Visual Angles (deg)")]
    public float thumbMaxAngle = 220f;
    public float indexMaxAngle = 220f;
    public float middleMaxAngle = 220f;
    public float ringMaxAngle = 220f;
    public float pinkyMaxAngle = 220f;
}

// HandVisualFromCurl は、HandCurlTracker の curl 値に基づいて
// 手のモデルの指の関節を回転させるコンポーネントである。
// - curl=0 のとき、Prefab / シーン上のポーズをそのまま再現
// - curl=1 のとき、MaxAngle まで曲げる
public class HandVisualFromCurl : MonoBehaviour
{
    [Header("Source")]
    public HandCurlTracker curlSource;

    [Header("Kinematics")]
    [Tooltip("curl（0〜1）から MCP/PIP/DIP の角度を計算するプロファイル。null の場合は従来の線形モデルを使用する。")]
    public HandKinematicsProfile kinematicsProfile;

    [Header("Visual Finger Joints (root -> tip)")]
    public Transform[] thumbJoints;
    public Transform[] indexJoints;
    public Transform[] middleJoints;
    public Transform[] ringJoints;
    public Transform[] pinkyJoints;

    [Header("Max curl angle (deg)")]
    public float thumbMaxAngle  = 220f;
    public float indexMaxAngle  = 220f;
    public float middleMaxAngle = 220f;
    public float ringMaxAngle   = 220f;
    public float pinkyMaxAngle  = 220f;

    [Header("Preset (optional)")]
    [Tooltip("左右で共通化するパラメータプリセット（config.toml に対応）")]
    public HandModelPreset preset;
    public bool applyPresetOnStart = true;

    // [finger][joint] の基準回転（curl=0 のときの姿勢）
    private readonly Quaternion[][] baseRot = new Quaternion[5][];
    private bool basePoseCaptured;

    private void Start()
    {
        // config.toml 由来の HandModelPreset を一度だけ適用
        if (preset != null && applyPresetOnStart)
        {
            ApplyPreset(preset);
        }

        // Prefab / シーン上のポーズをそのまま curl=0 の基準姿勢として保存
        basePoseCaptured = CaptureBasePose();
        if (!basePoseCaptured)
        {
            Debug.LogWarning("[HandVisualFromCurl] Base pose capture failed. 指ジョイント配列を確認してください。", this);
        }
    }

    private void LateUpdate()
    {
        if (!basePoseCaptured)
            return;
        if (curlSource == null || curlSource.curl01 == null || curlSource.curl01.Length < 5)
            return;

        // curl01（0〜1）から各指の角度を計算し、ジョイントに適用する
        ApplyFingerWithKinematics(HandFinger.Thumb,  0, thumbJoints,  curlSource.curl01[0], thumbMaxAngle);
        ApplyFingerWithKinematics(HandFinger.Index,  1, indexJoints,  curlSource.curl01[1], indexMaxAngle);
        ApplyFingerWithKinematics(HandFinger.Middle, 2, middleJoints, curlSource.curl01[2], middleMaxAngle);
        ApplyFingerWithKinematics(HandFinger.Ring,   3, ringJoints,   curlSource.curl01[3], ringMaxAngle);
        ApplyFingerWithKinematics(HandFinger.Pinky,  4, pinkyJoints,  curlSource.curl01[4], pinkyMaxAngle);
    }

    // 今の VisualHand の姿勢を curl=0 の基準として保存
    private bool CaptureBasePose()
    {
        InitFingerBaseRot(0, thumbJoints);
        InitFingerBaseRot(1, indexJoints);
        InitFingerBaseRot(2, middleJoints);
        InitFingerBaseRot(3, ringJoints);
        InitFingerBaseRot(4, pinkyJoints);

        // いずれかの指が未設定の場合 false を返す
        return baseRot[0] != null &&
               baseRot[1] != null &&
               baseRot[2] != null &&
               baseRot[3] != null &&
               baseRot[4] != null;
    }

    private void InitFingerBaseRot(int fingerIndex, Transform[] joints)
    {
        if (joints == null || joints.Length == 0)
        {
            baseRot[fingerIndex] = null;
            return;
        }

        baseRot[fingerIndex] = new Quaternion[joints.Length];
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] != null)
            {
                // Prefab / 現在の VisualHand の localRotation をそのまま基準姿勢として保存
                baseRot[fingerIndex][i] = joints[i].localRotation;
            }
        }
    }

    private void ApplyFingerWithKinematics(HandFinger finger, int fingerIndex, Transform[] joints, float curl, float legacyMaxAngle)
    {
        if (joints == null || joints.Length == 0 || baseRot[fingerIndex] == null)
            return;

        float c = Mathf.Clamp01(curl);

        float mcpDeg;
        float pipDeg;
        float dipDeg;

        if (kinematicsProfile != null)
        {
            // プロファイルに角度計算を委譲
            kinematicsProfile.Evaluate(finger, c, out mcpDeg, out pipDeg, out dipDeg);
        }
        else
        {
            // プロファイル未設定時は従来の線形モデルと同等の挙動にする
            float totalAngle = legacyMaxAngle * c;
            float perJoint = totalAngle / 3f;
            mcpDeg = perJoint;
            pipDeg = perJoint;
            dipDeg = perJoint;
        }

        // joints 配列の 1,2 を MCP/PIP とみなし、それ以降を DIP とみなす。
        // 0 番目は CMC / meta など curl では曲げない前提とする。
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null)
                continue;

            float angle;
            if (i == 1)
                angle = mcpDeg;
            else if (i == 2)
                angle = pipDeg;
            else if (i >= 3)
                angle = dipDeg;
            else
                angle = 0f;

            joints[i].localRotation =
                baseRot[fingerIndex][i] * Quaternion.Euler(0f, 0f, -angle);
        }
    }

    private void ApplyPreset(HandModelPreset p)
    {
        if (p == null) return;

        thumbMaxAngle  = p.thumbMaxAngle;
        indexMaxAngle  = p.indexMaxAngle;
        middleMaxAngle = p.middleMaxAngle;
        ringMaxAngle   = p.ringMaxAngle;
        pinkyMaxAngle  = p.pinkyMaxAngle;
    }
}
