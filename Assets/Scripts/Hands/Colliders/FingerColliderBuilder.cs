using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 手ボーン構造を参照し、実行時に指コライダを自動生成する。
/// ・各指 1〜3 本（デフォルト: 骨間ごとに1本）を TIP→根元方向に配置
/// ・レイヤー / PhysicMaterial を一括設定
/// ・手ルートに kinematic Rigidbody を自動付与（任意）
/// </summary>
[DefaultExecutionOrder(200)] // HandVisualFromCurl (デフォルト 0) などの後に実行したい
public class FingerColliderBuilder : MonoBehaviour
{
    [System.Serializable]
    public class FingerConfig
    {
        [Tooltip("指のボーンを TIP から根元方向へ並べる（最低2つ）。")]
        public Transform[] jointsTipToRoot;

        [Tooltip("生成する最大コライダ本数（1〜3推奨）。0なら自動で全区間。")]
        [Range(0, 3)] public int maxColliders = 0;

        [Tooltip("半径を個別指定したい場合。0 以下ならデフォルト半径を使用。")]
        public float overrideRadius = 0f;
    }

    [Header("Finger Config (Thumb → Pinky)")]
    public FingerConfig thumb;
    public FingerConfig index;
    public FingerConfig middle;
    public FingerConfig ring;
    public FingerConfig pinky;

    [Header("Finger Id")]
    [Tooltip("生成した指コライダに付与する手の左右。")]
    [FormerlySerializedAs("handSide")]
    public Handedness handedness = Handedness.Right;

    [Header("Collider Settings")]
    [Tooltip("CapsuleCollider の半径既定値（overrideRadius が 0 以下の場合に使用）。")]
    public float defaultRadius = 0.008f;
    [Tooltip("CapsuleCollider の方向。通常は Z 軸。")]
    public int capsuleDirection = 2; // 0=x,1=y,2=z
    [Tooltip("生成時に設定するレイヤー。-1 なら変更しない。")]
    public int layerToAssign = -1;
    [Tooltip("生成時に設定する PhysicMaterial。未指定ならそのまま。")]
    public PhysicMaterial physicMaterial;

    [Header("Rigidbody")]
    [Tooltip("手ルートに kinematic Rigidbody を自動付与する")]
    public bool ensureKinematicRigidbody = true;

    [Header("Debug")]
    public bool logBuild = false;
    [Tooltip("Sceneビューでギズモ表示（選択時）")]
    public bool drawGizmos = true;
    public Color gizmoColor = new Color(0f, 0.8f, 1f, 0.35f);
    public Color gizmoLineColor = new Color(0f, 0.8f, 1f, 0.8f);

    [Header("Timing")]
    [Tooltip("Awake 後に何フレーム待ってから初回生成するか（骨がまだ初期姿勢の場合のズレ対策）")]
    public int buildDelayFrames = 0;
    [Tooltip("毎フレーム追従更新する")]
    public bool updateEveryFrame = true;

    private class Segment
    {
        public Transform a;
        public Transform b;
        public CapsuleCollider col;
        public float radius;
    }

    private readonly List<GameObject> _generated = new List<GameObject>();
    private readonly List<Segment> _segments = new List<Segment>();

    private void Awake()
    {
        if (ensureKinematicRigidbody)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void Start()
    {
        if (buildDelayFrames > 0)
            StartCoroutine(BuildDelayed());
        else
            BuildAll();
    }

    private void OnDestroy()
    {
        ClearGenerated();
    }

    [ContextMenu("Rebuild Colliders")]
    public void Rebuild()
    {
        ClearGenerated();
        _segments.Clear();
        BuildAll();
    }

    private void ClearGenerated()
    {
        for (int i = 0; i < _generated.Count; i++)
        {
            if (_generated[i] != null)
            {
                if (Application.isPlaying)
                    Destroy(_generated[i]);
                else
                    DestroyImmediate(_generated[i]);
            }
        }
        _generated.Clear();
        _segments.Clear();
    }

    private void BuildAll()
    {
        BuildFinger(thumb, FingerId.Thumb, "Thumb");
        BuildFinger(index, FingerId.Index, "Index");
        BuildFinger(middle, FingerId.Middle, "Middle");
        BuildFinger(ring, FingerId.Ring, "Ring");
        BuildFinger(pinky, FingerId.Pinky, "Pinky");
    }

    private void BuildFinger(FingerConfig cfg, FingerId fingerId, string namePrefix)
    {
        if (cfg == null || cfg.jointsTipToRoot == null || cfg.jointsTipToRoot.Length < 2)
            return;

        int max = cfg.maxColliders > 0 ? cfg.maxColliders : cfg.jointsTipToRoot.Length - 1;
        max = Mathf.Min(max, cfg.jointsTipToRoot.Length - 1);

        for (int i = 0; i < max; i++)
        {
            Transform a = cfg.jointsTipToRoot[i];
            Transform b = cfg.jointsTipToRoot[i + 1];
            if (a == null || b == null) continue;

            CreateCapsuleBetween(a, b, cfg.overrideRadius > 0 ? cfg.overrideRadius : defaultRadius,
                $"{namePrefix}_col_{i}", fingerId, i);
        }
    }

    private void CreateCapsuleBetween(Transform tip, Transform root, float radius, string objName, FingerId fingerId, int segmentIndex)
    {
        Vector3 worldA = tip.position;
        Vector3 worldB = root.position;
        Vector3 mid = (worldA + worldB) * 0.5f;
        Vector3 dir = (worldB - worldA);
        float dist = dir.magnitude;
        if (dist < 1e-4f) return;

        GameObject go = new GameObject(objName);
        go.transform.SetParent(transform, worldPositionStays: false);

        var col = go.AddComponent<CapsuleCollider>();
        col.direction = capsuleDirection;
        if (physicMaterial != null) col.sharedMaterial = physicMaterial;

        var id = go.AddComponent<FingerColliderId>();
        id.Set(handedness, fingerId, segmentIndex);

        if (layerToAssign >= 0 && layerToAssign < 32)
        {
            go.layer = layerToAssign;
        }

        // 初期配置
        go.transform.position = mid;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.forward, dir.normalized);
        ApplySizeWithScale(col, dist, radius);

        _generated.Add(go);
        _segments.Add(new Segment { a = tip, b = root, col = col, radius = radius });

        if (logBuild)
        {
            Debug.Log($"[FingerColliderBuilder] Generated {objName} length={dist:F3} radius={radius:F3}");
        }
    }

