using Calculator_WinUI.Models;
using Calculator_WinUI.Models.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace Calculator_WinUI.Controls
{
    // draws a formula out of ordinary XAML elements: a TextBlock per run of glyphs, a Path for the marks
    // that have to scale with what they enclose, a Rectangle for a fraction bar and for an empty slot
    //
    // that choice is the point of the whole renderer. These composite over the system backdrop exactly
    // like the keypad does, while a WebView2 paints ApplicationPageBackgroundThemeBrush behind its page
    // and can never be transparent, see https://github.com/microsoft/microsoft-ui-xaml/issues/6527
    //
    // the layout itself is none of this classes business; MathLayoutEngine turns tokens into boxes with
    // no WinUI in sight, and what happens here is only realising those boxes into elements
    public sealed class MathPanel : Panel
    {
        // === fields ===

        private readonly XamlTextMeasurer _measurer = new XamlTextMeasurer();
        private readonly List<PlacedElement> _placed = new();

        private MathBox _root;
        private IReadOnlyList<MathToken> _tokens = new List<MathToken>();
        private CaretTarget _caret;
        private string _text;

        // where the caret ended up, for a scroller that has to keep it in view; null when there is none
        public Rect? CaretBounds { get; private set; }

        private readonly struct PlacedElement
        {
            public UIElement Element { get; }
            public Rect Bounds { get; }

            public PlacedElement(UIElement element, Rect bounds)
            {
                Element = element;
                Bounds = bounds;
            }
        }


        // === appearance ===

        public MathLayoutStyle LayoutStyle { get; set; } = new MathLayoutStyle();

        // what everything is drawn in
        //
        // a dependency property rather than a plain one so the markup can hand it a ThemeResource: that
        // re-resolves itself on a theme change, while reading a brush out of Application.Current.Resources
        // from code comes back with the light value whatever the theme is
        public static readonly DependencyProperty InkProperty = DependencyProperty.Register(
            nameof(Ink), typeof(Brush), typeof(MathPanel),
            new PropertyMetadata(null, (panel, e) => ((MathPanel)panel).Rebuild()));

        public Brush Ink
        {
            get => (Brush)GetValue(InkProperty);
            set => SetValue(InkProperty, value);
        }

        // the caret is drawn in the system accent rather than in the text color, so it stays findable in a
        // long formula
        public static readonly DependencyProperty CaretInkProperty = DependencyProperty.Register(
            nameof(CaretInk), typeof(Brush), typeof(MathPanel),
            new PropertyMetadata(null, (panel, e) => ((MathPanel)panel).Rebuild()));

        public Brush CaretInk
        {
            get => (Brush)GetValue(CaretInkProperty);
            set => SetValue(CaretInkProperty, value);
        }

        public FontFamily TextFont
        {
            get => _measurer.FontFamily;
            set => _measurer.FontFamily = value;
        }


        // === content ===

        // rebuilds the display from a token list
        //
        // the whole tree is thrown away and made again rather than diffed, because a formula is a handful
        // of elements and a keystroke can change any of them
        public void Show(IReadOnlyList<MathToken> tokens, CaretTarget caret = default)
        {
            _text = null;
            _tokens = tokens ?? new List<MathToken>();
            _caret = caret;
            Rebuild();
        }

        // a line of text rather than a formula, which is what an error message is
        public void ShowText(string text)
        {
            _text = text;
            _tokens = new List<MathToken>();
            _caret = default;
            Rebuild();
        }

        private void Rebuild()
        {
            Children.Clear();
            _placed.Clear();

            CaretBounds = null;

            if (_text != null)
            {
                TextMetrics metrics = _measurer.Measure(_text, LayoutStyle.FontSizePx);
                _root = new RowBox(new List<MathBox>
                {
                    new TextRunBox(_text, LayoutStyle.FontSizePx, metrics, new List<MathToken>())
                });
            }
            else
            {
                MathLayoutEngine engine = new MathLayoutEngine(_measurer, LayoutStyle, _caret);
                _root = engine.BuildRow(_tokens);
            }

            // placed against its own top edge, so every bound below is already in the space this panel
            // arranges in
            _root.Place(0, _root.Ascent);

            Realize(_root);
            InvalidateMeasure();
        }


        // === layout ===

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (PlacedElement placed in _placed)
            {
                placed.Element.Measure(new Size(placed.Bounds.Width, placed.Bounds.Height));
            }

            if (_root == null) return new Size(0, 0);

            return new Size(_root.Width, _root.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_root == null) return finalSize;

            // a calculator display fills from the right and sits in the middle of its box
            double offsetX = Math.Max(0, finalSize.Width - _root.Width);
            double offsetY = Math.Max(0, (finalSize.Height - _root.Height) / 2);

            foreach (PlacedElement placed in _placed)
            {
                placed.Element.Arrange(new Rect(
                    placed.Bounds.X + offsetX,
                    placed.Bounds.Y + offsetY,
                    placed.Bounds.Width,
                    placed.Bounds.Height));
            }

            return finalSize;
        }


        // === realising boxes ===

        private void Realize(MathBox box)
        {
            switch (box)
            {
                case RowBox row:
                    foreach (MathBox child in row.Children) Realize(child);
                    break;

                case TextRunBox run:
                    RealizeRun(run);
                    break;

                case FractionBox fraction:
                    // the bar reaches the full width of the box, which is what the side padding is for
                    Add(new Rectangle { Fill = Ink },
                        new Rect(fraction.X, fraction.BarTop, fraction.Width, fraction.BarThickness));

                    Realize(fraction.Numerator);
                    Realize(fraction.Denominator);
                    break;

                case RootBox root:
                    RealizeRadical(root);

                    if (root.Index != null) Realize(root.Index);
                    Realize(root.Radicand);
                    break;

                case DelimiterBox delimiter:
                    RealizeDelimiter(delimiter);
                    break;

                case CaretBox caret:
                    RealizeCaret(caret);
                    break;

                case PlaceholderBox placeholder:
                    Add(new Rectangle { Stroke = Ink, StrokeThickness = placeholder.Thickness },
                        BoundsOf(placeholder));
                    break;
            }
        }

        // the box itself has no width, so the bar is straddled over the point it marks: half of it either
        // side, which is how a text caret sits in the gap between two glyphs rather than beside one
        private void RealizeCaret(CaretBox caret)
        {
            double width = caret.FontSize * LayoutStyle.CursorWidth;
            double height = caret.FontSize * LayoutStyle.CursorHeight;
            double bottom = caret.Baseline - caret.FontSize * LayoutStyle.CursorShift;
            double radius = caret.FontSize * LayoutStyle.CursorCornerRadius;

            Rect caretRect = new Rect(caret.X - width / 2, bottom - height, width, height);
            CaretBounds = caretRect;

            Add(new Rectangle
            {
                Fill = CaretInk ?? Ink,
                RadiusX = radius,
                RadiusY = radius
            }, caretRect);
        }

        private void RealizeRun(TextRunBox run)
        {
            // a TextBlock puts its baseline at BaselineOffset from its own top, and that is the number the
            // run was measured with, so arranging it at the box top lands the baseline where the layout
            // decided it goes
            Add(new TextBlock
            {
                Text = run.Text,
                FontSize = run.FontSize,
                FontFamily = TextFont,
                Foreground = Ink
            }, BoundsOf(run));
        }

        // the radical is drawn rather than set from a glyph, so it grows with its radicand instead of
        // coming in the handful of sizes a font happens to carry
        private void RealizeRadical(RootBox root)
        {
            Rect bounds = BoundsOf(root);

            double hookLeft = root.IndexWidth;
            double ruleY = root.Ascent - root.SignAscent; // the index may stand higher than the sign
            double bottom = bounds.Height;
            double drop = bottom - ruleY;

            PathFigure figure = new PathFigure
            {
                StartPoint = new Point(hookLeft, ruleY + drop * 0.55),
                IsClosed = false
            };

            figure.Segments.Add(new LineSegment { Point = new Point(hookLeft + root.HookWidth * 0.28, ruleY + drop * 0.45) });
            figure.Segments.Add(new LineSegment { Point = new Point(hookLeft + root.HookWidth * 0.5, bottom) });
            figure.Segments.Add(new LineSegment { Point = new Point(hookLeft + root.HookWidth, ruleY) });
            figure.Segments.Add(new LineSegment { Point = new Point(bounds.Width, ruleY) });

            Add(StrokedPath(figure, root.RuleThickness), bounds);
        }

        private void RealizeDelimiter(DelimiterBox delimiter)
        {
            Rect bounds = BoundsOf(delimiter);
            double width = bounds.Width;
            double height = bounds.Height;

            PathFigure figure = new PathFigure { IsClosed = false };

            switch (delimiter.Kind)
            {
                case DelimiterKind.ParenthesisOpen:
                    figure.StartPoint = new Point(width * 0.8, 0);
                    figure.Segments.Add(new QuadraticBezierSegment
                    {
                        Point1 = new Point(width * 0.05, height / 2),
                        Point2 = new Point(width * 0.8, height)
                    });
                    break;

                case DelimiterKind.ParenthesisClose:
                    figure.StartPoint = new Point(width * 0.2, 0);
                    figure.Segments.Add(new QuadraticBezierSegment
                    {
                        Point1 = new Point(width * 0.95, height / 2),
                        Point2 = new Point(width * 0.2, height)
                    });
                    break;

                default:
                    figure.StartPoint = new Point(width / 2, 0);
                    figure.Segments.Add(new LineSegment { Point = new Point(width / 2, height) });
                    break;
            }

            Add(StrokedPath(figure, delimiter.Thickness), bounds);
        }


        // === helpers ===

        private Path StrokedPath(PathFigure figure, double thickness)
        {
            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            return new Path
            {
                Data = geometry,
                Stroke = Ink,
                StrokeThickness = thickness,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
        }

        // every figure above is written in the space of its own box, so the panel can shift the whole
        // formula by arranging each element at its bounds plus one offset
        private static Rect BoundsOf(MathBox box)
        {
            return new Rect(box.X, box.Top, box.Width, box.Height);
        }

        private void Add(UIElement element, Rect bounds)
        {
            Children.Add(element);
            _placed.Add(new PlacedElement(element, bounds));
        }
    }
}
