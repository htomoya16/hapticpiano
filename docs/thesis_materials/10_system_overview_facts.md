# 10_system_overview_facts (facts)

## 使用デバイス一覧
- HMD名: 未確認（コード/Scene/Prefab/要件docに具体名なし）
- VRランタイム/入力:
  - OpenVR設定: `Assets/XR/Settings/OpenVRSettings.asset:20`（`EditorAppKey` 等）
  - SteamVR actions: `Assets/StreamingAssets/SteamVR/actions.json`（Action定義）
- グローブ構成要素:
  - OpenGloves 記載: `docs/requirements/000-overview.md:5`
  - ESP32 記載: `docs/requirements/feature/haptics-serial-calibration.md:29`
- サーボ:
  - MG90S 記載: `docs/requirements/feature/haptics-serial-calibration.md:4`
- PC/OS:
  - 未確認（ただし Windowsパス/COM名が設定値として存在: `Packages/manifest.json:12`, `Assets/Scripts/Hands/Core/HandSensorReceiver.cs:17`）

## システム構成要素
- Unity:
  - EditorVersion: `2022.3.62f3`（`ProjectSettings/ProjectVersion.txt:1`）
- XRプラグイン/ランタイム:
  - `com.valvesoftware.unity.openvr`（ローカルtgz参照）: `Packages/manifest.json:12`
  - XR Management: `com.unity.xr.management` `4.4.0`（`Packages/packages-lock.json:175`）
  - OpenVR Loader asset: `Assets/XR/Loaders/OpenVRLoader.asset`
  - OpenVR Settings asset: `Assets/XR/Settings/OpenVRSettings.asset:21`（ActionManifest相対パス）
- SteamVR:
  - Action manifest: `Assets/StreamingAssets/SteamVR/actions.json`
  - OpenVRアプリマニフェスト: `unityProject.vrmanifest`（`action_manifest_path` 等）
- ESP32との通信方式（Unity側）:
  - `System.IO.Ports.SerialPort` 使用: `Assets/Scripts/IO/SerialPortAdapter.cs:2`
  - 受信: 1行読み取り→最終行採用: `Assets/Scripts/IO/SerialPortAdapter.cs:96`
  - パケット形式:
    - デコード（センサ）: `A(\d{1,4})B(\d{1,4})C(\d{1,4})D(\d{1,4})E(\d{1,4})`（`Assets/Scripts/IO/SerialPacketCodec.cs:15`）
    - センサ値クランプ 0..4095: `Assets/Scripts/IO/SerialPacketCodec.cs:11`
    - 送信値クランプ 0..1000 + 4桁ゼロパディング: `Assets/Scripts/IO/SerialPacketCodec.cs:12`

## データの流れ（矢印）
- センサ→curl:
  - `SerialPortAdapter.TryReadLatestLine()` → `HandSensorReceiver.Update()` → `SerialPacketCodec.TryDecode()` → `HandCurlTracker.UpdateSensorFromDecodedValues()` → `HandCurlTracker.Update()`
- curl→手モデル:
  - `HandCurlTracker.curl01` → `HandVisualFromCurl.LateUpdate()` → 指ジョイント回転（`HandKinematicsProfile.Evaluate()` 経由）
- 手→鍵盤接触:
  - `FingerColliderBuilder` による指コライダ生成 → `PianoKeyFingerContactReporter` が Collision から `FingerColliderId` を取得 → `PianoFingerContactRegistry` に enter/exit 登録
- 触覚（ランタイム制御）:
  - `HandCurlTracker.curl01` + `HapticCalibrationState(released)` + `PianoFingerContactRegistry(接触/底面)` → `HapticRuntimeFeedbackController.Update()` → `HapticSerialSender.currentFingerTargets` → `HapticSerialSender.Update()` → `SerialPacketCodec.Encode()` → `SerialPortAdapter.TryWriteLine()`
- 評価ログ:
  - `EvaluationTaskController`（trial/press生成） → `EvaluationLogSession/EvaluationLogTask` → CSVファイル追記（`Application.persistentDataPath` 配下）

## 実行形態
- PCVR/Standalone:
  - Standalone XR Settings asset存在: `Assets/XR/XRGeneralSettingsPerBuildTarget.asset`（`Standalone Settings`）
  - PCVR/Standalone の最終判定: 未確認（Build設定/実機情報はリポジトリ外）
- 実行に必要な外部ソフト:
  - 未確認（ただし SteamVR/OpenVR 前提のファイル群が存在: `unityProject.vrmanifest`, `Assets/StreamingAssets/SteamVR/actions.json`）