    private void LateUpdate()
    {
        if (!updateEveryFrame) return;
        // ボーン追従
        for (int i = 0; i < _segments.Count; i++)
        {
            var s = _segments[i];
            if (s.a == null || s.b == null || s.col == null) continue;

            Vector3 worldA = s.a.position;
            Vector3 worldB = s.b.position;
            Vector3 mid = (worldA + worldB) * 0.5f;
            Vector3 dir = (worldB - worldA);
            float dist = dir.magnitude;
            if (dist < 1e-4f) continue;

            Transform t = s.col.transform;
            t.position = mid;
            t.rotation = Quaternion.FromToRotation(Vector3.forward, dir.normalized);
            ApplySizeWithScale(s.col, dist, s.radius);
        }
    }

    private void ApplySizeWithScale(CapsuleCollider col, float worldDist, float worldRadius)
    {
        // カプセルは親のスケールの影響を受けるため、ワールドサイズを保つように補正
        Vector3 lossy = col.transform.lossyScale;
        float axisScale = 1f;
        switch (capsuleDirection)
        {
            case 0: axisScale = Mathf.Abs(lossy.x); break;
            case 1: axisScale = Mathf.Abs(lossy.y); break;
            case 2: axisScale = Mathf.Abs(lossy.z); break;
        }

        // 半径は残り2軸の平均スケールで割る
        float otherScale = capsuleDirection == 0
            ? (Mathf.Abs(lossy.y) + Mathf.Abs(lossy.z)) * 0.5f
            : capsuleDirection == 1
                ? (Mathf.Abs(lossy.x) + Mathf.Abs(lossy.z)) * 0.5f
                : (Mathf.Abs(lossy.x) + Mathf.Abs(lossy.y)) * 0.5f;

        float r = otherScale > 1e-4f ? worldRadius / otherScale : worldRadius;
        col.radius = r;
        // height は「両端の半球込み」の全長なので、+2r すると骨間より長くはみ出す。
        // 骨（jointsTipToRoot の各 Transform）の位置がカプセルの端になるよう、全長を骨間距離に合わせる。
        float h = (axisScale > 1e-4f ? worldDist / axisScale : worldDist);
        col.height = Mathf.Max(h, r * 2f);
        col.center = Vector3.zero;
    }

    private System.Collections.IEnumerator BuildDelayed()
    {
        for (int i = 0; i < buildDelayFrames; i++)
            yield return null;
        BuildAll();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.matrix = Matrix4x4.identity;
        DrawPlannedGizmos(thumb);
        DrawPlannedGizmos(index);
        DrawPlannedGizmos(middle);
        DrawPlannedGizmos(ring);
        DrawPlannedGizmos(pinky);
    }

    private void DrawPlannedGizmos(FingerConfig cfg)
    {
        if (cfg == null || cfg.jointsTipToRoot == null || cfg.jointsTipToRoot.Length < 2) return;
        int max = cfg.maxColliders > 0 ? cfg.maxColliders : cfg.jointsTipToRoot.Length - 1;
        max = Mathf.Min(max, cfg.jointsTipToRoot.Length - 1);

        for (int i = 0; i < max; i++)
        {
            Transform a = cfg.jointsTipToRoot[i];
            Transform b = cfg.jointsTipToRoot[i + 1];
            if (a == null || b == null) continue;

            Vector3 worldA = a.position;
            Vector3 worldB = b.position;
            Vector3 mid = (worldA + worldB) * 0.5f;
            float dist = Vector3.Distance(worldA, worldB);
            float r = cfg.overrideRadius > 0 ? cfg.overrideRadius : defaultRadius;

            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(mid, r);
            Gizmos.DrawCube(mid, new Vector3(r * 2f, r * 2f, dist));

            Gizmos.color = gizmoLineColor;
            Gizmos.DrawLine(worldA, worldB);
        }
    }
}
