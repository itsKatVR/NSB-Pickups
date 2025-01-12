using System;
using System.Collections;
using System.Collections.Generic;

public class CachedList<T> : IEnumerable<T>
{
    private readonly List<T> _list;
    private int _cachedCount;
    private bool _isCountValid;

    public CachedList()
    {
        _list = new List<T>();
        _isCountValid = false;
    }

    public CachedList(IEnumerable<T> collection)
    {
        _list = new List<T>(collection);
        _cachedCount = _list.Count;
        _isCountValid = true;
    }

    public int Count
    {
        get
        {
            if (!_isCountValid)
            {
                _cachedCount = _list.Count;
                _isCountValid = true;
            }
            return _cachedCount;
        }
    }

    public void Add(T item)
    {
        _list.Add(item);
        InvalidateCount();
    }

    public void AddRange(IEnumerable<T> collection)
    {
        _list.AddRange(collection);
        InvalidateCount();
    }

    public bool Remove(T item)
    {
        bool removed = _list.Remove(item);
        if (removed)
        {
            InvalidateCount();
        }
        return removed;
    }

    public void RemoveAt(int index)
    {
        _list.RemoveAt(index);
        InvalidateCount();
    }

    public void Clear()
    {
        _list.Clear();
        InvalidateCount();
    }

    public T this[int index]
    {
        get => _list[index];
        set => _list[index] = value;
    }

    private void InvalidateCount()
    {
        _isCountValid = false;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
