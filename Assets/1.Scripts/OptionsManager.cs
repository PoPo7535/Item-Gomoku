using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Utility;

public class OptionsManager : Singleton<OptionsManager>
{
    //[Header(":: Master")]
    //[SerializeField] private Slider masterVolume;
    //[SerializeField] private TextMeshProUGUI masterValueText;

    //[Header(":: BGM")]
    //[SerializeField] private Slider bgmSlider;
    //[SerializeField] private TextMeshProUGUI bgmValueText;
    //[SerializeField] private TextMeshProUGUI bgmTitleText;

    //[Header(":: SFX")]
    //[SerializeField] private Slider sfxSlider;
    //[SerializeField] private TextMeshProUGUI sfxValueText;
    //[SerializeField] private TextMeshProUGUI sfxTitleText;

    private void Awake()
    {
        if (FindObjectsOfType<OptionsManager>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        base.Awake();
        DontDestroyOnLoad(gameObject);
    }

    //private void Start()
    //{
    //    masterVolume.value = DEFAULT_VOLUME;
    //    bgmSlider.value = DEFAULT_VOLUME;
    //    sfxSlider.value = DEFAULT_VOLUME;

    //    Options.OnMasterVolumeChanged(DEFAULT_VOLUME);
    //    Options.OnBGMVolumeChanged(DEFAULT_VOLUME);
    //    Options.OnSFXVolumeChanged(DEFAULT_VOLUME);

    //    masterVolume.onValueChanged.AddListener(OnMasterChanged);
    //    bgmSlider.onValueChanged.AddListener(OnBGMChanged);
    //    sfxSlider.onValueChanged.AddListener(OnSFXChanged);

    //    UpdateBGMTitleText();
    //}

    //private void OnDestroy()
    //{
    //    masterVolume.onValueChanged.RemoveListener(OnMasterChanged);
    //    bgmSlider.onValueChanged.RemoveListener(OnBGMChanged);
    //    sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
    //}

    public void OnMasterChanged(float value)
    {
        Options.OnMasterVolumeChanged(value);
    }

    public void OnBGMChanged(float value)
    {
        Options.OnBGMVolumeChanged(value);
    }

    //private void UpdateBGMTitleText()
    //{
    //    if (bgmTitleText == null) return;
    //    bgmTitleText.text = SoundManager.I.CurrentBGMTitle();
    //}

    //public void OnBGMNext()
    //{
    //    SoundManager.I.PlayNextBGM();
    //    UpdateBGMTitleText();
    //}

    //public void OnBGMPrev()
    //{
    //    SoundManager.I.PlayPrevBGM();
    //    UpdateBGMTitleText();
    //}

    public void OnSFXChanged(float value)
    {
        Options.OnSFXVolumeChanged(value);
    }

}
