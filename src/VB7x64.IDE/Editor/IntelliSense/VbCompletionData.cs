using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using VB7x64.ProjectModel.IntelliSense;

namespace VB7x64.IDE.Editor.IntelliSense;

/// <summary>
/// Elemento individual para la ventana de autocompletado (Auto List Members) de AvalonEdit.
/// </summary>
public class VbCompletionData : ICompletionData
{
    private readonly VbSymbolItem _symbol;
    private readonly IntelliSenseContext _context;

    public VbCompletionData(VbSymbolItem symbol, IntelliSenseContext context)
    {
        _symbol = symbol;
        _context = context;
    }

    public ImageSource? Image => null;

    public string Text => _symbol.Name;

    public VbSymbolItem Symbol => _symbol;

    public object Content
    {
        get
        {
            var panel = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(2, 1, 4, 1)
            };

            var iconBlock = new TextBlock
            {
                Text = _symbol.Glyph,
                Foreground = GetGlyphBrush(_symbol.Kind),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 20,
                TextAlignment = TextAlignment.Center
            };
            DockPanel.SetDock(iconBlock, Dock.Left);
            panel.Children.Add(iconBlock);

            if (!string.IsNullOrEmpty(_symbol.ReturnType))
            {
                var typeBlock = new TextBlock
                {
                    Text = _symbol.ReturnType,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 2, 0)
                };
                DockPanel.SetDock(typeBlock, Dock.Right);
                panel.Children.Add(typeBlock);
            }

            var textBlock = new TextBlock
            {
                Text = _symbol.Name,
                FontFamily = new FontFamily("Segoe UI, Consolas"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(textBlock);

            return panel;
        }
    }

    public object Description
    {
        get
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 235)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 140)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 6, 8, 6),
                MaxWidth = 420
            };

            var stack = new StackPanel();

            var sigBlock = new TextBlock
            {
                Text = _symbol.Signature,
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 128)),
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(sigBlock);

            var kindBlock = new TextBlock
            {
                Text = _symbol.KindDisplayName,
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(0, 2, 0, 4)
            };
            stack.Children.Add(kindBlock);

            if (!string.IsNullOrEmpty(_symbol.Description))
            {
                var descBlock = new TextBlock
                {
                    Text = _symbol.Description,
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                    TextWrapping = TextWrapping.Wrap
                };
                stack.Children.Add(descBlock);
            }

            border.Child = stack;
            return border;
        }
    }

    public double Priority => _symbol.Kind switch
    {
        VbSymbolKind.Control => 12.0,
        VbSymbolKind.Property => 11.0,
        VbSymbolKind.Method => 10.0,
        VbSymbolKind.IntrinsicFunction => 9.0,
        VbSymbolKind.Variable => 8.0,
        VbSymbolKind.Constant => 7.0,
        VbSymbolKind.DataType => 6.0,
        VbSymbolKind.Keyword => 5.0,
        _ => 1.0
    };

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        if (completionSegment.Length > 0)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
        else if (_context.ReplaceLength > 0 && _context.ReplaceStartPosition >= 0 &&
                 _context.ReplaceStartPosition + _context.ReplaceLength <= textArea.Document.TextLength)
        {
            textArea.Document.Replace(_context.ReplaceStartPosition, _context.ReplaceLength, Text);
        }
        else
        {
            textArea.Document.Insert(textArea.Caret.Offset, Text);
        }
    }

    private static Brush GetGlyphBrush(VbSymbolKind kind) => kind switch
    {
        VbSymbolKind.Method => new SolidColorBrush(Color.FromRgb(220, 120, 0)),
        VbSymbolKind.IntrinsicFunction => new SolidColorBrush(Color.FromRgb(0, 102, 204)),
        VbSymbolKind.Property => new SolidColorBrush(Color.FromRgb(46, 125, 50)),
        VbSymbolKind.Control => new SolidColorBrush(Color.FromRgb(142, 36, 170)),
        VbSymbolKind.Variable => new SolidColorBrush(Color.FromRgb(69, 90, 100)),
        VbSymbolKind.Constant => new SolidColorBrush(Color.FromRgb(194, 24, 91)),
        VbSymbolKind.DataType => new SolidColorBrush(Color.FromRgb(0, 137, 123)),
        VbSymbolKind.Keyword => new SolidColorBrush(Color.FromRgb(48, 63, 159)),
        VbSymbolKind.Enum => new SolidColorBrush(Color.FromRgb(109, 76, 65)),
        VbSymbolKind.Event => new SolidColorBrush(Color.FromRgb(211, 47, 47)),
        _ => Brushes.DarkSlateGray
    };
}
