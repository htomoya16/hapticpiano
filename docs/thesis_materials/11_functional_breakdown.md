# 11_functional_breakdown (最重要)

## F-01: シリアル受信（センサ行）→デコード→センサ配列更新
- 機能ID: F-01
- 機能名: センサ受信・デコード・sensorRaw更新

**事実情報**
- 入力
  - デバイス/経路: シリアルポート（`System.IO.Ports`）`Assets/Scripts/IO/SerialPortAdapter.cs:2`
  - データ形式: 1行文字列（例: `A####B####C####D####E####`）`Assets/Scripts/IO/SerialPacketCodec.cs:15`
  - 更新周期: `HandSensorReceiver.Update()`（フレーム単位）`Assets/Scripts/Hands/Core/HandSensorReceiver.cs:33`
- 処理
  - `HandSensorReceiver.Start()` で未OpenならOpen試行: `Assets/Scripts/Hands/Core/HandSensorReceiver.cs:36`
  - `SerialPortAdapter.TryReadLatestLine()` で最新行取得（複数行なら最後）: `Assets/Scripts/IO/SerialPortAdapter.cs:96`
  - Kフラグ検出（`K`/`k`含む）→キャリブ中フラグON→更新停止: `Assets/Scripts/Hands/Core/HandSensorReceiver.cs:61`
  - デコード `SerialPacketCodec.TryDecode()`（センサ0..4095クランプ）: `Assets/Scripts/IO/SerialPacketCodec.cs:27`
  - `HandCurlTracker.UpdateSensorFromDecodedValues()` に配列コピー: `Assets/Scripts/Hands/Core/HandCurlTracker.cs:77`
- 出力
  - 出力先: `HandCurlTracker.sensorRaw[int[5]]`（Thumb..Pinky）`Assets/Scripts/Hands/Core/HandCurlTracker.cs:17`
  - 出力データ形式: `int[5]`（0..4095）`Assets/Scripts/IO/SerialPacketCodec.cs:11`
- 関連ファイル
  - `Assets/Scripts/Hands/Core/HandSensorReceiver.cs`
  - `Assets/Scripts/IO/SerialPortAdapter.cs`
  - `Assets/Scripts/IO/SerialPacketCodec.cs`
  - `Assets/Scripts/Hands/Core/HandCurlTracker.cs`
- Inspector主要パラメータ（Prefab実体）
  - 左手: `baudRate=115200`, `portName=COM3`（`Assets/Prefab/Hands/LeftHand.prefab:404`）
  - 右手: `baudRate=115200`, `portName=COM5`（`Assets/Prefab/Hands/RightHand.prefab:787`）
- 失敗時の挙動
  - `serialAdapter==null`/`targetTracker==null`/未Open: return（`Assets/Scripts/Hands/Core/HandSensorReceiver.cs:45`）
  - フォーマット不正: Warning（`Assets/Scripts/Hands/Core/HandSensorReceiver.cs:69`）

---

## F-02: センサ値→curl(0..1)算出（フィルタ含む）
- 機能ID: F-02
- 機能名: curl生成（sensorRaw→curlRaw/curl01）

**事実情報**
- 入力
  - `HandCurlTracker.sensorRaw[int[5]]`（0..4095想定）`Assets/Scripts/Hands/Core/HandCurlTracker.cs:17`
- 処理
  - `HandCurlTracker.Update()` → `UpdateFromSensor()`（フレーム単位）`Assets/Scripts/Hands/Core/HandCurlTracker.cs:69`
  - 正規化: `raw/4095.0f` → Clamp01 `Assets/Scripts/Hands/Core/HandCurlTracker.cs:181`
  - フィルタ:
    - `useFiltering=false` で素通し `Assets/Scripts/Hands/Core/HandCurlTracker.cs:121`
    - `noiseThreshold` 未満差分は保持 `Assets/Scripts/Hands/Core/HandCurlTracker.cs:137`
    - `snapThreshold` 超差分は alpha=1 `Assets/Scripts/Hands/Core/HandCurlTracker.cs:145`
    - `Mathf.Lerp(previous, cRaw, alpha)` `Assets/Scripts/Hands/Core/HandCurlTracker.cs:150`
  - プリセット適用: `Awake()` で `ApplyPreset(preset)`（任意）`Assets/Scripts/Hands/Core/HandCurlTracker.cs:52`
