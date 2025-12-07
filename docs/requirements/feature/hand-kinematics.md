# Hand Kinematics — PC1 ベース指モーションモデル

（参考文献: Furuya et al., *Hand kinematics of piano playing*, J Neurophysiol, 2011）

## 1. 目的と位置づけ

この文書は、Furuya ら (2011) の Fig.3 に示される **PC1 波形**をもとにした  
「PC1 ベース指モーションモデル」を、HAPTICPIANO でどのように利用するかを定義する。

- 元データ: `docs/data/PC1-hand-kinematics/` 以下の CSV
- 利用目的:
  - ピアノ打鍵の **典型的な運動パターン（synergy）** を Unity 側に取り込む
  - `curl` 値（0〜1）と組み合わせ、自然な打鍵モーションを生成するための **基底曲線** として使う
- 関連要件:
  - feature: `feature/hand-curl-tuning.md`
  - story: `story/hand-kinematics-pc1-data.md`（PC1 データ整備 / AnimationCurve 化）
  - story: `story/hand-curl-tuning-pc1-curve.md`（PC1 カーブ適用 / ランタイム統合）

本ドキュメントは、`docs/data/` の研究用データと feature/story 要件の **橋渡し** を行う。

実務上は、PC1 モデルの導入を次の 2 段階の story に分割して進める:

- データ整備側: PC1 生データを `phase, pc1` 形式に正規化し、`AnimationCurve` として利用可能にする（`story/hand-kinematics-pc1-data.md`）。
- ランタイム統合側: `curl → phase → PC1 → angle` のパイプラインを実装し、手モデルに適用する（`story/hand-curl-tuning-pc1-curve.md`）。

---

## 2. PC1 データセットの概要

WebPlotDigitizer を用いて、Fig.3 の PC1 波形から以下の形式で CSV を抽出した:

- column 1: time `t` （おおよそ -0.2 s 〜 +0.2 s）
- column 2: PC1 angular velocity（おおよそ -2500 〜 +2500 の範囲の相対値）

ここでの PC1 は、次を表す:

- 打鍵動作における **関節角速度の主要パターン（basic motion mode）**
- データ全体の約 70 % を説明する「典型的な動き方」
- **角度そのものではなく、速度の相対的シェイプ**（パターン）である

重要な点:

- PC1 は **“物理的な絶対角速度値” ではない**
- そのため、PC1 をそのまま積分しても **実際の関節角度には一致しない**
- 我々の目的は **“打鍵らしい動き方（タイミングと形）を再現すること”** であり、
  曲がり量という物理値はデバイスや手モデル側で決める

→ **PC1 = 運動パターンのテンプレート（相対量）**  
→ **角度の大きさ = Unity 側スケールで決める設計変数**

---

## 3. 時間軸の正規化（t → phase）

元 CSV の時間 `t`（−0.2〜+0.2 s）を、次の式で 0〜1 に正規化する:

\[
\mathrm{phase}_i = \frac{t_i - t_{\min}}{t_{\max} - t_{\min}}
\]

例:

- `t = -0.2` → `phase = 0.0`
- `t =  0.0` → `phase = 0.5`
- `t =  0.2` → `phase = 1.0`

論文内の `t = 0` は「**key press onset（キーが沈み始めた瞬間）**」を表す。  
本プロジェクトでは、これを **finger–key contact の近似点**として扱い、`phase = 0.5` を  
「打鍵の中心（接触基準）」とみなす。

---

## 4. AnimationCurve への変換

正規化後の CSV は次の形式とする:

```text
phase, pc1
0.00, -0.12
0.05, -0.35
...
1.00,  0.08
```

Unity 側での扱い:

- 各行について `Keyframe(phase, pc1)` を生成し、`AnimationCurve` に追加する
- MCP 用と PIP 用（指ごと）にカーブを用意する想定:
  - 例: `mcpCurve_index`, `pipCurve_index`, `mcpCurve_middle`, … など

`AnimationCurve` の役割:

- `curve.Evaluate(phase)` により、「打鍵の進行度（phase）」に応じた **PC1 値**を取得
- 取得した PC1 値にスケールを掛けて、各関節の回転角度を決定する

---

## 5. curl → phase → PC1 → angle 変換パイプライン

