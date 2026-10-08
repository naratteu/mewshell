using Aprillz.MewUI.Controls;
using Microsoft.AspNetCore.Components;
using Naratteu.MewUI.Razor.Components;

namespace MewShell.Razor;

// Typed selectors: rows in, the picked row out, the way a controlled input works in React --
// Selected is the value, SelectedChanged is the setter. Each keeps every parameter of the MewUI
// control it wraps (Width, Margin, Row, Column...) by deriving from its generated base.

public sealed class MewList<TItem> : MewListBoxBase<ListBox> where TItem : class
{
    private readonly Selection<TItem> _selection = new();

    public MewList() => Control.SelectionChanged += item =>
    {
        if (!IsApplyingParameters) _ = SelectedChanged.InvokeAsync(item as TItem);
    };

    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }

    [Parameter] public Func<TItem, string>? Text { get; set; }

    [Parameter] public TItem? Selected { get; set; }

    [Parameter] public EventCallback<TItem?> SelectedChanged { get; set; }

    protected override void ApplyParameters()
    {
        base.ApplyParameters();
        if (_selection.Apply(Items, Text, Selected, view => Control.ItemsSource = view, index => Control.SelectedIndex = index))
        {
            _ = SelectedChanged.InvokeAsync(null);
        }
    }
}

public sealed class MewDropdown<TItem> : MewDropDownBaseBase<ComboBox> where TItem : class
{
    private readonly Selection<TItem> _selection = new();

    public MewDropdown() => Control.SelectionChanged += item =>
    {
        if (!IsApplyingParameters) _ = SelectedChanged.InvokeAsync(item as TItem);
    };

    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }

    [Parameter] public Func<TItem, string>? Text { get; set; }

    [Parameter] public TItem? Selected { get; set; }

    [Parameter] public EventCallback<TItem?> SelectedChanged { get; set; }

    protected override void ApplyParameters()
    {
        base.ApplyParameters();
        if (_selection.Apply(Items, Text, Selected, view => Control.ItemsSource = view, index => Control.SelectedIndex = index))
        {
            _ = SelectedChanged.InvokeAsync(null);
        }
    }
}

public sealed class MewTable<TItem> : MewScrollableItemsBaseBase<GridView> where TItem : class
{
    private readonly Selection<TItem> _selection = new();
    private IReadOnlyList<GridViewColumn<TItem>>? _columns;

    public MewTable() => Control.SelectionChanged += item =>
    {
        if (!IsApplyingParameters) _ = SelectedChanged.InvokeAsync(item as TItem);
    };

    [Parameter] public IReadOnlyList<TItem>? Items { get; set; }

    [Parameter] public IReadOnlyList<GridViewColumn<TItem>>? Columns { get; set; }

    [Parameter] public TItem? Selected { get; set; }

    [Parameter] public EventCallback<TItem?> SelectedChanged { get; set; }

    protected override void ApplyParameters()
    {
        base.ApplyParameters();

        if (Columns is not null && !ReferenceEquals(Columns, _columns)) Control.SetColumns(_columns = Columns);

        if (_selection.Apply(Items, null, Selected, view => Control.ItemsSource = view, index => Control.SelectedIndex = index))
        {
            _ = SelectedChanged.InvokeAsync(null);
        }
    }
}