- 出力
  - `curlRaw[5]`（フィルタ前）`Assets/Scripts/Hands/Core/HandCurlTracker.cs:21`
  - `curl01[5]`（フィルタ後）`Assets/Scripts/Hands/Core/HandCurlTracker.cs:24`
- 関連ファイル
  - `Assets/Scripts/Hands/Core/HandCurlTracker.cs`
  - `Assets/Scripts/Hands/Core/HandCurlTrackerPreset.cs`
  - `Assets/Settings/HandModel/Hand Curl Tracker Preset.asset`
- Inspector主要パラメータ（Prefab実体）
  - `applyPresetOnAwake=1`, `preset`参照あり（左右）:
    - 左手 `Assets/Prefab/Hands/LeftHand.prefab:857`
    - 右手 `Assets/Prefab/Hands/RightHand.prefab:659`
  - プリセット値:
    - `useFiltering=1`, `filterAlpha=0.9`, `snapThreshold=0.35`, `noiseThreshold=0.008`（`Assets/Settings/HandModel/Hand Curl Tracker Preset.asset`）
- 失敗時の挙動
  - 入力配列長不一致: return（`Assets/Scripts/Hands/Core/HandCurlTracker.cs:83`）

---

## F-03: curl→手モデル可視化（指ジョイント回転）
- 機能ID: F-03
- 機能名: 手モデル可視化（curl01→joint rotations）

**事実情報**
- 入力
  - `HandCurlTracker.curl01[5]`（0..1）`Assets/Scripts/Hands/Core/HandVisualFromCurl.cs:51`
  - 指ジョイント配列（thumb/index/middle/ring/pinky）`Assets/Scripts/Hands/Core/HandVisualFromCurl.cs:28`
- 処理
  - `Start()` で基準姿勢キャプチャ: `CaptureBasePose()` `Assets/Scripts/Hands/Core/HandVisualFromCurl.cs:55`
  - `LateUpdate()`（フレーム単位）で適用 `Assets/Scripts/Hands/Core/HandVisualFromCurl.cs:73`
  - 角度計算:
    - `kinematicsProfile != null` の場合 `HandKinematicsProfile.Evaluate(...)` `Assets/Scripts/Hands/Core/HandVisualFromCurl.cs:115`
    - `kinematicsProfile == null` の場合 従来線形（totalAngle/3）`Assets/Scripts/Hands/Core/HandVisualFromCurl.cs:122`
  - ジョイント適用（i==1 MCP, i==2 PIP, i>=3 DIP）`Assets/Scripts/Hands/Core/HandVisualFromCurl.cs:131`
- 出力
  - `Transform.localRotation` 更新（指ジョイント）`Assets/Scripts/Hands/Core/HandVisualFromCurl.cs:150`
- 関連ファイル
  - `Assets/Scripts/Hands/Core/HandVisualFromCurl.cs`
  - `Assets/Scripts/Hands/Core/Kinematics/HandKinematicsProfile.cs`
  - `Assets/Scripts/Hands/Core/Kinematics/LinearHandKinematicsProfile.cs`
  - `Assets/Scripts/Hands/Core/Kinematics/Pc1HandKinematicsProfile.cs`（存在、ただし使用状況は別項）
  - `Assets/Settings/HandModel/KinematicsProfile/Linear Hand Kinematics Profile.asset`
  - `Assets/Settings/HandModel/KinematicsProfile/Pc 1 Hand Kinematics Profile.asset`（存在）
