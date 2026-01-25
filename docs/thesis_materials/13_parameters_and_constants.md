# 13_parameters_and_constants (numbers)

## レンジ/単位（共通）
| 項目 | 値 | 単位 | 定義場所 |
|---|---:|---|---|
| センサ生値クランプ上限 | 4095 | - | `Assets/Scripts/IO/SerialPacketCodec.cs:11` |
| サーボ送信クランプ上限 | 1000 | servo units | `Assets/Scripts/IO/SerialPacketCodec.cs:12` |
| curl正規化 | raw/4095.0 | - | `Assets/Scripts/Hands/Core/HandCurlTracker.cs:184` |

## シリアル（I/O）
| パラメータ | 値 | 単位 | 定義/設定場所 |
|---|---:|---|---|
| baudRate 既定 | 115200 | bps | `Assets/Scripts/Hands/Core/HandSensorReceiver.cs:14` |
| portName 既定 | COM5 | - | `Assets/Scripts/Hands/Core/HandSensorReceiver.cs:17` |
| 左手 portName（Prefab） | COM3 | - | `Assets/Prefab/Hands/LeftHand.prefab:404` |
| 右手 portName（Prefab） | COM5 | - | `Assets/Prefab/Hands/RightHand.prefab:787` |
| SerialPort NewLine | `\n` | - | `Assets/Scripts/IO/SerialPortAdapter.cs:48` |
| ReadTimeout / WriteTimeout | 5 / 5 | ms | `Assets/Scripts/IO/SerialPortAdapter.cs:49` |
| 受信デコード正規表現 | `A(\d{1,4})B...E(\d{1,4})` | - | `Assets/Scripts/IO/SerialPacketCodec.cs:15` |
| 空行ping間隔（既定） | 0.01 | s | `Assets/Scripts/IO/SerialEmptyLinePinger.cs:14` |
| 空行ping有効（Prefab） | 1 | bool | `Assets/Prefab/Hands/RightHand.prefab:854` |

## curlフィルタ
| パラメータ | 値 | 単位 | 定義/設定場所 |
|---|---:|---|---|
| filterAlpha 既定 | 0.25 | 0..1 | `Assets/Scripts/Hands/Core/HandCurlTracker.cs:32` |
| snapThreshold 既定 | 0.35 | 0..1 | `Assets/Scripts/Hands/Core/HandCurlTracker.cs:36` |
| noiseThreshold 既定 | 0.01 | 0..1 | `Assets/Scripts/Hands/Core/HandCurlTracker.cs:40` |
| HandCurlTrackerPreset.filterAlpha | 0.9 | 0..1 | `Assets/Settings/HandModel/Hand Curl Tracker Preset.asset` |
| HandCurlTrackerPreset.noiseThreshold | 0.008 | 0..1 | `Assets/Settings/HandModel/Hand Curl Tracker Preset.asset` |

## 手モデル（Kinematics）
| パラメータ | 値 | 単位 | 定義/設定場所 |
|---|---:|---|---|
| LinearKinematics max angles（asset） | 220（全指） | deg | `Assets/Settings/HandModel/KinematicsProfile/Linear Hand Kinematics Profile.asset` |
| HandVisualFromCurl joints数（Prefab） | thumb=4, 他=5 | - | `Assets/Prefab/Hands/RightHand.prefab:1007` |
| PC1 profile（asset）存在 | あり（参照: PC1CurveSet） | - | `Assets/Settings/HandModel/KinematicsProfile/Pc 1 Hand Kinematics Profile.asset` |
| PC1CurveSet curves | mcp(4)+pip(4) | AnimationCurve | `Assets/Settings/HandModel/PC1CurveSet.asset` |

### PC1CurveSet（要約: key数/範囲）
| curve | keys | time(min,max) | value(min,max) | 根拠 |
|---|---:|---|---|---|
| mcpIndex | 19 | 0.036668..0.940439 | -1492.7..561.91 | `Assets/Settings/HandModel/PC1CurveSet.asset` |
| mcpMiddle | 24 | 0.029583..0.978238 | -1800.22..983.232 | 同上 |
| mcpRing | 24 | 0.029851..0.981806 | -1441.32..1208.03 | 同上 |
| mcpPinky | 19 | 0.043798..0.988845 | -798.609..791.971 | 同上 |
| pipIndex | 18 | 0.035269..1 | -623.072..339.913 | 同上 |
| pipMiddle | 17 | 0.02421..0.951042 | -704.46..494.938 | 同上 |
| pipRing | 19 | 0.035269..0.995967 | -855.163..572.514 | 同上 |
| pipPinky | 16 | 0.035269..0.982144 | -352.907..481.476 | 同上 |

