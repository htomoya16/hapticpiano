using System.Collections;
using UnityEngine;
using Valve.VR;

// HandVisualFromCurl は、HandCurlTracker の curl 値に基づいて
// 手のモデルの指の関節を回転させるコンポーネントである。
public class HandVisualFromCurl : MonoBehaviour
{
    [Header("Source")]

    public HandCurlTracker curlSource;

    [Header("Skeleton Finger Joints (root -> tip)")]
    // SensorHand_Right/Left の SteamVR_Behaviour_Skeleton 側のボーンを対応させる
    public Transform[] skeletonThumbJoints;
    public Transform[] skeletonIndexJoints;
    public Transform[] skeletonMiddleJoints;
    public Transform[] skeletonRingJoints;
    public Transform[] skeletonPinkyJoints;

   // Right/Left Hand に付いている HandCurlTracker

    [Header("Visual Finger Joints (root -> tip)")]
    public Transform[] thumbJoints;
    public Transform[] indexJoints;
    public Transform[] middleJoints;
    public Transform[] ringJoints;
    public Transform[] pinkyJoints;

    [Header("Max curl angle (deg)")]
    public float thumbMaxAngle  = 45f;
    public float indexMaxAngle  = 70f;
    public float middleMaxAngle = 70f;
    public float ringMaxAngle   = 70f;
    public float pinkyMaxAngle  = 70f;

    [Header("Curve / Deadzone")]
    [Range(0f, 0.5f)]
    public float deadZone = 0.05f;
    [Range(0.5f, 3f)]
    public float gamma = 1.2f;

    [Header("Preset (optional)")]
    [Tooltip("左右で共通化するパラメータプリセット")]
    public HandModelPreset preset;
    public bool applyPresetOnStart = true;

    [Header("Calibration (UI trigger)")]
    [Tooltip("ワールド空間 UI のボタンから呼び出す。ログが不要ならオフ。")]
    public bool logCalibrateFailure = true;

    // [finger][joint] の基準回転（curl=0 のときの姿勢）
    private Quaternion[][] baseRot = new Quaternion[5][];
    private bool basePoseCaptured = false;

    [Header("Calibration")]
    [Tooltip("起動時に数フレーム待って Skeleton から基準ポーズを取る")]
    public bool autoCalibrateOnStart = true;
    public int waitFramesBeforeCalib = 10; // 起動後何フレーム待つか

    private void Awake()
    {
        // 何もしない。実際のキャリブレーションは Start コルーチンでやる
    }

    private IEnumerator Start()
    {
        // プリセット適用（左右整合を取りやすくする）
        if (applyPresetOnStart && preset != null)
        {
            ApplyPreset(preset);
            // curlSource 側にも共有値を反映
            if (curlSource != null)
            {
                curlSource.ApplyPreset(preset);
            }
        }

        if (!autoCalibrateOnStart)
            yield break;

        // SteamVR_Behaviour_Skeleton が姿勢を更新し終わるまで少し待つ
        for (int i = 0; i < waitFramesBeforeCalib; i++)
        {
            yield return null;
        }

        // 起動時に自動キャリブレーション
        CalibrateFromSkeleton();
    }

    /// <summary>
    /// 現在の Skeleton 参照ポーズを使って、curl=0 の基準姿勢を取り直す
    /// （ボタン入力などから呼び出す用）
    /// </summary>
    public bool CalibrateFromSkeleton()
    {
        // このタイミングで「手を伸ばした状態」にしておく想定
        CopyPoseFromSkeletonToVisual(thumbJoints,  skeletonThumbJoints);
        CopyPoseFromSkeletonToVisual(indexJoints,  skeletonIndexJoints);
        CopyPoseFromSkeletonToVisual(middleJoints, skeletonMiddleJoints);
        CopyPoseFromSkeletonToVisual(ringJoints,   skeletonRingJoints);
        CopyPoseFromSkeletonToVisual(pinkyJoints,  skeletonPinkyJoints);

        bool success = CaptureBasePose();
        basePoseCaptured = success;
        return success;
    }

    private void LateUpdate()
    {
        if (curlSource == null || curlSource.curl01 == null || curlSource.curl01.Length < 5)
            return;

        // まだ基準ポーズが取れていないなら何もしない（Start のコルーチン待ち）
        if (!basePoseCaptured)
            return;

        ApplyFinger(thumbJoints,  0, curlSource.curl01[0], thumbMaxAngle);
        ApplyFinger(indexJoints,  1, curlSource.curl01[1], indexMaxAngle);
        ApplyFinger(middleJoints, 2, curlSource.curl01[2], middleMaxAngle);
        ApplyFinger(ringJoints,   3, curlSource.curl01[3], ringMaxAngle);
        ApplyFinger(pinkyJoints,  4, curlSource.curl01[4], pinkyMaxAngle);
    }

    // Skeleton 側の joint 配列から Visual 側の joint 配列に localRotation をコピーする
    private void CopyPoseFromSkeletonToVisual(Transform[] visual, Transform[] skeleton)
    {
        if (visual == null || skeleton == null) return;

        int count = Mathf.Min(visual.Length, skeleton.Length);
        for (int i = 0; i < count; i++)
        {
            if (visual[i] == null || skeleton[i] == null) continue;
            visual[i].localRotation = skeleton[i].localRotation;
        }
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
        return baseRot[0] != null && baseRot[1] != null && baseRot[2] != null && baseRot[3] != null && baseRot[4] != null;
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
                baseRot[fingerIndex][i] = joints[i].localRotation;
        }
    }

    private void ApplyFinger(Transform[] joints, int fingerIndex, float raw, float maxAngle)
    {
        if (joints == null || joints.Length == 0 || baseRot[fingerIndex] == null)
            return;

        float x = Mathf.Clamp01(raw);

        // デッドゾーン
        if (x < deadZone)
            x = 0f;
        else
            x = (x - deadZone) / (1f - deadZone);

        // カーブ
        x = Mathf.Pow(x, gamma);

        float totalAngle = maxAngle * x;
        float perJointAngle = totalAngle / joints.Length;

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null) continue;
            joints[i].localRotation =
                baseRot[fingerIndex][i] * Quaternion.Euler(0f, 0f, -perJointAngle);
        }
    }

    /// <summary>
    /// ワールド空間 UI のボタン OnClick に紐付けて呼び出す。
    /// 左手ボタンは左手オブジェクトのこのメソッド、右手ボタンは右手オブジェクトのこのメソッドを指定する。
    /// </summary>
    public void RecalibrateFromUIButton()
    {
        bool ok = CalibrateFromSkeleton();
        if (!ok && logCalibrateFailure)
        {
            Debug.LogWarning("[HandVisualFromCurl] 再キャリブレーション失敗。Skeleton 参照を確認してください。");
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
        deadZone = p.visualDeadZone;
        gamma = p.visualGamma;
    }
}