ランタイムでは、LucidGloves から得られた **curl 値（0〜1）** を入力とし、  
以下の変換を行って関節角度を決定する。

### 5.1 接触点 / 底つき点の定義

打鍵時の「どこから鍵盤に触れたとみなすか」「どこで底つきとみなすか」を、  
curl 空間上で次の 2 点として定義する:

```text
curl_contact : 接触と見なす curl（例: 0.2〜0.3）
curl_bottom  : 底つきと見なす curl（例: 0.8〜0.9）
```

### 5.2 curl → phase 変換

上記 2 点をもとに、curl 値を 0〜1 の `phase` に線形マッピングする:

```csharp
if (curl <= curl_contact)      phase = 0f;
else if (curl >= curl_bottom)  phase = 1f;
else                            phase = Mathf.InverseLerp(curl_contact, curl_bottom, curl);
```

- `phase = 0` : 打鍵前（まだ鍵盤に触れていない）
- `phase = 1` : 底つき付近（最大打鍵）
- `phase ≒ 0.5` : キーが沈み始める付近（finger–key contact の近似）

### 5.3 phase → PC1 値取得

指・関節ごとに用意した `AnimationCurve` から PC1 値を取得する:

```csharp
pc1_mcp = mcpCurve.Evaluate(phase);
pc1_pip = pipCurve.Evaluate(phase);
```

- MCP と PIP で別カーブを持つことで、「MCP が先行し PIP が追従する」といった  
  **指関節間シナジー** を表現可能にする。

### 5.4 PC1 → 関節角度への変換（スケール指定）

PC1 はあくまで相対パターンなので、角度の大きさは Unity 側で決める:

```csharp
mcpAngle = baseMCP + pc1_mcp * mcpScaleDeg;
pipAngle = basePIP + pc1_pip * pipScaleDeg;
```

- `baseMCP`, `basePIP` : `curl = 0`（ホームポジション）時の基準角度  
- `mcpScaleDeg`, `pipScaleDeg` : PC1 の振幅を何度に相当させるかのスケール（調整パラメータ）

DIP については、PIP 角度の一定比率で近似する:

```csharp
dipAngle = baseDIP + dipFromPipRatio * (pipAngle - basePIP);  // 例: dipFromPipRatio = 0.5
```

---

## 6. なぜ角度を自分で決める必要があるのか

理由は次の通り:

- PC1 の縦軸は PCA 後の再構成値であり、**絶対的な関節角速度を保証するものではない**
- これを積分しても、計測系・被験者・条件の違いにより、実測角度とは一致しない
- HAPTICPIANO の目的は、
  - 「鍵盤とのタイミング」や「速度変化の形状」を **リアルな演奏に近づけること**
  - 「どれだけ曲げるか（度数）」は、グローブ・アバター・ユーザの快適性に合わせて調整すること

したがって:

- **PC1 は「タイミングと形のテンプレート」**
- **角度値は「テンプレートにスケールを掛けて決める自由度」**

という役割分担にするのが、正確かつ柔軟な設計である。

---

## 7. 実装ステップ（まとめ）

PC1 データを HAPTICPIANO に組み込む際の推奨ステップを、story 単位で整理すると次の通りになる。

### 7.1 データ整備 / AnimationCurve 化（story/hand-kinematics-pc1-data.md）

1. `docs/data/PC1-hand-kinematics/` の CSV を整理する（MCP/PIP × 指ごと）。
2. 元データの時間 `t` から `phase` を計算し、`phase, pc1` 形式の CSV に変換する。
3. Unity で `phase, pc1` CSV を読み込み、指・関節ごとの `AnimationCurve` を生成して保存する。

### 7.2 ランタイム統合 / 打鍵シナジー適用（story/hand-curl-tuning-pc1-curve.md）

1. `curl_contact` / `curl_bottom` を設定し（将来的にはユーザキャリブレーション UI で調整可能にする）、`curl` から `phase` を算出する。
2. ランタイムで `curl → phase → PC1 → angle` 変換を行い、計算した角度を指ボーンへ適用する。
3. `mcpScaleDeg`, `pipScaleDeg`, `dipFromPipRatio`, `base*` などを調整し、視覚的に自然な打鍵モーションとなるようチューニングする。

この設計により、外部論文ベースの運動学データを利用しつつ、  
デバイスやアバターに応じて柔軟にモーションを調整できる。
