using UnityEngine;
using Utility;
public enum ItemUseState
{
    None,
    Selecting,
    Using
}
public class GomokuItemManager : LocalFusionSingleton<GomokuItemManager>
{
    public GomokuItem CurrentSelectedItem; // 선택된 아이템 여기에 담자

    public void SelectItem(GomokuItem item) // UI에서 아이템 선택 시 호출 // 토글 
    {
       
        if (CurrentSelectedItem == item)
        {
            CurrentSelectedItem = null;
            return;
        }
        CurrentSelectedItem = item;
        Debug.Log($"선택된 아이템 이름 : {CurrentSelectedItem.name}");
    }

    public bool TryUseItem(int x, int z) // 실제 아이템 사용 
    {
        if (CurrentSelectedItem == null)
            return false;

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