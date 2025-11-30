# feature-hand-model — 手モデル機能要件

## 1. 対象シーン / スクリプト

- シーン  
  - `Assets/Scenes/hapticpiano/hapticpiano.unity`

- スクリプト  
  - `HandCurlTracker.cs`  
    - デバイスから curl 値を取得し 0〜1 に正規化  
    - 将来のフィルタ処理・キャリブレーションを担当  
  - `HandVisualFromCurl.cs`  
    - curl 値を手のボーン回転に反映  
    - 曲がり具合の係数・オフセット・非線形補正を担当  
  - （物理ボタンでの再キャリブ用）`CalibPhysicalButton.cs`

---

## 2. 目的

- XR 空間で現実の手の開閉を自然に再現する  
- 将来ピアノ鍵盤の当たり判定・押し込み量・FFB に連動できる構造を作る  
- SteamVR HandPhysics/HandCollider ではなく **見た目手にコライダを直付け** して扱う方針

---

## 3. 手モデルの機能要件

### (1) curl 値 → ボーン回転のマッピング
- 各指の 0〜1 の curl が自然な角度に変換される  
- 回転方向の誤り（逆方向）を防止するため、  
  **各指の曲げ軸（Vector3）を Inspector で指定可能にする**

### (2) ノイズ対策
- 低周波ノイズ・瞬間的ジャンプに対して平滑化が可能な構造  
- smoothingFactor を Inspector で指定可能にする

### (3) 左右手の整合性
- 右手と左手で極端に挙動が異ならないよう、  
  **共通パラメータを ScriptableObject またはプリセットとして共有可能**にする

### (4) Transform 構造の一貫性
- 指の階層（MCP → PIP → DIP → TIP）が Unity 上で統一されている  
- **見た目手のボーンに直接 Collider を付ける** 前提で、各指1〜3本のカプセルを TIP から根元方向へ配置できる構造にする  
- HandPhysics / HandCollider（別プレハブ）に依存しない

### (5) デバッグ容易性
- curl 値・回転角を確認できる UI（ログ / Gizmo / Text）のいずれかを  
  シーンに **常設** すること

### (6) キャリブレーション（将来）
- コントローラーボタン依存ではなく、ワールド空間 UI / 物理ボタンから  
  curl の基準値を更新できる仕組みを追加可能にする

---

## 4. 将来拡張

- 指先 Collider 追加（直付け済み前提で押し込み量計算へ拡張）  
- ピアノ鍵盤との押し込み量計算  
- 押し込み量 → FFB 連動  
- 非線形カーブ調整（ガンマ・重み付け）  
- モーションキャプチャ互換（Leap Motion など）検討

---

## 5. 関連 story
- story/hand-model-basic.md  
