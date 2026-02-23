using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OfficeTaskTracker;

public partial class ScreenshotOverlay : Window
{
    private System.Windows.Point _startPoint;
    private bool _isSelecting;
    public System.Drawing.Rectangle SelectedRegion { get; private set; }
    public bool RegionSelected { get; private set; }

    public ScreenshotOverlay()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            // Center instruction
            var screenWidth = ActualWidth;
            Canvas.SetLeft(InstructionBorder, (screenWidth - InstructionBorder.ActualWidth) / 2);
            Canvas.SetTop(InstructionBorder, 20);
            OverlayCanvas.Focus();
            Keyboard.Focus(OverlayCanvas);
        };

        OverlayCanvas.Focusable = true;
        PreviewKeyDown += Window_KeyDown;
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(OverlayCanvas);
        _isSelecting = true;
        SelectionRectangle.Visibility = Visibility.Visible;
        SizeIndicator.Visibility = Visibility.Visible;

        Canvas.SetLeft(SelectionRectangle, _startPoint.X);
        Canvas.SetTop(SelectionRectangle, _startPoint.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;

        var currentPoint = e.GetPosition(OverlayCanvas);

        var x = Math.Min(_startPoint.X, currentPoint.X);
        var y = Math.Min(_startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _startPoint.X);
        var height = Math.Abs(currentPoint.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;

        // Update size text
        SizeText.Text = $"{(int)width} × {(int)height}";
        Canvas.SetLeft(SizeIndicator, x);
        Canvas.SetTop(SizeIndicator, y - 28);

        // Update dark overlay with hole
        UpdateOverlay(x, y, width, height);
    }

    private void UpdateOverlay(double x, double y, double w, double h)
    {
        var fullRect = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        var selectionRect = new RectangleGeometry(new Rect(x, y, w, h));
        var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, selectionRect);
        DarkOverlay.Data = combined;
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;
        _isSelecting = false;

        var endPoint = e.GetPosition(OverlayCanvas);

        var x = (int)Math.Min(_startPoint.X, endPoint.X);
        var y = (int)Math.Min(_startPoint.Y, endPoint.Y);
        var width = (int)Math.Abs(endPoint.X - _startPoint.X);
        var height = (int)Math.Abs(endPoint.Y - _startPoint.Y);

        if (width > 10 && height > 10)
        {
            // Account for DPI scaling
            var source = PresentationSource.FromVisual(this);
            double dpiX = 1.0, dpiY = 1.0;
            if (source?.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            SelectedRegion = new System.Drawing.Rectangle(
                (int)(x * dpiX),
                (int)(y * dpiY),
                (int)(width * dpiX),
                (int)(height * dpiY));
            RegionSelected = true;
            DialogResult = true;
        }

        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RegionSelected = false;
            Close();
        }
    }
}
