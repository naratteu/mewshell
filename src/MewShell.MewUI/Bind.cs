using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace MewShell.MewUI;

/// <summary>
/// Puts a query's rows into a selector and its choice into a value: the edge from one command's
/// output to the next command's argument.
/// </summary>
public static class BindExtensions
{
    public static ListBox Bind<T>(
        this ListBox list, Query<IReadOnlyList<T>> rows, ObservableValue<T?> selected, Func<T, string>? text = null)
        where T : class
    {
        var link = new Link<T>(rows, selected, text, view => list.ItemsSource = view, index => list.SelectedIndex = index);
        list.SelectionChanged += link.Picked;
        return list;
    }

    public static ComboBox Bind<T>(
        this ComboBox box, Query<IReadOnlyList<T>> rows, ObservableValue<T?> selected, Func<T, string>? text = null)
        where T : class
    {
        var link = new Link<T>(rows, selected, text, view => box.ItemsSource = view, index => box.SelectedIndex = index);
        box.SelectionChanged += link.Picked;
        return box;
    }

    public static GridView Bind<T>(this GridView grid, Query<IReadOnlyList<T>> rows, ObservableValue<T?> selected)
        where T : class
    {
        var link = new Link<T>(rows, selected, null, view => grid.ItemsSource = view, index => grid.SelectedIndex = index);
        grid.SelectionChanged += link.Picked;
        return grid;
    }

    private sealed class Link<T> where T : class
    {
        private readonly Query<IReadOnlyList<T>> _rows;
        private readonly ObservableValue<T?> _selected;
        private readonly Func<T, string> _text;
        private readonly Action<ISelectableItemsView> _setItems;
        private readonly Action<int> _setIndex;
        private bool _applying;

        public Link(
            Query<IReadOnlyList<T>> rows, ObservableValue<T?> selected, Func<T, string>? text,
            Action<ISelectableItemsView> setItems, Action<int> setIndex)
        {
            (_rows, _selected, _text, _setItems, _setIndex) = (rows, selected, text ?? (item => item.ToString() ?? ""), setItems, setIndex);
            rows.Value.Changed += () => Apply(arrived: true);
            Apply(arrived: false);
        }

        public void Picked(object? item)
        {
            // Replacing the items resets the control's selection; that is not the user choosing.
            if (!_applying) _selected.Value = item as T;
        }

        /// <param name="arrived">
        /// False while the query has not answered yet: a choice made up front ("main") must survive
        /// until there are rows to check it against.
        /// </param>
        private void Apply(bool arrived)
        {
            var items = _rows.Value.Value ?? [];
            var index = _selected.Value is { } current ? IndexOf(items, current) : -1;

            _applying = true;
            try
            {
                _setItems(ItemsView.Create(items, _text, item => item));
                _setIndex(index);
            }
            finally
            {
                _applying = false;
            }

            // The choice is not among the new rows, so nothing downstream should keep running on it.
            if (arrived && index < 0 && _selected.Value is not null) _selected.Value = null;
        }

        private static int IndexOf(IReadOnlyList<T> items, T item)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(items[i], item)) return i;
            }

            return -1;
        }
    }
}
