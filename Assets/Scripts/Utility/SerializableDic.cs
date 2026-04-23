using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableDic<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>,ISerializationCallbackReceiver
{
    [SerializeField] private List<Pair> Dictionary = new();
    private Dictionary<TKey, TValue> dic = new();

    [Serializable]
    public struct Pair
    {
        public TKey key;
        public TValue value;
    }
    
    public Dictionary<TKey, TValue>.KeyCollection Keys => dic.Keys;


    public void Clear()
    {
        dic.Clear();
        Dictionary.Clear();
    }

    public void Add(TKey key, TValue value)
    {
        if (Application.isPlaying)
            return;

        Dictionary.Add(new Pair()
        {
            key = key,
            value = value
        });
    }

    public bool ContainsKey(TKey key) => dic.ContainsKey(key);

    public bool TryGetValue(TKey key, out TValue value)
    {
        value = dic.GetValueOrDefault(key);
        return dic.ContainsKey(key);
    }
    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize()
    {
        dic.Clear();
        foreach (var pair in Dictionary)
        {
            if (pair.key == null)
                continue;
            if (false == dic.TryAdd(pair.key, pair.value))
            {
                "Error".Log();
            }
        }
    }
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => dic.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => dic.GetEnumerator();
}

[Serializable]
public class SerializableDicInList<TKey, TValue> : IEnumerable<KeyValuePair<TKey, List<TValue>>>,ISerializationCallbackReceiver
{
    public SerializableDicInList()
    {
        Build();
    }
    [Serializable]
    public struct Pair
    {
        public TKey key;
        public TValue value;
    }

    [SerializeField] private List<Pair> Dictionary;
    private Dictionary<TKey, List<TValue>> dic = new();

    public void Build()
    {
        foreach (var pair in Dictionary)
        {
            if (false == dic.ContainsKey(pair.key))
                dic.TryAdd(pair.key, new List<TValue>());
            dic[pair.key].Add(pair.value);
        }
    }

    public void Clear()
    {
        dic.Clear();
        Dictionary.Clear();
    }

    public void Add(TKey key, TValue value)
    {
        if (Application.isPlaying)
            return;

        Dictionary.Add(new Pair()
        {
            key = key,
            value = value
        });
    }

    public bool ContainsKey(TKey key) => dic.ContainsKey(key);

    public bool TryGetValue(TKey key, out List<TValue> value)
    {
        value = dic.GetValueOrDefault(key);
        return dic.ContainsKey(key);
    }
    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize()
    {
        dic.Clear();

        foreach (var pair in Dictionary)
        {
            if (!dic.TryGetValue(pair.key, out var list))
            {
                list = new List<TValue>();
                dic[pair.key] = list;
            }
            list.Add(pair.value);
        }
    }
    public IEnumerator<KeyValuePair<TKey, List<TValue>>> GetEnumerator() => dic.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => dic.GetEnumerator();
}