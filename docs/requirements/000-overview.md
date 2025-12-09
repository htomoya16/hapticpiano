# 000-overview — 全体要件 / 非機能要件

## 1. プロジェクト概要

HAPTICPIANO は Unity + SteamVR(OpenVR) + OpenGloves を用い、  
XR 空間上でピアノを演奏し、触覚（Force Feedback）を返すシステムである。

現段階では **手モデル（hand-model）の入力・可視化** にフォーカスする。

---

## 2. 対象シーン / スクリプト（現フェーズ）

- シーン  
  - `Assets/Scenes/hapticpiano/hapticpiano.unity`

- スクリプト  
  - `Assets/Scripts`  
  - `Assets/Prefab`
  - `Assets/Setting`
  - `Assets/Editor`

---

## 3. 非機能要件（全体）

- シーン全体のフレームレートは **60fps 以上** を目標とする  
- 入力 → 視覚反映 → 触覚出力までの遅延を最小化する  
- コードは責務ごとに分離し、後続の piano / haptics と独立性を保つ

