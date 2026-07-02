using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace ms_paint_project
{
    public partial class MainWindow : Window
    {
        private DrawingManager _drawingManager = new DrawingManager();
        private IDrawingStrategy _currentStrategy = new PenStrategy();
        private Brush _selectedColor = Brushes.Black;
        private bool _isDrawing = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void PaintCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = true;
            double thickness = SliderThickness.Value; 

            if (_currentStrategy is TextStrategy ts)
            {
                if (ComboFontSize.SelectedItem is ComboBoxItem selectedItem)
                {
                    ts.FontSize = double.Parse(selectedItem.Content.ToString()!);
                }
            }

            _currentStrategy.MouseDown(PaintCanvas, e.GetPosition(PaintCanvas), _selectedColor, thickness);

        }

        private void PaintCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDrawing) _currentStrategy.MouseMove(e.GetPosition(PaintCanvas));
        }

        private void PaintCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawing)
            {
                UIElement? element = _currentStrategy.MouseUp();
                if (element != null) _drawingManager.ExecuteCommand(new AddShapeCommand(PaintCanvas, element));
                _isDrawing = false;
            }
        }

        private void BtnTool_Click(object sender, RoutedEventArgs e)
        {
            string tool = (sender as Button)?.Name ?? "";
            if (tool == "BtnPen") _currentStrategy = new PenStrategy();
            else if (tool == "BtnRect") _currentStrategy = new RectStrategy();
            else if (tool == "BtnCircle") _currentStrategy = new CircleStrategy();
            else if (tool == "BtnText") _currentStrategy = new TextStrategy();
            else if (tool == "BtnEraser") _currentStrategy = new EraserStrategy();
        }

        private void BtnColor_Click(object sender, RoutedEventArgs e) => _selectedColor = (sender as Button)?.Background ?? Brushes.Black;
        private void BtnUndo_Click(object sender, RoutedEventArgs e) => _drawingManager.Undo();
        private void BtnRedo_Click(object sender, RoutedEventArgs e) => _drawingManager.Redo();
        private void BtnClear_Click(object sender, RoutedEventArgs e) { PaintCanvas.Children.Clear(); _drawingManager = new DrawingManager(); }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog { Filter = "Bitmap Image|*.bmp" };
            if (dlg.ShowDialog() == true)
            {
                int width = (int)BorderForCanvas.ActualWidth;
                int height = (int)BorderForCanvas.ActualHeight;

                if (width <= 0) width = 800;
                if (height <= 0) height = 600;

                RenderTargetBitmap rtb = new RenderTargetBitmap((int)PaintCanvas.ActualWidth, (int)PaintCanvas.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(PaintCanvas);
                BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(dlg.FileName)) encoder.Save(fs);
                MessageBox.Show("Kaydedildi!");
            }
        }
    }

    public interface IDrawingStrategy
    {
        void MouseDown(Canvas c, Point p, Brush b, double thickness);
        void MouseMove(Point p);
        UIElement? MouseUp();
    }

    public class PenStrategy : IDrawingStrategy
    {
        private Polyline? _l;
        public void MouseDown(Canvas c, Point p, Brush b, double t)
        {
            _l = new Polyline { Stroke = b, StrokeThickness = t, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            _l.Points.Add(p); c.Children.Add(_l);
        }
        public void MouseMove(Point p) => _l?.Points.Add(p);
        public UIElement? MouseUp() { var temp = _l; _l = null; return temp; }
    }

    public class EraserStrategy : IDrawingStrategy
    {
        private Polyline? _eraserLine;
        private const double ERASER_SIZE = 20;

        public void MouseDown(Canvas c, Point p, Brush b, double t)
        {
            _eraserLine = new Polyline
            {
                Stroke = Brushes.White,
                StrokeThickness = ERASER_SIZE,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            _eraserLine.Points.Add(p);
            c.Children.Add(_eraserLine);
        }

        public void MouseMove(Point p) => _eraserLine?.Points.Add(p);

        public UIElement? MouseUp()
        {
            var temp = _eraserLine;
            _eraserLine = null;
            return temp;
        }
    }

    public class RectStrategy : IDrawingStrategy
    {
        private Rectangle? _r; private Point _s;
        public void MouseDown(Canvas c, Point p, Brush b, double t) { _s = p; _r = new Rectangle { Stroke = b, StrokeThickness = t }; c.Children.Add(_r); }
        public void MouseMove(Point p) { if (_r == null) return; _r.Width = Math.Abs(p.X - _s.X); _r.Height = Math.Abs(p.Y - _s.Y); Canvas.SetLeft(_r, Math.Min(p.X, _s.X)); Canvas.SetTop(_r, Math.Min(p.Y, _s.Y)); }
        public UIElement? MouseUp() { var temp = _r; _r = null; return temp; }
    }

    public class CircleStrategy : IDrawingStrategy
    {
        private Ellipse? _el; private Point _s;
        public void MouseDown(Canvas c, Point p, Brush b, double t) { _s = p; _el = new Ellipse { Stroke = b, StrokeThickness = t }; c.Children.Add(_el); }
        public void MouseMove(Point p) { if (_el == null) return; _el.Width = Math.Abs(p.X - _s.X); _el.Height = Math.Abs(p.Y - _s.Y); Canvas.SetLeft(_el, Math.Min(p.X, _s.X)); Canvas.SetTop(_el, Math.Min(p.Y, _s.Y)); }
        public UIElement? MouseUp() { var temp = _el; _el = null; return temp; }
    }

    public class TextStrategy : IDrawingStrategy
    {
        private TextBox? _txt;
        public double FontSize { get; set; } = 12;
        public void MouseDown(Canvas c, Point p, Brush b, double t)
        {
            _txt = new TextBox { Text = "Metin", Foreground = b, FontSize = this.FontSize, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
            Canvas.SetLeft(_txt, p.X); Canvas.SetTop(_txt, p.Y); c.Children.Add(_txt);
        }
        public void MouseMove(Point p) { }
        public UIElement? MouseUp() { var temp = _txt; _txt = null; return temp; }
    }

    public interface ICommand { void Execute(); void UnExecute(); }
    public class AddShapeCommand : ICommand
    {
        private Canvas _c; private UIElement _e;
        public AddShapeCommand(Canvas c, UIElement e) { _c = c; _e = e; }
        public void Execute() { if (!_c.Children.Contains(_e)) _c.Children.Add(_e); }
        public void UnExecute() { _c.Children.Remove(_e); }
    }
    public class DrawingManager
    {
        private Stack<ICommand> _undo = new Stack<ICommand>();
        private Stack<ICommand> _redo = new Stack<ICommand>();
        public void ExecuteCommand(ICommand cmd) { _undo.Push(cmd); _redo.Clear(); }
        public void Undo() { if (_undo.Count > 0) { var c = _undo.Pop(); c.UnExecute(); _redo.Push(c); } }
        public void Redo() { if (_redo.Count > 0) { var c = _redo.Pop(); c.Execute(); _undo.Push(c); } }
    }
}