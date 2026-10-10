using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using VB7x64.ProjectModel.IntelliSense;

namespace VB7x64.IDE.Editor.IntelliSense;

/// <summary>
/// Control visual enriquecido para Parameter Info (Información Rápida de Parámetros)
/// que resalta en negrita y color el parámetro actualmente activo.
/// </summary>
public class VbParameterInsightControl : Border
{
    private readonly StackPanel _mainStack;

    public VbParameterInsightControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(255, 255, 238));
        BorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 120));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(3);
        Padding = new Thickness(10, 7, 10, 7);
        MaxWidth = 600;

        _mainStack = new StackPanel();
        Child = _mainStack;
    }

    public void Update(IntelliSenseContext context)
    {
        _mainStack.Children.Clear();

        var symbol = context.InvokedSymbol;
        if (symbol == null) return;

        // 1. Cabecera con firma y resaltado del parámetro activo
        var sigBlock = new TextBlock
        {
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap
        };

        if (symbol.Parameters.Count > 0)
        {
            string prefix = symbol.Kind == VbSymbolKind.IntrinsicFunction || !string.IsNullOrEmpty(symbol.ReturnType)
                ? $"Function {symbol.Name}("
                : $"Sub {symbol.Name}(";

            sigBlock.Inlines.Add(new Run(prefix) { Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 136)), FontWeight = FontWeights.Normal });

            for (int i = 0; i < symbol.Parameters.Count; i++)
            {
                var p = symbol.Parameters[i];
                bool isActive = i == context.ParameterIndex;

                if (isActive)
                {
                    var activeRun = new Run(p.DisplayText)
                    {
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(160, 0, 0)),
                        Background = new SolidColorBrush(Color.FromRgb(255, 240, 200))
                    };
                    sigBlock.Inlines.Add(activeRun);
                }
                else
                {
                    var normalRun = new Run(p.DisplayText)
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30))
                    };
                    sigBlock.Inlines.Add(normalRun);
                }

                if (i < symbol.Parameters.Count - 1)
                {
                    sigBlock.Inlines.Add(new Run(", ") { Foreground = Brushes.Gray });
                }
            }

            string suffix = ")";
            if (!string.IsNullOrEmpty(symbol.ReturnType))
            {
                suffix += $" As {symbol.ReturnType}";
            }
            sigBlock.Inlines.Add(new Run(suffix) { Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 136)) });
        }
        else
        {
            sigBlock.Inlines.Add(new Run(symbol.Signature) { Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 136)), FontWeight = FontWeights.Bold });
        }

        _mainStack.Children.Add(sigBlock);

        // 2. Descripción del parámetro actualmente activo
        if (context.ParameterIndex >= 0 && context.ParameterIndex < symbol.Parameters.Count)
        {
            var activeParam = symbol.Parameters[context.ParameterIndex];
            if (!string.IsNullOrEmpty(activeParam.Description))
            {
                var paramDescBlock = new TextBlock
                {
                    Margin = new Thickness(0, 5, 0, 0),
                    FontSize = 11.5,
                    TextWrapping = TextWrapping.Wrap
                };

                paramDescBlock.Inlines.Add(new Run($"{activeParam.Name}: ") { FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)) });
                paramDescBlock.Inlines.Add(new Run(activeParam.Description) { Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40)) });

                _mainStack.Children.Add(paramDescBlock);
            }
        }

        // 3. Descripción general de la función
        if (!string.IsNullOrEmpty(symbol.Description))
        {
            var funcDescBlock = new TextBlock
            {
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic
            };
            funcDescBlock.Text = symbol.Description;
            _mainStack.Children.Add(funcDescBlock);
        }
    }
}