- Inspector主要パラメータ（Prefab実体）
  - `kinematicsProfile` 参照（左右）: Linear profile GUID（`Assets/Prefab/Hands/LeftHand.prefab:993`, `Assets/Prefab/Hands/RightHand.prefab:993`）
  - Linear profile値: max angles全指 `220`（`Assets/Settings/HandModel/KinematicsProfile/Linear Hand Kinematics Profile.asset`）
  - ジョイント配列要素数（左右同一）:
    - `thumbJoints=4`, `index/middle/ring/pinky=各5`（`Assets/Prefab/Hands/RightHand.prefab:1007`）
  - `preset` フィールドのGUID参照がAssets配下に存在せず（対応.meta未検出）:
    - 参照箇所例: `Assets/Prefab/Hands/LeftHand.prefab:1041`
- 失敗時の挙動
  - 基準姿勢取得失敗: Warning（`Assets/Scripts/Hands/Core/HandVisualFromCurl.cs:69`）
  - `curlSource==null` 等: return（`Assets/Scripts/Hands/Core/HandVisualFromCurl.cs:77`）

---

## F-04: 指コライダ自動生成＋指ID付与（衝突検出の前提）
- 機能ID: F-04
- 機能名: 指コライダ生成（FingerColliderBuilder）

**事実情報**
- 入力
  - 指ボーン配列（TIP→Root）: `FingerConfig.jointsTipToRoot` `Assets/Scripts/Hands/Colliders/FingerColliderBuilder.cs:18`
- 処理
  - `Start()` で Build（`buildDelayFrames` だけ遅延可）`Assets/Scripts/Hands/Colliders/FingerColliderBuilder.cs:90`
  - `CreateCapsuleBetween()` で `CapsuleCollider` 生成 + `FingerColliderId` 付与 `Assets/Scripts/Hands/Colliders/FingerColliderBuilder.cs:154`
  - `LateUpdate()` で毎フレーム追従（`updateEveryFrame`）`Assets/Scripts/Hands/Colliders/FingerColliderBuilder.cs:196`
- 出力
  - 生成物: 子GameObject（指区間ごとの `CapsuleCollider` + `FingerColliderId`）`Assets/Scripts/Hands/Colliders/FingerColliderBuilder.cs:162`
- 関連ファイル
  - `Assets/Scripts/Hands/Colliders/FingerColliderBuilder.cs`
  - `Assets/Scripts/Hands/Colliders/FingerColliderId.cs`
- Inspector主要パラメータ（Prefab実体）
  - `defaultRadius=0.0075`, `capsuleDirection=2`, `buildDelayFrames=0`, `updateEveryFrame=1`（左右）:
    - `Assets/Prefab/Hands/LeftHand.prefab:1043`
    - `Assets/Prefab/Hands/RightHand.prefab:1043`
  - `handedness` フィールドはPrefab内に明示シリアライズ無し（コード既定値 `Handedness.Right`）`Assets/Scripts/Hands/Colliders/FingerColliderBuilder.cs:38`
- 失敗時の挙動
  - joints未設定/短すぎ: BuildFingerでreturn（`Assets/Scripts/Hands/Colliders/FingerColliderBuilder.cs:114`）

---

## F-05: 鍵盤側の指接触検出（Collision→指ID→登録）
- 機能ID: F-05
- 機能名: 鍵盤Collision検出（PianoKeyFingerContactReporter）

**事実情報**
- 入力
  - Unity Physics `OnCollisionEnter/Exit`（鍵盤側）`Assets/Scripts/Piano/PianoKeyFingerContactReporter.cs:16`
  - 相手コライダ: `FingerColliderId`（handedness/fingerId/segmentIndex）`Assets/Scripts/Hands/Colliders/FingerColliderId.cs:21`
- 処理
  - Collisionから `FingerColliderId` を取得（collider + contacts走査）`Assets/Scripts/Piano/PianoKeyFingerContactReporter.cs:29`
  - `PianoFingerContactRegistry` 取得:
    - `FindObjectOfType`（Awake/遅延再探索）`Assets/Scripts/Piano/PianoKeyFingerContactReporter.cs:14`
    - 再生中かつ未存在なら自動生成 `PianoFingerContactRegistry (Auto)` `Assets/Scripts/Piano/PianoKeyFingerContactReporter.cs:77`
  - `RegisterCollisionEnter/Exit` 呼び出し `Assets/Scripts/Piano/PianoKeyFingerContactReporter.cs:20`
