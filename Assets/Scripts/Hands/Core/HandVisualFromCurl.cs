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

        // curl01（0〜1）をそのまま使って各指を回転
        ApplyFinger(thumbJoints,  0, curlSource.curl01[0], thumbMaxAngle);
        ApplyFinger(indexJoints,  1, curlSource.curl01[1], indexMaxAngle);
        ApplyFinger(middleJoints, 2, curlSource.curl01[2], middleMaxAngle);
        ApplyFinger(ringJoints,   3, curlSource.curl01[3], ringMaxAngle);
        ApplyFinger(pinkyJoints,  4, curlSource.curl01[4], pinkyMaxAngle);
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

    private void ApplyFinger(Transform[] joints, int fingerIndex, float raw, float maxAngle)
    {
        if (joints == null || joints.Length == 0 || baseRot[fingerIndex] == null)
            return;

        float x = Mathf.Clamp01(raw);
        float totalAngle = maxAngle * x;

        int jointCount = joints.Length;
        if (jointCount <= 0) return;

        float perJointAngle = totalAngle / jointCount;
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null) continue;
            joints[i].localRotation =
                baseRot[fingerIndex][i] * Quaternion.Euler(0f, 0f, -perJointAngle);
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
