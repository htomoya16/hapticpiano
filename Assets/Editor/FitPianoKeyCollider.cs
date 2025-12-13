// Assets/Editor/FitPianoKeyColliders.cs
using UnityEditor;
using UnityEngine;

public static class FitPianoKeyColliders
{
    // 余白（BoxCollider用）。干渉を避けたいのでゼロにする
    static readonly Vector3 margin = Vector3.zero;
    // どちら側を「手前」とみなすか（+1 or -1）。見た目と逆なら -1 に反転して試す。
    const int frontDirectionSign = 1;
    // 白鍵の前側フル幅として残す割合（奥行き方向）
    const float whiteFrontRatio = 0.45f;
    // 白鍵後方の欠けを横幅からどれだけ削るか（割合ベース）
    const float cutRatioSingle = 0.35f; // 片側欠け: 幅の35%を削る
    const float cutRatioBoth   = 0.55f; // 両側欠け: 幅の55%を削る（左右27.5%ずつ）

    [MenuItem("Tools/Fit Piano Key Colliders (Prefab)")]
    public static void Fit()
    {
        // ピアノ鍵盤の対象Prefabパス
        const string prefabPath = "Assets/Prefab/Piano/PianoKeys.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        foreach (Transform t in root.transform)
        {
            var mr = t.GetComponent<MeshRenderer>();
            if (!mr) continue;

            // 接触管理スクリプトを付与（Collisionベース）
            if (t.GetComponent<PianoKey>() != null && t.GetComponent<PianoKeyFingerContactReporter>() == null)
            {
                var reporter = t.gameObject.AddComponent<PianoKeyFingerContactReporter>();
                EditorUtility.SetDirty(reporter);
            }

            // 既存のコライダーをすべて除去（前回実行で増えた分もリセット）
            foreach (var oldBc in t.GetComponents<BoxCollider>())
                Object.DestroyImmediate(oldBc, true);
            foreach (var oldMc in t.GetComponents<MeshCollider>())
                Object.DestroyImmediate(oldMc, true);

            var b = mr.localBounds;              // メッシュのローカルBounds
            // 白鍵/黒鍵判定: material名に White/Black が含まれる想定
            bool isWhite = mr.sharedMaterials != null && mr.sharedMaterials.Length > 0 &&
                           mr.sharedMaterials[0].name.ToLower().Contains("white");

            if (!isWhite)
            {
                // 黒鍵: 単純にメッシュBoundsへフィット
                var newBc = t.gameObject.AddComponent<BoxCollider>();
                newBc.size = b.size + margin;
                newBc.center = b.center;
                EditorUtility.SetDirty(newBc);
                continue;
            }

            // 白鍵: 形状に合わせて2つのBoxColliderで近似（手前フル幅 + 後方ノッチ付き）
            float totalZ = b.size.z;
            float totalX = b.size.x;
            float frontZ = Mathf.Max(0.0001f, totalZ * whiteFrontRatio);
            float backZ  = Mathf.Max(0.0001f, totalZ - frontZ);

            // 後方ノッチ種類をキー番号から判定（A0始まりで12音階に当てはめ）
            // PianoKey.001 が A0（両側欠け）になるように整数をパース
            string nm = t.name.ToLower();
            int keyNumber = 0;
            var digits = System.Text.RegularExpressions.Regex.Match(nm, @"\d+");
            if (digits.Success) int.TryParse(digits.Value, out keyNumber);
            int noteIndex = (keyNumber - 1) % 12; // 0:A, 1:A#, 2:B, 3:C, 4:C#, 5:D, 6:D#, 7:E, 8:F, 9:F#, 10:G, 11:G#

            // 実ピアノの配置に合わせた欠けパターン（例外あり）
            bool cutRight = noteIndex == 3 || noteIndex == 8;      // C, F
            bool cutLeft  = noteIndex == 2 || noteIndex == 7;      // B, E
            bool cutBoth  = noteIndex == 0 || noteIndex == 5 || noteIndex == 10; // A, D, G

            // 例外: PianoKey.001 は右欠け、PianoKey.088 は欠けなし
            if (keyNumber == 1)
            {
                cutRight = true; cutLeft = cutBoth = false;
            }
            if (keyNumber == 88)
            {
                cutRight = cutLeft = cutBoth = false;
            }

            float backWidth = totalX;
            float backCenterX = b.center.x;
            if (cutBoth)
            {
                float remove = Mathf.Clamp01(cutRatioBoth) * totalX;
                backWidth = Mathf.Max(0.0001f, totalX - remove);
            }
            else if (cutRight)
            {
                float remove = Mathf.Clamp01(cutRatioSingle) * totalX;
                backWidth = Mathf.Max(0.0001f, totalX - remove);
                // 右側を削るので、残す部分は左へ寄せる
                backCenterX -= remove * 0.5f;
            }
            else if (cutLeft)
            {
                float remove = Mathf.Clamp01(cutRatioSingle) * totalX;
                backWidth = Mathf.Max(0.0001f, totalX - remove);
                // 左側を削るので、残す部分は右へ寄せる
                backCenterX += remove * 0.5f;
            }

            // 後方コライダー（ノッチ反映）※先に追加（上下関係を逆に）
            {
                var c = t.gameObject.AddComponent<BoxCollider>();
                c.size = new Vector3(backWidth + margin.x, b.size.y + margin.y, backZ + margin.z);
                c.center = new Vector3(backCenterX,
                                       b.center.y,
                                       b.center.z + frontDirectionSign * (frontZ * 0.5f));
                EditorUtility.SetDirty(c);
            }
            // 手前コライダー（フル幅）
            {
                var c = t.gameObject.AddComponent<BoxCollider>();
                c.size = new Vector3(totalX + margin.x, b.size.y + margin.y, frontZ + margin.z);
                c.center = new Vector3(b.center.x,
                                       b.center.y,
                                       b.center.z + frontDirectionSign * (-backZ * 0.5f));
                EditorUtility.SetDirty(c);
            }
        }
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
