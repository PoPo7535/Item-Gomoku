using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class BrushPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup cg;
    [SerializeField] private TMP_Text text;
    public async void ShowFindFail()
    {
        await SetText("<color=#7A2E38>간파하기 실패했습니다.</color>");
    }
    public async void ShowTimerItem()
    {
        await SetText("상대방이 <color=#7A2E38>타이머감소</color> 아이템을 사용하여 제한시간이 감소되었습니다.");
    }
    public async void ShowFindItem(ItemType itemType)
    {
        var itemName = string.Empty;
        switch (itemType)
        {
            case ItemType.TransparentStone:
                itemName = "착수숨김";
                break;
            default:
                "예외발생".WarningLog();
                break;
        }
        await SetText($"<color=#7A2E38>{itemName}</color> 아이템위에 착수를 시도했습니다.");
    }

    private async Task SetText(string text)
    {
        cg.ActiveCG(true);
        this.text.text = text;
        await UniTask.WaitForSeconds(3);
        cg.ActiveCG(false);
    }
}
