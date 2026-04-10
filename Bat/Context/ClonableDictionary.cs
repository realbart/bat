namespace Bat.Context;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Copy-on-write dictionary using stack-based delta layers.
/// null values encode "key does not exist" (deleted).
/// </summary>
internal class ClonableDictionary<TKey, TValue>() : IDictionary<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private Stack _stack = new();

    private sealed class Stack
    {
        public Dictionary<TKey, TValue?> Dictionary = new();
        public Stack? Parent;

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (Dictionary.TryGetValue(key, out var val))
            {
                value = val!;
                return val != null;
            }
            if (Parent != null) return Parent.TryGetValue(key, out value);
            value = default;
            return false;
        }

        public bool ContainsKey(TKey key) =>
            Dictionary.TryGetValue(key, out var val)
                ? val != null
                : Parent?.ContainsKey(key) ?? false;
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) =>
        _stack.TryGetValue(key, out value);

    public bool TryAdd(TKey key, TValue value)
    {
        if (_stack.ContainsKey(key)) return false;
        _stack.Dictionary[key] = value;
        return true;
    }

    public ClonableDictionary<TKey, TValue> Clone()
    {
        if (_stack.Dictionary.Count > 0) _stack = new Stack { Parent = _stack };
        return new ClonableDictionary<TKey, TValue> { _stack = new Stack { Parent = _stack.Parent } };
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out var v) ? v : throw new KeyNotFoundException();
        set => _stack.Dictionary[key] = value;
    }

    public void Add(TKey key, TValue value)
    {
        if (_stack.ContainsKey(key)) throw new ArgumentException("Key already exists");
        _stack.Dictionary[key] = value;
    }

    public bool ContainsKey(TKey key) => _stack.ContainsKey(key);

    public bool Remove(TKey key)
    {
        if (!_stack.ContainsKey(key)) return false;
        _stack.Dictionary[key] = null;
        return true;
    }

    public ICollection<TKey> Keys => this.Select(kv => kv.Key).ToList();
    public ICollection<TValue> Values => this.Select(kv => kv.Value).ToList();
    public int Count => this.Count();
    public bool IsReadOnly => false;

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
    public void Clear() => _stack = new();
    public bool Contains(KeyValuePair<TKey, TValue> item) =>
        TryGetValue(item.Key, out var v) && EqualityComparer<TValue>.Default.Equals(v, item.Value);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        var index = arrayIndex;
        foreach (var kv in this)
            array[index++] = kv;
    }
    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        var seen = new HashSet<TKey>();
        var current = _stack;

        while (current != null)
        {
            foreach (var kv in current.Dictionary)
            {
                if (seen.Add(kv.Key) && kv.Value != null)
                    yield return new KeyValuePair<TKey, TValue>(kv.Key, kv.Value);
            }
            current = current.Parent;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

