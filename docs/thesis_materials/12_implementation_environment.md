# 12_implementation_environment (table)

## Unity/Project
| 項目 | 値 | 根拠 |
|---|---|---|
| Unity Editor Version | 2022.3.62f3 | `ProjectSettings/ProjectVersion.txt:1` |
| 主要シーン | `Assets/Scenes/hapticpiano/hapticpiano.unity` | `docs/requirements/000-overview.md:12` |

## Packages（主要）
| パッケージ | バージョン/参照 | 根拠 |
|---|---|---|
| com.valvesoftware.unity.openvr | `file:D:/ダウンロード/com.valvesoftware.unity.openvr-1.2.4.tgz` | `Packages/manifest.json:12` |
| com.unity.xr.management | 4.4.0 | `Packages/packages-lock.json:175` |
| com.unity.xr.legacyinputhelpers | 2.1.12 | `Packages/packages-lock.json:165` |
| com.unity.textmeshpro | 3.0.7 | `Packages/manifest.json`（dependencies内） |
| com.unity.postprocessing | 3.4.0 | `Packages/manifest.json`（dependencies内） |
| com.unity.cinemachine | 2.10.5 | `Packages/manifest.json`（dependencies内） |

## SDK/Plugin（Assets側）
| 名称 | 形態 | 根拠 |
|---|---|---|
| SteamVR | `Assets/SteamVR/**` ディレクトリ | `Assets/SteamVR`（存在） |
| SteamVR Actions | StreamingAssets JSON | `Assets/StreamingAssets/SteamVR/actions.json` |
| NAudio | DLL plugin | `Assets/Plugins/NAudio/NAudio.dll` |
| JSON.NET（Valve） | DLL plugin | `Assets/SteamVR/Input/Plugins/JSON.NET/Valve.Newtonsoft.Json.dll` |

## XR設定（OpenVR）
| 項目 | 値 | 根拠 |
|---|---|---|
| OpenVR ActionManifest | `StreamingAssets\SteamVR\actions.json` | `Assets/XR/Settings/OpenVRSettings.asset:21` |
| OpenVR EditorAppKey | `application.generated.unity.ffbdemo.exe` | `Assets/XR/Settings/OpenVRSettings.asset:20` |
| XRGeneralSettings (Standalone) | `m_InitManagerOnStart: 1` | `Assets/XR/XRGeneralSettingsPerBuildTarget.asset` |

## 実行時に必要な外部ソフト
| 項目 | 値 | 根拠 |
|---|---|---|
| SteamVR/OpenVR 関連 | 未確認（必要性の明記なし） | ただし OpenVR/SteamVR ファイル群が存在: `Packages/manifest.json:12`, `unityProject.vrmanifest` |

## ファームウェア開発環境（ESP32）
| 項目 | 値 | 根拠 |
|---|---|---|
| ESP32ファームウェア | 本リポジトリに含まれない | `AGENTS.md:13` |
| IDE/書き込み方法 | 未確認（リポジトリ内に記載なし） | 未確認 |
