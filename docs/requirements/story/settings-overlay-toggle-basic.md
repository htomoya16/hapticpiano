# story-settings-overlay-toggle-basic — 設定パネル開閉と一時停止

この story は、任意の設定パネルを F1 で開閉し、開いている間はシーンを一時停止してマウス操作を許可する最小要件を定義する。

## 受け入れ条件

### 1. パネル開閉
- シーン内に設定パネルのルート `panelRoot` が存在する。
- `SettingsOverlayOpener`（または同等コンポーネント）が `panelRoot` を制御する。
- 起動時に `panelRoot` は非表示。
- `openKey`（既定 F1）を押すと `panelRoot.activeSelf` がトグルする。

### 2. 一時停止と復帰
- パネルを開いた瞬間に `Time.timeScale` を 0 にする。
- パネルを閉じた瞬間に開く直前の `Time.timeScale` に戻す。

### 3. マウス操作の有効化
- パネルを開いた瞬間にカーソルを表示しロック解除する。
- 閉じたら元の `Cursor.lockState` / `Cursor.visible` に戻す。
- エディタ上で EventSystem + StandaloneInputModule（または InputSystemUIInputModule）が配置され、マウスクリックで UI 操作できること。

### 4. Canvas のオーバーレイ切替（任意）
- パネルがワールド空間 Canvas の場合、開いている間だけ `RenderMode.ScreenSpaceOverlay` に自動切替し、閉じたら元に復帰できるオプションがあること。

### 5. ヒント表示
- ヒント用 GameObject `hintRoot` があり、パネル非表示時のみ有効になる。

### 6. COM 設定との両立
- 既存の `SerialSettingsUI` が同じ F1 をトグルに使わないよう、キーを None にするか `SettingsOverlayOpener` に統合する運用が説明されていること。

## 動作確認の目安
- プレイ中に F1 でパネルを開く → ワールドが停止し、カーソルが出て UI をマウスで編集できる。
- F1 で閉じると時間が再開し、カーソル・Canvas が元に戻る。
