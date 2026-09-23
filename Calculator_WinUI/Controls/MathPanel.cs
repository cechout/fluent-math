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

        // the caret a click would leave behind, one step down from the text rather than in the accent:
        // it has to read as a promise and not as the thing itself
        public static readonly DependencyProperty PreviewCaretInkProperty = DependencyProperty.Register(
            nameof(PreviewCaretInk), typeof(Brush), typeof(MathPanel),
            new PropertyMetadata(null, (panel, e) => ((MathPanel)panel).RealizePreview()));

        public Brush PreviewCaretInk
        {
            get => (Brush)GetValue(PreviewCaretInkProperty);
            set => SetValue(PreviewCaretInkProperty, value);
        }

        // and one step down again while the button is held, which is the direction a press takes
        // everything else in this app: the keypad answers a press by fading rather than by brightening
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

        // whether a point in this line is worth aiming at
        //
        // the display sometimes draws a formula the input manager does not hold. The zero on an empty
        // line is one of those and holds a single position, so the hit test would happily answer with
        // the other side of it while the caret stays put; the page turns this off for that state and
        // neither the preview nor a tap offers a place that is not one
        //
        // it is off by default and the page turns it on for the line being typed in, which leaves the
        // history line out of it without having to say so; the trailing room below is read from it at
        // every rebuild, so it is set before the line is shown and not after
        public bool CaretIsPlaceable { get; set; }


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

            // a line that can carry a caret keeps room for the half of one that stands right of the
            // last position, or the edge of the display cuts it in two
            //
            // reserved for a line that could show a caret and not only for one that does: a result
            // carries none and is still aimed at, and room that came and went with it would step the
            // whole formula sideways the moment = replaced it. That is the very shift the trailing
            // space exists to prevent, one state further out than it was written for
            _caretPad = caret != null || CaretIsPlaceable
                ? LayoutStyle.FontSizePx * LayoutStyle.CursorTrailingSpace
                : 0;

            // placed against its own top edge, so every bound below is already in the space this panel
            // arranges in
            _root.Place(0, _root.Ascent);

            Realize(_root);

            // last, so the bar is drawn over its neighbours rather than under them; it is allowed to
            // overlap the digit beside it and never to move it
            RealizeCaret(caret);

            // the tree it was aimed at is gone, so the preview is worked out again from the point the
            // pointer is still resting on
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

            // the preview is not one of the placed elements: it moves with the pointer rather than with
            // the formula, and rebuilding the tree for a mouse move is what this whole arrangement is
            // there to avoid
            if (_preview != null && _previewLocal is Rect preview)
            {
                _preview.Measure(new Size(preview.Width, preview.Height));
            }

            if (_root == null) return new Size(0, 0);

            _fitScale = MathFit.ScaleFor(_root.Height, availableSize.Height, LayoutStyle.MinFitScale);

            // the children stay in their own coordinates and the whole panel is scaled instead, which is
            // why the size reported here is the scaled one: the scroller around it has to see the size it
            // will actually occupy
            RenderTransform = _fitScale < 1
                ? new ScaleTransform { ScaleX = _fitScale, ScaleY = _fitScale }
                : null;

            return new Size((_root.Width + _caretPad) * _fitScale, _root.Height * _fitScale);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_root == null) return finalSize;

            // the children are placed in their own unscaled coordinates, so the box they are fitted into
            // has to be read back out of the scale the panel carries
            double width = finalSize.Width / _fitScale;
            double height = finalSize.Height / _fitScale;

            // a calculator display fills from the right and sits in the middle of its box; the caret room
            // comes off the right, which is the only place it is needed
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

        // the nearest place the caret could go to a point in this panel, or null when there is nothing
        // to aim at
        //
        // the point arrives in this panels own coordinates, which is the unscaled space the boxes were
        // placed in: a render transform belongs to the step from the panel into its parent, so anything
        // that reports a point relative to the panel has already taken the fit scale back off and only
        // the two arrange offsets are left to undo
        public string AddressAt(Point point)
        {
            if (_root == null || _text != null || !CaretIsPlaceable) return null;

            // a line drawn without a caret is still one that can be clicked into: after = the display
            // holds the result rather than the formula that produced it, and the ViewModel answers that
            // by seeding the result, which puts the very tokens the address was worked out against into
            // the manager
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

        // the box a caret fills, for the one being typed in and for a preview of one alike
        //
        // the baseline is the one of the line it stands in and never the one of the box it hangs off: an
        // operator rides above the baseline of its row, and taking the height from it would stand the
        // caret higher in front of a plus than in front of a digit
        private Rect CaretRect(CaretPlacement caret)
        {
            double size = caret.FontSize;
            double width = size * LayoutStyle.CursorWidth;
            double height = size * LayoutStyle.CursorHeight;
            double bottom = caret.Line.Baseline - size * LayoutStyle.CursorShift;

            return new Rect(caret.Box.X + caret.Offset - width / 2, bottom - height, width, height);
        }

        // === caret preview ===

        // the caret a click would leave behind, drawn under the pointer rather than where the cursor is
        //
        // it goes through the hit test a tap goes through and comes back as the same CaretPlacement the
        // layout reports the real caret in, so the two are drawn by one piece of geometry and the
        // promise cannot drift away from what the click does
        //
        // what is kept is the pointer position and not the rectangle: a keystroke rebuilds the tree
        // under a pointer that never moved, and the preview has to answer for the new one
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
            // an error message is a line of text rather than a formula, and there is nothing in it to
            // aim at; a result carries no caret either and is still clickable, so the caret target is
            // not what decides this
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

            // added last, so it is drawn over the caret it is a preview of rather than under it; a
            // rebuild empties the panel, which is what puts it back on top again afterwards
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

            // two strokes and not one: the sign is a letter stroke and the bar over the radicand is a
            // rule, and they carry their own weights
            //
            // they meet at the top of the hook, which both figures name as the same point; the round
            // joins StrokedPath draws with are what closes the step when the two weights differ
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
