# story-serial-port-settings-basic — LucidGloves COM ポート設定 UI 基礎

この story は、LucidGloves からのシリアル入力に対して、  
**VR 実行中に COM ポートを変更・再接続できる** 最小限の UI を定義する。

---

## 受け入れ条件（完了判定）

### 1. HandSerialInput のランタイム再接続

- `HandSerialInput` に以下のメソッドが実装されていること:

  ```csharp
  public void SetPortNameAndReconnect(string newPortName);
  ```

- 上記メソッドの挙動:

  - `newPortName` が空文字 / null の場合は何もしない（警告ログは任意）。
  - 現在開いているポートを `ClosePort()` で閉じる。
  - `portName = newPortName` に更新する。
  - `autoOpenOnStart == true` の場合は `OpenPort()` を呼び、新しい COM に接続を試みる。

### 2. 設定パネルの表示・非表示

- シーン内に、以下を持つ UI が存在する:

  - ルート GameObject（設定パネル全体）: `panelRoot`
  - このパネルを制御するコンポーネント: `SerialSettingsUI`

- `SerialSettingsUI` の要件:

  - フィールド:
    - `KeyCode toggleKey`（既定: `F1`）
    - `GameObject panelRoot`
  - 起動時 (`Start()`):
    - `panelRoot` を非表示 (`SetActive(false)`) にする。
  - 実行中 (`Update()`):
    - `toggleKey` が押されたフレームで `panelRoot.activeSelf` を反転させる。
    - → F1 でパネルの開閉ができることを確認する。

### 3. 左右 COM ポート入力と反映

- `SerialSettingsUI` に以下のフィールドがある:

  - `HandSerialInput leftSerialInput`
  - `HandSerialInput rightSerialInput`
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
    - 空でなければ、対応する `HandSerialInput.SetPortNameAndReconnect(text)` を呼ぶ。
  - 実際に COM を変更したあと、
    - `CurlDebugUI` などで `sensorRaw` が新しいポートから更新されていることを確認できる。

### 4. エラー時の挙動

- 無効な COM ポートを指定した場合でも、アプリがフリーズしないこと。
- `HandSerialInput` の既存ログ (`logErrors`, `logOpenClose`) を ON にしておけば、
  - ポートオープン失敗時に適切なエラーログが出ること。

---

## 関連 feature / story

- `feature/hand-curl-tuning.md`
- `story/hand-curl-tuning-basic.md`

