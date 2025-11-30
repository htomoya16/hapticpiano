# feature-hand-collider — 手コライダ自動生成

## 1. 対象シーン / スクリプト
- シーン  
  - `Assets/Scenes/hapticpiano/hapticpiano.unity`
- スクリプト  
  - `FingerColliderBuilder.cs`（実装済）  
    - 手ボーン構造を読み取り、実行時に指コライダを生成・配置  
    - レイヤー / PhysicMaterial / コライダ本数・半径・長さを一括設定  
    - buildDelayFrames・updateEveryFrame・gizmo 表示オプション付き  
  - `HandVisualFromCurl.cs`（回転追従）と併用

## 2. 目的
- 手の見た目ボーンに対するコライダを **実行時に自動生成** し、物理手プレハブに依存せず鍵盤との当たり判定を構築する。
- プレハブ編集の手間を減らし、左右手のコライダ設定をプリセットで共有できるようにする。

## 3. 機能要件（現時点）
1. 指ごとに 1〜3 本のカプセルコライダを TIP→根元方向へ自動生成できる。  
2. 生成時にレイヤーと PhysicMaterial を一括設定し、ピアノ鍵盤レイヤーと衝突する。  
3. コライダは `HandVisualFromCurl` のボーン回転に正しく追従し、見た目とのズレが目立たない。  
4. 手ルート（または適切な親）に kinematic Rigidbody を自動付与・設定する。  
5. 60fps 目標を維持できる負荷で動作する（不要なコライダを生成しない）。  
6. デバッグ時に生成結果（有効/無効、レイヤー）を確認できる手段を持つ。

## 4. 設定・拡張の想定
- ScriptableObject で左右共有のコライダ設定プリセットを保持（本数・半径・長さ・レイヤー・PhysicMaterial）。
- 押し込み量計算への拡張（TIP コライダの侵入量で 0〜1 を算出）。
- FFB 連動への拡張（押し込み量を Force Feedback 入力へマッピング）。

## 5. 関連 story
- story/hand-collider.md