- 出力
  - `PianoFingerContactRegistry` 内部カウント更新
- 関連ファイル
  - `Assets/Scripts/Piano/PianoKeyFingerContactReporter.cs`
  - `Assets/Scripts/Piano/PianoFingerContactRegistry.cs`
- Scene/Prefab実体
  - `PianoKeys.prefab` に `PianoKeyFingerContactReporter` 88個（=鍵盤数）`Assets/Prefab/Piano/PianoKeys.prefab`（count抽出結果）
- 失敗時の挙動
  - registry未設定（Editor等）: Warning（`Assets/Scripts/Piano/PianoKeyFingerContactReporter.cs:85`）

---

## F-06: 指ごとの接触/底面状態の集約（最優先キー選択＋ヒステリシス）
- 機能ID: F-06
- 機能名: 指接触状態レジストリ（PianoFingerContactRegistry）

**事実情報**
- 入力
  - `RegisterCollisionEnter/Exit(key, fingerColliderId)` `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:62`
  - `FingerColliderId.SegmentIndex` によるフィルタ `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:77`
  - 鍵盤Transformの `eulerAngles.x`（0→360補正）`Assets/Scripts/Piano/PianoFingerContactRegistry.cs:180`
- 処理
  - `Update()` で全指（左右×5）Refresh（フレーム単位）`Assets/Scripts/Piano/PianoFingerContactRegistry.cs:102`
  - 接触キー候補が複数ある場合: `GetAngleX360` 最小のキーをprimaryに採用 `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:119`
  - 途切れ対策: `touchReleaseGraceSeconds` 以内は前回キー保持 `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:133`
  - 底面ロック: enter/exit角度のヒステリシス `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:155`
- 出力
  - `TryGetFingerState(handedness, fingerId, out isTouching, out isBottomLocked, out key)` `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:52`
- 関連ファイル
  - `Assets/Scripts/Piano/PianoFingerContactRegistry.cs`
- Inspector主要パラメータ（Scene実体）
  - `requiredSegmentIndex=0`（TIP側想定）`Assets/Scenes/hapticpiano/hapticpiano.unity:6557`
  - `bottomEnterAngleX=352.5`, `bottomExitAngleX=354.0` `Assets/Scenes/hapticpiano/hapticpiano.unity:6558`
  - `touchReleaseGraceSeconds=0.08` `Assets/Scenes/hapticpiano/hapticpiano.unity:6562`
  - `logChanges=1` `Assets/Scenes/hapticpiano/hapticpiano.unity:6561`
- 失敗時の挙動
  - `key==null`/`fingerColliderId==null` return（`Assets/Scripts/Piano/PianoFingerContactRegistry.cs:64`）

---

## F-07: 触覚キャリブレーション（0→1000スイープ＋sensorRaw逸脱検知）
- 機能ID: F-07
- 機能名: released値推定・保存（HapticGripCalibrationController）

**事実情報**
- 入力
  - `HandCurlTracker.sensorRaw[5]` `Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs:410`（GetSensorRawSafe）
  - 送信: `HapticSerialSender.TrySendNow(int[5])` `Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs:130`
- 処理
  - Coroutine `CalibrationRoutine()`（`WaitForSecondsRealtime` 使用）`Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs:265`
  - baselineサンプル: `baselineSamples` 回、間隔 `baselineSampleIntervalSeconds` `Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs:181`
  - 判定: baseline±`allowedBaselineDeviation` から外れた状態が連続 `requiredConsecutiveOutOfRange` 回 `Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs:216`
  - saved value: 「外れた瞬間の1ステップ前 + releasedValueOffset」→ 0..1000クランプ `Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs:292`
  - timeout: `timeoutSeconds`（0以下で無効）`Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs:244`
- 出力
  - `HapticCalibrationState.SetReleasedServoValue(fingerIndex, value)` `Assets/Scripts/ForceFeedBack/HapticCalibrationState.cs:45`
  - 送信状態更新: `serialSender.currentFingerTargets` 反映 `Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs:352`
