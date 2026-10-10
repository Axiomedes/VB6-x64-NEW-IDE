using System;
using VB7x64.ProjectModel.Designers;
using Xunit;

namespace VB7x64.Tests;

public class FormDesignerVisualTests
{
    [Fact]
    public void VbColorModel_ParseSystemColors_RecognizesKnownIndices()
    {
        // &H8000000F& = ButtonFace
        var btnFace = VbColorModel.ParseColor("&H8000000F&", VbColorModel.DefaultControlColor);
        Assert.True(btnFace.IsSystemColor);
        Assert.Equal(0x0F, btnFace.SystemColorIndex);
        Assert.Equal(240, btnFace.R);
        Assert.Equal(240, btnFace.G);
        Assert.Equal(240, btnFace.B);

        // &H80000005& = Window (Blanco)
        var windowCol = VbColorModel.ParseColor("&H80000005&", VbColorModel.DefaultWindowColor);
        Assert.True(windowCol.IsSystemColor);
        Assert.Equal(0x05, windowCol.SystemColorIndex);
        Assert.Equal(255, windowCol.R);
        Assert.Equal(255, windowCol.G);
        Assert.Equal(255, windowCol.B);

        // &H80000008& = WindowText (Negro)
        var winText = VbColorModel.ParseColor("&H80000008&", VbColorModel.DefaultTextColor);
        Assert.True(winText.IsSystemColor);
        Assert.Equal(0, winText.R);
        Assert.Equal(0, winText.G);
        Assert.Equal(0, winText.B);
    }

    [Fact]
    public void VbColorModel_ParseRgbColors_HandlesBgrByteOrder()
    {
        // En VB6 &H00BBGGRR&: Rojo = &H000000FF&
        var red = VbColorModel.ParseColor("&H000000FF&", VbColorModel.DefaultControlColor);
        Assert.False(red.IsSystemColor);
        Assert.Equal(255, red.R);
        Assert.Equal(0, red.G);
        Assert.Equal(0, red.B);

        // Verde = &H0000FF00&
        var green = VbColorModel.ParseColor("&H0000FF00&", VbColorModel.DefaultControlColor);
        Assert.Equal(0, green.R);
        Assert.Equal(255, green.G);
        Assert.Equal(0, green.B);

        // Azul = &H00FF0000&
        var blue = VbColorModel.ParseColor("&H00FF0000&", VbColorModel.DefaultControlColor);
        Assert.Equal(0, blue.R);
        Assert.Equal(0, blue.G);
        Assert.Equal(255, blue.B);

        // Formato Hex CSS #FF00AA
        var cssHex = VbColorModel.ParseColor("#FF00AA", VbColorModel.DefaultControlColor);
        Assert.Equal(255, cssHex.R);
        Assert.Equal(0, cssHex.G);
        Assert.Equal(170, cssHex.B);
    }

    [Fact]
    public void VbColorModel_ToVbColorHex_ConvertsCorrectly()
    {
        string hexRed = VbColorModel.ToVbColorHex(255, 0, 0);
        Assert.Equal("&H000000FF&", hexRed);

        string hexBlue = VbColorModel.ToVbColorHex(0, 0, 255);
        Assert.Equal("&H00FF0000&", hexBlue);

        string hexGreen = VbColorModel.ToVbColorHex(0, 255, 0);
        Assert.Equal("&H0000FF00&", hexGreen);
    }

    [Fact]
    public void GridSnapHelper_Snap_RoundsToNearestStep()
    {
        double step = 8.0;

        Assert.Equal(0.0, GridSnapHelper.Snap(0.0, step));
        Assert.Equal(8.0, GridSnapHelper.Snap(5.0, step));
        Assert.Equal(8.0, GridSnapHelper.Snap(7.9, step));
        Assert.Equal(8.0, GridSnapHelper.Snap(11.9, step));
        Assert.Equal(16.0, GridSnapHelper.Snap(12.1, step));
        Assert.Equal(16.0, GridSnapHelper.Snap(16.0, step));
    }

    [Fact]
    public void GridSnapHelper_SnapPoint_SnapsBothCoordinates()
    {
        var (snapX, snapY) = GridSnapHelper.SnapPoint(13.4, 25.1, step: 8.0);

        Assert.Equal(16.0, snapX);
        Assert.Equal(24.0, snapY);
    }

    [Fact]
    public void GridSnapHelper_CustomSteps_CalculatesProperly()
    {
        // Paso de 4 px (60 twips)
        Assert.Equal(4.0, GridSnapHelper.Snap(3.0, step: 4.0));
        Assert.Equal(8.0, GridSnapHelper.Snap(6.5, step: 4.0));

        // Paso de 16 px (240 twips)
        Assert.Equal(16.0, GridSnapHelper.Snap(10.0, step: 16.0));
        Assert.Equal(32.0, GridSnapHelper.Snap(25.0, step: 16.0));
    }

    [Fact]
    public void FormDesignerControl_SetBoundsPixels_ConvertsTwipsAccurately()
    {
        var control = new FormDesignerControl();
        control.SetBoundsPixels(100.0, 50.0, 200.0, 30.0);

        Assert.Equal(1500, control.Left);
        Assert.Equal(750, control.Top);
        Assert.Equal(3000, control.Width);
        Assert.Equal(450, control.Height);

        Assert.Equal(100.0, control.LeftPixels);
        Assert.Equal(50.0, control.TopPixels);
        Assert.Equal(200.0, control.WidthPixels);
        Assert.Equal(30.0, control.HeightPixels);
    }

    [Fact]
    public void FormDesignerControl_Clone_DuplicatesPropertiesAndChildren()
    {
        var parent = new FormDesignerControl
        {
            TypeName = "VB.Frame",
            Name = "Frame1",
            Left = 1000,
            Top = 500,
            Width = 3000,
            Height = 2000,
            Caption = "My Frame"
        };

        var child = new FormDesignerControl
        {
            TypeName = "VB.CommandButton",
            Name = "Command1",
            Left = 200,
            Top = 300,
            Caption = "Click Me",
            ParentControl = parent
        };
        parent.Children.Add(child);

        var clonedParent = parent.Clone();

        Assert.NotSame(parent, clonedParent);
        Assert.Equal("Frame1", clonedParent.Name);
        Assert.Equal("My Frame", clonedParent.Caption);
        Assert.Single(clonedParent.Children);

        var clonedChild = clonedParent.Children[0];
        Assert.NotSame(child, clonedChild);
        Assert.Same(clonedParent, clonedChild.ParentControl);
        Assert.Equal("Command1", clonedChild.Name);
        Assert.Equal("Click Me", clonedChild.Caption);
    }
}
