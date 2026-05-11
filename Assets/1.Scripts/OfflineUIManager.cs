using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Utility;

public class OfflineUIManager : LocalSingleton<OfflineUIManager>
{
    [SerializeField] private Button _AIBtn;
    [SerializeField] private TMP_Dropdown _difficultyDropdown;
    [SerializeField] private TMP_Text _ainame;
    [SerializeField] private TMP_Text _laveltext;
    [SerializeField] private TMP_Text _aimsg;
    
    
    private bool _isAIActive = false;
    private GameRoomPanel roomPanel;
    private void Start()
    {   
       
        if (App.I.PlayMode == GamePlayMode.Multi)
        {
            SetUIVisible(false);
            return;
        }

        SetUIVisible(false);
        _AIBtn.onClick.AddListener(OnClickAIButton);
        _difficultyDropdown.onValueChanged.AddListener(OnChanged);
        OnChanged(_difficultyDropdown.value);
        roomPanel = FindObjectOfType<GameRoomPanel>();
    }

    void Update()
    {   
        //멀티 모드 UI체크
        if (App.I.PlayMode == GamePlayMode.Multi)
        {
            SetUIVisible(false);
            return;
        }

        // 싱글 모드 UI체크
        if (App.I.PlayMode == GamePlayMode.Single)
        {
            SetUIVisible(false);
            return;
        }
        // GomokuManager먼저 실행대는걸 막기위함
        var gm = GomokuManager.I;
        if (gm == null || gm.Object == null || !gm.Object.IsValid)
            return;

        // 게임 시작 체크
        if (GomokuManager.I.IsPlaying)
        {
            OnStartGameUI();
        }
        else
        {
            RefreshUIVisibility();
        }
    }
    public void ToggleAiMsg()
    {
        bool isActive = _aimsg.gameObject.activeSelf;
        _aimsg.gameObject.SetActive(!isActive);

        if (!isActive)
            _aimsg.text = "AI가 생각중입니다...";
    }
    /// <summary>
    /// AI 모드 활성화/비활성화 토글
    /// 프로필 ui 버튼
    /// </summary>
    private void OnClickAIButton()
    {   
        
        if (App.I.PlayMode == GamePlayMode.Multi || GomokuManager.I.IsPlaying) return;

        _isAIActive = !_isAIActive;
        
        if (_isAIActive)
        {
            App.I.PlayMode = GamePlayMode.AI;
            RefreshUIVisibility();
            roomPanel.SetAIModeUI();
        }
        else
        {
            App.I.PlayMode = GamePlayMode.Single;
            SetUIVisible(false);
            roomPanel.ResetToSingleMode();
        }
    }
    /// <summary>
    /// AI UI 전체 표시 상태 갱신
    /// </summary>
    private void RefreshUIVisibility()
    {
        _difficultyDropdown.gameObject.SetActive(true);
        _laveltext.gameObject.SetActive(true);
        _ainame.gameObject.SetActive(true);
    }
    /// <summary>
    /// 게임 시작 시 AI UI 일부 비활성화
    /// 난이도 선택 UI 숨기고 게임 표시용 UI로 전환
    /// </summary>
    public void OnStartGameUI()
    {
        _difficultyDropdown.gameObject.SetActive(false);
        _laveltext.gameObject.SetActive(false);
        _ainame.gameObject.SetActive(true);
    }
    /// <summary>
    /// AI 설정 UI 전체 표시/숨김 처리
    /// </summary>
    private void SetUIVisible(bool visible)
    {
        // Null 레퍼런스 방지를 위한 체크 (안전장치)
        if (_difficultyDropdown != null) _difficultyDropdown.gameObject.SetActive(visible);
        if (_laveltext != null) _laveltext.gameObject.SetActive(visible);
        if (_ainame != null) _ainame.gameObject.SetActive(visible);
    }
    /// <summary>
    /// AI 난이도 선택 처리
    /// </summary>
    private void OnChanged(int index)
    {
        GomokuAIDifficulty difficulty;
        switch (index)
        {
            case 0: difficulty = GomokuAIDifficulty.Easy; _ainame.text = "Beginner Bot"; break;
            case 1: difficulty = GomokuAIDifficulty.Normal; _ainame.text = "Advanced Bot"; break;
            case 2: difficulty = GomokuAIDifficulty.Hard; _ainame.text = "Expert Bot"; break;
            default: difficulty = GomokuAIDifficulty.Easy; _ainame.text = "Beginner Bot"; break;
        }
        GomokuManager.I.SetAIDifficulty(difficulty);
    }
}