using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utility;


public class App : SimulationSingleton<App>
{
    private new void Awake()
    {
        base.Awake();
    }

    public async void GameStart(GameMode gameMode)
    {
        var runner = gameObject.GetComponent<NetworkRunner>();
        var runnerEvent = gameObject.GetComponent<NetworkEvents>();
        
        // 연결끊김 이벤트 
        runnerEvent.OnShutdown.AddListener((r,response)=>{});

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(1));
        var startArguments = new StartGameArgs()
        {
            GameMode = gameMode,
            PlayerCount = 2,
            SessionName = "RoomName",
            SessionNameGenerator = null,
            // SessionProperties = new Dictionary<string, SessionProperty> {["GameMode"] = GameModeIdentifier},
            Scene = sceneInfo,
        };


        var startTask = runner.StartGame(startArguments);
        await startTask;
        if (startTask.Result.Ok)
        {
            
        }
        else
        {
            
        }
    }
}
