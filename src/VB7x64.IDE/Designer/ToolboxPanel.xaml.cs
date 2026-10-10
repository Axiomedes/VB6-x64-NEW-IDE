using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using VB7x64.ProjectModel.Designers;

namespace VB7x64.IDE.Designer;

public partial class ToolboxPanel : UserControl, INotifyPropertyChanged
{
    public class ToolboxItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string TypeName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Glyph { get; init; } = string.Empty;
        public string ToolTip { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public ObservableCollection<ToolboxItemViewModel> Tools { get; } = new();

    public event Action<string?>? ToolSelected;
    public event Action<string>? ToolDoubleClicked;
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isCompactMode = false;
    public bool IsCompactMode
    {
        get => _isCompactMode;
        set
        {
            if (_isCompactMode != value)
            {
                _isCompactMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompactMode)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemWidth)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextVisibility)));
            }
        }
    }

    public double ItemWidth => IsCompactMode ? 28 : 124;
    public Visibility TextVisibility => IsCompactMode ? Visibility.Collapsed : Visibility.Visible;

    public string? ActiveToolTypeName => Tools.FirstOrDefault(t => t.IsSelected && t.TypeName != ControlToolboxRegistry.PointerTool)?.TypeName;

    public ToolboxPanel()
    {
        InitializeComponent();
        DataContext = this;
        LoadTools();
    }

    private void LoadTools()
    {
        Tools.Clear();
        foreach (var tool in ControlToolboxRegistry.GetAllTools())
        {
            Tools.Add(new ToolboxItemViewModel
            {
                TypeName = tool.TypeName,
                DisplayName = tool.DisplayName,
                Glyph = tool.Glyph,
                ToolTip = tool.ToolTip,
                IsSelected = tool.TypeName == ControlToolboxRegistry.PointerTool
            });
        }
        ToolsItemsControl.ItemsSource = Tools;
    }

    public void ResetToPointer()
    {
        foreach (var item in Tools)
        {
            item.IsSelected = (item.TypeName == ControlToolboxRegistry.PointerTool);
        }
        ToolSelected?.Invoke(null);
    }

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn && btn.DataContext is ToolboxItemViewModel clickedItem)
        {
            // Radio button behavior
            foreach (var item in Tools)
            {
                item.IsSelected = (item == clickedItem);
            }

            if (clickedItem.TypeName == ControlToolboxRegistry.PointerTool)
            {
                ToolSelected?.Invoke(null);
            }
            else
            {
                ToolSelected?.Invoke(clickedItem.TypeName);
            }
        }
    }

    private void ToolButton_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton btn && btn.DataContext is ToolboxItemViewModel clickedItem)
        {
            if (clickedItem.TypeName != ControlToolboxRegistry.PointerTool)
            {
                ToolDoubleClicked?.Invoke(clickedItem.TypeName);
                ResetToPointer();
            }
        }
    }

    private void BtnToggleViewMode_Click(object sender, RoutedEventArgs e)
    {
        IsCompactMode = !IsCompactMode;
    }
}
