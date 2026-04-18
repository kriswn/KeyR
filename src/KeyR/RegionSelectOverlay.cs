using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SupTask;

public class RegionSelectOverlay : Window
{
	private Point _startPoint;

	private Rectangle _selectionBox;

	private Canvas _canvas;

	public Int32Rect SelectedRect { get; private set; }

	public RegionSelectOverlay()
	{
		base.WindowStyle = WindowStyle.None;
		base.AllowsTransparency = true;
		base.Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
		base.Topmost = true;
		base.WindowState = WindowState.Maximized;
		base.Cursor = Cursors.Cross;
		_canvas = new Canvas
		{
			Background = Brushes.Transparent
		};
		base.Content = _canvas;
		_selectionBox = new Rectangle
		{
			Stroke = Brushes.Red,
			StrokeThickness = 2.0,
			Fill = new SolidColorBrush(Color.FromArgb(40, byte.MaxValue, 0, 0)),
			Visibility = Visibility.Collapsed
		};
		_canvas.Children.Add(_selectionBox);
	}

	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		_startPoint = e.GetPosition(_canvas);
		Canvas.SetLeft(_selectionBox, _startPoint.X);
		Canvas.SetTop(_selectionBox, _startPoint.Y);
		_selectionBox.Width = 0.0;
		_selectionBox.Height = 0.0;
		_selectionBox.Visibility = Visibility.Visible;
		_canvas.CaptureMouse();
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (_canvas.IsMouseCaptured)
		{
			Point position = e.GetPosition(_canvas);
			double length = Math.Min(position.X, _startPoint.X);
			double length2 = Math.Min(position.Y, _startPoint.Y);
			double width = Math.Abs(position.X - _startPoint.X);
			double height = Math.Abs(position.Y - _startPoint.Y);
			Canvas.SetLeft(_selectionBox, length);
			Canvas.SetTop(_selectionBox, length2);
			_selectionBox.Width = width;
			_selectionBox.Height = height;
		}
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		_canvas.ReleaseMouseCapture();
		SelectedRect = new Int32Rect((int)Canvas.GetLeft(_selectionBox), (int)Canvas.GetTop(_selectionBox), (int)_selectionBox.Width, (int)_selectionBox.Height);
		base.DialogResult = true;
		Close();
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		if ((int)e.Key == 13)
		{
			base.DialogResult = false;
			Close();
		}
	}
}


