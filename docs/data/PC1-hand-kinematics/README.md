# PC1-hand-kinematics データ概要

Furuya et al.「Hand kinematics of piano playing」  
（Journal of Neurophysiology, 2011, DOI: 10.1152/jn.00378.2011）の図から、  
指関節の主成分（PC1）に関する情報を抽出した研究用データをまとめる。

> 注意: ここに置かれた CSV / 画像は、元論文の図をもとにした手作業のデジタイズ結果であり、  
> 元データそのものではない。正確な値が必要な場合は、必ず原著論文を参照すること。

---

## 1. 参照した図

- `Fig3.jpeg`
  - 元論文 Fig.3（各指関節の joint angular velocity と PC1〜PC3 による再構成波形）からキャプチャした画像。
  - 主に **PC1 段の MCP / PIP の波形**を、指ごと（index / middle / ring / little）に読み取る際の参照として使用した。
  - 実装やパラメータ調整の際に、元の波形の形状（ピーク位置・符号・相対的大きさ）を確認するために利用する。

---

## 2. MCP 用 PC1 波形（`PC1_MCP`）

ディレクトリ: `docs/data/PC1-hand-kinematics/PC1_MCP/`

- `PC1_MCP_Index.csv`
- `PC1_MCP_Middle.csv`
- `PC1_MCP_Ring.csv`
- `PC1_MCP_Little.csv`
- `PC1_MCP.jpeg`（Fig.3 から MCP 行を拡大・抜粋した参考画像）

### 2.1 内容

- 各 CSV は、以下の 2 列を持つことを想定している（実際の列名はツール依存）:
  - 1列目: 時間軸を 0〜1 に正規化したパラメータ `t`  
           （元図の「1 打鍵ぶんの時間」を等間隔サンプリングしたもの）
  - 2列目: MCP 関節の **PC1 成分による角速度波形** `v_MCP_PC1(t)` の相対値  
           （符号・相対形状を維持しつつ、のちに 0〜1 へスケーリングして利用する前提）
- 指ごとに、論文 Fig.3 の PC1 段・MCP 行から目視・ツールで座標を読み取って作成した。

### 2.2 利用目的

- `feature/hand-curl-tuning.md` / `story/hand-curl-tuning-pc1-curve.md` に記述したとおり、
  - LucidGloves から得られる `curl01`（0〜1）をパラメータとして、
  - MCP の屈曲角度を「PC1 に由来する相対パターン」に沿わせるためのテンプレートとして利用する。
- 実装側では、この波形を適宜整形（符号の決定・積分・0〜1 正規化など）したうえで、
  `AnimationCurve pc1McpCurve` として用いることを想定している。

---

## 3. PIP 用 PC1 波形（`PC1_PIP`）

ディレクトリ: `docs/data/PC1-hand-kinematics/PC1_PIP/`

- `PC1_PIP_Index.csv`
- `PC1_PIP_Middle.csv`
- `PC1_PIP_Ring.csv`
- `PC1_PIP_Little.csv`
- `PC1_PIP.jpeg`（Fig.3 から PIP 行を拡大・抜粋した参考画像）

### 3.1 内容

- 各 CSV は、MCP と同様に
  - 1列目: 時間軸を 0〜1 に正規化したパラメータ `t`
  - 2列目: PIP 関節の **PC1 成分による角速度波形** `v_PIP_PC1(t)` の相対値
 から構成される。
- 論文 Fig.3 の PC1 段・PIP 行から指ごとに読み取って作成した。

### 3.2 利用目的

- MCP と同様に、PIP の屈曲角を PC1 シナジーに沿わせるためのテンプレートとして利用する。
- 実装側では、PIP 用に `AnimationCurve pc1PipCurve` を構築し、

  ```text
  u       = curl01             // 0〜1
  k_PIP   = pc1PipCurve(u)     // 正規化した PC1 係数
  θ_PIP = base_PIP + k_PIP * range_PIP
  ```

  のような形で使用することを想定している（詳細は feature/story を参照）。

---

## 4. 注意事項 / TODO

- どの被験者・どのクラスタの PC1 をサンプリングしたか（例: プロフェッショナル奏者, 第1クラスタ平均など）は、
  可能であれば本ファイル内に追記しておくこと（現時点では省略）。
- 本データはあくまで「ピアノ演奏中の代表的な屈曲シナジー」を VR 用に利用するための近似であり、
  生理学的・神経科学的な機構を厳密に再現することを目的とはしていない。