- 関連ファイル
  - `Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs`
  - `Assets/Scripts/ForceFeedBack/HapticCalibrationState.cs`
  - `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs`
  - `Assets/Scripts/UI/HapticCalibrationUI.cs`
- Inspector主要パラメータ（Prefab実体）
  - 共通（左右）: `stepSize=20`, `stepIntervalSeconds=0.05`, `allowedBaselineDeviation=120`, `requiredConsecutiveOutOfRange=3`, `baselineSamples=10`, `baselineSampleIntervalSeconds=0.02`, `initialCountdownSeconds=10`, `releasedValueOffset=-135`, `timeoutSeconds=6`
    - 右手: `Assets/Prefab/Hands/RightHand.prefab:153`
    - 左手: `Assets/Prefab/Hands/LeftHand.prefab:669`
- 失敗時の挙動
  - 参照未設定: Warningしてreturn `Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs:82`

---

## F-08: released値保持（セッション内）
- 機能ID: F-08
- 機能名: キャリブ結果保持（HapticCalibrationState）

**事実情報**
- 入力
  - `SetReleasedServoValue(fingerIndex, value)` `Assets/Scripts/ForceFeedBack/HapticCalibrationState.cs:45`
- 処理
  - `IsFullyCalibrated`（全指保存済み判定）`Assets/Scripts/ForceFeedBack/HapticCalibrationState.cs:22`
  - clamp 0..1000 `Assets/Scripts/ForceFeedBack/HapticCalibrationState.cs:49`
- 出力
  - `TryGetReleasedServoValue(fingerIndex, out value)` `Assets/Scripts/ForceFeedBack/HapticCalibrationState.cs:56`
  - `GetReleasedValuesCopyOrNull()`（未完了ならnull）`Assets/Scripts/ForceFeedBack/HapticCalibrationState.cs:64`
- 関連ファイル
  - `Assets/Scripts/ForceFeedBack/HapticCalibrationState.cs`

---

## F-09: ランタイム触覚目標値生成（curl/接触/底面→サーボ目標）
- 機能ID: F-09
- 機能名: 触覚追従制御（HapticRuntimeFeedbackController）

**事実情報**
- 入力
  - `HandCurlTracker.curl01` `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:206`
  - `HapticCalibrationState.released` `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:139`
  - `PianoFingerContactRegistry.TryGetFingerState(...)`（接触/底面）`Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:135`
- 処理
  - `Update()`（フレーム単位）`Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:103`
  - 未キャリブ（`IsFullyCalibrated==false`）: 全指0送信 + stateクリア `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:118`
  - `t = invertCurl ? (1-curl) : curl` + `tStrength01` `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:141`
  - 目標 `desired = Lerp(released, servoMax, t)`（0..1000クランプ）`Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:145`
  - gap:
    - Ratioモード: `(max - released) * ratio` `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:148`
    - FixedUnitsモード: `airGapUnits/pianoGapUnits` `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:156`
  - 底面ロック: `freezeWhileBottomLocked` で値保持 `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:165`
  - deadband: 差分 `deadbandUnits` 未満は更新抑制 `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:179`
  - `serialSender.currentFingerTargets` へ書込み `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:200`
- 出力
  - `HapticSerialSender.currentFingerTargets[int[5]]`（Thumb..Pinky）`Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:29`
- 関連ファイル
  - `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs`
  - `Assets/Scripts/Piano/PianoFingerContactRegistry.cs`
  - `Assets/Scripts/ForceFeedBack/HapticCalibrationState.cs`
  - `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs`
- Inspector主要パラメータ（Scene実体）
  - 左手: `handedness=0`, `gapMode=1(Ratio)`, `airGapRatio01=0.45`, `pianoGapRatio01=0.09`, `airGapUnits=300`, `pianoGapUnits=45`, `deadbandUnits=1`, `servoMax=1000`（`Assets/Scenes/hapticpiano/hapticpiano.unity:1685`）
  - 右手: `handedness=1`, 上記同値（`Assets/Scenes/hapticpiano/hapticpiano.unity:6369`）
  - `contactRegistry` は Scene上 null（fileID=0）で、`FindObjectOfType` で探索（`Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:93`）
