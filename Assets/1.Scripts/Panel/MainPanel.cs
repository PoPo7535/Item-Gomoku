using Fusion;
using UnityEngine;
using UnityEngine.UI;
//
public class MainPanel : MonoBehaviour
{
    public Button singlePlayBtn;
    public Button makeRoomBtn;
    public Button findRoomBtn;
    public Button fastGameBtn;

    private void Awake()
    {
        singlePlayBtn.onClick.AddListener(() =>
        {
            App.I.CreateRoom(GameMode.Single, true);
            App.I.PlayMode = GamePlayMode.Single;
        });
        makeRoomBtn.onClick.AddListener(() =>
        {
            App.I.CreateRoom(GameMode.Host, false);
            App.I.PlayMode = GamePlayMode.Multi;
        });
        findRoomBtn.onClick.AddListener(() =>
        {
            App.I.JoinRoom("1");
            App.I.PlayMode = GamePlayMode.Multi;
        });
        fastGameBtn.onClick.AddListener(() =>
        {
            App.I.CreateRoom(GameMode.AutoHostOrClient, true);
            App.I.PlayMode = GamePlayMode.Multi;
        });
    }
}
