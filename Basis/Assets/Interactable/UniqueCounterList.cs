using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class UniqueCounterList<T> : IEnumerable<T> where T : UnityEngine.Object
{
    private readonly List<UItem> _list;
    private readonly HashSet<T> _hashSet;
    private int _cachedCount;
    private bool _isCountValid;

    public UniqueCounterList()
    {
        _list = new List<UItem>();
        _hashSet = new HashSet<T>();
        _isCountValid = false;
    }
    
    public struct UItem
    {
        public T Item;
        public int Counter;
    }

    public UniqueCounterList(IEnumerable<T> collection)
    {
        var unique = collection.GroupBy(s => s.GetInstanceID(), s => s, (k, v) => new UItem { Item = (T)v, Counter = k});

        _list = new List<UItem>(unique);
        _hashSet = new HashSet<T>(collection);
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
        if (_hashSet.Contains(item))
        {
            int index = _list.FindIndex(x => x.Item.GetInstanceID() == item.GetInstanceID());
            Debug.Assert(index >= 0);
            var old = _list[index];
            old.Counter++;
            _list[index] = old;
        }
        else
        {
            _list.Add(new UItem {Item = item, Counter = 1});
            _hashSet.Add(item);
            InvalidateCount();
        }
    }

    // public void AddRange(IEnumerable<T> collection)
    // {
    //     _list.AddRange(collection);
    //     InvalidateCount();
    // }

    public bool Remove(T item)
    {
        bool removed = false;

        if(_hashSet.Contains(item)) {
            int index = _list.FindIndex(x => x.Item.GetInstanceID() == item.GetInstanceID());
            Debug.Assert(index >= 0);
            var old = _list[index];
            old.Counter--;
            _list[index] = old;
            removed = old.Counter <= 0;
        }
        if (removed)
        {
            bool listRemoved = _list.RemoveAll(x => x.Item.GetInstanceID() == item.GetInstanceID()) > 0;
            bool hashSetRemoved = _hashSet.Remove(item);
            Debug.Assert(listRemoved && hashSetRemoved);
            InvalidateCount();
        }
        return removed;
    }

    public bool Contains(T item)
    {
        return _hashSet.Contains(item);
    }
    public int GetCounterOf(T item)
    {
        if(!_hashSet.Contains(item)) return 0;
        return _list.Find(x => x.Item.GetInstanceID() == item.GetInstanceID()).Counter;
    }

    // public void RemoveAt(int index)
    // {
    //     _list.RemoveAt(index);
    //     InvalidateCount();
    // }

    public void Clear()
    {
        _list.Clear();
        _hashSet.Clear();
        InvalidateCount();
    }

    public T this[int index]
    {
        get => _list[index].Item;
        // set => _list[index] = value;
    }

    private void InvalidateCount()
    {
        _isCountValid = false;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _list.Select(x => x.Item).GetEnumerator();
    }

    public IEnumerator<UItem> GetCountersEnumerator()
    {
        return _list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
