using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using ColorUtility = UnityEngine.ColorUtility;


public static class Extensions
{
    public static List<Transform> GetAllChild(this Transform transform)
    {
        Queue<Transform> queue = new();
        List<Transform> list = new();
        queue.Enqueue(transform);
        
        while (0 != queue.Count)
        {
            var tr = queue.Dequeue();
            list.Add(tr);
            for (int i = 0; i<tr.childCount; ++i)
            
                queue.Enqueue(tr.GetChild(i));
        }
        return list;
    }

    public static Tween ActiveCG(this CanvasGroup _cg, bool _isShow, float _duration = 0.3f, Action _start = null,
        Action _complete = null)
    {
        return DOTween.To(() => _cg.alpha, x => _cg.alpha = x, _isShow ? 1 : 0, _duration)
            .OnStart(() =>
            {
                _start?.Invoke();

                _cg.alpha = _isShow ? 0 : 1;
                _cg.blocksRaycasts = false;
                // _cg.interactable = false;
            })
            .OnComplete(() =>
            {
                _cg.blocksRaycasts = _isShow;
                // _cg.interactable = _isShow;
                _complete?.Invoke();
            });
    }

    public static T Next<T>(this T value) where T : struct, Enum
    {
        var values = (T[])Enum.GetValues(typeof(T));
        var index = Array.IndexOf(values, value);
        return Enum.Parse<T>(values[(index + 1) % values.Length].ToString());
    }
    public static bool IsLast<T>(this T value) where T : struct, Enum
    {
        var values = Enum.GetValues(value.GetType()).Cast<T>().ToArray();
        return EqualityComparer<T>.Default.Equals(value, values.Last());
    }

    public static T[] ToArray<T>(this T value) where T : Enum
    {
        return Enum.GetValues(value.GetType()).Cast<T>().ToArray();
    }
    
    public static void SetParent(this RectTransform rect, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var pos = rect.anchoredPosition;
        var size = rect.sizeDelta;
        rect.SetParent(parent);
        rect.anchorMax = anchorMax;
        rect.anchorMin = anchorMin;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    private static string TextColor(this object _object, Color _clr) => $"<color=#{ColorUtility.ToHtmlStringRGBA(_clr)}>{_object}</color>";
    private static string TextSize (this object _object, int _fontSize = 13) => $"<size={_fontSize}>{_object}</size>";
    private static string Text     (this object _object, Color _clr, int _fontSize = 13) => _object.TextColor(_clr).TextSize(_fontSize);

    private static string StackTransValue(int _fontSize)
    {
#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_STANDALONE_WIN)
        var str = "";
#else
        var sf = new StackTrace(true).GetFrame(2);

        var str = sf.GetMethod().ReflectedType!.Name.TextColor(Color.blue * 0.8f) +
                  ".".TextColor(Color.white) +
                  sf.GetMethod().Name.TextColor(Color.magenta * 0.8f) +
                  ":".TextColor(Color.white) +
                  sf.GetFileLineNumber().TextColor(Color.green);
        str = (Time.time + "[" + str + "]").Text(Color.black, _fontSize);
#endif
        return str;
    }
    public static object Log(this object _string, Color _clr = default, int _fontSize = 14)
    {
        _clr = _clr == default ? Color.white : _clr;
        UnityEngine.Debug.Log(StackTransValue(_fontSize) + Text(_string, _clr, _fontSize));
        return _string;
    }

    public static object WarningLog(this object _string, Color _clr = default, int _fontSize = 14)
    {
        _clr = _clr == default ? Color.yellow : _clr;
        UnityEngine.Debug.LogWarning(StackTransValue(_fontSize) + Text(_string, _clr, _fontSize));
        return _string;
    }

    public static object ErrorLog(this object _string, Color _clr = default, int _fontSize = 16)
    {
        _clr = _clr == default ? Color.red : _clr;
        UnityEngine.Debug.LogError(StackTransValue(_fontSize) + Text(_string, _clr, _fontSize));
        return _string;
    }
}
