# feature: ピアノ鍵盤コライダー拡張

## 背景 / 目的
- 既存の鍵盤 `BoxCollider` が実寸より小さく、手指コライダーが触れても押下判定されないケースがある。
- XR ハンドとの当たり判定を鍵盤実寸に合わせ、押下しやすさとデバッグの再現性を上げる。

## ゴール
- 各鍵盤の `BoxCollider` をメッシュ寸法に近づけ、視覚サイズと物理判定サイズの差を解消する。
- 変更後も既存の `HingeJoint`・`Rigidbody` 設定で安定動作する。

## 白鍵の欠けパターン（形状グループ）
- 右奥が欠ける: C, F 系  
- 左奥が欠ける: B, E 系  
- 両側が欠ける: A, D, G 系  
- 例外: `PianoKey.001` は右奥欠け、`PianoKey.088` は欠けなし（フル幅）
- キー命名の基準: `PianoKey.001` が最も左端の白鍵で A0 に対応。

## スコープ
- `Assets/Scenes/hapticpiano/hapticpiano.unity` 内の `PianoKey.*` GameObject に付与された `BoxCollider` のサイズ・センター調整。
- 鍵盤以外（ペダル・手モデルなど）のコライダーは対象外。

## 非スコープ
- 新しい入力デバイス対応やサウンド挙動の変更。
- コード側ロジック (`PianoKey.cs` など) の大規模改修。

## 受け入れ条件への参照
- story: `docs/requirements/story/piano-key-collider-sizing.md`
