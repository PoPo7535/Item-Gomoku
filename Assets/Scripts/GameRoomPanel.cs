using System;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameRoomPanel : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyText;
    [SerializeField] private Button shutdownButton;
    [SerializeField] private TMP_Text playerText1;
    [SerializeField] private TMP_Text playerText2;
    private bool _ready = false;
    public override void Spawned()
    {
        shutdownButton.onClick.AddListener(() => App.I.GameQuit());
        readyButton.onClick.AddListener(() =>
        {
            if (Object.HasStateAuthority)
            {
                RPC_GameStart();
            }
            else
            {
                RPC_Ready(false == _ready);
            }
        });
        readyText.text = Object.HasStateAuthority ? "Start" : "Ready";
        readyButton.interactable = false == Object.HasStateAuthority;
    }

    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void RPC_GameStart(RpcInfo info = default)
    {
        "GameStart".Log();
    }
    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void RPC_Ready(bool ready, RpcInfo info = default)
    {
        _ready = ready;
        if (false == Object.HasStateAuthority)
            return;
        readyButton.interactable = _ready;
    }
    
    public void PlayerJoined(PlayerRef player)
    {
        Object.HasStateAuthority.Log();
        if (Object.HasStateAuthority)
            playerText1.text = player.ToString();
        else
        {
            playerText2.text = player.ToString();
            _ready = false;
        }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Object.HasStateAuthority)
            playerText1.text = string.Empty;
        else
        {
            playerText2.text = string.Empty;
            _ready = false;
        }
    }
}
