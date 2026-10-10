using System;

namespace VB7x64.ProjectModel.Designers;

/// <summary>
/// Utilidad matemática para ajuste a cuadrícula (Snap to Grid).
/// </summary>
public static class GridSnapHelper
{
    public const double DefaultGridStep = 8.0;

    /// <summary>
    /// Ajusta un valor escalar al múltiplo más cercano del paso de la cuadrícula.
    /// </summary>
    public static double Snap(double value, double step = DefaultGridStep)
    {
        if (step <= 0) return value;
        return Math.Round(value / step) * step;
    }

    /// <summary>
    /// Ajusta un punto (X, Y) a la cuadrícula.
    /// </summary>
    public static (double X, double Y) SnapPoint(double x, double y, double step = DefaultGridStep)
    {
        return (Snap(x, step), Snap(y, step));
    }
}
