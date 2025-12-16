# story: 卒論評価タスク（ガイド提示＋ログ収集）

## 背景
- 触覚あり/なしを比較するため、統一手順でタスクを提示し、客観ログを残す必要がある。

## 目的
- 実験手順の再現性を高めるため、Unity 上で
  - メトロノーム（60BPM）
  - ターゲット鍵のガイド提示（点灯）
  - 押下イベントログ
  を最小構成で提供する。

## スコープ
- `hapticpiano.unity` で評価タスクを実行できること
- `touch_on/touch_off` の条件切り替えにより、触覚送信（`HapticSerialSender.enableSend`）が切り替わること
- CSV ログが要件のカラムで出力されること
- A/B グループとして順序割当（カウンターバランス）を運用できること

## 非スコープ
- 統計解析コード、アンケート UI、被験者同意 UI
- きらきら星の「演奏正誤（正しい音列か）」の自動判定

## 受け入れ条件

### 1. 事前練習（デモ）
1. デモとして、`MidiPlayer` により **きらきら星のデモ再生**を開始/中止できること。
2. デモとして、**打鍵精度パターンのデモ**を開始/中止できること（3セット分）。

### 2. Accuracy（主要評価）
1. 60 BPM のメトロノームに同期して 1拍=1trial で進行すること。
2. 各 trial でターゲット鍵が点灯し、ターゲット系列が反復されること：
   - 1セット目（1往復=15拍）: `C4 → D4 → E4 → F4 → G4 → A4 → B4 → C5 → B4 → A4 → G4 → F4 → E4 → D4 → C4`
   - 2セット目以降：先頭のドを除外して連結（`D4 → E4 → ... → D4 → C4`）
   - 上記を **3セット** 行うこと（60 BPMなら合計43拍=43秒の想定）
3. タスクが規定のセット数（拍数）で終了できること。
4. タスク中の押下イベントが記録されること（押下時刻＋押下キー）。

### 3. Twinkle（副次評価）
1. きらきら星（`Assets/StreamingAssets/MIDI/twinkle_twinkle_60bpm_12bars.mid`）に基づくタスクを実行できること。
2. タスク中、ターゲット鍵のガイド提示（点灯）は行わないこと（自由に演奏してもらう運用）。
3. ターゲット提示（trial）と押下イベントがログに記録できること（トグルで ON/OFF 可能）。
4. 時間制限は設けず、タスク終了は操作（次タスクへ）で行えること。

### 4. ログ（CSV）
1. ログの保存先が `Application.persistentDataPath` 配下であること：
   - `Application.persistentDataPath/EvaluationLogs/<participant_id>/<run_id>/`
2. セッションメタが出力されること：
   - `session_meta.csv`
   - 何のため：このフォルダ一式が「誰の」「どの順序割当（A/B）」の記録かを残す
   - `created_time,participant_id,participant_name,group`
3. タスク単位のログが 1 行/タスクで出力されること：
   - `task_summary.csv`
   - 何のため：タスクの開始/終了、条件、タスク種別の一覧（全体のインデックス）
   - `start_time,end_time,participant_id,condition,task`
4. イベント（trial/press）ログが 1 ファイルで出力されること：
   - `{task}_{condition}_{task_instance_id}_events.csv`
   - 何のため：正解ターゲット提示と実押下を時系列でまとめる
   - `event_time,event_type,trial_index,beat_time,target_key,pressed_key`
5. 時刻が UTC（ISO 8601）で保存されること。

補足（意味）
- `event_type=trial` は「その時点で提示した正解ターゲット」側、`event_type=press` は「実際の押下」側。
- Twinkle の `beat_time` は「ノートを処理してガイド提示したタイミング」で、厳密な音声再生時刻ではない。

## 実装参照（現状）
- `Assets/Scripts/Evaluation/EvaluationTaskController.cs`
- `Assets/Scripts/Evaluation/EvaluationLogging.cs`

## 運用メモ（A/Bグループの順序）
- Aグループ：
  - Accuracy：`touch_off → touch_on`
  - Twinkle：`touch_off → touch_on`
- Bグループ（逆順）：
  - Accuracy：`touch_on → touch_off`
  - Twinkle：`touch_on → touch_off`

## 運用メモ（タスク間インターバルとVR表示）
- 各タスク開始前（**初回含む**）に **60秒** のインターバルを設ける。
- インターバル中は、VR 内に「次のタスク説明」「休憩時間（残り秒）」「補足説明（文章＋図）」を表示する。
- インターバル終了後、開始直前に **5秒カウントダウン** を行う（表示は `5,4,3,2,1` の数字のみ）。
- インターバル中は「休憩スキップ」ボタンで直前5秒カウントダウンへ進められる。
- きらきら星（演奏）タスク中は「次へ」ボタンで次タスクへ進められる。
