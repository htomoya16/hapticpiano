# 20_evaluation_design_facts (facts only)

## 評価条件（触覚あり/なし）
- 条件ID:
  - `touch_on` / `touch_off`（要件doc）`docs/requirements/feature/evaluation-thesis-experiment.md:14`
- 実装上の切替
  - `EvaluationCondition.TouchOn/TouchOff` `Assets/Scripts/Evaluation/EvaluationTaskController.cs:8`
  - 条件適用: `ApplyHapticsForCondition` が `HapticSerialSender.enableSend` を変更 `Assets/Scripts/Evaluation/EvaluationTaskController.Runtime.cs:217`
  - TouchOffへ切替時の relax（任意）:
    - `TryRelaxAllSendersOnce()`（released優先/なければ0）`Assets/Scripts/Evaluation/EvaluationTaskController.Runtime.cs:256`

## A/B グループ（順序割当）
- 要件doc記載:
  - A: Accuracy off→on, Twinkle off→on `docs/requirements/feature/evaluation-thesis-experiment.md:22`
  - B: Accuracy on→off, Twinkle on→off `docs/requirements/feature/evaluation-thesis-experiment.md:25`
- 実装（ScheduleStep配列）:
  - A: `Assets/Scripts/Evaluation/EvaluationTaskController.Schedule.cs:124`
  - B: `Assets/Scripts/Evaluation/EvaluationTaskController.Schedule.cs:133`
- グループ明示選択ガード:
  - `requireExplicitGroupSelection` `Assets/Scripts/Evaluation/EvaluationTaskController.cs:37`
  - Scene値 `requireExplicitGroupSelection=1` `Assets/Scenes/hapticpiano/hapticpiano.unity:2530`

## タスク一覧（実装上）
### 事前練習（デモ）
- Training MIDI demo（きらきら星）:
  - `PlayTrainingMidiDemoOnce()` `Assets/Scripts/Evaluation/EvaluationTaskController.Demo.cs:29`
  - `MidiPlayer.KeyMode = ForShow` `Assets/Scripts/Evaluation/EvaluationTaskController.Demo.cs:92`
- Accuracy pattern demo:
  - `PlayAccuracyPatternDemoOnce()` `Assets/Scripts/Evaluation/EvaluationTaskController.Demo.cs:54`

### Accuracy（打鍵精度タスク）
- 進行:
  - 1拍=1trial、`bpm` で拍生成 `Assets/Scripts/Evaluation/EvaluationTaskController.Accuracy.cs:44`
- ターゲット系列:
  - `accuracyPattern`（C4..）`Assets/Scripts/Evaluation/EvaluationTaskController.cs:68`
  - `accuracySetCount`（3セット）`Assets/Scripts/Evaluation/EvaluationTaskController.cs:75`
  - plannedTrials算出 `GetAccuracyPlannedTrials()` `Assets/Scripts/Evaluation/EvaluationTaskController.Accuracy.cs:99`
- 終了条件
  - plannedTrials到達で終了予約（`taskEndDelaySeconds`）`Assets/Scripts/Evaluation/EvaluationTaskController.Accuracy.cs:62`

### Twinkle（きらきら星演奏タスク）
- 入力MIDI:
  - `twinkleMidiFileNameNoExt` `Assets/Scripts/Evaluation/EvaluationTaskController.cs:83`
- 終了条件
  - 自動終了なし（ユーザー操作で終了）`Assets/Scripts/Evaluation/EvaluationTaskController.Twinkle.cs:61`

## タスク間インターバル（カウントダウン）
- 休憩（各タスク前）:
  - `countdownSeconds`（Scene: 60）`Assets/Scenes/hapticpiano/hapticpiano.unity:2556`
  - スキップ: `SkipRestCountdown()` `Assets/Scripts/Evaluation/EvaluationTaskController.Schedule.cs:8`
- 開始直前カウントダウン:
  - `taskIntroSeconds`（Scene: 5）`Assets/Scenes/hapticpiano/hapticpiano.unity:2566`

## 被験者操作（Unity UI / 入力）
- 設定パネル開閉:
  - `SettingsOverlayOpener.openKey` Scene値 `27` `Assets/Scenes/hapticpiano/hapticpiano.unity:11028`
- 参加者情報入力:
  - `EvaluationSettingsUI`（participantId/Name入力欄）`Assets/Scripts/UI/EvaluationSettingsUI.cs`
- COMポート変更:
  - `SerialSettingsUI`（left/right）`Assets/Scripts/UI/SerialSettingUI.cs:97`
- 触覚キャリブレーション:
  - `HapticCalibrationUI.StartCalibration/Cancel/Reset` `Assets/Scripts/UI/HapticCalibrationUI.cs`
- タスク開始/中止:
  - `EvaluationSettingsUI`（Start/Abort/Next等）`Assets/Scripts/UI/EvaluationSettingsUI.cs`

## VR内表示（説明/休憩/現在タスク）
- `EvaluationCountdownWorldUI` Scene文字列（そのまま）
  - accuracyInstructionText: `Assets/Scenes/hapticpiano/hapticpiano.unity:2608`
  - twinkleInstructionText: `Assets/Scenes/hapticpiano/hapticpiano.unity:2609`
  - defaultInstructionText: `Assets/Scenes/hapticpiano/hapticpiano.unity:2610`
  - twinkleSolfege: `Assets/Scenes/hapticpiano/hapticpiano.unity:2616`

### accuracyInstructionText（decoded）
このタスクでは、"人差し指だけ"を使ってください。
右図のように、光って（緑になって）いる鍵盤をテンポに合わせてタッチしてください。

手首を大きく振るような動きは避けてください。
指を中心に動かすよう意識してください。

間違えてしまっても止まらず、そのまま続けてください。
このあと5秒のカウントダウンが始まります
タスクは5秒のカウントダウンのあとに始まります。


### twinkleInstructionText（decoded）
このタスクでは、指使いは自由です。
「きらきら星」を自由に演奏してください。
テンポや速さに決まりはありません。
楽譜はタスク中表示されます。
演奏が終わったら、伝えてください。

手首を大きく振るような動きは避けてください。
指を中心に動かすよう意識してください。

間違えても弾き直す必要はありません
このあと5秒のカウントダウンが始まります
タスクは5秒のカウントダウンのあとに始まります。


### twinkleSolfege（decoded, Scene値）
ド ド ソ ソ ラ ラ ソ
ファ
ファ ミ ミ レ レ ド
ソ ソ ファ ファ
ミ ミ レ
ソ ソ ファ ファ ミ ミ レ
ド
ド ソ ソ ラ ラ ソ
ファ ファ ミ ミ
レ レ ド