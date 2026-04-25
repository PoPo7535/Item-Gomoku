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
    }
    void OnGUI()
    {
        int buttonWidth = 100;
        int buttonHeight = 40;
        int padding = 10;

        // 첫 번째 버튼
        if (GUI.Button(new Rect(padding, padding, buttonWidth, buttonHeight), "버튼 1"))
        {
            CreateRoom(GameMode.AutoHostOrClient);
        }

        // 두 번째 버튼
        if (GUI.Button(new Rect(padding, padding * 2 + buttonHeight, buttonWidth, buttonHeight), "버튼 2"))
        {
            CreateRoom(GameMode.AutoHostOrClient);
        }

        // 세 번째 버튼
        if (GUI.Button(new Rect(padding, padding * 3 + buttonHeight * 2, buttonWidth, buttonHeight), "버튼 3"))
        {
            CreateRoom(GameMode.AutoHostOrClient);
        }
    }

    public async void FastStart()
    {
        PopUp.I.Open("접속시도...");
        SetNetworkEvents();

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(1));
        
        var clientStartTask = StartGame(
            GameMode.Client, 
            null, 
            true);
        
        await clientStartTask;
        if (clientStartTask.Result.Ok)
            PopUp.I.Close();
        else
        {
            var hostStartTask = StartGame(
                GameMode.Host, 
                Extensions.GenerateBase36(6), 
                true);
   
            await hostStartTask;
            if (hostStartTask.Result.Ok)
                PopUp.I.Close();
            else
            {
                PopUp.I.Open(
                    hostStartTask.Result.ErrorMessage, 
                    () => { PopUp.I.Close();}, "확인");
            }
        }
    }
    public async void CreateRoom(GameMode gameMode)
    {
        PopUp.I.Open("Hosting...");
        SetNetworkEvents();

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(1));
        
        var startTask = StartGame(
            gameMode, 
            Extensions.GenerateBase36(6), 
            false);

        await startTask;
        if (startTask.Result.Ok)
            PopUp.I.Close();
        else
        {
            PopUp.I.Open(
                startTask.Result.ErrorMessage, 
                () => { PopUp.I.Close();}, "Ok");
        }
    }
    public async void JoinRoom(string roomCode)
    {
        PopUp.I.Open("접속중...");
        SetNetworkEvents();
        
        var startTask = StartGame(
            GameMode.Client,
            roomCode, 
            false);
        
        await startTask;
        if (startTask.Result.Ok)
            PopUp.I.Close();
        else
        {
            PopUp.I.Open(
                startTask.Result.ErrorMessage, 
                () => { PopUp.I.Close();}, "Ok");
        }
    }

    private  Task<StartGameResult> StartGame(GameMode gameMode,string roomCode, bool isVisible)
    {
        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(1));

        var startArgs = new StartGameArgs()
        {
            GameMode = gameMode,
            PlayerCount = 2,
            SessionName = roomCode,
            IsVisible = isVisible,
            Scene = sceneInfo,
            MatchmakingMode = MatchmakingMode.SerialMatching,
        };
        var hostStartTask = _runner.StartGame(startArgs);
        return hostStartTask;
    }
    private void SetNetworkEvents()
    {
        _runnerEvent.OnShutdown.RemoveAllListeners();
        _runnerEvent.OnShutdown.AddListener((r, response) =>
        {
            SceneManager.LoadScene(0);
            PopUp.I.Open(
                response.ToString(), 
                () => { PopUp.I.Close();}, "확인");
        });
    }
    public void GameQuit()
    {
        Runner.Shutdown();
    }
}
