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
1. 事前練習として、`MidiPlayer` により **デモ再生を1回**流せること。
2. **きらきら星の本番タスクは練習に含めない**運用であることが docs 上で明確であること。

### 2. Accuracy（主要評価）
1. 60 BPM のメトロノームに同期して 1拍=1trial で進行すること。
2. 各 trial でターゲット鍵が点灯し、ターゲット系列が反復されること：
   - `C4 → D4 → E4 → F4 → G4 → F4 → E4 → D4 → C4`
3. タスク時間が規定秒数（例: 30秒）で終了できること。
4. タスク中の押下イベントが記録されること（押下時刻＋押下キー）。

### 3. Twinkle（副次評価）
1. きらきら星（`Assets/StreamingAssets/MIDI/twinkle_twinkle_60bpm_12bars.mid`）に基づいて、ターゲット鍵が点灯すること。
2. 余裕があれば、ターゲット提示（trial）と押下イベントがログに記録できること（トグル等で ON/OFF 可能）。

### 4. ログ（CSV）
1. タスク単位のログが 1 行/タスクで出力されること：
   - `start_time,end_time,participant_id,condition,task`
2. trial ログが 1 行/trial で出力されること：
   - `trial_index,beat_time,target_key`
3. 押下ログが 1 行/押下で出力されること：
   - `press_time,pressed_key`
4. ログの保存先が `Application.persistentDataPath` 配下であること。

## 実装参照（現状）
- `Assets/Scripts/Evaluation/EvaluationTaskController.cs`
- `Assets/Scripts/Evaluation/EvaluationLogging.cs`

## 運用メモ（A/Bグループの順序）
- Aグループ：
  - Accuracy：`touch_off → touch_on`
  - Twinkle：`touch_on → touch_off`
- Bグループ（逆順）：
  - Accuracy：`touch_on → touch_off`
  - Twinkle：`touch_off → touch_on`

## 運用メモ（タスク間インターバルとVR表示）
- 各タスク開始前（**初回含む**）に **20秒** のインターバルを設ける。
- インターバル中は、VR 内に「次のタスク説明」と「開始までのカウントダウン」を表示する。
