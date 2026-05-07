using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionSceneUI : MonoBehaviour
{
    [Header(":: Master")]
    [SerializeField] private Slider masterVolume;
    [SerializeField] private TextMeshProUGUI masterValueText;

    [Header(":: BGM")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private TextMeshProUGUI bgmValueText;
    [SerializeField] private TextMeshProUGUI bgmTitleText;

    [SerializeField] private Button nextBtn;
    [SerializeField] private Button prevBtn;

    [Header(":: SFX")]
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TextMeshProUGUI sfxValueText;
    //[SerializeField] private TextMeshProUGUI sfxTitleText;

    private void Start()
    {
        if (OptionsManager.I == null) return;

        masterVolume.value  = SoundManager.I.CurrentMasterVolume;
        bgmSlider.value     = SoundManager.I.CurrentBGMVolume;
        sfxSlider.value     = SoundManager.I.CurrentSFXVolume;

        if (masterValueText != null) masterValueText.text = ((int)masterVolume.value).ToString() + "%";
        if (bgmValueText != null) bgmValueText.text = ((int)bgmSlider.value).ToString() + "%";
        if (sfxValueText != null) sfxValueText.text = ((int)sfxSlider.value).ToString() + "%";

        masterVolume.onValueChanged.AddListener(OnMasterChanged);
        bgmSlider.onValueChanged.AddListener(OnBGMChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXChanged);

        nextBtn.onClick.AddListener(() =>
        {
            SoundManager.I.PlayNextBGM();
            UpdateBGMTitleText();
        });

        prevBtn.onClick.AddListener(() =>
        {
            SoundManager.I.PlayPrevBGM();
            UpdateBGMTitleText();
        });

        UpdateBGMTitleText();
    }

    private void OnDestroy()
    {
        masterVolume.onValueChanged.RemoveListener(OnMasterChanged);
        bgmSlider.onValueChanged.RemoveListener(OnBGMChanged);
        sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
        nextBtn.onClick.RemoveAllListeners();
        prevBtn.onClick.RemoveAllListeners();
    }

    private void OnMasterChanged(float value)
    {
        if (masterValueText != null) masterValueText.text = ((int)value).ToString() + "%";
        OptionsManager.I.OnMasterChanged(value);
    }

    private void OnBGMChanged(float value)
    {
        if (bgmValueText != null) bgmValueText.text = ((int)value).ToString() + "%";
        OptionsManager.I.OnBGMChanged(value);
    }

    private void OnSFXChanged(float value)
    {
        if (sfxValueText != null) sfxValueText.text = ((int)value).ToString() + "%";
        OptionsManager.I.OnSFXChanged(value);
    }

    private void UpdateBGMTitleText()
    {
        if (bgmTitleText == null) return;
        bgmTitleText.text = SoundManager.I.CurrentBGMTitle();
    }
}