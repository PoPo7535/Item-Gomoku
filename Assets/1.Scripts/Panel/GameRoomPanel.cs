using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameRoomPanel : NetworkBehaviour, IPlayerLeft
{
    [SerializeField] private ItemSelectPanel itemSelectPanel;
    [SerializeField] private TMP_Text playerText1;
    [SerializeField] private Image playerImg1;
    [SerializeField] private TMP_Text playerText2;
    [SerializeField] private Image playerImg2;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Toggle openRoomToggle;
    [SerializeField] private Toggle itemToggle;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button shutdownButton;
    [SerializeField] private TMP_Text readyText;

    private bool _clientReady = false;
    [Networked, OnChangedRender(nameof(OnChangedOpenRoomBool))] private NetworkBool OpenRoomToggleBool { get; set; }
    private void OnChangedOpenRoomBool() => openRoomToggle.isOn = OpenRoomToggleBool;
    [Networked, OnChangedRender(nameof(OnChangedItemBool))] private NetworkBool ItemToggleBool { get; set; }
    private void OnChangedItemBool() => itemToggle.isOn = ItemToggleBool;
    [Networked, OnChangedRender(nameof(OnChangedNickName1))] private NetworkString<_16> Player1Str { set; get; }
    private void OnChangedNickName1() => playerText1.text = Player1Str.Value;
    [Networked, OnChangedRender(nameof(OnChangedNickName2))] private NetworkString<_16> Player2Str { set; get; }
    private void OnChangedNickName2() => playerText2.text = Player2Str.Value;
    public override void Spawned()
    {
        InspectorInit();
        SetupGameStartButton();
        SetNickName();
    }

    public void LateUpdate()
    {
        if (false == GomokuManager.I.IsPlaying)
        {
            timerText.text = "VS";   
            return;
        }
        var time = App.I.TickTimerRemainingTime(GomokuManager.I.TickTimer);
        timerText.text = $"{time:0}";
    }

    private void SetNickName()
    {
        playerText1.text = Player1Str.Value;
        playerText2.text = Player2Str.Value;
        if (Object.HasStateAuthority)
            Player1Str = App.I.nickName;
        else
            Rpc_SetNickName(App.I.nickName);
    }
    private void InspectorInit()
    {
        shutdownButton.onClick.AddListener(() => App.I.GameQuit());
        readyButton.onClick.AddListener(() =>
        {
            if (Object.HasStateAuthority)
                RPC_GameStart();
            else
                RPC_Ready(false == _clientReady);
        });
        readyText.text = Object.HasStateAuthority ? "게임시작" : "준비";
        readyButton.interactable = false == Object.HasStateAuthority;
        
        roomCodeText.text = App.I.Runner.SessionInfo.Name;
        OpenRoomToggleBool = App.I.Runner.SessionInfo.IsVisible;
        itemToggle.isOn = ItemToggleBool;
        openRoomToggle.onValueChanged.AddListener((isOn) =>
        {
            OpenRoomToggleBool= isOn;
            App.I.Runner.SessionInfo.IsVisible = isOn;
        });
        itemToggle.onValueChanged.AddListener((isOn) =>
        {
            ItemToggleBool = isOn;
        });
        GomokuManager.I.TurnEvents.Add(isBlackTurn =>
        {
            playerImg1.color = isBlackTurn ? new Color32(255, 210, 210, 255) : Color.white;
            playerImg2.color = isBlackTurn ? Color.white : new Color32(210, 210, 255, 255);
        });
        GomokuManager.I.PlayEvents.Add(isPlay =>
        {
            if (false == isPlay)
            {
                playerImg1.color = Color.white;
                playerImg2.color = Color.white;
            }
        });
    }

    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void RPC_GameStart(RpcInfo info = default)
    {
        itemSelectPanel.ActiveCg(true);
    }
    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void RPC_Ready(bool ready, RpcInfo info = default)
    {
        _clientReady = ready;
        if (false == Object.HasStateAuthority)
            return;
        readyButton.interactable = _clientReady;
    }

    public void PlayerLeft(PlayerRef player)
    {
        Player2Str = null;
        _clientReady = false;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void Rpc_SetNickName(string nickName)
    {
        Player2Str = nickName;
    }

    private void SetupGameStartButton() //추가
    {   
        if (App.I.PlayMode == GamePlayMode.Multi) 
            return;
        readyButton.interactable = true;
        readyText.text = "게임시작";

        readyButton.onClick.RemoveAllListeners();
        readyButton.onClick.AddListener(() =>
        {
            GomokuManager.I.StartGame();
        }); 
    }
 
}
