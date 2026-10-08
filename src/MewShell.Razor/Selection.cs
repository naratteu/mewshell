using Aprillz.MewUI;

namespace MewShell.Razor;

/// <summary>Keeps a selector's items and selection in step with typed parameters.</summary>
internal sealed class Selection<TItem> where TItem : class
{
    private IReadOnlyList<TItem>? _shown;

    /// <returns>
    /// True when the selected item is not among items that have arrived: the owner has to hear
    /// that the choice is gone, so nothing downstream keeps running on it.
    /// </returns>
    public bool Apply(
        IReadOnlyList<TItem>? items, Func<TItem, string>? text, TItem? selected,
        Action<ISelectableItemsView> setItems, Action<int> setIndex)
    {
        // Rebuilt only when the list itself is new, so an unrelated render keeps scroll and selection.
        if (!ReferenceEquals(items, _shown))
        {
            _shown = items;
            setItems(ItemsView.Create(items ?? [], text ?? (item => item.ToString() ?? ""), item => item));
        }

        var index = selected is null || items is null ? -1 : IndexOf(items, selected);
        setIndex(index);
        return items is not null && selected is not null && index < 0;
    }

    private static int IndexOf(IReadOnlyList<TItem> items, TItem item)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (EqualityComparer<TItem>.Default.Equals(items[i], item)) return i;
        }

        return -1;
    }
}
