using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
//
public class MainPanel : MonoBehaviour
{
    [SerializeField] private Button singlePlayBtn;
    [SerializeField] private Button makeRoomBtn;
    [SerializeField] private Button findRoomBtn;
    [SerializeField] private Button fastGameBtn;
    [SerializeField] private Button nickNameBtn;
    [SerializeField] private TMP_InputField nickNameIF;
    [SerializeField] private TMP_InputField roomCodeIF;
    private void Start()
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
            roomCodeIF.text.Log();
            App.I.JoinRoom(roomCodeIF.text);
            App.I.PlayMode = GamePlayMode.Multi;
        });
        fastGameBtn.onClick.AddListener(() =>
        {
            App.I.CreateRoom(GameMode.AutoHostOrClient, true);
            App.I.PlayMode = GamePlayMode.Multi;
        });
        nickNameBtn.onClick.AddListener(() => { App.I.nickName = nickNameIF.text; });
        nickNameIF.onEndEdit.AddListener((str) => { App.I.nickName = str; });
        nickNameIF.onSubmit.AddListener((str) => { App.I.nickName = str; });
    }
}
