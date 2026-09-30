using FluentMath.Models;
using FluentMath.Models.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace FluentMath.Controls
{
    // the formula renderer:
    // draws the boxes of MathLayoutEngine as plain XAML elements, so they sit on the Mica like the keypad;
    // a TextBlock per run, a Path per mark that scales, a Rectangle per bar and empty slot
    public sealed class MathPanel : Panel
    {
        // === fields ===

        private readonly XamlTextMeasurer _measurer = new XamlTextMeasurer();
        private readonly List<PlacedElement> _placed = new();

        private MathBox _root;
        private IReadOnlyList<MathToken> _tokens = new List<MathToken>();
        private CaretTarget _caret;
        private string _text;

        // the caret in scroller space, to keep it in view; null without one
        public Rect? CaretViewport { get; private set; }

        private Rect? _caretLocal;
        private Rect? _previewLocal;
        private double _caretPad;
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

        // what everything is drawn in; a dependency property, so the markup can hand it a ThemeResource
        // (a brush read from Application.Current.Resources in code is always the light one)
        public static readonly DependencyProperty InkProperty = DependencyProperty.Register(
            nameof(Ink), typeof(Brush), typeof(MathPanel),
            new PropertyMetadata(null, (panel, e) => ((MathPanel)panel).Rebuild()));

        public Brush Ink
        {
            get => (Brush)GetValue(InkProperty);
            set => SetValue(InkProperty, value);
        }

        // the accent, so the caret stays findable in a long formula
        public static readonly DependencyProperty CaretInkProperty = DependencyProperty.Register(
            nameof(CaretInk), typeof(Brush), typeof(MathPanel),
            new PropertyMetadata(null, (panel, e) => ((MathPanel)panel).Rebuild()));

        public Brush CaretInk
        {
            get => (Brush)GetValue(CaretInkProperty);
            set => SetValue(CaretInkProperty, value);
        }

        // the caret a click would leave; one step down from the text, a promise and not the thing itself
        public static readonly DependencyProperty PreviewCaretInkProperty = DependencyProperty.Register(
            nameof(PreviewCaretInk), typeof(Brush), typeof(MathPanel),
            new PropertyMetadata(null, (panel, e) => ((MathPanel)panel).RealizePreview()));

        public Brush PreviewCaretInk
        {
            get => (Brush)GetValue(PreviewCaretInkProperty);
            set => SetValue(PreviewCaretInkProperty, value);
        }

        // one step further down while the button is held; (a press fades, like on the keypad)
        public static readonly DependencyProperty PreviewCaretPressedInkProperty = DependencyProperty.Register(
            nameof(PreviewCaretPressedInk), typeof(Brush), typeof(MathPanel),
            new PropertyMetadata(null, (panel, e) => ((MathPanel)panel).RealizePreview()));

        public Brush PreviewCaretPressedInk
        {
            get => (Brush)GetValue(PreviewCaretPressedInkProperty);
            set => SetValue(PreviewCaretPressedInkProperty, value);
        }

        public FontFamily TextFont
        {
            get => _measurer.FontFamily;
            set => _measurer.FontFamily = value;
        }

        // whether a point in this line is worth aiming at; off for the history line and for the zero of
        // an empty line, which the input manager does not hold
        // (read at every rebuild, so it is set before the line is shown)
        public bool CaretIsPlaceable { get; set; }


        // === content ===

        // rebuilds the display from a token list; made again rather than diffed, a formula is a handful
        // of elements
        public void Show(IReadOnlyList<MathToken> tokens, CaretTarget caret = default)
        {
            _text = null;
            _tokens = tokens ?? new List<MathToken>();
            _caret = caret;
            Rebuild();
        }

        // a line of text, an error message
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

            // room for the half caret right of the last position, or the display edge cuts it in two
            // (for every line that could show one, so = does not step the formula sideways)
            _caretPad = caret != null || CaretIsPlaceable
                ? LayoutStyle.FontSizePx * LayoutStyle.CursorTrailingSpace
                : 0;

            // placed against its own top edge, the space this panel arranges in
            _root.Place(0, _root.Ascent);

            Realize(_root);

            // last, so the caret is drawn over its neighbours; it may overlap a digit, never move it
            RealizeCaret(caret);

            // the preview again for the new tree under the resting pointer
            RealizePreview();

            InvalidateMeasure();
        }


        // === layout ===

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (PlacedElement placed in _placed)
            {
                placed.Element.Measure(new Size(placed.Bounds.Width, placed.Bounds.Height));
            }

            // the preview is not a placed element; it moves with the pointer, without a rebuild
            if (_preview != null && _previewLocal is Rect preview)
            {
                _preview.Measure(new Size(preview.Width, preview.Height));
            }

            if (_root == null) return new Size(0, 0);

            _fitScale = MathFit.ScaleFor(_root.Height, availableSize.Height, LayoutStyle.MinFitScale);

            // the panel is scaled, not the children, so the size reported is the scaled one
            RenderTransform = _fitScale < 1
                ? new ScaleTransform { ScaleX = _fitScale, ScaleY = _fitScale }
                : null;

            return new Size((_root.Width + _caretPad) * _fitScale, _root.Height * _fitScale);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_root == null) return finalSize;

            // back into the unscaled space of the children
            double width = finalSize.Width / _fitScale;
            double height = finalSize.Height / _fitScale;

            // right aligned and vertically centered; the caret room comes off the right
            double offsetX = Math.Max(0, width - _root.Width - _caretPad);
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

            if (_preview != null)
            {
                _preview.Arrange(_previewLocal is Rect preview
                    ? new Rect(preview.X + offsetX, preview.Y + offsetY, preview.Width, preview.Height)
                    : new Rect(0, 0, 0, 0));
            }

            return finalSize;
        }


        // === hit testing ===

        // the nearest caret address to a point in this panel, or null with nothing to aim at
        // (a point relative to the panel is already unscaled; only the arrange offsets are undone)
        public string AddressAt(Point point)
        {
            if (_root == null || _text != null || !CaretIsPlaceable) return null;

            // a result has no caret but takes a click; the ViewModel seeds it into the manager first
            return MathHitTest.NearestAddress(_root, point.X - _offsetX, point.Y - _offsetY);
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

                // the bounds and the sign are boxes of their own
                case StackBox stack:
                    foreach (MathBox child in stack.Children) Realize(child);
                    break;

                case OverlineBox overline:
                    Add(new Rectangle { Fill = Ink },
                        new Rect(overline.X, overline.BarTop, overline.Width, overline.BarThickness));

                    Realize(overline.Content);
                    break;

                case PlaceholderBox placeholder:
                    // the square stands where a digit would, see PlaceholderBox.SquareTop
                    Add(new Rectangle { Stroke = Ink, StrokeThickness = placeholder.Thickness },
                        new Rect(placeholder.X, placeholder.SquareTop, placeholder.Side, placeholder.Side));
                    break;
            }
        }

        // the caret is no part of the layout; it straddles the point it marks, half either side
        private void RealizeCaret(CaretPlacement? placement)
        {
            if (placement is not CaretPlacement caret) return;

            Rect caretRect = CaretRect(caret);
            double radius = caret.FontSize * LayoutStyle.CursorCornerRadius;

            _caretLocal = caretRect;

            Add(new Rectangle
            {
                Fill = CaretInk ?? Ink,
                RadiusX = radius,
                RadiusY = radius
            }, caretRect);
        }

        // the box a caret fills, the real one and the preview alike
        // (on the baseline of the line, not of the box; an operator rides higher than a digit)
        private Rect CaretRect(CaretPlacement caret)
        {
            double size = caret.FontSize;
            double width = size * LayoutStyle.CursorWidth;
            double height = size * LayoutStyle.CursorHeight;
            double bottom = caret.Line.Baseline - size * LayoutStyle.CursorShift;

            return new Rect(caret.Box.X + caret.Offset - width / 2, bottom - height, width, height);
        }

        // === caret preview ===

        // the caret a click would leave, under the pointer; the hit test and geometry of a tap, so the
        // preview cannot drift from what the click does
        // (the pointer is kept, not the rectangle, since a keystroke rebuilds the tree under it)
        private Rectangle _preview;
        private Point? _previewPoint;
        private bool _previewPressed;

        public void ShowPreviewCaret(Point point, bool pressed)
        {
            _previewPoint = point;
            _previewPressed = pressed;

            RealizePreview();
        }

        public void HidePreviewCaret()
        {
            _previewPoint = null;

            CollapsePreview();
        }

        private void RealizePreview()
        {
            // nothing to aim at in an error message; a result has no caret but is still clickable
            if (_previewPoint is not Point point || _root == null || _text != null || !CaretIsPlaceable)
            {
                CollapsePreview();
                return;
            }

            CaretPlacement? nearest = MathHitTest.NearestCaret(_root, point.X - _offsetX, point.Y - _offsetY);
            if (nearest is not CaretPlacement caret)
            {
                CollapsePreview();
                return;
            }

            Rect rect = CaretRect(caret);
            double radius = caret.FontSize * LayoutStyle.CursorCornerRadius;

            _preview ??= new Rectangle();

            _preview.Fill = (_previewPressed ? PreviewCaretPressedInk : PreviewCaretInk) ?? Ink;
            _preview.RadiusX = radius;
            _preview.RadiusY = radius;
            _preview.Width = rect.Width;
            _preview.Height = rect.Height;
            _preview.Visibility = Visibility.Visible;

            // added last, so it is drawn over the caret; a rebuild empties the panel and puts it back on top
            if (!Children.Contains(_preview)) Children.Add(_preview);

            _previewLocal = rect;

            InvalidateArrange();
        }

        private void CollapsePreview()
        {
            _previewLocal = null;

            if (_preview != null) _preview.Visibility = Visibility.Collapsed;
        }


        // === realising boxes ===

        private void RealizeRun(TextRunBox run)
        {
            // measured with BaselineOffset, so at the box top the baseline lands where the layout put it
            Add(new TextBlock
            {
                Text = run.Text,
                FontSize = run.FontSize,
                FontFamily = TextFont,
                Foreground = Ink
            }, BoundsOf(run));
        }

        // drawn rather than a glyph, so it grows with its radicand
        private void RealizeRadical(RootBox root)
        {
            Rect bounds = BoundsOf(root);

            double hookLeft = root.IndexWidth;
            double ruleY = root.Ascent - root.SignAscent; // the index and the room above the radicand may stand higher than the sign
            double bottom = root.Ascent + root.SignDescent; // the radicand may reach lower than the sign
            double drop = bottom - ruleY;

            // two strokes with their own weights, the sign and the rule; they meet at the shoulder, where
            // the round joins close the step between the two weights
            Point shoulder = new Point(hookLeft + root.HookWidth, ruleY);

            PathFigure hook = new PathFigure
            {
                StartPoint = new Point(hookLeft, ruleY + drop * 0.55),
                IsClosed = false
            };

            hook.Segments.Add(new LineSegment { Point = new Point(hookLeft + root.HookWidth * 0.28, ruleY + drop * 0.45) });
            hook.Segments.Add(new LineSegment { Point = new Point(hookLeft + root.HookWidth * 0.5, bottom) });
            hook.Segments.Add(new LineSegment { Point = shoulder });

            PathFigure rule = new PathFigure { StartPoint = shoulder, IsClosed = false };
            rule.Segments.Add(new LineSegment { Point = new Point(bounds.Width, ruleY) });

            Add(StrokedPath(hook, root.HookThickness), bounds);
            Add(StrokedPath(rule, root.RuleThickness), bounds);
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

                // a stem with a foot pointing in, at the bottom for the floor and at the top for the ceiling
                case DelimiterKind.FloorOpen:
                    AddCorner(figure, width * 0.35, 0, height, width * 0.95);
                    break;

                case DelimiterKind.FloorClose:
                    AddCorner(figure, width * 0.65, 0, height, width * 0.05);
                    break;

                case DelimiterKind.CeilingOpen:
                    AddCorner(figure, width * 0.35, height, 0, width * 0.95);
                    break;

                case DelimiterKind.CeilingClose:
                    AddCorner(figure, width * 0.65, height, 0, width * 0.05);
                    break;

                default:
                    figure.StartPoint = new Point(width / 2, 0);
                    figure.Segments.Add(new LineSegment { Point = new Point(width / 2, height) });
                    break;
            }

            Add(StrokedPath(figure, delimiter.Thickness), bounds);
        }


        // === helpers ===

        // a stem from the free end down or up to the corner, and the foot from the corner across
        private static void AddCorner(PathFigure figure, double stemX, double freeY, double cornerY, double footX)
        {
            figure.StartPoint = new Point(stemX, freeY);
            figure.Segments.Add(new LineSegment { Point = new Point(stemX, cornerY) });
            figure.Segments.Add(new LineSegment { Point = new Point(footX, cornerY) });
        }

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

        // every figure is drawn in the space of its own box; the arrange adds one offset to all
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
