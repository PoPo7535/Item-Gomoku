using System;
using DG.Tweening;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToggleSlider : NetworkBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private TMP_Text onText;
    [SerializeField] private Image onImage;
    [SerializeField] private TMP_Text offText;
    [SerializeField] private Image offImage;
    [SerializeField] private RectTransform slider;
    private readonly Color32 _activeColor = new(220,220,220,255);
    private readonly Color32 _deActiveColor = new(30,30,30,255);

    public void Awake()
    {
        toggle.onValueChanged.AddListener((isOn) =>
        {
            var xSize = slider.sizeDelta.x / 2;
            onText.DOColor(isOn ? _activeColor : _deActiveColor, 0.3f);
            onImage.DOColor(isOn ? _activeColor : _deActiveColor, 0.3f);
            offText.DOColor(isOn ? _deActiveColor : _activeColor, 0.3f);
            offImage.DOColor(isOn ? _deActiveColor : _activeColor, 0.3f);
            toggle.interactable = false;
            slider.SetAnchorKeepPosition(new Vector2(isOn ? 0 : 1, 0), new Vector2(isOn ? 0 : 1, 1));
            slider.DOAnchorPosX(isOn ? xSize : -xSize, 0.3f).OnComplete(() =>
            {
                if (Object.HasStateAuthority && 
                    App.I.PlayMode != GamePlayMode.Single)
                    toggle.interactable = true;
            });
        });
    }
}
