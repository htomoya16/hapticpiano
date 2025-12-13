# story-hand-collider — 見た目手コライダ自動生成

見た目の手ボーンに対し、`FingerColliderBuilder.cs` で実行時にコライダを自動生成・配置し、物理手プレハブに依存せず鍵盤との当たり判定準備を整える。

---

## 受け入れ条件（完了判定）

1. **コライダ自動生成**
   - `FingerColliderBuilder` で各指（親指〜小指）に 1〜3 本のカプセル／コライダを TIP から根元方向へ生成・配置できる。
   - 生成時にレイヤー・PhysicMaterial が統一設定され、ピアノ鍵盤レイヤーと衝突するよう自動でセットされる。

2. **追従精度**
   - 生成されたコライダが HandVisualFromCurl のボーン回転に正しく追従し、見た目と衝突位置のズレが目立たない。

3. **Rigidbody設定**
   - 手のルート（または適切な親）に kinematic Rigidbody が付与され、生成されたコライダが安定して移動する（FingerColliderBuilder の ensureKinematicRigidbody で自動付与可）。

4. **パフォーマンス**
   - 物理手プレハブ（HandPhysics/HandCollider）は使用せず、自動生成コライダで 60fps 目標を維持できる。

5. **デバッグ確認**
   - シーン内で生成されたコライダの有効/無効やレイヤー確認ができ、少なくとも一度は鍵盤オブジェクトと当たり判定が取れることを確認する（Gizmos 表示や logBuild を活用）。

6. **指の識別**
   - 生成された各コライダに「どの手・どの指か」を識別できる情報が付与される（例: `FingerColliderId`）。
   - 鍵盤側の当たり判定（接触/底面）で、コライダから指IDを取得できる。

---

## 関連 feature / story
- feature/hand-collider.md
- story/hand-model-basic.md
- story/hand-model-improvements.md
