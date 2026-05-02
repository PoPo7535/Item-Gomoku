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
            SetName();
            App.I.CreateRoom(GameMode.Single, true);
            App.I.PlayMode = GamePlayMode.Single;
        });
        makeRoomBtn.onClick.AddListener(() =>
        {
            SetName();
            App.I.CreateRoom(GameMode.Host, false);
            App.I.PlayMode = GamePlayMode.Multi;
        });
        findRoomBtn.onClick.AddListener(() =>
        {
            SetName();
            App.I.JoinRoom(roomCodeIF.text);
            App.I.PlayMode = GamePlayMode.Multi;
        });
        fastGameBtn.onClick.AddListener(() =>
        {
            SetName();
            App.I.CreateRoom(GameMode.AutoHostOrClient, true);
            App.I.PlayMode = GamePlayMode.Multi;
        });
        nickNameBtn.onClick.AddListener(() => { App.I.nickName = nickNameIF.text; });
        nickNameIF.onEndEdit.AddListener((str) => { App.I.nickName = str; });
        nickNameIF.onSubmit.AddListener((str) => { App.I.nickName = str; });
        roomCodeIF.onValueChanged.AddListener(str => roomCodeIF.SetTextWithoutNotify(str.ToUpper()));
    }

    private void SetName()
    {
        if (nickNameIF.text != string.Empty)
            return;

        var adjectives = new[]
        {
            "훌륭한", "멍청한", "멋있는", "반짝이는", "용감한", "겁많은",
            "지혜로운", "어리석은", "날렵한", "느릿한", "강력한", "약한",
            "신비로운", "어두운", "밝은", "화려한", "조용한", "시끄러운",
            "차가운", "뜨거운", "재빠른", "느긋한", "귀여운", "무서운",
            "사나운", "온화한", "기묘한", "영리한", "둔한", "활기찬"
        };

        var nouns = new[]
        {
            "사자", "사람", "하이에나", "영웅", "마법사", "기사",
            "도적", "용", "늑대", "호랑이", "독수리", "곰",
            "여우", "판다", "고양이", "강아지", "해적", "왕",
            "여왕", "전사", "수호자", "괴물", "악마", "천사",
            "유령", "로봇", "외계인", "탐험가", "연금술사", "사냥꾼"
        };

        var a = Random.Range(0, adjectives.Length - 1);
        var b = Random.Range(0, nouns.Length - 1);

        App.I.nickName = $"{adjectives[a]} {nouns[b]}";
    }
}
