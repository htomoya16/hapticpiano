# 14_hand_pose_update_facts (facts)

卒論 3.5.1「手全体の位置および姿勢の更新」に対応する、実装根拠（ファイルパス:行）をまとめる。

## 対象と前提
- 対象: VR上の手モデル（手首基準/root）の位置・姿勢更新、および ESP 由来の `K` フラグ受信時の挙動。
- 本プロジェクトの現状実装は、**手の位置・姿勢はコントローラ（SteamVR Pose）で決まり、ESP からは主に指の曲げ（curl）と K フラグが届く**構成である。

## 手rootの追従方式（SteamVR / OpenVR）
### 追従コンポーネント
- `SteamVR_Behaviour_Pose` が Pose Action から Transform を更新する:
  - `Assets/SteamVR/Input/SteamVR_Behaviour_Pose.cs:123`（`UpdateTransform()`）
  - `Assets/SteamVR/Input/SteamVR_Behaviour_Pose.cs:127`（`origin != null` 分岐）
  - `Assets/SteamVR/Input/SteamVR_Behaviour_Pose.cs:129`（`transform.position = origin.TransformPoint(localPosition)`）
  - `Assets/SteamVR/Input/SteamVR_Behaviour_Pose.cs:130`（`transform.rotation = origin.rotation * localRotation`）
  - `Assets/SteamVR/Input/SteamVR_Behaviour_Pose.cs:134`（`transform.localPosition = localPosition`）
  - `Assets/SteamVR/Input/SteamVR_Behaviour_Pose.cs:135`（`transform.localRotation = localRotation`）

### 左右手のPose入力（Prefab実体）
- 右手:
  - `poseAction=/actions/default/in/Pose`: `Assets/Prefab/Hands/RightHand.prefab:638`
  - `inputSource: 2`（Right）: `Assets/Prefab/Hands/RightHand.prefab:641`
  - `origin: {fileID: 0}`（未指定 → `localPosition/localRotation` 更新経路）: `Assets/Prefab/Hands/RightHand.prefab:642`
- 左手:
  - `poseAction=/actions/default/in/Pose`: `Assets/Prefab/Hands/LeftHand.prefab:836`
  - `inputSource: 1`（Left）: `Assets/Prefab/Hands/LeftHand.prefab:839`
  - `origin: {fileID: 0}`（未指定 → `localPosition/localRotation` 更新経路）: `Assets/Prefab/Hands/LeftHand.prefab:840`

## コントローラ→手首オフセット（Toffset）について
### 実装上の観察
- 「コントローラPoseに対して、非ゼロの固定オフセット（位置+回転）を毎フレーム掛ける」C#実装は確認できない。
- 視覚手（BaseHand）は、コントローラ追従オブジェクトの子として配置されているが、Prefab上で **相対Transformがゼロ/単位**に上書きされている（実質 `Toffset ≈ I`）。

### 根拠（Prefabの修正差分）
- 右手の BaseHand_Right（PrefabInstance）の `m_LocalPosition` が 0 に上書き:
  - `Assets/Prefab/Hands/RightHand.prefab:935`
  - `Assets/Prefab/Hands/RightHand.prefab:939`
  - `Assets/Prefab/Hands/RightHand.prefab:943`
- 右手の BaseHand_Right（PrefabInstance）の `m_LocalRotation` が単位回転に上書き:
  - `Assets/Prefab/Hands/RightHand.prefab:947`
  - `Assets/Prefab/Hands/RightHand.prefab:951`
  - `Assets/Prefab/Hands/RightHand.prefab:955`
  - `Assets/Prefab/Hands/RightHand.prefab:959`
- 左手も同様:
  - `Assets/Prefab/Hands/LeftHand.prefab:935`
  - `Assets/Prefab/Hands/LeftHand.prefab:947`

