using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// プレイヤーの Y 位置を UI スライダーで調整する。
/// </summary>
public class PlayerHeightSetting : MonoBehaviour
{
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Slider heightSlider;
    [SerializeField] private float minY = -0.5f;
    [SerializeField] private float maxY = 1f;
    [SerializeField] private TMP_Text minLabel;
    [SerializeField] private TMP_Text maxLabel;
    [SerializeField] private TMP_Text currentLabel;

    private float _originalY;

    private void Awake()
    {
        if (playerRoot != null)
        {
            _originalY = playerRoot.position.y;
        }

        if (heightSlider != null)
        {
            heightSlider.minValue = minY;
            heightSlider.maxValue = maxY;
            heightSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        UpdateLabels(initialSetValue: false);
    }

    private void Start()
    {
        // 初期表示を現在位置で揃える
        if (heightSlider != null && playerRoot != null)
        {
            var current = Mathf.Clamp(playerRoot.position.y, minY, maxY);
            heightSlider.SetValueWithoutNotify(current);
        }

        UpdateLabels(initialSetValue: true);
    }

    private void OnDestroy()
    {
        if (heightSlider != null)
        {
            heightSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
    }

    public void OnSliderValueChanged(float value)
    {
        if (playerRoot == null) return;
        var pos = playerRoot.position;
        pos.y = Mathf.Clamp(value, minY, maxY);
        playerRoot.position = pos;

        UpdateLabels(initialSetValue: false);
    }

    public void ResetToOriginal()
    {
        if (playerRoot == null) return;
        var y = Mathf.Clamp(_originalY, minY, maxY);
        var pos = playerRoot.position;
        pos.y = y;
        playerRoot.position = pos;
        if (heightSlider != null)
        {
            heightSlider.SetValueWithoutNotify(y);
        }

        UpdateLabels(initialSetValue: false);
    }

    private void UpdateLabels(bool initialSetValue)
    {
        if (minLabel != null) minLabel.text = minY.ToString("0.00");
        if (maxLabel != null) maxLabel.text = maxY.ToString("0.00");

        if (currentLabel != null)
        {
            float v = heightSlider != null ? heightSlider.value :
                      playerRoot != null ? Mathf.Clamp(playerRoot.position.y, minY, maxY) : 0f;
            if (initialSetValue && heightSlider != null && playerRoot != null)
            {
                v = Mathf.Clamp(playerRoot.position.y, minY, maxY);
                heightSlider.SetValueWithoutNotify(v);
            }
            currentLabel.text = v.ToString("0.00");
        }
    }
}
