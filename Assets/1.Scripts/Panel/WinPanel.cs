using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WinPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup _cg;
    [SerializeField] private Button okBtn;
    
    [SerializeField] private Image img;
    [SerializeField] private Sprite whiteWin;
    [SerializeField] private Sprite blackWin;

    private void Start()
    {
        okBtn.onClick.AddListener(() =>
        {
            _cg.ActiveCG(false);
        });
    }

    public void OpPanel(StoneColor color)
    {
        _cg.ActiveCG(true);
        img.sprite = color == StoneColor.Black ? blackWin : whiteWin;
    }
}
