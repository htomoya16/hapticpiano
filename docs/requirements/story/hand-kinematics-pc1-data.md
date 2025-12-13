# story-hand-kinematics-pc1-data — PC1 データ整備と AnimationCurve 化

この story は、Furuya et al. (2011) の PC1 データを  
`phase, pc1` 形式に整え、Unity から `AnimationCurve` として扱える状態にするための  
**データ整備およびエディタ側準備の最小セット** を定義する。

---

## 1. この story の目的

- `docs/data/PC1-hand-kinematics/` 内の生 CSV（t, pc1）を  
  HAPTICPIANO で利用しやすい `phase, pc1` 形式に正規化する。
- MCP / PIP × 各指ごとの PC1 カーブを、Unity の `AnimationCurve` として参照できるようにする。
- ランタイム実装（curl→phase→angle）は別 story（PC1 カーブ適用側）に委ね、  
  本 story では「**データとカーブが正しく準備されていること**」までをゴールとする。

---

## 2. 受け入れ条件（完了判定）

### 2.1 phase,pc1 形式 CSV の整備

- `docs/data/PC1-hand-kinematics/` 配下に、少なくとも以下のような CSV が存在する：

  - `PC1_MCP_index_phase.csv`
  - `PC1_MCP_middle_phase.csv`
  - `PC1_MCP_ring_phase.csv`
  - `PC1_MCP_pinky_phase.csv`
  - `PC1_PIP_index_phase.csv`
  - `PC1_PIP_middle_phase.csv`
  - `PC1_PIP_ring_phase.csv`
  - `PC1_PIP_pinky_phase.csv`

- 各 CSV の 1 行目は `phase,pc1` 形式のヘッダ、2 行目以降は

  ```text
  phase,pc1
  0.00,-0.12
  0.05,-0.35
  ...
  1.00,0.08
  ```

  のように `phase`（0.0〜1.0）、`pc1`（正負の実数）がカンマ区切りで並んでいる。

- `phase` の意味がドキュメント上で明示されている：

  - `phase = 0.0` : 打鍵前（key press onset より十分前）
  - `phase = 0.5` : 論文中の `t = 0`（key press onset）付近
  - `phase = 1.0` : 打鍵底つき付近

  （変換スクリプト内のコメント、もしくは本 story から参照される形で説明がある）

### 2.2 Unity 内での AnimationCurve 生成

- 上記 CSV から `AnimationCurve` を生成する仕組みが用意されていること：

  - エディタスクリプト（例: `PC1CurveImporter`）またはランタイム初期化時に 1 度だけ実行する処理など。
  - MCP / PIP × 指（index/middle/ring/pinky）ごとに `AnimationCurve` フィールドが存在する。
  - 生成されたカーブは `ScriptableObject` やコンポーネントのシリアライズフィールドとして保存され、  
    シーンを再生し直しても再インポートなしで利用できる。

- Unity エディタのインスペクタ上で、各 `AnimationCurve` の波形を確認できる。

### 2.3 デバッグ確認手段

- 開発者が `phase` と `pc1` の対応を確認できる何らかの手段がある：

  - シンプルなデバッグコンポーネントで、`phase` スライダー（0〜1）を動かすと  
    対応する MCP/PIP カーブの `Evaluate(phase)` 結果が数値表示される。
  - もしくは EditorWindow / OnValidate などで  
    `phase = 0.0 / 0.5 / 1.0` の `pc1` 値をログ出力する仕組みがある。

- 目視で、カーブが単純な直線ではなく、論文 Fig.3 の PC1 波形に近い特徴的な形状になっていることを確認済みである。

---

## 3. 非機能要件

- CSV からのインポート処理は、プロジェクトの通常動作（プレイ中のフレームレート）に影響しない構成  
  （例: エディタのみ、もしくは初回起動時のみ実行）。
- CSV パース失敗やファイル欠如時に、例外でエディタが落ちるのではなく  
  ログや警告で開発者に気付けるようになっている。

---

## 4. 関連 feature / story

- feature: `feature/hand-kinematics.md`
- 関連 story（PC1 カーブ適用側）: `story/hand-curl-tuning-pc1-curve.md`
