using System;
using System.Collections.Generic;
namespace Worksheet.App.Views.Support
{
    public class SelectionManager<T> where T : class
    {
        private readonly Dictionary<T, (Action onSelect, Action onDeselect)> _items = new();

        public T? Selected { get; private set; }

        public event Action<T?>? SelectionChanged;

        public void Select(T item)
        {
            if (Selected == item)
                return;

            Deselect();

            Selected = item;

            if (_items.TryGetValue(item, out var callbacks))
                callbacks.onSelect();

            SelectionChanged?.Invoke(item);
        }

        public void Deselect()
        {
            if (Selected == null)
                return;

            if (_items.TryGetValue(Selected, out var callbacks))
                callbacks.onDeselect();

            Selected = null;
            SelectionChanged?.Invoke(null);
        }

        public void Register(T item, Action onSelect, Action onDeselect)
        {
            _items[item] = (onSelect, onDeselect);
        }

        public void Unregister(T item)
        {
            if (Selected == item)
                Deselect();

            _items.Remove(item);
        }
    }
}
