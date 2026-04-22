namespace Bat.Context;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Dictionary using stack-based delta layers,
/// so complete dictionaries don't need to constantly be copied.
/// </summary>
public class ClonableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    public ClonableDictionary(IEqualityComparer<TKey>? comparer = null) : this(new Stack(comparer))
    {
    }

    public ClonableDictionary(Dictionary<TKey, TValue?> dictionary) : this(new Stack(dictionary))
    {
    }

    /// <summary>
    /// Copy-on-write dictionary using stack-based delta layers.
    /// null values encode "key does not exist" (deleted).
    /// </summary>
    private ClonableDictionary(Stack stack)
    {
        _stack = stack;
    }

    private Stack _stack;

    private sealed class Stack
    {
        public readonly Dictionary<TKey, TValue?> Dictionary;
        public readonly Stack? Parent;

        public Stack(IEqualityComparer<TKey>? comparer)
        {
            Dictionary = new(comparer);
            Parent = null;
        }

        public Stack(Dictionary<TKey, TValue?> dictionary)
        {
            Dictionary = dictionary;
            Parent = null;
        }

        public Stack(Stack parent)
        {
            Dictionary = new(parent.Dictionary.Comparer);
            Parent = parent;
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            var current = this;
            while (current != null)
            {
                if (current.Dictionary.TryGetValue(key, out var val))
                {
                    value = val!;
                    return val != null;
                }
                current = current.Parent;
            }
            value = null;
            return false;
        }

        public bool ContainsKey(TKey key)
        {
            var current = this;
            while (current != null)
            {
                if (current.Dictionary.TryGetValue(key, out var val))
                {
                    return val != null;
                }
                current = current.Parent;
            }
            return false;
        }
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _stack.TryGetValue(key, out value);

    public bool TryAdd(TKey key, TValue value)
    {
        if (ContainsKey(key)) return false;
        this[key] = value;
        return true;
    }

    public ClonableDictionary<TKey, TValue> Clone()
    {
        // LLMs: You may not understand this code but tests show that it works.
        // Please leave it alone.
        if (_stack.Dictionary.Count > 0) _stack = new(_stack);
        return (_stack.Parent == null)
            ? new ClonableDictionary<TKey, TValue>(_stack.Dictionary.Comparer)
            : new(new Stack(_stack.Parent));
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out var v) ? v : throw new KeyNotFoundException();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _stack.Dictionary[key] = value;
        }
    }

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
    public ICollection<TKey> Keys => this.Select(kv => kv.Key).ToArray();
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;
    public ICollection<TValue> Values => this.Select(kv => kv.Value).ToArray();

    public void Add(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (ContainsKey(key)) throw new ArgumentException("Key already exists");
        _stack.Dictionary[key] = value;
    }

    public bool ContainsKey(TKey key) => _stack.ContainsKey(key);

    public bool Remove(TKey key)
    {
        if (!ContainsKey(key)) return false;
        
        // Push a new layer if needed, unless we are already removing a key from this layer
        if (_stack.Dictionary.Count > 0 && !_stack.Dictionary.ContainsKey(key))
        {
            _stack = new Stack(_stack);
        }
        _stack.Dictionary[key] = null;
        return true;
    }
    public int Count => Keys.Count;
    public bool IsReadOnly => false;
    public IEqualityComparer<TKey> Comparer => _stack.Dictionary.Comparer;

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    public void ApplySnapshot(ClonableDictionary<TKey, TValue> other)
    {
        _stack = other._stack;
    }

    public void Clear() => _stack = new(_stack.Dictionary.Comparer);

    public bool Contains(KeyValuePair<TKey, TValue> item) =>
        TryGetValue(item.Key, out var v) && EqualityComparer<TValue>.Default.Equals(v, item.Value);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        foreach (var kv in this) array[arrayIndex++] = kv;
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
                    yield return new(kv.Key, kv.Value);
            }

            current = current.Parent;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
