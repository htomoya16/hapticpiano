# System Architecture

## 1. 概要

HAPTICPIANO は以下の 3 層構造で成り立つ：

1. **入力層**（OpenVR → HandCurlTracker）  
2. **可視化層**（HandVisualFromCurl → ボーン回転）  
3. **応用層（将来）**  
   - ピアノ押し込み  
   - FFB  
   - 評価ロガー

現フェーズでは 1・2 を重点的に改善する。

---

## 2. コンポーネント構成

### HandCurlTracker
- OpenGloves/SteamVR skeleton から curl 値取得  
- 平滑化・キャリブレーション・正規化を担当  
- Null セーフティを備える

### HandVisualFromCurl
- curl 値をボーン回転へ変換  
- 指の曲げ軸・角度・係数を Inspector から調整可能  
- 当たり判定用の指先アンカー Transform に対応可能な構造

### hapticpiano.unity
- 手モデルの動作確認用シーン  
- 今後、鍵盤・押し込み量・FFB の統合先にもなる

---

## 3. 設計方針（重要）

- **責務分離**：入力（CurlTracker）と可視化（VisualFromCurl）を常に分ける  
- **拡張性**：当たり判定・FFB を追加する際に構造を壊さない  
- **再現性**：左右手は同じパラメータ体系で動作させる  
