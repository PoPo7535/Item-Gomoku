using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class MainPanel : MonoBehaviour
{
    public Button _button1;
    public Button _button2;

    private void Awake()
    {
        _button1.onClick.AddListener(() =>
        {
            App.I.GameStart(GameMode.Single);
        });
        _button2.onClick.AddListener(() =>
        {
            App.I.GameStart(GameMode.AutoHostOrClient);
        });
    }
}
