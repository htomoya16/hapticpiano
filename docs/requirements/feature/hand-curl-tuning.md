# 指 curl 値を用いた関節モーション生成の要件定義

（LucidGloves / シリアル入力 → SteamVR 手モデル）

## 1. 背景・目的

本システムは、LucidGloves のポテンショメータ生値を **シリアル通信で Unity に直接取り込み**、  
指単位の 1 次元センサ値から **curl 値（0〜1）** を生成し、  
SteamVR 手モデルの各関節（MCP / PIP / DIP、親指は CMC / MCP / IP）を駆動して  
**ピアノ演奏時の指の形状・動きに近いモーションを生成する**ことを目的とする。

PC1 ベースの指モーションモデルそのもの（`curl → phase → PC1 → angle` の考え方など）は  
`feature/hand-kinematics.md` に参考情報としてまとめるが、  
**研究目的（ユーザスタディ・計測・解析）で用いる運動学モデルは、等分モデル（`LinearHandKinematicsProfile`）を正準とする。**  
本仕様では、Linear モデルを前提とした **curl 値からの具体的な分配・実装側の要件** を扱う。

ここでいう curl 値は「縦方向の屈曲のみ」を表す 1 次元センサ値であり、横方向（外転・内転）は扱わない前提とする。

補足:

- SteamVR_Behaviour_Skeleton が提供する `fingerCurls` は、  
  本仕様では **メイン経路としては使用せず**、将来的な別入力ソースとして扱う。  
- 視覚側 (`HandVisualFromCurl`) は、「0〜1 の curl 値」を唯一の入力とし、  
  入力源がシリアルか SteamVR_Skeleton かを意識しない。

---

## 2. 入出力仕様

### 3.1 センサ入力（Serial）

* LucidGloves 側から PC / Unity へ送られる 1 フレーム分の ASCII 文字列:

  * 例: `A2301B2391C3431D3313E1234`
  * フォーマット:
    * `A` = 親指 (thumb)
    * `B` = 人差し指 (index)
    * `C` = 中指 (middle)
    * `D` = 薬指 (ring)
    * `E` = 小指 (pinky)
    * （オプション）`K` = キャリブレーション中フラグ  
      - 行内のどこかに `K`（または `k`）が含まれている場合、そのフレームは「位置合わせ用キャリブレーション中」とみなす。
  * 各文字に続く数字列が、その指のポテンショメータ生値（ADC 値の整数）  
    * レンジ: **0〜4095**（ESP 側でキャリブレーション済み）  
      - `0`   = 指が伸びている（ホームポジション）  
      - `4095`= 完全に握っている（最大屈曲）

* Unity 側 (`HandCurlTracker`) では、受信文字列をパースし、  
  指ごとに以下のような配列として保持する。

  * `sensorRaw[0]` = 親指 A の生値  
  * `sensorRaw[1]` = 人差し指 B の生値  
  * `sensorRaw[2]` = 中指 C の生値  
  * `sensorRaw[3]` = 薬指 D の生値  
  * `sensorRaw[4]` = 小指 E の生値  

* パース不能 / 通信エラーの場合は、安全側に倒す:
  * 例: 直前フレームの値を維持する or 指を開いた状態に近い値へクリップする。

### 3.2 正規化された curl 値（0〜1）

* 各指の curl 値（0.0〜1.0）

  * `curl_thumb`
  * `curl_index`
  * `curl_middle`
  * `curl_ring`
  * `curl_pinky`

* これらは `HandCurlTracker` が `sensorRaw` から計算し、  
  `HandVisualFromCurl` や Force Feedback へ渡す共通インタフェースとする。

* 意味付け:

  * `curl = 0.0` : 指が伸びた状態（ホームポジション）  
  * `curl = 1.0` : 最大押し込み（強打鍵）付近の状態

### 3.3 出力（Output）

* SteamVR Skeleton ベースの手モデルの各ボーンのローカル回転（Quaternion または Euler）
* 指腹 collider の位置（ピアノ鍵盤との接触判定用）

