using Fusion;
using TMPro;
using UnityEngine;

public class ChatPanel : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    private TMP_InputField _if;
    
    public override void Spawned()
    {
        _if.onSubmit.AddListener((str) =>
        {
            
        });
        _if.onEndEdit.AddListener((str) =>
        {
            
        });
        
    }

    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void Rpc_SendChat(string message, RpcInfo info)
    {
        
    }

    [Rpc(RpcSources.All, RpcTargets.All, HostMode = RpcHostMode.SourceIsHostPlayer)]
    private void Rpc_SendNotice(string message, RpcInfo info)
    {
        
    }
    
    
    public void PlayerJoined(PlayerRef player)
    {
        
    }

    public void PlayerLeft(PlayerRef player)
    {
        
    }
}
