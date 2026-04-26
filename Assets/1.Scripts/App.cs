using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utility;


public class App : SimulationSingleton<App>
{
    private NetworkRunner _runner;
    private NetworkEvents _runnerEvent;
    private new void Awake()
    {
        base.Awake();
        _runner = gameObject.GetComponent<NetworkRunner>();
        _runnerEvent = gameObject.GetComponent<NetworkEvents>();
        _runnerEvent.OnShutdown.RemoveAllListeners();
        _runnerEvent.OnShutdown.AddListener((r, response) =>
        {
            SceneManager.LoadScene(0);
            PopUp.I.Open(
                response.ToString(), 
                () => { PopUp.I.Close();}, "확인");
        });
    }

    public void FastRoom()
    {
        CreateGame(GameMode.AutoHostOrClient, null, true);
    }
    public void CreateRoom(GameMode gameMode)
    {
        CreateGame(gameMode, null, true);
    }
    public void JoinRoom(string roomCode)
    {
        CreateGame(GameMode.Client, roomCode, false);
    }
    private async void CreateGame(GameMode gameMode, string roomCode, bool isVisible)
    {
        PopUp.I.Open("연결 중 . . .");
        
        var startTask = StartGame(
            gameMode,
            roomCode, 
            isVisible);
        
        await startTask;
        if (startTask.Result.Ok)
            PopUp.I.Close();
        else
        {
            PopUp.I.Open(
                startTask.Result.ErrorMessage, 
                () => { PopUp.I.Close();}, "확인");
        }
    }

    private Task<StartGameResult> StartGame(GameMode gameMode, string roomCode, bool isVisible)
    {
        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(1));

        var startArgs = new StartGameArgs()
        {
            GameMode = gameMode,
            PlayerCount = 2,
            SessionName = roomCode,
            SessionNameGenerator = () => Extensions.GenerateBase36(6),
            IsVisible = isVisible,
            Scene = sceneInfo,
            MatchmakingMode = MatchmakingMode.SerialMatching,
        };
        var hostStartTask = _runner.StartGame(startArgs);
        return hostStartTask;
    }

    public void GameQuit()
    {
        Runner.Shutdown();
    }
}