- 失敗時の挙動
  - 参照未設定: return（`Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:106`）
  - 設定UIオープン中停止（任意）: `pauseWhileSettingsOpen`（`Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:108`）

---

## F-10: サーボ送信（目標値→チャンネル変換→シリアル送信）
- 機能ID: F-10
- 機能名: 触覚送信（HapticSerialSender）

**事実情報**
- 入力
  - `currentFingerTargets[int[5]]`（Thumb/Index/Middle/Ring/Pinky; 0..1000）`Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:29`
  - `SerialPortAdapter`（共有）`Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:16`
- 処理
  - `Update()`（フレーム単位）`Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:70`
  - キャリブガード: `calibrationState.IsFullyCalibrated` 必須 `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:253`
  - レート制限: `maxSendHz` `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:117`
  - 変化量抑制: `sendOnlyWhenChanged` + `minDeltaToSend` `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:128`
  - 指→チャンネル変換:
    - `FingerIndexToChannelIndex = {0,4,3,2,1}` `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:13`
    - チャンネル割り当て注記: `A=Thumb, B=Pinky, C=Ring, D=Middle, E=Index` `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:177`
  - エンコード: `SerialPacketCodec.Encode(channelTargets)` `Assets/Scripts/IO/SerialPacketCodec.cs:17`
  - 送信: `serialAdapter.TryWriteLine(encoded)` `Assets/Scripts/IO/SerialPortAdapter.cs:124`
  - 安全初期化/停止: Open時0送信（`TrySendZeroOnOpen`）/ 終了時0送信（`TrySendZeroOnShutdownIfPossible`）`Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:83`
- 出力
  - シリアル送信行（例: `A####B####C####D####E####`）`Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:6`
- 関連ファイル
  - `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs`
  - `Assets/Scripts/IO/SerialPacketCodec.cs`
  - `Assets/Scripts/IO/SerialPortAdapter.cs`
- Inspector主要パラメータ
  - `maxSendHz` 既定 `30f` `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:36`
  - Prefab内は `enableSend=1`（左右）:
    - 左: `Assets/Prefab/Hands/LeftHand.prefab:644`
    - 右: `Assets/Prefab/Hands/RightHand.prefab:128`
- 失敗時の挙動
  - 未キャリブ: Warning +送信停止 `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:101`
  - `serialAdapter==null`/未Open: status更新してreturn `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:73`

---

## F-11: ピアノ鍵盤の物理押下判定（角度ベース）＋音＋押下イベント
- 機能ID: F-11
- 機能名: 物理押下判定（PianoKey / PianoKeyController）

**事実情報**
- 入力
  - `transform.eulerAngles.x`（0→360補正）`Assets/Scripts/Piano/PianoKey.cs:128`
  - 判定閾値（enter/exit）: `PianoKeyController.PhysicalPressEnterAngleX/Exit` `Assets/Scripts/Piano/PianoKey.cs:130`
- 処理
  - `PianoKey.Update()` 内で `KeyMode.Physical` のとき判定 `Assets/Scripts/Piano/PianoKey.cs:121`
  - `x<=enter` で押下扱い → `Pressed?.Invoke(NoteName)` `Assets/Scripts/Piano/PianoKey.cs:148`
  - `PianoKeyController.IsPhysicalPressSuppressed` 中は押下イベント抑制 `Assets/Scripts/Piano/PianoKey.cs:137`
- 出力
  - `PianoKey.Pressed` イベント（string noteName）`Assets/Scripts/Piano/PianoKey.cs:26`
  - AudioSource再生（物理/デモで分岐）`Assets/Scripts/Piano/PianoKey.cs:146`
- 関連ファイル
  - `Assets/Scripts/Piano/PianoKey.cs`
  - `Assets/Scripts/Piano/PianoKeyController.cs`
