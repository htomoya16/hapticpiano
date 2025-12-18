# 90_traceability_map

## 論文に書く内容 → 根拠（例）
| 論文に書く内容 | 根拠ファイル | 備考 |
|---|---|---|
| Unityバージョン | `ProjectSettings/ProjectVersion.txt:1` | `2022.3.62f3` |
| 主要シーン | `docs/requirements/000-overview.md:12` | `hapticpiano.unity` |
| OpenVR導入（package） | `Packages/manifest.json:12` | local tgz 1.2.4 |
| OpenVR action manifest | `Assets/XR/Settings/OpenVRSettings.asset:21` | `StreamingAssets\\SteamVR\\actions.json` |
| SteamVR actions定義 | `Assets/StreamingAssets/SteamVR/actions.json` | `/actions/default/in/...` |
| シリアル受信/送信方式 | `Assets/Scripts/IO/SerialPortAdapter.cs:28` | `SerialPort` open/read/write |
| パケット形式（A..E 5ch） | `Assets/Scripts/IO/SerialPacketCodec.cs:15` | Regex定義 |
| センサ値レンジ（0..4095） | `Assets/Scripts/IO/SerialPacketCodec.cs:11` | `SensorRawMax` |
| サーボ値レンジ（0..1000） | `Assets/Scripts/IO/SerialPacketCodec.cs:12` | `ServoMax` |
| 指→チャンネル割当 | `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:177` | A=Thumb, B=Pinky, C=Ring, D=Middle, E=Index |
| 送信レート上限（既定） | `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs:36` | `maxSendHz=30` |
| キャリブ方式（0→1000 sweep + sensor逸脱） | `Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs:23` | step/interval/判定閾値 |
| releasedオフセット | `Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs:51` | `releasedValueOffset=-135` |
| ランタイム触覚（gap ratio）Scene値 | `Assets/Scenes/hapticpiano/hapticpiano.unity:1689` | `airGapRatio01=0.45` |
| 鍵盤接触TIPのみ | `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:10` | `requiredSegmentIndex=0` |
| 底面判定角度 | `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:14` | enter/exit |
| 物理押下判定角度 | `Assets/Scripts/Piano/PianoKeyController.cs:28` | enter/exit |
| 評価条件（touch_on/off） | `docs/requirements/feature/evaluation-thesis-experiment.md:14` | 2条件 |
| A/B順序（コード） | `Assets/Scripts/Evaluation/EvaluationTaskController.Schedule.cs:124` | group A steps |
| 休憩60秒・開始前5秒 | `docs/requirements/feature/evaluation-thesis-experiment.md:54` | 要件記載 |
| 休憩60秒・開始前5秒（実装） | `Assets/Scripts/Evaluation/EvaluationTaskController.cs:112` | countdown/taskIntro |
| Accuracy: 60BPM | `Assets/Scripts/Evaluation/EvaluationTaskController.cs:51` | bpm default |
| Accuracy: パターン固定 | `Assets/Scripts/Evaluation/EvaluationTaskController.Accuracy.cs:89` | `EnsureAccuracyPattern` |
| Twinkle: 時間制限なし | `Assets/Scripts/Evaluation/EvaluationTaskController.Twinkle.cs:61` | コメント |
| ログ保存先 | `Assets/Scripts/Evaluation/EvaluationLogging.cs:28` | persistentDataPath |
| events.csv 列定義 | `Assets/Scripts/Evaluation/EvaluationLogging.cs:155` | header文字列 |

## 根拠が見つからない項目（明示）
- HMDの具体モデル名（Index/Vive等）: 未確認（Scene/Prefab/Docsに明記なし）
- PCスペック（CPU/GPU/RAM）: 未確認
- OSバージョン: 未確認（ただし設定値に Windowsパス/COM名が存在）
- グローブの構成詳細（センサ種類/配置/サーボ取り付け）: 未確認（要件docはMG90S/ESP32レベル）
- ESP32ファームウェアの実装詳細/書き込み手順: 本リポジトリ外（`AGENTS.md:13`）
- 実験被験者数/募集条件/倫理手続き: 未確認
- 主観評価（質問紙項目/尺度）: 未確認（要件docは「主観」語のみで具体項目なし）
- 解析指標の計算式（正答判定・誤差定義など）: 未確認（ログは生データ提供のみ）

## 抽出できた情報一覧
- Unity/Packages/OpenVR/SteamVR actions（環境・設定値）
- シリアルI/O（形式、レンジ、ポート設定、更新箇所）
- curl生成（正規化・フィルタ・プリセット値）
- 手モデル可視化（LateUpdate、kinematicsProfile参照、関節割当）
- 指コライダ生成（CapsuleCollider生成条件、半径等）
- 鍵盤接触/底面判定（角度閾値、ヒステリシス、TIPフィルタ）
- ランタイム触覚（gap、deadband、底面フリーズ、Scene実値）
- キャリブレーション（手順パラメータ、released保存）
- 評価タスク（条件、A/B順序、カウントダウン、Accuracy/Twinkle/デモ）
- 評価ログ（保存先、ファイル構成、CSV列）

## 抽出できなかった情報一覧（実験後に人が補う必要）
- 実機構成（HMD/PC/グローブ配線/サーボ治具）
- 実験プロトコルの最終版（参加者説明、同意、主観質問紙）
- 実験データ（ログ実ファイル、結果値）
- 解析手順（スクリプト、前処理、指標定義、統計手法）

## 卒論執筆時に注意すべき「未確定要素」
- `HandVisualFromCurl.preset` の参照GUIDがAssets配下で解決できない（prefab内にGUID参照のみ存在）: 例 `Assets/Prefab/Hands/RightHand.prefab:1041`
- `FingerColliderBuilder.handedness` がPrefab内に明示設定されていない（コード既定値依存）: `Assets/Scripts/Hands/Colliders/FingerColliderBuilder.cs:38`
- Twinkle用ソルフェージュ表示文字列がScene内で改行が不規則（Scene値）: `Assets/Scenes/hapticpiano/hapticpiano.unity:2616`
