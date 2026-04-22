using System;
using UnityEngine;
using UnityEngine.UI;

public class GameRoomPanel : MonoBehaviour
{
    [SerializeField] private Button shutdownButton;

    public void Start()
    {
        shutdownButton.onClick.AddListener(() => App.I.GameQuit());
    }
}
