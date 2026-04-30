using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class GameRoomPanel : NetworkBehaviour, IPlayerLeft
{
    [SerializeField] private Button readyButton;
    [SerializeField] private Button shutdownButton;
    [SerializeField] private TMP_Text readyText;
    [SerializeField] private TMP_Text playerText1;
    [SerializeField] private TMP_Text playerText2;
    [SerializeField] private Slider timerSlider;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private ItemSelectPanel itemSelectPanel;
    private bool _clientReady = false;
    [Networked, OnChangedRender(nameof(OnChangedNickName1))] private NetworkString<_16> Player1Text { set; get; }
    private void OnChangedNickName1() => playerText1.text = Player1Text.Value;
    [Networked, OnChangedRender(nameof(OnChangedNickName2))] private NetworkString<_16> Player2Text { set; get; }
    private void OnChangedNickName2() => playerText2.text = Player2Text.Value;
    public override void Spawned()
    {
        App.I.Runner.SessionInfo.Name.Log();

        InspectorInit();
        SetupGameStartButton();
        SetNickName();
    }

    public void LateUpdate()
    {
        var time = App.I.TickTimerRemainingTime(GomokuManager.I.TickTimer);
        timerText.text = $"{time:0.0}";
        timerSlider.value = time/ GomokuManager.I.TurnTimeLimit;
    }

    private void SetNickName()
    {
        playerText1.text = Player1Text.Value;
        playerText2.text = Player2Text.Value;
        if (Object.HasStateAuthority)
            Player1Text = App.I.nickName;
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
        Player2Text = null;
        _clientReady = false;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void Rpc_SetNickName(string nickName)
    {
        Player2Text = nickName;
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
