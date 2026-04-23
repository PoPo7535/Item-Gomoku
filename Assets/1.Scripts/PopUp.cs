using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

public class PopUp : Singleton<PopUp>
{
    [SerializeField] private CanvasGroup cg;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Button button1;
    [SerializeField] private TMP_Text buttonText1;
    [SerializeField] private Button button2;
    [SerializeField] private TMP_Text buttonText2;
    [SerializeField] private Button button3;
    [SerializeField] private TMP_Text buttonText3;
    private new void Awake()
    {
        base.Awake();
    }
    public void Open(string msg, 
        Action action1 = null, string text1 = null, 
        Action action2 = null, string text2 = null, 
        Action action3 = null, string text3 = null)
    {
        cg.ActiveCG(true);
        text.text = msg;
        SetButton(button1, action1, buttonText1, text1);
        SetButton(button2, action2, buttonText2, text2);
        SetButton(button3, action3, buttonText3, text3);

        void SetButton(Button button, Action action,TMP_Text text, string msg)
        {
            if (action == null)
            {
                button.gameObject.SetActive(false);
                return;
            }
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                action?.Invoke();
                cg.ActiveCG(false);
            });
            text.text = msg;
        }
    }

    public void Close()
    {
        cg.ActiveCG(false);
    }
    
    
    
    
}
