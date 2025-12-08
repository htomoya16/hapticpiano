using System;
using UnityEngine;

/// <summary>
/// メニューの各ボタンから設定パネルを切り替えるルーター。
/// SettingsOverlayOpener と併用し、指定パネルだけを表示する。
/// </summary>
public class SettingsMenuRouter : MonoBehaviour
{
    [Serializable]
    public struct PanelEntry
    {
        public string id;         // ボタンから渡す識別子
        public GameObject panel;  // 実際のパネル GameObject
    }

    [Header("Bindings")]
    [SerializeField] private SettingsOverlayOpener overlayOpener;
    [SerializeField] private GameObject mainMenuPanel; // メインメニュー（ボタン一覧）
    [SerializeField] private PanelEntry[] panels;

    [Header("Behavior")]
    [SerializeField] private bool openOverlayOnNavigate = true;

    private void Awake()
    {
        // 起動時はメインメニューだけ表示
        SetActiveOnly(mainMenuPanel);
    }

    /// <summary>
    /// メインメニューに戻るボタン用。
    /// </summary>
    public void ShowMain()
    {
        SetActiveOnly(mainMenuPanel);
        OpenOverlayIfNeeded();
    }

    /// <summary>
    /// ボタンの OnClick(string id) に紐付ける。
    /// </summary>
    public void ShowPanelById(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        GameObject target = null;
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i].panel == null) continue;
            if (string.Equals(panels[i].id, id, StringComparison.Ordinal))
            {
                target = panels[i].panel;
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"SettingsMenuRouter: panel id '{id}' not found", this);
            return;
        }

        SetActiveOnly(target);
        OpenOverlayIfNeeded();
    }

    private void SetActiveOnly(GameObject target)
    {
        // メインメニュー
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(target == mainMenuPanel);
        }

        // サブパネル群
        for (int i = 0; i < panels.Length; i++)
        {
            var p = panels[i].panel;
            if (p == null) continue;
            p.SetActive(p == target);
        }
    }

    private void OpenOverlayIfNeeded()
    {
        if (openOverlayOnNavigate && overlayOpener != null)
        {
            overlayOpener.Open();
        }
    }
}
