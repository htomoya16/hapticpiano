# 指 curl 値を用いた関節モーション生成の要件定義

（LucidGloves / SteamVR Skeleton 向け）

## 1. 背景・目的

本システムは、LucidGloves または SteamVR Skeleton から取得できる **指単位の curl 値（0〜1）** を用いて、
SteamVR 手モデルの各関節（MCP / PIP / DIP、親指は CMC / MCP / IP）を駆動し、
**ピアノ演奏時の指の形状・動きに近いモーションを生成する**ことを目的とする。

curl 値は「縦方向の屈曲のみ」を表す 1 次元センサ値であり、横方向（外転・内転）は扱わない前提とする。

---

## 2. 対象モデルと関節対応

末端（指尖）→ DIP → PIP → MCP → CMC の順。`_l` は左右反転のみ。

| ボーン名                 | 解剖学的部位 / 役割                 |
|--------------------------|--------------------------------------|
| finger_thumb_r_end       | 親指 指尖（tip）                    |
| finger_thumb_2_r         | 親指 IP（PIP/DIP 相当）             |
| finger_thumb_1_r         | 親指 MCP                             |
| finger_thumb_0_r         | 親指 CMC                             |
| finger_index_r_end       | 示指 指尖（tip）                     |
| finger_index_2_r         | 示指 DIP                              |
| finger_index_1_r         | 示指 PIP                              |
| finger_index_0_r         | 示指 MCP                              |
| finger_index_meta_r      | 示指 CMC               |
| finger_middle_r_end      | 中指 指尖（tip）                     |
| finger_middle_2_r        | 中指 DIP                              |
| finger_middle_1_r        | 中指 PIP                              |
| finger_middle_0_r        | 中指 MCP                              |
| finger_middle_meta_r     | 中指 CMC             |
| finger_ring_r_end        | 薬指 指尖（tip）                     |
| finger_ring_2_r          | 薬指 DIP                              |
| finger_ring_1_r          | 薬指 PIP                              |
| finger_ring_0_r          | 薬指 MCP                              |
| finger_ring_meta_r       | 薬指 CMC              |
| finger_pinky_r_end       | 小指 指尖（tip）                     |
| finger_pinky_2_r         | 小指 DIP                              |
| finger_pinky_1_r         | 小指 PIP                              |
| finger_pinky_0_r         | 小指 MCP                              |
| finger_pinky_meta_r      | 小指 CMC             |

![alt text](image.png)

補足
- `_meta` は MCP より手根側の補助ボーン（姿勢/当たり調整用）。曲げ分配は MCP 相当の `*_0` 以降で行うと解剖学的に近い。  
- 親指は IP が1関節のみのため、PIP/DIP 相当を `finger_thumb_2_r` にまとめている。

---

## 3. 入出力仕様

### 3.1 入力（Input）

* 各指の curl 値（0.0〜1.0）

  * `curl_thumb`
  * `curl_index`
  * `curl_middle`
  * `curl_ring`
  * `curl_pinky`

### 3.2 出力（Output）

* SteamVR Skeleton の各ボーンのローカル回転（Quaternion または Euler）
* 指腹 collider の位置（ピアノ鍵盤との接触判定用）

---

## 4. 機能要件
（実装対象スクリプト: `Assets/Scripts/Hands/Core/HandCurlTracker.cs`, `Assets/Scripts/Hands/Core/HandVisualFromCurl.cs`）

### 4.1 curl の正規化・キャリブレーション

#### 手順

1. ユーザが「ピアノのホームポジション」を取る → `curl_rest`
2. 最大屈曲（押し込み動作）を行う → `curl_press`
3. ランタイムでは次式で正規化：

```
c_norm = saturate( (curl_raw - curl_rest) / (curl_press - curl_rest) )
```

#### 要件

* 指ごとに `curl_rest` / `curl_press` を保持し調整可能であること。
* 異常値の場合はデフォルト値にフォールバックすること。

---

