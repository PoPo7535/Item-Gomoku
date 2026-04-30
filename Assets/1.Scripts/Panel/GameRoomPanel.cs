using System;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class GameRoomPanel : NetworkBehaviour, IPlayerJoined, IPlayerLeft
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

    public override void Spawned()
    {
        InspectorInit();
        UpdatePlayers();
        SetupGameStartButton();
        App.I.Runner.SessionInfo.Name.Log();
    }

    public void LateUpdate()
    {
        var time = App.I.TickTimerRemainingTime(GomokuManager.I.TickTimer);
        timerText.text = $"{time:0.0}";
        timerSlider.value = time/ GomokuManager.I.TurnTimeLimit;
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

    private void UpdatePlayers()
    {
        playerText1.text = string.Empty;
        playerText2.text = string.Empty;
        foreach (var playerRef in App.I.Runner.ActivePlayers) 
        {
            var isLocalPlayer = playerRef == App.I.Runner.LocalPlayer;
            var hostClient = Object.HasStateAuthority == isLocalPlayer;
            var targetText = hostClient ? playerText1 : playerText2;
            targetText.text = playerRef.ToString();
        }
    }
    public void PlayerJoined(PlayerRef player)
    {
        UpdatePlayers();
    }

    public void PlayerLeft(PlayerRef player)
    {
        UpdatePlayers();
        _clientReady = false;
    }
    public void SetupGameStartButton() //추가
    {   
        if (App.I.PlayMode == GamePlayMode.Multi) return;
        readyButton.interactable = true;
        readyText.text = "게임시작";

        readyButton.onClick.RemoveAllListeners();
        readyButton.onClick.AddListener(() =>
        {
            GomokuManager.I.StartGame();
        }); 
    }
 
}
