using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using VB7x64.ProjectModel.Designers;

namespace VB7x64.IDE.Designer;

/// <summary>
/// Fábrica y renderizador visual de controles de VB6 en WPF con alta fidelidad gráfica histórica.
/// </summary>
public static class ControlVisualRenderer
{
    private static readonly FontFamily DefaultFontFamily = new("Tahoma, MS Sans Serif, Segoe UI, Arial");

    /// <summary>
    /// Crea el elemento visual de WPF para un control del diseñador.
    /// </summary>
    public static FrameworkElement CreateVisual(FormDesignerControl control, Action<FormDesignerControl, FrameworkElement>? onChildCreated = null)
    {
        FrameworkElement visual = control.TypeName.ToUpperInvariant() switch
        {
            "VB.COMMANDBUTTON" => CreateCommandButton(control),
            "VB.TEXTBOX" => CreateTextBox(control),
            "VB.LABEL" => CreateLabel(control),
            "VB.CHECKBOX" => CreateCheckBox(control),
            "VB.OPTIONBUTTON" => CreateOptionButton(control),
            "VB.FRAME" => CreateFrame(control, onChildCreated),
            "VB.LISTBOX" => CreateListBox(control),
            "VB.COMBOBOX" => CreateComboBox(control),
            "VB.PICTUREBOX" => CreatePictureBox(control, onChildCreated),
            "VB.IMAGE" => CreateImage(control),
            "VB.TIMER" => CreateTimer(control),
            "VB.HSCROLLBAR" => CreateHScrollBar(control),
            "VB.VSCROLLBAR" => CreateVScrollBar(control),
            "VB.SHAPE" => CreateShape(control),
            "VB.LINE" => CreateLine(control),
            _ => CreateGenericControl(control)
        };

        if (control.IsNonResizable)
        {
            visual.Width = 32;
            visual.Height = 32;
            control.Width = 480;
            control.Height = 480;
        }
        else
        {
            visual.Width = control.WidthPixels;
            visual.Height = control.HeightPixels;
        }
        visual.Tag = control;

        Canvas.SetLeft(visual, control.LeftPixels);
        Canvas.SetTop(visual, control.TopPixels);

        return visual;
    }

    private static FrameworkElement CreateCommandButton(FormDesignerControl ctrl)
    {
        var border = new Border
        {
            Background = VbColorHelper.ToBrush(ctrl.BackColorHex, new SolidColorBrush(Color.FromRgb(236, 233, 216))),
            BorderBrush = new SolidColorBrush(Color.FromRgb(115, 115, 115)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(1)
        };

        // Bisel interior 3D clásico
        var innerBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            Padding = new Thickness(4, 2, 4, 2)
        };

        var textBlock = BuildTextBlockWithHotkey(
            string.IsNullOrEmpty(ctrl.Caption) ? ctrl.Name : ctrl.Caption,
            ctrl.Font,
            ctrl.ForeColorHex,
            TextAlignment.Center
        );
        textBlock.VerticalAlignment = VerticalAlignment.Center;
        textBlock.HorizontalAlignment = HorizontalAlignment.Center;

        innerBorder.Child = textBlock;
        border.Child = innerBorder;

        return border;
    }

    private static FrameworkElement CreateTextBox(FormDesignerControl ctrl)
    {
        var border = new Border
        {
            Background = VbColorHelper.ToBrush(ctrl.BackColorHex, Brushes.White),
            BorderBrush = new SolidColorBrush(Color.FromRgb(127, 157, 185)), // Bisel Windows clásico
            BorderThickness = new Thickness(1),
            Padding = new Thickness(3, 1, 3, 1)
        };

        var textBlock = new TextBlock
        {
            Text = ctrl.Text,
            FontFamily = GetFontFamily(ctrl.Font),
            FontSize = GetFontSize(ctrl.Font),
            FontWeight = GetFontWeight(ctrl.Font),
            FontStyle = GetFontStyle(ctrl.Font),
            Foreground = VbColorHelper.ToBrush(ctrl.ForeColorHex, Brushes.Black),
            TextWrapping = (ctrl.MultiLine == true) ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Top
        };

        if (ctrl.Alignment.HasValue)
        {
            textBlock.TextAlignment = ctrl.Alignment.Value switch
            {
                1 => TextAlignment.Right,
                2 => TextAlignment.Center,
                _ => TextAlignment.Left
            };
        }

        border.Child = textBlock;
        return border;
    }

