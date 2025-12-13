# story-serial-port-settings-basic — LucidGloves COM ポート設定 UI 基礎

この story は、LucidGloves からのシリアル入力に対して、  
**VR 実行中に COM ポートを変更・再接続できる** 最小限の UI を定義する。

> 触覚フィードバック送信系も同一 COM を使用する可能性があるため、ポート開閉は共通の SerialPortAdapter（`Assets/Scripts/IO/`）を介して行うことを想定する。UI はアダプタ経由でポート名を更新し、受信系（HandSensorReceiver）と送信系（HapticSerialSender）双方に適用される。

---

## 受け入れ条件（完了判定）

### 1. HandSensorReceiver のランタイム再接続

- `HandSensorReceiver` に以下のメソッドが実装されていること:

  ```csharp
  public void SetPortNameAndReconnect(string newPortName);
  ```

- 上記メソッドの挙動:

  - `newPortName` が空文字 / null の場合は何もしない（警告ログは任意）。
  - 現在開いているポートを `SerialPortAdapter.Close()` で閉じる。
  - `portName = newPortName` に更新する。
  - `SerialPortAdapter.TryOpen(portName, baudRate)` で新しい COM に接続を試みる。

### 2. 設定パネルの表示・非表示

- シーン内に、以下を持つ UI が存在する:

  - ルート GameObject（設定パネル全体）: `panelRoot`
  - このパネルを制御するコンポーネント: `SerialSettingsUI`

- `SerialSettingsUI` の要件:

  - フィールド:
    - `KeyCode toggleKey`（既定: `F1`）
    - `GameObject panelRoot`
    - `SerialPortAdapter serialAdapter`（共通ポート管理を使う場合、`Assets/Scripts/IO/` 配置）
  - 起動時 (`Start()`):
    - `panelRoot` を非表示 (`SetActive(false)`) にする。
  - 実行中 (`Update()`):
    - `toggleKey` が押されたフレームで `panelRoot.activeSelf` を反転させる。
    - → F1 でパネルの開閉ができることを確認する。

### 3. 左右 COM ポート入力と反映

- `SerialSettingsUI` に以下のフィールドがある:

  - `HandSensorReceiver leftReceiver`
  - `HandSensorReceiver rightReceiver`
  - （送信を同一ポートで行う場合）`HapticSerialSender leftHapticSender`, `rightHapticSender`（配置: `Assets/Scripts/ForceFeedBack/`）
  - `TMP_InputField leftPortInput`
  - `TMP_InputField rightPortInput`

- 起動時 (`Start()`):

  - `leftSerialInput` / `rightSerialInput` が設定されていれば、
    それぞれの `portName` を対応する `TMP_InputField.text` に反映する。

- UI 操作:

  - パネル上に「Left Apply」「Right Apply」などのボタンがあり、
    - Left ボタンの `OnClick` に `SerialSettingsUI.ApplyLeftPort()` が紐付いている。
    - Right ボタンの `OnClick` に `SerialSettingsUI.ApplyRightPort()` が紐付いている。

- `ApplyLeftPort()` / `ApplyRightPort()` の要件:

  - 対応する `TMP_InputField.text` を読み取り、
    - 空でなければ、`SerialPortAdapter` または `HandSensorReceiver.SetPortNameAndReconnect(text)` を経由してポートを更新する。
    - 送信系が同一 COM を共有する場合、`HapticSerialSender` 側にも同じポート名を伝搬させる。
  - 実際に COM を変更したあと、
    - `CurlDebugUI` などで `sensorRaw` が新しいポートから更新されていることを確認できる。

### 4. エラー時の挙動

- 無効な COM ポートを指定した場合でも、アプリがフリーズしないこと。
- `SerialPortAdapter` のログ (`logErrors`, `logOpenClose`) を ON にしておけば、
  - ポートオープン失敗時に適切なエラーログが出ること。

---

## 関連 feature / story

- `feature/hand-curl-tuning.md`
- `story/hand-curl-tuning-basic.md`

