using System;
using ProceduralUITool.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemToggle : MonoBehaviour
{
    public Toggle toggle;
    [HideInInspector] public GomokuItem gomokuItem;
    [SerializeField] private Image image;
    [SerializeField] private Image selectedImage;
    [SerializeField] private TMP_Text nameText;
    [Header("ProceduralUI")]
    [SerializeField] private ProceduralUIComponent proceduralUI;
    [SerializeField] private ProceduralUIProfile onProfile;
    [SerializeField] private ProceduralUIProfile offProfile;

    public void Start()
    {
        toggle.onValueChanged.AddListener(isOn =>
        {
            var colors = toggle.colors;
            colors.normalColor = isOn ? new Color32(255, 237, 237, 255) : Color.white;
            colors.highlightedColor = isOn ? new Color32(255, 222, 222, 255) : new Color32(245, 245, 245, 255);
            toggle.colors = colors;
            proceduralUI.profile = isOn ? onProfile : offProfile;
            proceduralUI.ForceUpdate();
            proceduralUI.UpdateEffect(); 
            selectedImage?.gameObject.SetActive(isOn);
        });
    }

    public void Set(GomokuItem gomokuItem)
    {
        this.gomokuItem = gomokuItem;
        image.sprite = this.gomokuItem.sprite;
        nameText.text = gomokuItem.itemName;
    }
}
