using System.Globalization;
using System.Windows.Controls;
using Faktum.ScreenMarker.App.Interaction;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace Faktum.ScreenMarker.App.Toolbar;

internal sealed class ToolbarFontSizeSelectionController
{
    private readonly ToolbarInteractionCoordinator _coordinator;
    private WpfComboBox? _comboBox;
    private bool _syncing;
    private SelectionChangedEventHandler? _selectionChangedHandler;

    public ToolbarFontSizeSelectionController(ToolbarInteractionCoordinator coordinator) =>
        _coordinator = coordinator;

    public void Register(WpfComboBox comboBox)
    {
        _comboBox = comboBox;
        foreach (var size in ToolbarTextFontSizeValues.SelectableSizes)
        {
            comboBox.Items.Add(size.ToString("0", CultureInfo.InvariantCulture));
        }

        _selectionChangedHandler = (_, _) => OnSelectionChanged();
        comboBox.SelectionChanged += _selectionChangedHandler;
        SyncSelection();
    }

    public void Detach()
    {
        if (_comboBox is not null && _selectionChangedHandler is not null)
        {
            _comboBox.SelectionChanged -= _selectionChangedHandler;
            _comboBox = null;
            _selectionChangedHandler = null;
        }
    }

    public void SyncSelection()
    {
        if (_comboBox is null)
        {
            return;
        }

        _syncing = true;
        try
        {
            var label = _coordinator.ActiveTextFontSize.ToString("0", CultureInfo.InvariantCulture);
            _comboBox.SelectedItem = label;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnSelectionChanged()
    {
        if (_syncing || _comboBox?.SelectedItem is not string label)
        {
            return;
        }

        if (!double.TryParse(label, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
        {
            return;
        }

        _coordinator.SelectTextFontSize(size);
    }
}