### 3.4 キャリブレーションフラグ（K）

* ESP → Unity のシリアル文字列に `K` が含まれている間、そのフレームは「キャリブレーション中」とみなす。
* Unity 側では以下の挙動を行うこと:
  * `K` を含むフレームでは `sensorRaw` を更新せず、直前フレームの値を保持する（= 指の curl を凍結）。
  * 同時に、VR 上の手の Transform を固定し、実際の手を動かしても VR 手が追従しない状態にする。  
    （例: `HandFreezeOnCalibrate` のようなコンポーネントで、`HandSerialInput.isCalibrating` を監視して手の位置・向きを固定する）
* `K` を含まないフレームに戻った時点で、通常の追従動作（センサ値更新 + Transform 更新）に復帰する。

---

## 4. 機能要件

（実装対象スクリプト: `Assets/Scripts/Hands/Core/HandCurlTracker.cs`, `Assets/Scripts/Hands/Core/HandVisualFromCurl.cs`）

### 4.1 curl の正規化（ESP 側キャリブレーション前提）

#### 対象

* LucidGloves ポテンショメータから取得した指ごとのセンサ値 `sensor_raw`（整数）
  * ESP 側のキャリブレーションにより、各指で  
    `0` = 伸展（ホームポジション）、`4095` = 最大屈曲 に近い状態になっていることを前提とする。

#### 手順（Unity 側）

1. ESP から届いた `sensor_raw`（0〜4095）をそのまま `HandCurlTracker.sensorRaw[i]` に格納する。
2. ランタイムでは次式で正規化して curl を得る:

```text
c_norm = saturate( sensor_raw / 4095.0 )
```

* `saturate(x) = min( max(x, 0), 1 )`
* 各指ごとに独立して計算する。
* `c_norm` は `HandCurlTracker.curl01[i]` に対応する。

#### 要件

* ESP 側キャリブレーションにより、少なくとも以下が成り立つこと:
  * 指を伸ばした状態で `sensor_raw ≒ 0`（ノイズを除き、極端に大きくない）。  
  * 指を最大限握った状態で `sensor_raw ≒ 4095`（あるいは十分大きな値）。
* Unity 側では、必要に応じて「下限オフセット」「上限クリップ」「ゲイン」などの軽微な補正を行ってもよいが、  
  基本は `0〜4095 → 0〜1` の線形マッピングとする。

---

### 4.2 各関節への curl 割り当て（Mapping Logic）

本実装では、  
**「BaseHand プレハブのポーズ＝curl=0 の姿勢」** とみなし、  
そこから各指のジョイントを一括で回転させる **シンプルな Mapping** を採用する。

#### 4.2.1 Base ポーズの決め方

- 使用するモデル:
  - `Assets/Prefab/Hands/HandBase/BaseHand_Left.prefab`
  - `Assets/Prefab/Hands/HandBase/BaseHand_Right.prefab`
- エディタ上で、ベースとなる手の形状を手動調整する:
  - 指を「伸ばし気味〜軽く曲がったホームポジション」にしておく。
  - 左右で極端に違わないように、おおよそ対称になるよう揃えておく。
- ランタイムでは `HandVisualFromCurl` が `Start()` 内で
  - 各指ジョイント配列（`thumbJoints` など）の `localRotation` を
  - `baseRot[finger][joint]` としてキャプチャし、
  - これを **curl=0 の基準姿勢** とする。

### 4.3 ピアノ演奏の要件

* DIP が折れすぎず、やや伸び気味であること。
* 強打鍵時（c_norm=1.0）には MCP が主役となり、PIP が追従し、DIP は最小限の変化に留まること。
* 親指は CMC の初期オフセットにより「指腹の側面が鍵盤方向」を向くこと。

実装上は、BaseHand のポーズと `maxAngle` の調整によって  
これらの条件に近づける方針とする（コード側で MCP/PIP/DIP を個別制御するのではなく、  
**見た目のチューニングをプレハブ＋最大角で行う**）。

