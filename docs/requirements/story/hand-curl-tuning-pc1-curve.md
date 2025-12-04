# story-hand-curl-tuning-pc1-curve — PC1 ベースの MCP / PIP カーブ適用

この story は、Tan et al. (2011, Journal of Neurophysiology) Fig.2 の PC1 をもとに、  
MCP / PIP 関節の屈曲パターンを `AnimationCurve` で再現し、  
`curl01` に応じて MCP / PIP を PC1 波形に沿って動かすための最小セットを定義する。

---

## 前提

- LucidGloves のセンサ値は ESP 側で 0〜4095 にキャリブレーション済みであり、  
  Unity 側では `HandCurlTracker` が `curl01`（0〜1）を生成済みである。
- 対象指は index / middle / ring / pinky（親指は当面シンプルな Mapping のままでもよい）。
- 論文 Fig.2 から MCP / PIP の PC1 波形を読み取って作成した CSV/JSON データが手元にある
  （例: `PC1_MCP.json`, `PC1_PIP.json`, `info.json`, `wpd.json` 等）。

---

## 受け入れ条件（完了判定）

### 1. PC1 データの AnimationCurve 化

- Unity プロジェクト内に、以下のいずれかの形で PC1 カーブが表現されていること:
  - `ScriptableObject` あるいは `AnimationCurve` フィールドとして、
    - `pc1McpCurve`（MCP 用）
    - `pc1PipCurve`（PIP 用）
    が定義されている。
- カーブの仕様:
  - 横軸: `u = 0〜1`（`u = curl01` をそのまま使う）
  - 縦軸: `k = 0〜1` に正規化された PC1 値
    - Fig.2 の PC1 振幅を、最小値→0、最大値→1 となるようスケーリング済み。

### 2. MCP / PIP の角度計算が PC1 ベースになっている

- `HandVisualFromCurl` もしくはそれに相当するビジュアル制御コードにおいて、
  index / middle / ring / pinky の MCP / PIP 角度が次の形で計算されていること:

  ```text
  u       = curl01[finger]          // 0〜1
  k_MCP   = pc1McpCurve.Evaluate(u) // 0〜1
  k_PIP   = pc1PipCurve.Evaluate(u) // 0〜1

  θ_MCP = base_MCP + k_MCP * range_MCP
  θ_PIP = base_PIP + k_PIP * range_PIP

  // DIP は簡略化のため、PIP をスケールして利用（例）
  θ_DIP = base_DIP + (k_PIP * range_DIP)
  ```

- `base_*` / `range_*` は、`feature/hand-curl-tuning.md` に記述した方針どおり  
  BaseHand プレハブ＋Inspector で決める。

### 3. フォールバック動作（PC1 未設定時）

- `pc1McpCurve` / `pc1PipCurve` が未設定（null）の場合は、
  現行の「totalAngle を全ジョイントへ等分配するシンプルな Mapping」に自動フォールバックする。
- これにより、PC1 カーブを差し替える前後で最低限の互換性が維持される。

### 4. デバッグ確認

- `CurlDebugUI` や一時的なデバッグコードなどを用いて、
  - `curl01` を 0→1 にゆっくり変化させたとき、
  - MCP と PIP の角度変化が **線形ではなく PC1 由来のカーブを描いている** ことを確認できる。
- 少なくとも index finger について、PC1 カーブに沿った屈曲になることを目視確認する。

---

## 関連 feature / story

- `feature/hand-curl-tuning.md`
- `story/hand-curl-tuning-basic.md`

