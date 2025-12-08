# Requirements — 索引 / 入口

このディレクトリには HAPTICPIANO の要件を階層構造でまとめる。

- **上位層：プロジェクト全体の目的・非機能要件**
- **中位層：機能（feature）単位の仕様**
- **下位層：タスク（story）単位の受け入れ条件**

---

## 1. 上位層（全体像）
- [000-overview.md](000-overview.md)

---

## 2. 中位層（機能要件 / feature）
`feature/` に配置：

- [feature/hand-model.md](feature/hand-model.md)
- [feature/hand-collider.md](feature/hand-collider.md)
- [feature/piano-key-collider.md](feature/piano-key-collider.md)
- [feature/hand-curl-tuning.md](feature/hand-curl-tuning.md)
- [feature/hand-kinematics.md](feature/hand-kinematics.md)

今後 piano / haptics / evaluation などの機能が増えた場合も、この階層に追加する。

---

## 3. 下位層（story / 受け入れ条件）
`story/` に配置：

- [story/hand-model-basic.md](story/hand-model-basic.md)
- [story/hand-model-improvements.md](story/hand-model-improvements.md)
- [story/hand-collider.md](story/hand-collider.md)
- [story/piano-key-collider-sizing.md](story/piano-key-collider-sizing.md)
- [story/hand-curl-tuning-basic.md](story/hand-curl-tuning-basic.md)
- [story/hand-kinematics-pc1-data.md](story/hand-kinematics-pc1-data.md)
- [story/hand-kinematics-tuning-pc1-curve.md](story/hand-kinematics-tuning-pc1-curve.md)
- [story/hand-curl-filtering-noise-reduction.md](story/hand-curl-filtering-noise-reduction.md)

story は「今回のタスクで何をもって完了とするか」を定義する。

---

## 4. 読む順番

1. 000-overview  
2. feature  
3. story（作業前に確認）

---

## 5. 研究用データ（外部論文ベースの生データ）

`docs/data/` には、外部論文から抽出した元データなど、  
要件や実装方針の根拠となる研究用データを配置する。

- `data/PC1-hand-kinematics/PC1_MCP/`
  - Furuya et al., *Hand kinematics of piano playing*（J Neurophysiol, 2011）の Fig.2 PC1 段から
    抽出した MCP 用 PC1 波形（index/middle/ring/little）の CSV / 参考画像。
- `data/PC1-hand-kinematics/PC1_PIP/`
  - 同論文 Fig.2 PC1 段から抽出した PIP 用 PC1 波形（index/middle/ring/little）の CSV / 参考画像。

これらのデータは、`feature/hand-kinematics.md` および  
`feature/hand-curl-tuning.md` で述べる「PC1 ベースの指関節シナジーモデル」の根拠として利用する。