### 4.2 各関節への curl 割り当て（Mapping Logic）

#### 4.2.1 index/middle/ring/pinky

**初期角度（base）**

* MCP: 20〜30°
* PIP: 10〜20°
* DIP: 0〜10°

**可動範囲（range）**

* MCP: 30〜50°
* PIP: 20〜35°
* DIP: 10〜15°

**角度計算式**

```
// c_norm=0 は指が伸びた（ホームポジション）状態
θ_MCP = base_MCP + c_norm * range_MCP
θ_PIP = base_PIP + c_norm * range_PIP
θ_DIP = base_DIP + c_norm * range_DIP
```

---

#### 4.2.2 親指（thumb）

**初期角度（base）**

* CMC: 母指球が鍵盤に向くように手動調整
* MCP: 10〜20°
* IP: 0〜10°

**可動範囲（range）**

* MCP: 25〜45°
* IP: 15〜25°
* CMC は固定または最小限（0〜5°）

**角度計算式**

```
// c_norm=0 は指が伸びた（ホームポジション）状態
θ_CMC = base_CMC + c_norm * range_CMC  (小さめ or 0)
θ_MCP = base_MCP + c_norm * range_MCP
θ_IP  = base_IP  + c_norm * range_IP
```

---

### 4.3 ピアノ演奏の要件

* DIP が折れすぎず、やや伸び気味であること。
* 強打鍵時（c_norm=1.0）には MCP が主役となり、PIP が追従し、DIP は最小限の変化に留まること。
* 親指は CMC の初期オフセットにより「指腹の側面が鍵盤方向」を向くこと。

---

### 4.4 ノイズ低減（Filtering）

* curl 値は一次ローパスフィルタまたは指数平均を適用し、
  遅延を生じさせない範囲（8〜15 Hz 程度）で平滑化すること。

### 4.5 目標角・レイテンシ・キャリブ手順（ピアノ演奏向け目安）

- 目標関節角（打鍵最大付近の目安）  
  - 親指: MCP 70° / IP 50°  
  - 他指: MCP 70–80° / PIP 90° / DIP 45° （奏者・曲に応じ ±調整可）
- レイテンシ目標: curl 入力 → 可視化・物理反映まで **20 ms 以下**。
- キャリブレーション手順: 「腕を水平に伸ばし、指を伸展（ホームポジション） → 手元 UI ボタン押下 → 1 秒静止」を必須ルーチンとする。

---

## 5. 非機能要件

### 5.1 パフォーマンス

* VR フレームレート（90Hz）で更新しても十分軽量であること。

### 5.2 安定性

* NaN や範囲外の curl 値に対して安全に動作すること。

### 5.3 モジュール性

* LucidGloves / SteamVR Skeleton / その他のグローブでも
  同一の 0〜1 curl インタフェースで扱えるよう抽象化すること。

---

## 6. デバッグ要求

* Unity 上で以下を可視化可能であること：

  * `curl_raw`, `curl_norm`
  * MCP/PIP/DIP の角度
  * 各 `base_*`, `range_*` の値
* スライダーで `c_norm` を動かし、
  ホームポジション〜最大曲げの見た目を確認できる UI を提供すること。

---

## 7. ピアノ演奏との統合要件

* ホームポジション時、VR 手と現実の手に著しい乖離がないこと。
* 鍵盤接触が指腹 collider で正しく認識されること。
* 連続打鍵時に滑らかなモーションが実現されること。

---

## 8. まとめ

本要件の中心は以下である：

* **curl（指一本につき 1 値）を MCP/PIP/DIP へ分配する「比率＋オフセット」モデルで動かすこと。**
* **ピアノ演奏フォームに最適化し、MCP を主役、PIP を補助、DIP は最小限とする。**
* **親指は CMC を固定オフセットで向き調整し、曲げは MCP/IP が担当する。**

この仕様に従うことで、
LucidGloves の制約下でも「それらしいピアノ演奏手」を Unity・SteamVR 上に実現できる。

---




---