## ESP受信と K フラグ処理
### 受信・解析の責務分担
- シリアル読取（行単位）: `SerialPortAdapter`
  - `Assets/Scripts/IO/SerialPortAdapter.cs:96`（`TryReadLatestLine`）
  - `Assets/Scripts/IO/SerialPortAdapter.cs:105`（`ReadLine()`）
- 受信行の取り出し・K判定・デコード呼び出し: `HandSensorReceiver`
  - `Assets/Scripts/Hands/Core/HandSensorReceiver.cs:50`（最新行の取得）
  - `Assets/Scripts/Hands/Core/HandSensorReceiver.cs:61`（`K/k` 検出）
  - `Assets/Scripts/Hands/Core/HandSensorReceiver.cs:63`（K受信中は更新停止）
  - `Assets/Scripts/Hands/Core/HandSensorReceiver.cs:66`（`SerialPacketCodec.TryDecode`）
  - `Assets/Scripts/Hands/Core/HandSensorReceiver.cs:76`（`HandCurlTracker` へ反映）
- A〜E 5ch 抽出（K等が付与されてもOK）: `SerialPacketCodec`
  - `Assets/Scripts/IO/SerialPacketCodec.cs:15`（Regex）
  - `Assets/Scripts/IO/SerialPacketCodec.cs:27`（`TryDecode`）

### K受信中の「手の凍結」
- `HandFreezeOnCalibrate` が `HandSensorReceiver.isCalibrating` を監視し、K受信中は手rootのワールド姿勢を固定する。
  - `Assets/Scripts/Hands/Core/HandFreezeOnCalibrate.cs:35`（`LateUpdate`）
  - `Assets/Scripts/Hands/Core/HandFreezeOnCalibrate.cs:50`（凍結開始時に姿勢保存）
  - `Assets/Scripts/Hands/Core/HandFreezeOnCalibrate.cs:58`（`handRoot.position` を固定）
  - `Assets/Scripts/Hands/Core/HandFreezeOnCalibrate.cs:59`（`handRoot.rotation` を固定）
- 右手Prefabで凍結対象の handRoot が設定されている:
  - `Assets/Prefab/Hands/RightHand.prefab:704`
- 左手Prefabでも同様（handRoot指定）:
  - `Assets/Prefab/Hands/LeftHand.prefab:902`

### 重要: 「再基準化/アライメント（offset更新）」は行っていない
- K受信をトリガに、基準Transformやオフセット（Toffset）を更新して追従基準を変える処理は確認できない。
- 現状のK処理は **(1) curl更新停止 + (2) 手rootの凍結**であり、K終了後は通常追従に戻る。

## 主張A/Bの判定（論文記述との整合）
### 主張A
> `Thand(t) = Tctrl(t) * Toffset` のように、コントローラPoseから手モデルrootを更新し、固定オフセットを適用している。

- **判定: 一部のみ整合**
  - `Tctrl(t)`（SteamVR Pose）で手のTransformが更新される点は整合: `Assets/SteamVR/Input/SteamVR_Behaviour_Pose.cs:123`〜`135`
  - ただし `Toffset` に相当する **非ゼロ固定オフセットを毎フレーム掛ける実装は確認できず**、Prefab上は `Toffset ≈ I`（相対Transformがゼロ/単位）: `Assets/Prefab/Hands/RightHand.prefab:935`〜`959`

### 主張B
> ESPからの「Kフレーム」受信をトリガに、Unity側で手モデル基準を固定/再基準化して合わせられるようにする（キャリブ/アライメント）を行っている。

- **判定: 固定はYes / 再基準化はNo**
  - 固定（凍結）は実装あり: `Assets/Scripts/Hands/Core/HandFreezeOnCalibrate.cs:50`〜`59`
  - しかし「基準更新」「オフセット更新」などの再基準化（アライメント確定）処理は実装根拠なし。

## ESP側仕様（このリポジトリの範囲）
- ESP32ファームウェアは本リポジトリに含まれず、K送信条件（ボタン長押し等）はここからは確定できない（外部リポジトリ参照）: `AGENTS.md:13`
