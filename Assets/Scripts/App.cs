using Fusion;
using Fusion.Photon.Realtime;
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
        
        PopUp.I.Open("Hosting...");
        // 연결끊김 이벤트 
        runnerEvent.OnShutdown.RemoveAllListeners();
        runnerEvent.OnShutdown.AddListener((r, response) =>
        {
            SceneManager.LoadScene(0);
            PopUp.I.Open(
                response.ToString(), 
                () => { PopUp.I.Close();}, "Ok");
        });
        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(1));
        var startArguments = new StartGameArgs()
        {
            GameMode = gameMode,
            PlayerCount = 2,
            SessionName = "TestRoom", // Extensions.GenerateBase36(6),
            SessionNameGenerator = null,
            // SessionProperties = new Dictionary<string, SessionProperty> {["GameMode"] = GameModeIdentifier},
            Scene = sceneInfo,
            MatchmakingMode = MatchmakingMode.FillRoom,
        };

        var startTask = runner.StartGame(startArguments);
        await startTask;
        if (startTask.Result.Ok)
        {
            PopUp.I.Close();
        }
        else
        {
            PopUp.I.Open(
                startTask.Result.ErrorMessage, 
                () => { PopUp.I.Close();}, "Ok");
        }
    }

    public void GameQuit()
    {
        ((SimulationBehaviour)this).Runner.Shutdown();
    }
}