- Inspector主要パラメータ（Scene実体）
  - `PhysicalPressEnterAngleX=359.5`, `PhysicalPressExitAngleX=359.8`（`Assets/Scenes/hapticpiano/hapticpiano.unity:3147`）
  - `SuppressPhysicalPressSecondsAfterForShow=0.35`（`Assets/Scenes/hapticpiano/hapticpiano.unity:3149`）
  - `PrewarmAudioSourcesPerKey=2`（`Assets/Scenes/hapticpiano/hapticpiano.unity:3154`）
- 失敗時の挙動
  - `PianoKeyController` null時: enter/exit は既定値へフォールバック `Assets/Scripts/Piano/PianoKey.cs:130`

---

## F-12: MIDI再生（StreamingAssets .mid）→ノート列→鍵盤Play
- 機能ID: F-12
- 機能名: MIDI再生（MidiPlayer + MidiFileInspector）

**事実情報**
- 入力
  - MIDIファイル: `Application.streamingAssetsPath/MIDI/<name>.mid` `Assets/Scripts/Piano/MidiPlayer.cs:123`
  - MIDI解析: `Assets/Plugins/NAudio/NAudio.dll`（存在） 
- 処理
  - `MidiPlayer.Start()` で自動再生（条件付き）`Assets/Scripts/Piano/MidiPlayer.cs:33`
  - 評価シーン検出で自動再生抑制: `FindObjectOfType<EvaluationTaskController>() != null` `Assets/Scripts/Piano/MidiPlayer.cs:58`
  - `MidiFileInspector.GetNotes()` でノート列生成 `Assets/Scripts/NAudio/MidiFileInspector.cs:24`
  - `MidiPlayer.Update()` で時刻に応じて `PianoKey.Play()` `Assets/Scripts/Piano/MidiPlayer.cs:70`
- 出力
  - `PianoKey.Play()`（ForShowモードで視覚/音）`Assets/Scripts/Piano/MidiPlayer.cs:84`
- 関連ファイル
  - `Assets/Scripts/Piano/MidiPlayer.cs`
  - `Assets/Scripts/NAudio/MidiFileInspector.cs`
  - `Assets/Plugins/NAudio/NAudio.dll`
- Inspector主要パラメータ（Scene実体）
  - `AutoPlayOnStart=1`, `DisableAutoPlayIfEvaluationScene=1`, `GlobalSpeed=1`（`Assets/Scenes/hapticpiano/hapticpiano.unity:12253`）

---

## F-13: 評価タスク実行（条件切替・スケジュール・ガイド・デモ）
- 機能ID: F-13
- 機能名: 評価タスク制御（EvaluationTaskController）

**事実情報**
- 入力
  - 参加者情報: `participantId`, `participantName`, `group` `Assets/Scripts/Evaluation/EvaluationTaskController.cs:28`
  - ピアノ押下イベント: `PianoKey.Pressed` を購読 `Assets/Scripts/Evaluation/EvaluationTaskController.Runtime.cs:311`
  - MIDI（Twinkle）: `StreamingAssets/MIDI/<twinkleMidiFileNameNoExt>.mid` `Assets/Scripts/Evaluation/EvaluationTaskController.Twinkle.cs:35`
- 処理（主要）
  - Updateループ: countdown→intro→task tick `Assets/Scripts/Evaluation/EvaluationTaskController.cs:176`
  - グループスケジュール:
    - A: Accuracy off→on, Twinkle off→on `Assets/Scripts/Evaluation/EvaluationTaskController.Schedule.cs:124`
    - B: Accuracy on→off, Twinkle on→off `Assets/Scripts/Evaluation/EvaluationTaskController.Schedule.cs:133`
  - 休憩カウントダウン: `countdownSeconds` `Assets/Scripts/Evaluation/EvaluationTaskController.cs:112`
  - 開始直前カウントダウン: `taskIntroSeconds` `Assets/Scripts/Evaluation/EvaluationTaskController.cs:122`
  - 触覚条件適用: `ApplyHapticsForCondition`（TouchOffで enableSend=false）`Assets/Scripts/Evaluation/EvaluationTaskController.Runtime.cs:217`
  - デモ:
    - Training MIDI demo: `PlayTrainingMidiDemoOnce` `Assets/Scripts/Evaluation/EvaluationTaskController.Demo.cs:29`
    - Accuracy pattern demo: `PlayAccuracyPatternDemoOnce` `Assets/Scripts/Evaluation/EvaluationTaskController.Demo.cs:54`
