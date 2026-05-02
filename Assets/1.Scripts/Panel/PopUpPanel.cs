using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utility;

public class PopUpPanel : Singleton<PopUpPanel>
{
    [SerializeField] private CanvasGroup cg;
    [SerializeField] private TMP_Text mainMsg;
    [SerializeField] private Button button1;
    [SerializeField] private TMP_Text buttonText1;
    private new void Awake()
    {
        base.Awake();
    }
    public void Open(string msg, Action action1 = null, string text1 = null)
    {
        cg.ActiveCG(true);
        mainMsg.text = msg;
        SetButton(button1, action1, buttonText1, text1);

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
