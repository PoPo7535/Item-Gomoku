using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InfoBox : MonoBehaviour
{
    [SerializeField] private EventTrigger eventTrigger;
    [SerializeField] private Image image;
    [SerializeField] private TMP_Text text;

    public void PointEnter(BaseEventData eventData)
    {
        1.Log();
        image.DOFade(1,0.3f);
        text.DOFade(1,0.3f);
    }

    public void PointExit(BaseEventData eventData)
    {
        2.Log();
        image.DOFade(0,0.3f);
        text.DOFade(0,0.3f);
    }
}
