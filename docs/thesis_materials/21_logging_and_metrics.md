# 21_logging_and_metrics

## ログ出力の有無
- あり（評価タスク用）: `Assets/Scripts/Evaluation/EvaluationLogging.cs`
- 触覚/シリアルのログ: Unity Console（Debug.Log/Warning）中心（例: `Assets/Scripts/IO/SerialPortAdapter.cs:57`）

## ログ形式
- CSV
- 保存先:
  - `Application.persistentDataPath/EvaluationLogs/<participant_id>/<run_id>/` `Assets/Scripts/Evaluation/EvaluationLogging.cs:28`
  - `<run_id>`: UTC `yyyyMMdd_HHmmss` `Assets/Scripts/Evaluation/EvaluationLogging.cs:29`

## ファイル一覧（評価）
- `session_meta.csv`
  - header: `created_time,participant_id,participant_name,group` `Assets/Scripts/Evaluation/EvaluationLogging.cs:37`
- `task_summary.csv`
  - header: `start_time,end_time,participant_id,condition,task` `Assets/Scripts/Evaluation/EvaluationLogging.cs:34`
- `{task}_{condition}_{task_instance_id}_events.csv`
  - header: `event_time,event_type,trial_index,beat_time,target_key,pressed_key` `Assets/Scripts/Evaluation/EvaluationLogging.cs:155`

## CSV 列定義（events）
| 列名 | 意味 | 単位/形式 | 根拠 |
|---|---|---|---|
| event_time | イベント記録時刻 | UTC ISO 8601 | `Assets/Scripts/Evaluation/EvaluationLogging.cs:171` |
| event_type | `trial` / `press` | enum文字列 | `Assets/Scripts/Evaluation/EvaluationLogging.cs:173` |
| trial_index | trial番号（pressは直近trial） | int / 空あり | `Assets/Scripts/Evaluation/EvaluationLogging.cs:188` |
| beat_time | trial基準時刻 | UTC ISO 8601 / 空あり | `Assets/Scripts/Evaluation/EvaluationLogging.cs:175` |
| target_key | 正解ターゲット | 例: `C4` | `Assets/Scripts/Evaluation/EvaluationTaskController.Accuracy.cs:80` |
| pressed_key | 押下キー | 例: `C4` | `Assets/Scripts/Evaluation/EvaluationTaskController.Runtime.cs:352` |

## 指標算出に使えるデータ（計算式は未記載）
- 時刻
  - `event_time`（全行）
  - `beat_time`（trial行のみ）
- キー種別
  - `target_key`（trial行のみ）
  - `pressed_key`（press行のみ）
- 条件フラグ
  - eventsファイル名に `task` / `condition` が含まれる（prefix生成）`Assets/Scripts/Evaluation/EvaluationLogging.cs:152`
  - task_summary.csv に `condition,task` 列 `Assets/Scripts/Evaluation/EvaluationLogging.cs:34`