## 鍵盤接触/底面（PianoFingerContactRegistry）
| パラメータ | 値 | 単位 | 定義/設定場所 |
|---|---:|---|---|
| requiredSegmentIndex 既定 | 0 | - | `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:10` |
| bottomEnterAngleX 既定 | 352.5 | deg(補正後) | `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:14` |
| bottomExitAngleX 既定 | 354.0 | deg(補正後) | `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:17` |
| touchReleaseGraceSeconds 既定 | 0.08 | s | `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:24` |
| Scene上 logChanges | 1 | bool | `Assets/Scenes/hapticpiano/hapticpiano.unity:6561` |

## ランタイム触覚（HapticRuntimeFeedbackController）
| パラメータ | 値 | 単位 | 定義/設定場所 |
|---|---:|---|---|
| servoMax 既定 | 1000 | servo units | `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:49` |
| gapMode 既定 | RatioOfRange | enum | `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:53` |
| airGapRatio01 既定 | 0.30 | 0..1 | `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:65` |
| pianoGapRatio01 既定 | 0.005 | 0..1 | `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:69` |
| deadbandUnits 既定 | 4 | servo units | `Assets/Scripts/ForceFeedBack/HapticRuntimeFeedbackController.cs:77` |
| Scene airGapRatio01 | 0.45 | 0..1 | `Assets/Scenes/hapticpiano/hapticpiano.unity:1689` |
| Scene pianoGapRatio01 | 0.09 | 0..1 | `Assets/Scenes/hapticpiano/hapticpiano.unity:1694` |
| Scene deadbandUnits | 1 | servo units | `Assets/Scenes/hapticpiano/hapticpiano.unity:1688` |

## 送信（HapticSerialSender）
| パラメータ | 値 | 単位 | 定義/設定場所 |
|---|---:|---|---|
| maxSendHz 既定 | 30 | Hz | `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:36` |
| minDeltaToSend 既定 | 2 | servo units | `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:42` |
| 指→チャンネル変換 | {0,4,3,2,1} | index map | `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:13` |

## 評価（EvaluationTaskController）
| パラメータ | 値 | 単位 | 定義/設定場所 |
|---|---:|---|---|
| bpm 既定 | 60 | BPM | `Assets/Scripts/Evaluation/EvaluationTaskController.cs:51` |
| accuracySetCount 既定 | 3 | - | `Assets/Scripts/Evaluation/EvaluationTaskController.cs:75` |
| noteOctaveOffset 既定 | -1 | octave | `Assets/Scripts/Evaluation/EvaluationTaskController.cs:79` |
| twinkleMidiFileNameNoExt 既定 | twinkle_twinkle_60bpm_12bars | - | `Assets/Scripts/Evaluation/EvaluationTaskController.cs:83` |
| countdownSeconds 既定 | 60 | s | `Assets/Scripts/Evaluation/EvaluationTaskController.cs:112` |
| taskIntroSeconds 既定 | 5 | s | `Assets/Scripts/Evaluation/EvaluationTaskController.cs:122` |
| taskEndDelaySeconds 既定 | 3 | s | `Assets/Scripts/Evaluation/EvaluationTaskController.cs:126` |
| 触覚切替（enableSend） | TouchOn=true / TouchOff=false | bool | `Assets/Scripts/Evaluation/EvaluationTaskController.Runtime.cs:221` |

## Editorユーティリティ（手動実行）
| 項目 | 値 | 単位 | 定義場所 |
|---|---:|---|---|
| 鍵盤コライダ生成対象Prefab | `Assets/Prefab/Piano/PianoKeys.prefab` | - | `Assets/Editor/FitPianoKeyCollider.cs:25` |
| 白鍵手前割合 | 0.45 | ratio | `Assets/Editor/FitPianoKeyCollider.cs:12` |
| 欠け削り割合（片側/両側） | 0.35 / 0.55 | ratio | `Assets/Editor/FitPianoKeyCollider.cs:14` |
| 白鍵高さ倍率 | 0.5 | ratio | `Assets/Editor/FitPianoKeyCollider.cs:19` |
| PC1CurveSet生成/更新 | `Assets/Settings/HandModel/PC1CurveSet.asset` | - | `Assets/Editor/PC1CurveImporter.cs:19` |