- 出力
  - タスクイベント（trial/press）を logger に渡す（F-14）
- Inspector主要パラメータ（Scene実体）
  - `participantId=P01`, `group=A(0)`, `requireExplicitGroupSelection=1`（`Assets/Scenes/hapticpiano/hapticpiano.unity:2513`）
  - `bpm=60`, `countdownSeconds=60`, `taskIntroSeconds=5`, `taskEndDelaySeconds=3`（`Assets/Scenes/hapticpiano/hapticpiano.unity:2513`）
  - `accuracySetCount=3`, `noteOctaveOffset=-1`（`Assets/Scenes/hapticpiano/hapticpiano.unity:2513`）
- 失敗時の挙動
  - `midiPlayer==null` など: Warningしてreturn（Demo等）`Assets/Scripts/Evaluation/EvaluationTaskController.Demo.cs:33`

---

## F-14: 評価ログ（CSV: session_meta/task_summary/events）
- 機能ID: F-14
- 機能名: 評価ログ出力（EvaluationLogging）

**事実情報**
- 入力
  - セッション開始: `new EvaluationLogSession(participantId, participantName, group)` `Assets/Scripts/Evaluation/EvaluationTaskController.Runtime.cs:204`
  - trial/press: `EvaluationLogTask.LogTrial/LogPress` `Assets/Scripts/Evaluation/EvaluationLogging.cs:160`
- 処理
  - 保存先: `Application.persistentDataPath/EvaluationLogs/<participant_id>/<run_id>/` `Assets/Scripts/Evaluation/EvaluationLogging.cs:28`
  - `session_meta.csv` ヘッダ: `created_time,participant_id,participant_name,group` `Assets/Scripts/Evaluation/EvaluationLogging.cs:37`
  - `task_summary.csv` ヘッダ: `start_time,end_time,participant_id,condition,task` `Assets/Scripts/Evaluation/EvaluationLogging.cs:34`
  - `{task}_{condition}_{task_instance_id}_events.csv` ヘッダ: `event_time,event_type,trial_index,beat_time,target_key,pressed_key` `Assets/Scripts/Evaluation/EvaluationLogging.cs:155`
  - 新規作成はUTF-8 BOM（Excel想定）`Assets/Scripts/Evaluation/EvaluationLogging.cs:67`
- 出力
  - CSVファイル（上記3種）
- 関連ファイル
  - `Assets/Scripts/Evaluation/EvaluationLogging.cs`
  - `docs/requirements/feature/evaluation-thesis-experiment.md:62`（保存先/カラム要件）

---

## F-15: 設定UI（Overlay開閉・COM設定・キャリブUI・評価UI）
- 機能ID: F-15
- 機能名: 設定/操作UI群

**事実情報（抜粋）**
- Settings overlay開閉:
  - `SettingsOverlayOpener`（openKey, panelRoot, hintRoot, Time.timeScale操作等）`Assets/Scripts/UI/SettingsOverlayOpener.cs`
  - Scene設定 `openKey=27` `Assets/Scenes/hapticpiano/hapticpiano.unity:11028`
- COM設定:
  - `SerialSettingsUI`（left/right receiverへ `SetPortNameAndReconnect`）`Assets/Scripts/UI/SerialSettingUI.cs:97`
- キャリブUI:
  - `HapticCalibrationUI`（rightController/leftController切替、VR overlay複製生成等）`Assets/Scripts/UI/HapticCalibrationUI.cs`
- 評価UI:
  - `EvaluationSettingsUI`（タスク選択/開始/中止/グループ表示）`Assets/Scripts/UI/EvaluationSettingsUI.cs`
  - `EvaluationCountdownWorldUI`（説明/休憩/カウントダウンのワールド表示）`Assets/Scripts/UI/EvaluationCountdownWorldUI.cs`
