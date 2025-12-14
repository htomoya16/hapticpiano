# 卒論評価（触覚あり/なしの比較実験）

## 背景
- 本研究では、**触覚フィードバックの有無**が XR 仮想ピアノ演奏に与える影響を評価する。
- VR 実験では再現性（手順・刺激・ログ形式）が重要になるため、Unity 側で「タスク提示」と「ログ収集」を最小構成で提供する。

## 評価目的
- 触覚フィードバックの有無が
  - **打鍵精度（客観）**
  - **主観的体験（主観）**
  に与える影響を明らかにする。

## 実験条件（2条件）
- 条件：`touch_off`（触覚なし） / `touch_on`（触覚あり）
- 被験者内比較、順序はカウンターバランスする。

### グループ（順序割当）
本研究では「A/B グループ」は **触覚の有無そのもの**ではなく、
**実施順序（カウンターバランス）**を表す。

- Aグループ：
  - Accuracy：`touch_off → touch_on`
  - Twinkle：`touch_on → touch_off`
- Bグループ（逆順）：
  - Accuracy：`touch_on → touch_off`
  - Twinkle：`touch_off → touch_on`

## タスク構成
評価は「主要評価（客観）＋副次評価（主観）」で構成する。

### 1) 主要評価：打鍵精度タスク（Accuracy）
- テンポ：**60 BPM**（メトロノーム）
- 1拍＝1試行（trial）として扱う
- ターゲット系列（「ドレミファソラシドシラソファミレド」を1往復として3セット）
  - 1セット目：`C4 → D4 → E4 → F4 → G4 → A4 → B4 → C5 → B4 → A4 → G4 → F4 → E4 → D4 → C4`
  - 2セット目以降：先頭のドを除外して連結（`D4 → E4 → ... → D4 → C4`）
  - 60 BPM の場合：1セット目=15拍、2-3セット目=各14拍、合計43拍=43秒
- 各拍で **正解鍵を光らせる**（ガイド提示）
- ログを取得する

### 2) 副次評価：演奏タスク（Twinkle）
- きらきら星（Twinkle Little Star）
- ガイド（光る鍵盤）を提示する
- 余裕があればログも取得する（ターゲット提示＋押下イベント）

### 事前練習（トレーニング）
- 本評価の学習効果を切り離すためのフェーズ。
- **きらきら星は練習に含めない**。
- 代わりに「ガイドに従って演奏するイメージ」を掴むため、`MidiPlayer` で **デモ再生を1回**流す。

## タスク間インターバル（VR表示）
- 各タスク開始前（初回含む）に **20秒** のインターバルを設ける。
- インターバル中は VR 内に「次のタスク説明」と「開始までのカウントダウン」を表示する。

## ログ設計（CSV）
ログは `Application.persistentDataPath` 配下へ保存し、解析にそのまま使える CSV とする。

### 保存先
- `Application.persistentDataPath/EvaluationLogs/<participant_id>/<run_id>/`
  - `<run_id>` は UTC の `yyyyMMdd_HHmmss` で自動生成する。

### ファイル構成
- `session_meta.csv`（セッションのメタ情報）
  - 何のため：このフォルダ一式が「誰の」「どの順序割当（A/B）」の記録かを残す
  - いつ出る：ログフォルダ作成時（最初のタスク開始でセッション生成されたとき）
  - カラム：`created_time,participant_id,participant_name,group`
- `task_summary.csv`（タスクの要約：タスク1回=1行）
  - 何のため：タスクの開始/終了、条件、タスク種別の一覧（全体のインデックス）
  - いつ出る：各タスク終了時（中止/自動終了を含む）
  - カラム：`start_time,end_time,participant_id,condition,task`
- `{task}_{condition}_{task_instance_id}_trials.csv`（正解ターゲット：trial=1行）
  - 何のため：ガイドとして提示した「正解ターゲット」の時系列
  - いつ出る：各タスク開始時にファイル作成され、trialのたびに追記
  - カラム：`trial_index,beat_time,target_key`
- `{task}_{condition}_{task_instance_id}_presses.csv`（実入力：押下=1行）
  - 何のため：実際に押されたキーの時系列
  - いつ出る：各タスク開始時にファイル作成され、押下のたびに追記
  - カラム：`press_time,pressed_key`

補足（ファイル名の意味）
- `task`：`accuracy` / `twinkle`
- `condition`：`touch_off` / `touch_on`
- `task_instance_id`：同一セッション内で複数回タスクを回したときに、ファイル名が衝突しないよう付与する一意ID（UTC）

ファイル名の例
- `accuracy_touch_off_20251214_123456_123_trials.csv`
- `accuracy_touch_off_20251214_123456_123_presses.csv`

### trials / presses の意味
- `*_trials.csv`：その時点で提示した「正解ターゲット」側（Accuracy は拍、Twinkle はノート）
- `*_presses.csv`：実際の押下イベント側（押した時刻＋押したキー）

### 時刻の基準
- すべて UTC（ISO 8601）で保存する。
- `press_time`：鍵盤の物理押下判定が成立した瞬間（入力イベント発火タイミング）。
- `beat_time`：
  - Accuracy：メトロノームの「拍の開始」を狙った時刻（実装は realtime を UTC に変換）。
  - Twinkle：MIDI ノートを「処理してガイド提示した」タイミング（厳密な音声再生時刻ではない）。

## 既存実装（参照）
- タスク実行・ガイド・ログ：`Assets/Scripts/Evaluation/EvaluationTaskController.cs`
- CSV ロガー：`Assets/Scripts/Evaluation/EvaluationLogging.cs`
- 打鍵イベント（物理押下のみ）：`Assets/Scripts/Piano/PianoKey.cs`
- きらきら星 MIDI：`Assets/StreamingAssets/MIDI/twinkle_twinkle_60bpm_12bars.mid`
