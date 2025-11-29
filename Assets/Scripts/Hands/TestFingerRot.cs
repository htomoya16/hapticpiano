using UnityEngine;

public class TestFingerRot : MonoBehaviour
{
    public Transform testBone;

    void Update()
    {
        // サイン波で -60〜+60 度くらいブン回す
        float angle = Mathf.Sin(Time.time * 2f) * 60f;
        testBone.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }
}
