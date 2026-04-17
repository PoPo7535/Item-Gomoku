using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utility;

public class App : SimulationSingleton<App>
{
    [SerializeField]private Scene[] _scenes;
    private NetworkRunner _runner;
    private NetworkEvents _event;
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _runner = gameObject.GetComponent<NetworkRunner>();
        _event = gameObject.GetComponent<NetworkEvents>();
    }

    public async void GameStart(GameMode gameMode)
    {
        // 연결끊김 이벤트 추가
        _event.OnShutdown.AddListener((runner,response)=>{});

        var sceneInfo = new NetworkSceneInfo();
        SceneManager.GetSceneByPath("Scenes/1.RoomScene").buildIndex.Log();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(1));
        var startArguments = new StartGameArgs()
        {
            GameMode = gameMode,
            SessionName = "RoomName",
            PlayerCount = 2,
            // SessionProperties = new Dictionary<string, SessionProperty> {["GameMode"] = GameModeIdentifier},
            Scene = sceneInfo,
        };


        var startTask = _runner.StartGame(startArguments);
        await startTask;

        if (startTask.Result.Ok)
        {
            
        }
        else
        {
            
        }
    }
}
