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

        // where the caret ended up in the space the scroller works in, so it can be kept in view; null
        // when there is none
        public Rect? CaretViewport { get; private set; }

        private Rect? _caretLocal;
        private double _fitScale = 1;
        private double _offsetX;
        private double _offsetY;

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

            _caretLocal = null;
            CaretViewport = null;

            if (_text != null)
            {
                TextMetrics metrics = _measurer.Measure(_text, LayoutStyle.FontSizePx);
                _root = new RowBox(new List<MathBox>
                {
                    new TextRunBox(_text, LayoutStyle.FontSizePx, metrics, new List<MathToken>())
                });
            }
            CaretPlacement? caret = null;
            if (_text == null)
            {
                MathLayoutEngine engine = new MathLayoutEngine(_measurer, LayoutStyle, _caret);
                _root = engine.BuildRow(_tokens);
                caret = engine.Caret;
            }

            // placed against its own top edge, so every bound below is already in the space this panel
            // arranges in
            _root.Place(0, _root.Ascent);

            Realize(_root);

            // last, so the bar is drawn over its neighbours rather than under them; it is allowed to
            // overlap the digit beside it and never to move it
            RealizeCaret(caret);

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

            _fitScale = MathFit.ScaleFor(_root.Height, availableSize.Height, LayoutStyle.MinFitScale);

            // the children stay in their own coordinates and the whole panel is scaled instead, which is
            // why the size reported here is the scaled one: the scroller around it has to see the size it
            // will actually occupy
            RenderTransform = _fitScale < 1
                ? new ScaleTransform { ScaleX = _fitScale, ScaleY = _fitScale }
                : null;

            return new Size(_root.Width * _fitScale, _root.Height * _fitScale);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_root == null) return finalSize;

            // the children are placed in their own unscaled coordinates, so the box they are fitted into
            // has to be read back out of the scale the panel carries
            double width = finalSize.Width / _fitScale;
            double height = finalSize.Height / _fitScale;

            // a calculator display fills from the right and sits in the middle of its box
            double offsetX = Math.Max(0, width - _root.Width);
            double offsetY = Math.Max(0, (height - _root.Height) / 2);

            _offsetX = offsetX;
            _offsetY = offsetY;

            foreach (PlacedElement placed in _placed)
            {
                placed.Element.Arrange(new Rect(
                    placed.Bounds.X + offsetX,
                    placed.Bounds.Y + offsetY,
                    placed.Bounds.Width,
                    placed.Bounds.Height));
            }

            CaretViewport = _caretLocal is Rect caret
                ? new Rect(
                    (caret.X + offsetX) * _fitScale,
                    (caret.Y + offsetY) * _fitScale,
                    caret.Width * _fitScale,
                    caret.Height * _fitScale)
                : null;

            return finalSize;
        }


        // === hit testing ===

        // the nearest place the caret could go to a point in this panel, or null when there is nothing
        // to aim at
        //
        // the point arrives in the space the panel is drawn in, so the fit scale and the two arrange
        // offsets have to come back off before the boxes recognise it
        public string AddressAt(Point point)
        {
            if (_root == null || _text != null) return null;

            return MathHitTest.NearestAddress(
                _root, point.X / _fitScale - _offsetX, point.Y / _fitScale - _offsetY);
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

                case PlaceholderBox placeholder:
                    // the box is as tall as the text that would fill the slot, the square is not, so it
                    // sits in the middle of it
                    double middle = (placeholder.Top + placeholder.Bottom) / 2;

                    Add(new Rectangle { Stroke = Ink, StrokeThickness = placeholder.Thickness },
                        new Rect(placeholder.X, middle - placeholder.Side / 2,
                            placeholder.Side, placeholder.Side));
                    break;
            }
        }

        // the caret is no part of the layout at all; it hangs off a box that was placed without it and
        // is straddled over the point it marks, half of it either side, the way a text caret sits in the
        // gap between two glyphs rather than beside one
        private void RealizeCaret(CaretPlacement? placement)
        {
            if (placement is not CaretPlacement caret) return;

            double size = caret.FontSize;
            double width = size * LayoutStyle.CursorWidth;
            double height = size * LayoutStyle.CursorHeight;
            double bottom = caret.Box.Baseline - size * LayoutStyle.CursorShift;
            double radius = size * LayoutStyle.CursorCornerRadius;

            Rect caretRect = new Rect(
                caret.Box.X + caret.Offset - width / 2, bottom - height, width, height);

            _caretLocal = caretRect;

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
