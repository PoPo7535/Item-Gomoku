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
    
    [SerializeField] private ItemSelectPanel itemSelectPanel;
    private bool _ready = false;

    public override void Spawned()
    {
        InspectorInit();
        UpdatePlayers();
        App.I.Runner.SessionInfo.IsVisible.Log();
        App.I.Runner.SessionInfo.Name.Log();
    }

    private void InspectorInit()
    {
        shutdownButton.onClick.AddListener(() => App.I.GameQuit());
        readyButton.onClick.AddListener(() =>
        {
            if (Object.HasStateAuthority)
                RPC_GameStart();
            else
                RPC_Ready(false == _ready);
        });
        readyText.text = Object.HasStateAuthority ? "Start" : "Ready";
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
        _ready = ready;
        if (false == Object.HasStateAuthority)
            return;
        readyButton.interactable = _ready;
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
        _ready = false;
    }
}
