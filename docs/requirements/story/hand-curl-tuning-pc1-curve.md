# story-hand-curl-tuning-pc1-curve — PC1 ベース打鍵モーション統合

この story は、`curl` 値（0〜1）から PC1 ベースの `AnimationCurve` を経由して  
MCP / PIP / DIP 角度を決定し、SteamVR 手モデルに適用することで、  
**ピアノ打鍵らしい指関節シナジー** を実現するための最小セットを定義する。

---

## 1. この story の目的

- `feature/hand-kinematics.md` で定義した  
  `curl → phase → PC1 → angle` パイプラインを、実際の手モデルに適用する。
- 強打鍵時に MCP が主役となり、PIP が追従し、DIP が折れすぎないといった  
  **指関節間の協調パターン** を再現する。
- `feature/hand-curl-tuning.md` / `story-hand-curl-tuning-basic.md` で整備した  
  「curl 正規化」と整合する形で PC1 を組み込む。

---

## 2. 受け入れ条件（完了判定）

### 2.1 curl → phase 変換の実装

- 少なくとも 1 本の指（推奨: index）について、  
  以下のロジックで `curl`（0〜1）から `phase`（0〜1）が算出されている：

  ```csharp
  if (curl <= curl_contact)      phase = 0f;
  else if (curl >= curl_bottom)  phase = 1f;
  else                            phase = Mathf.InverseLerp(curl_contact, curl_bottom, curl);
  ```

- `curl_contact` と `curl_bottom` はインスペクタから調整可能であり、  
  例えば `curl_contact = 0.2〜0.3`, `curl_bottom = 0.8〜0.9` 程度を初期値とする。

- デバッグ UI などで、`curl` を 0→1 にスライダー操作したとき、  
  `phase` が 0→1 に単調増加していることが確認できる。

### 2.2 phase → PC1 → angle 変換の実装

- MCP / PIP の各関節について、PC1 ベースの角度計算が行われている：

  ```csharp
  var pc1_mcp = mcpCurve.Evaluate(phase);
  var pc1_pip = pipCurve.Evaluate(phase);

  mcpAngle = baseMCP + pc1_mcp * mcpScaleDeg;
  pipAngle = basePIP + pc1_pip * pipScaleDeg;
  ```

- `baseMCP`, `basePIP`, `mcpScaleDeg`, `pipScaleDeg` などのパラメータは  
  インスペクタで調整可能であり、実行中に変更して見た目の変化を確認できる。

- DIP 角は PIP 角の一定比率で近似されている：

  ```csharp
  dipAngle = baseDIP + dipFromPipRatio * (pipAngle - basePIP);
  ```

- `dipFromPipRatio` を変化させると DIP の曲がりやすさが変わることを確認できる。

### 2.3 SteamVR 手モデルへの適用

- 上記で計算された MCP / PIP / DIP 角度が、  
  SteamVR 手モデルの該当ボーン（例: `finger_index_0_r`, `finger_index_1_r`, `finger_index_2_r`）に  
  適切な軸で適用されている。

- `curl=0` 付近では `base*` に近い自然なホームポジション、  
  `curl=1` 付近ではピアノ演奏の強打鍵に近い指曲げが得られる。

- 既存の `story-hand-curl-tuning-basic.md` の受け入れ条件（  
  「curl と見た目の対応」「左右の整合性」「ガクつきの少なさ」など）を損なっていない。

### 2.4 視覚的な打鍵シナジーの確認

- `curl` を 0→1 にゆっくり変化させたとき、少なくとも以下が目視で確認できる：

  - MCP が先に大きく曲がり始め、PIP がやや遅れて追従するパターンになっている。  
  - DIP は PIP に比べて折れすぎず、やや伸び気味に保たれている。
  - `phase ≒ 0.5`（key press onset 付近）で、指が鍵盤に触れ始める自然な姿勢になっている  
    （まだ深く握り込んでいない）。

- `mcpScaleDeg`, `pipScaleDeg`, `dipFromPipRatio`, `curl_contact`, `curl_bottom` を調整することで、  
  `feature/hand-curl-tuning.md` に記載された

  - 「DIP が折れすぎず、やや伸び気味」
  - 「強打鍵時には MCP が主役、PIP が追従」

  といった要件に近づけられる。

### 2.5 デバッグ・比較手段

- 開発者が「PC1 ベース」と「単純線形」の違いを確認できるよう、  
  いずれかの方法でモード切り替えが可能になっている（必須ではないが望ましい）：

  - 例: `usePC1Curve` フラグで、  
    - OFF: `angle = base + curl * maxAngle`（従来の単純モデル）  
    - ON : `angle = base + pc1(phase) * scaleDeg`（PC1 モデル）  
    を切り替えられる。

- 少なくとも、`curl`, `phase`, MCP/PIP/DIP 角度、PC1 値（MCP/PIP）が  
  インスペクタまたはデバッグ UI で確認できる。

---

## 3. 非機能要件

- PC1 カーブの評価および角度適用が、VR フレームレート（90Hz）で実行しても  
  パフォーマンス上問題にならない（`AnimationCurve.Evaluate` の呼び出し回数が過剰でない）こと。
- NaN / 範囲外の `curl` や `phase` に対しても、安全なフォールバック挙動を持つ  
  （例: 0〜1 にクランプ、角度を基準値に戻す等）。

---

## 4. 関連 feature / story

- feature: `feature/hand-kinematics.md`
- feature: `feature/hand-curl-tuning.md`
- story: `story-hand-curl-tuning-basic.md`
- story: `story-hand-kinematics-pc1-data.md`
