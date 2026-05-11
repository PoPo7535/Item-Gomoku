using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UISoundHelper : MonoBehaviour
{
    [SerializeField] private List<Toggle> toggles = new();
    [SerializeField] private List<Button> buttons = new();

    [Button]
    public void GetUi()
    {
        toggles = FindObjectsByType<Toggle>(FindObjectsSortMode.None).ToList();
        buttons = FindObjectsByType<Button>(FindObjectsSortMode.None).ToList();
    }

    public void Start()
    {
        foreach (var toggle in toggles)
        {
            toggle.onValueChanged.AddListener(isOn =>
            {
                SoundManager.I.PlaySound("click");
            });
            var trigger = toggle.GetOrAddComponent<EventTrigger>();
            SetEvent(trigger);
        }
        foreach (var button in buttons)
        {
            button.onClick.AddListener(() =>
            {
                SoundManager.I.PlaySound("click");
            });
            var trigger = button.GetOrAddComponent<EventTrigger>();
            SetEvent(trigger);
        }
    }

    private void SetEvent(EventTrigger trigger)
    {
        var entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };

        entry.callback.AddListener((eventData) =>
        {
            OnPointerEnter((PointerEventData)eventData);
        });

        trigger.triggers.Add(entry);
        void OnPointerEnter(PointerEventData data)
        {
            SoundManager.I.PlaySound("onmouse");
        }
    }
}
