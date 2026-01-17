using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Path = Avalonia.Controls.Shapes.Path;

namespace MFAToolsPlus.Extensions;

public static class BubbleExtensions
{
    private const string BubbleLayerName = "BubbleLayer";
    private static readonly Regex GeometryHintRegex = new(@"[MmLlHhVvCcSsQqTtAaZz]", RegexOptions.Compiled);

    public static readonly AttachedProperty<bool> EnableBubbleProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "EnableBubble",
            typeof(BubbleExtensions),
            defaultValue: false);

    public static readonly AttachedProperty<object?> BubbleContentProperty =
        AvaloniaProperty.RegisterAttached<Control, object?>(
            "BubbleContent",
            typeof(BubbleExtensions),
            defaultValue: null);

    public static readonly AttachedProperty<double> BubbleDurationProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "BubbleDuration",
            typeof(BubbleExtensions),
            defaultValue: 1.2);

    public static readonly AttachedProperty<double> BubbleRiseProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "BubbleRise",
            typeof(BubbleExtensions),
            defaultValue: 30);

    public static readonly AttachedProperty<double> BubbleOffsetXProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "BubbleOffsetX",
            typeof(BubbleExtensions),
            defaultValue: 0);

    public static readonly AttachedProperty<double> BubbleOffsetYProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "BubbleOffsetY",
            typeof(BubbleExtensions),
            defaultValue: -8);

    public static bool GetEnableBubble(Control control) =>
        control.GetValue(EnableBubbleProperty);

    public static void SetEnableBubble(Control control, bool value) =>
        control.SetValue(EnableBubbleProperty, value);

    public static object? GetBubbleContent(Control control) =>
        control.GetValue(BubbleContentProperty);

    public static void SetBubbleContent(Control control, object? value) =>
        control.SetValue(BubbleContentProperty, value);

    public static double GetBubbleDuration(Control control) =>
        control.GetValue(BubbleDurationProperty);

    public static void SetBubbleDuration(Control control, double value) =>
        control.SetValue(BubbleDurationProperty, value);

    public static double GetBubbleRise(Control control) =>
        control.GetValue(BubbleRiseProperty);

    public static void SetBubbleRise(Control control, double value) =>
        control.SetValue(BubbleRiseProperty, value);

    public static double GetBubbleOffsetX(Control control) =>
        control.GetValue(BubbleOffsetXProperty);

    public static void SetBubbleOffsetX(Control control, double value) =>
        control.SetValue(BubbleOffsetXProperty, value);

    public static double GetBubbleOffsetY(Control control) =>
        control.GetValue(BubbleOffsetYProperty);

    public static void SetBubbleOffsetY(Control control, double value) =>
        control.SetValue(BubbleOffsetYProperty, value);

    static BubbleExtensions()
    {
        EnableBubbleProperty.Changed.Subscribe(OnEnableBubbleChanged);
    }

    private static void OnEnableBubbleChanged(AvaloniaPropertyChangedEventArgs<bool> args)
    {
        if (args.Sender is not Control control)
            return;

        if (args.NewValue.Value)
        {
            control.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble, true);
        }
        else
        {
            control.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
            return;

        if (!GetEnableBubble(control))
            return;

        var point = e.GetCurrentPoint(control);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        ShowBubble(control);
    }

    private static void ShowBubble(Control target)
    {
        var layer = FindBubbleLayer(target);
        if (layer is null)
            return;

        var contentValue = GetBubbleContent(target);
        if (contentValue is null)
            return;

        var content = BuildContent(contentValue);

        var bubble = new Border
        {
            Classes = { "BubbleFlyout" },
            Child = content,
            IsHitTestVisible = false
        };

        bubble.Measure(Size.Infinity);
        var size = bubble.DesiredSize;

        var anchor = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), layer);
        if (anchor is null)
            return;

        var left = anchor.Value.X - size.Width / 2 + GetBubbleOffsetX(target);
        var top = anchor.Value.Y - size.Height / 2 + GetBubbleOffsetY(target);

        Canvas.SetLeft(bubble, left);
        Canvas.SetTop(bubble, top);

        layer.Children.Add(bubble);

        var durationSeconds = Math.Max(0, GetBubbleDuration(target));
        var rise = GetBubbleRise(target);
        _ = PlayAnimationAndRemoveAsync(layer, bubble, rise, TimeSpan.FromSeconds(durationSeconds));
    }

    private static Canvas? FindBubbleLayer(Control target)
    {
        var topLevel = TopLevel.GetTopLevel(target);
        if (topLevel is null)
            return null;

        return topLevel.FindControl<Canvas>(BubbleLayerName);
    }

    private static Control BuildContent(object content)
    {
        if (content is Control control)
            return control;

        if (content is IImage image)
            return new Image { Source = image };

        if (content is Bitmap bitmap)
            return new Image { Source = bitmap };

        if (content is Geometry geometry)
            return new Path { Data = geometry };

        if (content is string text)
        {
            if (TryCreateImageFromString(text, out var imageControl))
                return imageControl;

            if (TryCreatePathFromString(text, out var pathControl))
                return pathControl;

            return new TextBlock { Text = text };
        }

        return new TextBlock { Text = content.ToString() ?? string.Empty };
    }

    private static bool TryCreateImageFromString(string value, out Control control)
    {
        control = null!;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && AssetLoader.Exists(uri))
        {
            using var stream = AssetLoader.Open(uri);
            control = new Image { Source = new Bitmap(stream) };
            return true;
        }

        if (File.Exists(value))
        {
            using var stream = File.OpenRead(value);
            control = new Image { Source = new Bitmap(stream) };
            return true;
        }

        return false;
    }

    private static bool TryCreatePathFromString(string value, out Control control)
    {
        control = null!;

        if (!GeometryHintRegex.IsMatch(value))
            return false;

        try
        {
            var geometry = Geometry.Parse(value);
            control = new Path { Data = geometry };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task PlayAnimationAndRemoveAsync(Canvas layer, Control bubble, double rise, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            layer.Children.Remove(bubble);
            return;
        }

        var startTop = Canvas.GetTop(bubble);

        var animation = new Animation
        {
            Duration = duration,
            FillMode = FillMode.Forward,
            Easing = new CubicEaseOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, 1d),
                        new Setter(Canvas.TopProperty, startTop)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, 0d),
                        new Setter(Canvas.TopProperty, startTop - rise)
                    }
                }
            }
        };

        try
        {
            await animation.RunAsync(bubble, CancellationToken.None);
        }
        catch
        {
        }

        layer.Children.Remove(bubble);
    }
}