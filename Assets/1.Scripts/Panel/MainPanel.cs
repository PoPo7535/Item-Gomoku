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
            App.I.CreateRoom(GameMode.Single);
            App.I.PlayMode = GamePlayMode.Single;
        });
        _button2.onClick.AddListener(() =>
        {
            App.I.CreateRoom(GameMode.AutoHostOrClient);
            App.I.PlayMode = GamePlayMode.Multi;
        });
    }
}