---

### 4.4 ノイズ低減（Filtering）

* curl 値に対してローパスフィルタ（指数平均など）を入れることで、  
  ガクつきを抑えつつ遅延を抑える、という方針は維持する。
* 具体的なフィルタ実装（`sensorRaw` → `curlRaw` → `curl01` へのローパス処理）や  
  パラメータ調整の要件は `story/hand-curl-filtering-noise-reduction.md` で扱う。

---

### 4.5 目標角・レイテンシ・キャリブレーションの考え方

- 目標関節角（打鍵最大付近の目安）  
  - 親指: MCP 70° / IP 50°  
  - 他指: MCP 70–80° / PIP 90° / DIP 45° （奏者・曲に応じ ±調整可）
- レイテンシ目標: センサ入力 → curl 正規化 → 可視化・物理反映まで **20 ms 以下**。

#### 4.5.1 ESP 側キャリブレーション

- ESP 側ファームウェアで、各指のポテンショメータに対し

  - 指を伸ばした状態で ≒0
  - 指を最大限握った状態で ≒4095

  になるようキャリブレーションを行う。  
- Unity 側は **`0〜4095 → 0〜1 の線形正規化のみ** を行い、  
  追加のセンサキャリブレーションは行わない。

#### 4.5.2 Unity 側（Visual）の基準姿勢

- Visual 側の **curl=0 の基準姿勢** は、BaseHand プレハブのポーズで決める:
  1. エディタで BaseHand_Left/Right をシーンに配置し、
     ホームポジションとして望ましい指の形に調整する。
  2. 必要に応じてプレハブに Apply しておく。
  3. ランタイム開始時、`HandVisualFromCurl` が `Start()` 内で
     すべての指ジョイントの `localRotation` を `baseRot` としてキャプチャする。

- 以降の挙動:
  - `curl = 0` では Visual Hand は `baseRot` の姿勢を保つ。
  - `curl > 0` に応じて、各ジョイントに等分された回転を追加する。

- ワールド空間 UI からの「再キャリブレーションボタン」は現バージョンでは用意せず、  
  **ESP 側キャリブ＋BaseHand 調整で完結** させる。

---

## 5. 非機能要件

### 5.1 パフォーマンス

* VR フレームレート（90Hz）で更新しても十分軽量であること。

### 5.2 安定性

* NaN や範囲外の curl 値に対して安全に動作すること。
* シリアル通信が一時的に途切れた場合でも、  
  指が暴れたり極端な姿勢にならないようにフェイルセーフを入れること。

### 5.3 モジュール性

* LucidGloves / SteamVR Skeleton / その他のグローブでも  
  同一の 0〜1 curl インタフェースで扱えるよう抽象化すること。
* 具体的には:
  * `HandCurlTracker` は「ESP などから届いたキャリブ済み生値 → 0〜1 curl」変換の責務を負う。
  * `HandVisualFromCurl` は `curl01` のみを参照し、入力ソースの違いを意識しない。

---

## 6. デバッグ要求

* Unity 上で以下を可視化可能であること：

  * `sensorRaw`（生値）、`curl_raw`（必要なら中間値）、`curl_norm`
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

* **LucidGloves のポテンショメータ値は ESP 側でキャリブレーションし、Unity 側では 0〜4095 → 0〜1 に正規化すること。**
* **curl（指一本につき 1 値）を MCP/PIP/DIP へ分配する「比率＋オフセット」モデルで動かすこと。**
* **親指は CMC を固定オフセットで向き調整し、曲げは MCP/IP が担当すること。**
* **視覚側は Skeleton fingerCurl ではなく、「キャリブ時に保存した Visual Hand の姿勢」を curl=0 の基準とすること。**

この仕様に従うことで、  
LucidGloves の制約下でも「それらしいピアノ演奏手」を Unity・SteamVR 上に実現できる。
