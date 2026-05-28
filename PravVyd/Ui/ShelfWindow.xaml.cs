using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PravVyd.Ui;

/// <summary>«Полка» у края экрана. Дроп файла кладёт чип; перетаскивание чипа наружу отдаёт файл (DoDragDrop).</summary>
public partial class ShelfWindow : Window
{
    private Point _dragStart;
    private Border? _pressed;

    public ShelfWindow()
    {
        InitializeComponent();

        var area = SystemParameters.WorkArea;
        Height = area.Height * 0.55;
        Left = area.Right - Width;
        Top = area.Top + (area.Height - Height) / 2;

        AllowDrop = true;
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        Drop += OnDrop;
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        foreach (var path in (string[])e.Data.GetData(DataFormats.FileDrop))
            AddChip(path);

        UpdateHint();
    }

    private void AddChip(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
            name = path;

        var label = new TextBlock
        {
            Text = name,
            Foreground = Brushes.White,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var close = new TextBlock
        {
            Text = "✕",
            Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0),
            Cursor = Cursors.Hand,
        };

        var row = new DockPanel();
        DockPanel.SetDock(close, Dock.Right);
        row.Children.Add(close);
        row.Children.Add(label);

        var chip = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x52)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 6),
            Cursor = Cursors.Hand,
            ToolTip = path,
            Tag = path,
            Child = row,
        };

        chip.PreviewMouseLeftButtonDown += Chip_MouseDown;
        chip.PreviewMouseMove += Chip_MouseMove;
        close.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Items.Children.Remove(chip);
            UpdateHint();
        };

        Items.Children.Add(chip);
    }

    private void Chip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = sender as Border;
        _dragStart = e.GetPosition(this);
    }

    private void Chip_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var now = e.GetPosition(this);
        if (Math.Abs(now.X - _dragStart.X) < 6 && Math.Abs(now.Y - _dragStart.Y) < 6)
            return;

        var path = (string)_pressed.Tag;
        var chip = _pressed;
        _pressed = null;

        var data = new DataObject(DataFormats.FileDrop, new[] { path });
        DragDrop.DoDragDrop(chip, data, DragDropEffects.Copy);
    }

    private void UpdateHint()
    {
        Hint.Visibility = Items.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
