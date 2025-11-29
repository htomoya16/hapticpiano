using UnityEngine;

public class HandVisualFromCurl : MonoBehaviour
{
    [Header("Source")]
    public HandCurlTracker curlSource;  // LeftHand に付いている HandCurlTracker

    [Header("Finger Joints (base bone per finger)")]
    public Transform thumbJoint;
    public Transform indexJoint;
    public Transform middleJoint;
    public Transform ringJoint;
    public Transform pinkyJoint;

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

    Quaternion[] baseRot;

    void Awake()
    {
        baseRot = new Quaternion[5];
        if (thumbJoint  != null) baseRot[0] = thumbJoint.localRotation;
        if (indexJoint  != null) baseRot[1] = indexJoint.localRotation;
        if (middleJoint != null) baseRot[2] = middleJoint.localRotation;
        if (ringJoint   != null) baseRot[3] = ringJoint.localRotation;
        if (pinkyJoint  != null) baseRot[4] = pinkyJoint.localRotation;
    }

    void LateUpdate()
    {
        if (curlSource == null || curlSource.curl01 == null) return;

        ApplyFinger(thumbJoint,  0, curlSource.curl01[0], thumbMaxAngle);
        ApplyFinger(indexJoint,  1, curlSource.curl01[1], indexMaxAngle);
        ApplyFinger(middleJoint, 2, curlSource.curl01[2], middleMaxAngle);
        ApplyFinger(ringJoint,   3, curlSource.curl01[3], ringMaxAngle);
        ApplyFinger(pinkyJoint,  4, curlSource.curl01[4], pinkyMaxAngle);
    }

    void ApplyFinger(Transform joint, int idx, float raw, float maxAngle)
    {
        if (joint == null) return;

        float x = Mathf.Clamp01(raw);

        // デッドゾーン処理
        if (x < deadZone)
            x = 0f;
        else
            x = (x - deadZone) / (1f - deadZone);

        // カーブ
        x = Mathf.Pow(x, gamma);

        float angle = maxAngle * x;

        // モデルによって X/Y/Z が違う可能性があるので、必要なら軸は変える
        joint.localRotation = baseRot[idx] * Quaternion.Euler(angle, 0f, 0f);
    }
}