    private static FrameworkElement CreateLabel(FormDesignerControl ctrl)
    {
        var border = new Border
        {
            Background = VbColorHelper.ToBrush(ctrl.BackColorHex, Brushes.Transparent),
            Padding = new Thickness(1)
        };

        if (ctrl.BorderStyle == 1)
        {
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));
            border.BorderThickness = new Thickness(1);
        }

        var align = ctrl.Alignment.HasValue ? (ctrl.Alignment.Value switch
        {
            1 => TextAlignment.Right,
            2 => TextAlignment.Center,
            _ => TextAlignment.Left
        }) : TextAlignment.Left;

        var textBlock = BuildTextBlockWithHotkey(
            string.IsNullOrEmpty(ctrl.Caption) ? ctrl.Name : ctrl.Caption,
            ctrl.Font,
            ctrl.ForeColorHex,
            align
        );
        textBlock.TextWrapping = TextWrapping.Wrap;
        textBlock.VerticalAlignment = VerticalAlignment.Top;

        border.Child = textBlock;
        return border;
    }

    private static FrameworkElement CreateCheckBox(FormDesignerControl ctrl)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Cajita de verificación 3D hundida (13x13)
        var box = new Border
        {
            Width = 13,
            Height = 13,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(115, 115, 115)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var textBlock = BuildTextBlockWithHotkey(
            ctrl.Caption,
            ctrl.Font,
            ctrl.ForeColorHex,
            TextAlignment.Left
        );
        textBlock.VerticalAlignment = VerticalAlignment.Center;
        textBlock.Margin = new Thickness(4, 0, 0, 0);

        Grid.SetColumn(box, 0);
        Grid.SetColumn(textBlock, 1);

        grid.Children.Add(box);
        grid.Children.Add(textBlock);

        return grid;
    }

    private static FrameworkElement CreateOptionButton(FormDesignerControl ctrl)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Círculo de opción (13x13)
        var circle = new Ellipse
        {
            Width = 13,
            Height = 13,
            Fill = Brushes.White,
            Stroke = new SolidColorBrush(Color.FromRgb(115, 115, 115)),
            StrokeThickness = 1,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var textBlock = BuildTextBlockWithHotkey(
            ctrl.Caption,
            ctrl.Font,
            ctrl.ForeColorHex,
            TextAlignment.Left
        );
        textBlock.VerticalAlignment = VerticalAlignment.Center;
        textBlock.Margin = new Thickness(4, 0, 0, 0);

        Grid.SetColumn(circle, 0);
        Grid.SetColumn(textBlock, 1);

        grid.Children.Add(circle);
        grid.Children.Add(textBlock);

        return grid;
    }

    private static FrameworkElement CreateFrame(FormDesignerControl ctrl, Action<FormDesignerControl, FrameworkElement>? onChildCreated)
    {
        var groupBox = new GroupBox
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(208, 206, 191)),
            BorderThickness = new Thickness(1),
            Background = VbColorHelper.ToBrush(ctrl.BackColorHex, Brushes.Transparent),
            Padding = new Thickness(2)
        };

        var headerText = BuildTextBlockWithHotkey(
            string.IsNullOrEmpty(ctrl.Caption) ? ctrl.Name : ctrl.Caption,
            ctrl.Font,
            ctrl.ForeColorHex,
            TextAlignment.Left
        );
        groupBox.Header = headerText;

        // Lienzo interno para alojar los controles anidados del Frame
        var innerCanvas = new Canvas
        {
            ClipToBounds = true,
            Background = Brushes.Transparent
        };

        foreach (var child in ctrl.Children)
        {
            var childVisual = CreateVisual(child, onChildCreated);
            innerCanvas.Children.Add(childVisual);
            onChildCreated?.Invoke(child, childVisual);
        }

        groupBox.Content = innerCanvas;
        return groupBox;
    }

    private static FrameworkElement CreateListBox(FormDesignerControl ctrl)
    {
        var border = new Border
        {
            Background = VbColorHelper.ToBrush(ctrl.BackColorHex, Brushes.White),
            BorderBrush = new SolidColorBrush(Color.FromRgb(127, 157, 185)),
            BorderThickness = new Thickness(1)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });

        var itemsPanel = new StackPanel { Margin = new Thickness(3) };
        itemsPanel.Children.Add(new TextBlock { Text = $"{ctrl.Name} Item 1", FontSize = 11, Foreground = Brushes.Gray });
        itemsPanel.Children.Add(new TextBlock { Text = $"{ctrl.Name} Item 2", FontSize = 11, Foreground = Brushes.Gray });

        // Scrollbar falso visual a la derecha
        var scrollbar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(215, 215, 215)),
            BorderThickness = new Thickness(1, 0, 0, 0)
        };

        Grid.SetColumn(itemsPanel, 0);
        Grid.SetColumn(scrollbar, 1);

        grid.Children.Add(itemsPanel);
        grid.Children.Add(scrollbar);

        border.Child = grid;
        return border;
    }

    private static FrameworkElement CreateComboBox(FormDesignerControl ctrl)
    {
        var border = new Border
        {
            Background = VbColorHelper.ToBrush(ctrl.BackColorHex, Brushes.White),
            BorderBrush = new SolidColorBrush(Color.FromRgb(127, 157, 185)),
            BorderThickness = new Thickness(1),
            Height = Math.Max(21, ctrl.HeightPixels)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });

        var text = new TextBlock
        {
            Text = string.IsNullOrEmpty(ctrl.Text) ? ctrl.Name : ctrl.Text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 2, 0),
            FontSize = 11.5
        };

        var arrowBtn = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(236, 233, 216)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = new TextBlock
            {
                Text = "▼",
                FontSize = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Black
            }
        };

        Grid.SetColumn(text, 0);
        Grid.SetColumn(arrowBtn, 1);

        grid.Children.Add(text);
        grid.Children.Add(arrowBtn);

        border.Child = grid;
        return border;
    }

    private static FrameworkElement CreatePictureBox(FormDesignerControl ctrl, Action<FormDesignerControl, FrameworkElement>? onChildCreated)
    {
        var border = new Border
        {
            Background = VbColorHelper.ToBrush(ctrl.BackColorHex, Brushes.White),
            BorderBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            BorderThickness = new Thickness(1)
        };

        var canvas = new Canvas { ClipToBounds = true };
        foreach (var child in ctrl.Children)
        {
            var childVisual = CreateVisual(child, onChildCreated);
            canvas.Children.Add(childVisual);
            onChildCreated?.Invoke(child, childVisual);
        }

        border.Child = canvas;
        return border;
    }

    private static FrameworkElement CreateImage(FormDesignerControl ctrl)
    {
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
            BorderThickness = new Thickness(1),
            Background = Brushes.Transparent
        };

        var text = new TextBlock
        {
            Text = $"🖼 {ctrl.Name}",
            FontSize = 10,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        border.Child = text;
        return border;
    }

    private static FrameworkElement CreateTimer(FormDesignerControl ctrl)
    {
        var border = new Border
        {
            Width = 32,
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(255, 255, 230)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            ToolTip = $"{ctrl.Name} (Timer)"
        };

        var text = new TextBlock
        {
            Text = "⏱",
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        border.Child = text;
        return border;
    }

    private static FrameworkElement CreateHScrollBar(FormDesignerControl ctrl)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            BorderThickness = new Thickness(1)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });

        var leftArrow = new TextBlock { Text = "◄", FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var rightArrow = new TextBlock { Text = "►", FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var thumb = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(210, 210, 210)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(4, 1, 4, 1)
        };

        Grid.SetColumn(leftArrow, 0);
        Grid.SetColumn(thumb, 1);
        Grid.SetColumn(rightArrow, 2);

        grid.Children.Add(leftArrow);
        grid.Children.Add(thumb);
        grid.Children.Add(rightArrow);

        border.Child = grid;
        return border;
    }

    private static FrameworkElement CreateVScrollBar(FormDesignerControl ctrl)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            BorderThickness = new Thickness(1)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });

        var upArrow = new TextBlock { Text = "▲", FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var downArrow = new TextBlock { Text = "▼", FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var thumb = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(210, 210, 210)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(1, 4, 1, 4)
        };

        Grid.SetRow(upArrow, 0);
        Grid.SetRow(thumb, 1);
        Grid.SetRow(downArrow, 2);

        grid.Children.Add(upArrow);
        grid.Children.Add(thumb);
        grid.Children.Add(downArrow);

        border.Child = grid;
        return border;
    }

    private static FrameworkElement CreateShape(FormDesignerControl ctrl)
    {
        var rect = new Rectangle
        {
            Stroke = VbColorHelper.ToBrush(ctrl.ForeColorHex, Brushes.Black),
            StrokeThickness = 1,
            Fill = VbColorHelper.ToBrush(ctrl.BackColorHex, Brushes.Transparent)
        };
        return rect;
    }

    private static FrameworkElement CreateLine(FormDesignerControl ctrl)
    {
        var line = new Line
        {
            X1 = 0,
            Y1 = 0,
            X2 = Math.Max(10, ctrl.WidthPixels),
            Y2 = Math.Max(10, ctrl.HeightPixels),
            Stroke = VbColorHelper.ToBrush(ctrl.ForeColorHex, Brushes.Black),
            StrokeThickness = 1
        };
        return line;
    }

    private static FrameworkElement CreateGenericControl(FormDesignerControl ctrl)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };

        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new TextBlock
        {
            Text = $"⚙ {ctrl.DisplayType}",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.DarkSlateGray,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        sp.Children.Add(new TextBlock
        {
            Text = ctrl.Name,
            FontSize = 9.5,
            Foreground = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        border.Child = sp;
        return border;
    }

    /// <summary>
    /// Construye un TextBlock interpretando aceleradores '&' de VB6 con subrayado.
    /// </summary>
    public static TextBlock BuildTextBlockWithHotkey(
        string rawText,
        FormFontDescription? font,
        string? foreColorHex,
        TextAlignment alignment)
    {
        var textBlock = new TextBlock
        {
            FontFamily = GetFontFamily(font),
            FontSize = GetFontSize(font),
            FontWeight = GetFontWeight(font),
            FontStyle = GetFontStyle(font),
            Foreground = VbColorHelper.ToBrush(foreColorHex, Brushes.Black),
            TextAlignment = alignment
        };

        if (string.IsNullOrEmpty(rawText))
        {
            return textBlock;
        }

        bool nextIsHotkey = false;

        for (int i = 0; i < rawText.Length; i++)
        {
            char c = rawText[i];

            if (c == '&' && !nextIsHotkey)
            {
                if (i + 1 < rawText.Length && rawText[i + 1] == '&')
                {
                    // Ampersand doble '&&' representa '&' literal
                    textBlock.Inlines.Add(new Run("&"));
                    i++;
                }
                else
                {
                    nextIsHotkey = true;
                }
                continue;
            }

            if (nextIsHotkey)
            {
                var run = new Run(c.ToString());
                run.TextDecorations.Add(TextDecorations.Underline);
                textBlock.Inlines.Add(run);
                nextIsHotkey = false;
            }
            else
            {
                textBlock.Inlines.Add(new Run(c.ToString()));
            }
        }

        return textBlock;
    }

    private static FontFamily GetFontFamily(FormFontDescription? font)
    {
        if (font != null && !string.IsNullOrWhiteSpace(font.Name))
        {
            return new FontFamily($"{font.Name}, Tahoma, Arial");
        }
        return DefaultFontFamily;
    }

    private static double GetFontSize(FormFontDescription? font)
    {
        if (font != null && font.Size > 0)
        {
            // Tamaño en puntos a tamaño en píxeles aproximado (points * 96 / 72)
            return Math.Max(9.0, Math.Min(48.0, font.Size * (96.0 / 72.0)));
        }
        return 11.0;
    }

    private static FontWeight GetFontWeight(FormFontDescription? font)
    {
        return font != null && font.IsBold ? FontWeights.Bold : FontWeights.Normal;
    }

    private static FontStyle GetFontStyle(FormFontDescription? font)
    {
        return font != null && font.Italic ? FontStyles.Italic : FontStyles.Normal;
    }
}
