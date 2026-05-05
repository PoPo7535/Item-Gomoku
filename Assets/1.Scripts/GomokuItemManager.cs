using UnityEngine;
using Utility;
public enum ItemUseState
{
    None,
    Selecting,
    Using
}
public class GomokuItemManager : MonoBehaviour
{   
    public static GomokuItemManager I;
    public GomokuItem CurrentSelectedItem; // 선택된 아이템 여기에 담자
    
    private void Awake()
    {
        I = this;
    }
    
    public void SelectItem(GomokuItem item) // ui 에 아이템 서택시 호출 // 토글
    {
        if (item == null)
        {
            CurrentSelectedItem = null;
            Debug.Log("아이템 선택 해제");
            return;
        }

        if (CurrentSelectedItem == item)
        {
            CurrentSelectedItem = null;
            Debug.Log("아이템 선택 해제");
            return;
        }

        CurrentSelectedItem = item;
        Debug.Log($"선택된 아이템 이름 : {item.name}");
    }

    public bool TryUseItem(int x, int z) // 실제 아이템 사용 
    {
        if (CurrentSelectedItem == null)
            return false;

        if (!GomokuManager.I.IsMyTurn()) // 자기턴 아닐때 사용방지 사실없어도되긴함
        {
            Debug.Log("내 턴 아님");
            return false;
        }

        bool success = false;

        switch (CurrentSelectedItem.name)
        {
            case "더블 표시":
                GomokuManager.I.RemoveStoneProcess(x, z);
                success = true;
                break;

            case "가짜 돌":
                // 아이템 효과
                success = true;
                break;

            case "착수 숨김":
                // 아이템 효과
                success = true;
                break;
            case "돌 바꾸기":
                // 아이템 효과
                success = true;
                break;
            case "타이머 감소":
                // 아이템 효과
                success = true;
                break;
            case "투명 돌":
                // 아이템 효과
                success = true;
                break;
        }

        if (success)
            CurrentSelectedItem = null;

        return success;
    }


}