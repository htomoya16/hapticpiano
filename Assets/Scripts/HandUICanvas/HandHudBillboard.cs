using UnityEngine;

public class HandHudBillboard_HmdAligned : MonoBehaviour
{
    [Header("HMD カメラ (VR Camera)")]
    public Transform cameraTransform;

    [Header("手のアンカー (wrist など)")]
    public Transform handAnchor;

    [Header("手首からのオフセット（手のローカル座標系で指定）")]
    public Vector3 localOffset = new Vector3(0.05f, 0.03f, 0.0f);

    [Header("HMD 正面からの回転オフセット")]
    public Vector3 eulerOffset = new Vector3(0f, 180f, 0f); 
    // Canvas が裏向きなら Y=180 にしておく

    void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        if (cameraTransform == null || handAnchor == null)
            return;

        // ① 手首位置 + オフセット → ワールド座標に変換
        //    handAnchor の回転はここでは使って OK（手に対して相対的な位置調整のため）
        Vector3 worldPos = handAnchor.TransformPoint(localOffset);
        transform.position = worldPos;

        // ② 回転は「HMD と完全に同じ向き」＋オフセット
        //    → これで HMD に対して常に正面・水平・垂直になる
        Quaternion rot = cameraTransform.rotation * Quaternion.Euler(eulerOffset);
        transform.rotation = rot;
    }
}
