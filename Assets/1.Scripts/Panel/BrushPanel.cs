using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class BrushPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup cg;
    [SerializeField] private TMP_Text text;

    /// <summary>
    /// 간파하기에 사용되는 메세지
    /// </summary>
    public async void ShowFindFail(int remainCount) // 실패했을때
    {
        await SetText($"<color=#7A2E38>간파하기 실패했습니다.</color> {GetRemainText(remainCount)}");
    }

    public async void ShowFind_TransparentStone(int remainCount) // 투명돌 찾았을대
    {
        await SetText($"<color=#22C55E>[간파성공]</color> <color=#7A2E38>투명돌을 제거합니다.</color> {GetRemainText(remainCount)}");
    }

    public async void ShowFind_FakeStone(int remainCount) // 가짜돌 찾았을때
    {
         await SetText($"<color=#22C55E>[간파성공]</color> <color=#7A2E38>가짜돌을 제거합니다.</color> {GetRemainText(remainCount)}");
    }

    public async void ShowFind_DoubleMarker(int remainCount) // 가짜마커 찾았을때
    {
         await SetText($"<color=#22C55E>[간파성공]</color> <color=#7A2E38>가짜마커 제거합니다.</color> {GetRemainText(remainCount)}");
    }
    private string GetRemainText(int remainCount)
    {
        int nextCount = remainCount - 1;
        return nextCount > 0 ? $" <color=#DE7935>사용 가능 횟수 {nextCount}회</color>" : "";
    }
    /// <summary>
    /// 투명돌에 사용되는 메세지
    /// </summary>
    public async void TransparentStone_Fail() // 사용할수없는곳에 착수했을때 
    {
        await SetText("<color=#E53935>그 자리에는 사용할수없습니다</color>");
    }
    public async void TransparentStone_Clear() // 사용할수있는곳에 착수했을때
    {
        await SetText("<color=#16A34A>[투명돌]</color> <color=#7A2E38>투명돌 효과가 적용됩니다.</color>");
    }
    public async void TransparentStone_Fake() //투명돌 함정 건렸을떄
    {
        await SetText("<color=#E53935>[투명돌]</color> <color=#7A2E38>투명돌 함정 발동.</color>");
    }
    /// <summary>
    /// 가짜돌에 사용되는 메세지
    /// </summary>
    public async void FakeStone_Fail() // 사용할수없는곳에 착수했을때 
    {
        await SetText("<color=#E53935>그 자리에는 사용할수없습니다</color>");
    }
    public async void FakeStone_Clear() // 사용할수있는곳에 착수했을때
    {
        await SetText("<color=#16A34A>[가짜돌]</color> <color=#7A2E38>가짜돌 착수.</color>");
    }
    public async void FakeStone_Fake() // 가짜돌 함정 걸렸을때
    {
        await SetText("<color=#E53935>[가짜돌]</color> <color=#7A2E38>가짜돌 함정 발동.</color>");
    }
    /// <summary>
    /// 돌바꾸기에 사용되는 메세지
    /// </summary>
    public async void StoneSwap() // 돌바꾸기 아이템 사용했을 시
    {
        await SetText("<color=#C62828>[돌 바꾸기] 발동! 판의 돌 색상이 바뀝니다.</color>");
    }
    /// <summary>
    /// 더블표시에 사용되는 메세지
    /// </summary>
    public async void DoubleMarker() // 더블표시 아이템 사용했을 시
    {
        await SetText("<color=#7A2E38>[더블 표시] 가짜 착수 위치가 생성됩니다.</color>");
    }
    public async void DoubleMarker_Fake() // 더블표시 아이템 사용했을 시
    {
        await SetText("<color=#E53935>[가짜돌]</color> <color=#7A2E38>가짜돌 함정 발동.</color>");
    }
    /// <summary>
    /// 착수숨김에 사용되는 메세지
    /// </summary>
    public async void shouldHideNextMarker() // 더블표시 아이템 사용했을 시
    {
        await SetText("<color=#7A2E38>[착수 숨김] 착수 위치를 숨깁니다.</color>");
    }
    /// <summary>
    /// 타이머감소에 사용되는 메세지
    /// </summary>
    public async void ShowTimerItem_1() // 사용한 본인 한테 보일 메세지
    {
        await SetText("<color=#7A2E38>[타이머감소]</color> 상대방 제한시간을 감소시킵니다.");
    }
    public async void ShowTimerItem_2() // 상대방한테 보일 메세지 
    {
        await SetText("<color=#E53935>타이머감소</color> 제한시간 감소 되었습니다.");
    }
    /// <summary>
    /// 금수 메세지
    /// </summary>
    public async void forbidden() // 금수 위치 클릭때 나올메세지
    {
        await SetText("<color=#FF0000>[경고]</color> 금수 자리입니다.");
    }
    public async void ShowFindItem(ItemType itemType)
    {
        var itemName = string.Empty;
        switch (itemType)
        {
            case ItemType.TransparentStone:
                itemName = "투명 돌";
                break;
            case ItemType.FakeStone:
                itemName = "가짜돌";
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
