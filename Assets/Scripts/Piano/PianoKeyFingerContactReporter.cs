using UnityEngine;

[RequireComponent(typeof(PianoKey))]
[DisallowMultipleComponent]
public sealed class PianoKeyFingerContactReporter : MonoBehaviour
{
    private PianoKey _pianoKey;
    private PianoFingerContactRegistry _registry;
    private bool _warnedNoRegistry;

    private void Awake()
    {
        _pianoKey = GetComponent<PianoKey>();
        _registry = FindObjectOfType<PianoFingerContactRegistry>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!TryGetFingerId(collision, out var id)) return;
        if (!TryGetRegistry(out var reg)) return;
        reg.RegisterCollisionEnter(_pianoKey, id);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!TryGetFingerId(collision, out var id)) return;
        if (!TryGetRegistry(out var reg)) return;
        reg.RegisterCollisionExit(_pianoKey, id);
    }

    private bool TryGetFingerId(Collision collision, out FingerColliderId id)
    {
        id = null;
        if (collision == null) return false;

        // collision.collider は Unity バージョン/状況で指コライダ側を指す前提だが、
        // 念のため ContactPoint も走査して確実に指IDを拾う。
        if (TryGetIdFromCollider(collision.collider, out id)) return true;

        var contacts = collision.contacts;
        for (int i = 0; i < contacts.Length; i++)
        {
            if (TryGetIdFromCollider(contacts[i].thisCollider, out id)) return true;
            if (TryGetIdFromCollider(contacts[i].otherCollider, out id)) return true;
        }

        return false;
    }

    private static bool TryGetIdFromCollider(Collider col, out FingerColliderId id)
    {
        id = null;
        if (col == null) return false;
        id = col.GetComponent<FingerColliderId>();
        if (id != null) return true;
        id = col.GetComponentInParent<FingerColliderId>();
        return id != null;
    }

    private bool TryGetRegistry(out PianoFingerContactRegistry registry)
    {
        if (_registry != null)
        {
            registry = _registry;
            return true;
        }

        _registry = FindObjectOfType<PianoFingerContactRegistry>();
        if (_registry != null)
        {
            registry = _registry;
            return true;
        }

        if (Application.isPlaying)
        {
            var go = new GameObject("PianoFingerContactRegistry (Auto)");
            _registry = go.AddComponent<PianoFingerContactRegistry>();
            registry = _registry;
            return true;
        }

        if (!_warnedNoRegistry)
        {
            Debug.LogWarning("[PianoKeyFingerContactReporter] PianoFingerContactRegistry がシーンに存在しません。", this);
            _warnedNoRegistry = true;
        }

        registry = null;
        return false;
    }
}
