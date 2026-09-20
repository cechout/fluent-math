using Calculator_WinUI.Engines;
using Calculator_WinUI.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Calculator_WinUI.ViewModels
{
    // the only translator between the keypad and the input engine
    //
    // every button in StandardPage is bound to the same InputCommand and identifies itself through its
    // CommandParameter, so adding a key is a XAML change plus one arm in the switch below; the page
    // itself stays free of input logic
    public class StandardViewModel : INotifyPropertyChanged
    {
        // === fields ===

        private readonly MathInputManager _inputManager = new MathInputManager();
        private readonly MathEvaluator _evaluator = new MathEvaluator();

        // true while the display shows a result instead of the formula being typed; the next keypress
        // decides whether that result is dropped or carried into the next calculation
        private bool _isShowingResult;

        // which shape the shown result is in; the S to D key cycles it, every = starts over at decimal
        private AnswerForm _answerForm;


        // === display properties ===

        // both hold LaTeX, not plain text; StandardPage feeds them straight to KaTeX
        private string _inputAndResultText = "0";
        public string InputAndResultText
        {
            get => _inputAndResultText;
            set
            {
                if (_inputAndResultText != value)
                {
                    _inputAndResultText = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _calculationText = "";
        public string CalculationText
        {
            get => _calculationText;
            set
            {
                if (_calculationText != value)
                {
                    _calculationText = value;
                    OnPropertyChanged();
                }
            }
        }


        // === angle mode ===

        // the evaluator owns the mode; this pair exists so the keypad can set it and the indicator above
        // the display can follow it
        public AngleMode CurrentAngleMode
        {
            get => _evaluator.AngleMode;
            set
            {
                if (_evaluator.AngleMode == value) return;

                _evaluator.AngleMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AngleModeLabel));
            }
        }

        // the three letters a Casio shows above the formula
        public string AngleModeLabel
        {
            get
            {
                if (CurrentAngleMode == AngleMode.Radians) return "RAD";
                if (CurrentAngleMode == AngleMode.Gradians) return "GRA";

                return "DEG";
            }
        }

        // --- revisit: angle unit selector ---
        // the three keys below have no button anywhere yet; they were in the extra functions flyout and
        // came back out because the unit is a mode rather than a function and wants a place of its own
        // the evaluator and the header indicator are finished and stay, so the selector is markup plus
        // three CommandParameters whenever that place is decided
        private void SetAngleMode(string sign)
        {
            if (sign == "cmd_angle_rad") { CurrentAngleMode = AngleMode.Radians; }
            else if (sign == "cmd_angle_gra") { CurrentAngleMode = AngleMode.Gradians; }
            else { CurrentAngleMode = AngleMode.Degrees; }
        }


        // === shift layer ===

        // the second keyboard layer is not a separate panel; each shiftable key is two buttons stacked in
        // the same grid cell, and these two flags swap which of them is visible
        //
        // plain bools rather than Visibility, which x:Bind converts on its own; that keeps the whole
        // ViewModel free of WinUI and is what lets it be tested without the Windows App SDK
        private bool _isShiftLayer;

        public bool IsNormalLayer => !_isShiftLayer;

        public bool IsShiftLayer => _isShiftLayer;


        // === commands ===

        public ICommand InputCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand BackspaceCommand { get; }
        public ICommand ToggleAnswerFormCommand { get; }


        // === constructor ===

        public StandardViewModel()
        {
            InputCommand = new RelayCommand<string>(AddToTextBox);
            CalculateCommand = new RelayCommand<object>(_ => CalculateResult());
            ClearCommand = new RelayCommand<object>(_ => ClearAll());
            BackspaceCommand = new RelayCommand<object>(_ => Backspace());
            ToggleAnswerFormCommand = new RelayCommand<object>(_ => ToggleAnswerForm());

            // the starting display comes from the engine rather than a literal, so the cursor is already
            // where it belongs before the first key is pressed
            PublishInput();
        }


        // === input handling ===

        // three kinds of parameter arrive here: a "cmd_" keyword for anything structural, a bare operator,
        // and anything else, which is treated as a digit or a decimal point
        //
        // the extra functions flyout on the keypad sends the same parameters as the keys around it, so
        // nothing about it reaches this far
        private void AddToTextBox(string sign)
        {
            // shift only swaps the keyboard layer, it must never disturb the input or a shown result
            if (sign == "cmd_shift")
            {
                ToggleShift();
                return;
            }

            // the angle unit is a mode rather than an input, so like shift it must leave both the
            // formula and a shown result exactly where they are
            if (sign.StartsWith("cmd_angle_"))
            {
                SetAngleMode(sign);
                return;
            }

            if (_isShowingResult) BeginInputAfterResult(sign);

            // an empty formula is shown as a 0, so a key that reads an operand has to find one standing
            // there; without this the 0 on screen has nothing behind it and x squared opens on an empty
            // box instead, which reads as the 0 having been deleted
            // only at the root, since an empty slot shows a box rather than a 0 and promises nothing
            if (ContinuesFromResult(sign) && _inputManager.RootTokens.Count == 0)
            {
                _inputManager.AddNumber("0");
            }

            if (sign.StartsWith("cmd_"))
            {
                switch (sign)
                {
                    case "cmd_nav_left":
                        _inputManager.Move(NavDirection.Left);
                        break;
                    case "cmd_nav_right":
                        _inputManager.Move(NavDirection.Right);
                        break;
                    case "cmd_nav_up":
                        _inputManager.Move(NavDirection.Up);
                        break;
                    case "cmd_nav_down":
                        _inputManager.Move(NavDirection.Down);
                        break;

                    case "cmd_sqrt":
                        _inputManager.StartRoot(customIndex: false);
                        break;

                    case "cmd_root_n":
                        _inputManager.StartRoot(customIndex: true);
                        break;

                    case "cmd_pow_n":
                        _inputManager.StartPower();
                        break;

                    // x squared is the generic power with the exponent prefilled; the Right afterwards
                    // steps back out so typing continues after the power instead of inside it
                    case "cmd_pow_2":
                        _inputManager.StartPower();
                        _inputManager.AddNumber("2");
                        _inputManager.Move(NavDirection.Right);
                        break;

                    case "cmd_ans":
                        _inputManager.AddAns();
                        break;

                    case "cmd_exp":
                        _inputManager.StartScientific();
                        break;

                    case "cmd_fact":
                        _inputManager.AddPostfix("!");
                        break;

                    case "cmd_inv":
                        _inputManager.AddPostfix("inv");
                        break;

                    case "cmd_percent":
                        _inputManager.AddPostfix("%");
                        break;

                    case "cmd_sin":
                        _inputManager.StartFunction("sin");
                        break;

                    case "cmd_cos":
                        _inputManager.StartFunction("cos");
                        break;

                    case "cmd_tan":
                        _inputManager.StartFunction("tan");
                        break;

                    case "cmd_asin":
                        _inputManager.StartFunction("arcsin");
                        break;

                    case "cmd_acos":
                        _inputManager.StartFunction("arccos");
                        break;

                    case "cmd_atan":
                        _inputManager.StartFunction("arctan");
                        break;

                    case "cmd_sinh":
                        _inputManager.StartFunction("sinh");
                        break;

                    case "cmd_cosh":
                        _inputManager.StartFunction("cosh");
                        break;

                    case "cmd_tanh":
                        _inputManager.StartFunction("tanh");
                        break;

                    case "cmd_asinh":
                        _inputManager.StartFunction("arsinh");
                        break;

                    case "cmd_acosh":
                        _inputManager.StartFunction("arcosh");
                        break;

                    case "cmd_atanh":
                        _inputManager.StartFunction("artanh");
                        break;

                    case "cmd_abs":
                        _inputManager.StartFunction("abs");
                        break;

                    case "cmd_pi":
                        _inputManager.AddConstant("pi");
                        break;

                    case "cmd_e":
                        _inputManager.AddConstant("e");
                        break;

                    case "cmd_paren_open":
                        _inputManager.AddBracket(open: true);
                        break;

                    case "cmd_paren_close":
                        _inputManager.AddBracket(open: false);
                        break;

                    case "cmd_frac":
                        _inputManager.StartFraction();
                        break;

                    case "cmd_pow_e":
                        _inputManager.StartPowerOfE();
                        break;

                    // the log key is the common logarithm, the way it is on an FX-991; a chosen base is
                    // its own key, so an untouched log never opens an empty box for one
                    case "cmd_log":
                        _inputManager.StartLogarithm(customBase: false);
                        break;

                    case "cmd_log_b":
                        _inputManager.StartLogarithm(customBase: true);
                        break;

                    case "cmd_ln":
                        _inputManager.StartFunction("ln");
                        break;
                }
            }
            else if (IsOperator(sign))
            {
                _inputManager.AddOperator(sign);
            }
            else
            {
                _inputManager.AddNumber(sign);
            }

            // the engine has no change notification of its own, so the display is republished after
            // every single keypress
            PublishInput();
        }

        // set from the page out of MathDisplayStyle, since the engine has no idea a display exists
        public bool UseDisplayFractions { get; set; }

        // the input line is the only one that can be clicked, so it is the only one that asks for the
        // addresses that make a click resolvable back into a cursor position
        private void PublishInput()
        {
            InputAndResultText = _inputManager.GetLatexString(withCursor: true, withAddresses: true,
                displayFractions: UseDisplayFractions);
        }

        // a click in the display rather than a keypress; the address is written by the renderer and
        // checked by the input manager, so an unusable one simply changes nothing
        public void PlaceCursor(string address)
        {
            if (!_inputManager.SetCursorPosition(address)) return;

            // clicking into the formula is editing it, the same way an arrow key after = is
            _isShowingResult = false;
            PublishInput();
        }

        // after = the display holds a result rather than the formula that produced it, so the next key
        // has to say what happens to it: an operator carries it into the next calculation, an arrow key
        // goes back to editing the old formula, anything else starts over
        private void BeginInputAfterResult(string sign)
        {
            _isShowingResult = false;

            if (sign.StartsWith("cmd_nav_")) return; // the tree still holds the formula that was evaluated

            if (IsOperator(sign) || ContinuesFromResult(sign))
            {
                SeedWithShownResult();
                return;
            }

            _inputManager.Clear();
        }

        // the next calculation continues from exactly what the display is showing, digits or fraction,
        // rather than from an Ans token; watching the number stay put is what makes it read as the same
        // calculation carrying on instead of a new one
        private void SeedWithShownResult()
        {
            double value = _evaluator.LastAnswer;
            bool hasFraction = ResultFormatter.TryToFraction(value, out long numerator, out long denominator);

            if (_answerForm != AnswerForm.Decimal && hasFraction && denominator > 1)
            {
                _inputManager.SeedWithFraction(numerator, denominator);
                return;
            }

            _inputManager.SeedWithValue(ResultFormatter.ToPlainString(value));
        }

        // the keys that read an operand to their left instead of opening a new one; pressing one of
        // them on a shown result continues from it, the way 5 = followed by x squared carries on with
        // the 5 on a Casio rather than starting over
        //
        // a key missing from this list is not merely inconvenient: the result is cleared first, the key
        // then finds nothing to work on and refuses, and the display is left showing a bare 0 with the
        // formula gone
        // CommandVocabularyTests presses every key in the vocabulary on a result to catch exactly that
        private static bool ContinuesFromResult(string sign)
        {
            return sign == "cmd_fact"
                || sign == "cmd_inv"
                || sign == "cmd_percent"
                || sign == "cmd_pow_2"
                || sign == "cmd_pow_n"
                || sign == "cmd_frac"
                || sign == "cmd_exp";
        }

        private static bool IsOperator(string sign)
        {
            return sign == "+" || sign == "-" || sign == "*" || sign == "/";
        }

        // the second keyboard layer is two buttons stacked in the same cell, so switching layers is
        // purely a matter of which of the two is visible
        private void ToggleShift()
        {
            _isShiftLayer = !_isShiftLayer;

            OnPropertyChanged(nameof(IsNormalLayer));
            OnPropertyChanged(nameof(IsShiftLayer));
        }

        // = deliberately leaves the tree alone; only the display switches over to the result, so a
        // Math ERROR can be corrected instead of retyped from scratch
        private void CalculateResult()
        {
            CalculationText = _inputManager.GetLatexString(withCursor: false,
                displayFractions: UseDisplayFractions) + "=";

            EvaluationResult result = _evaluator.Evaluate(_inputManager.RootTokens);
            if (result.IsSuccess)
            {
                _evaluator.LastAnswer = result.Value;
                _answerForm = AnswerForm.Decimal;
                PublishResult(result.Value);
                _isShowingResult = true;
            }
            else
            {
                InputAndResultText = ResultFormatter.ErrorToLatex(result.Error);
            }
        }

        private void PublishResult(double value)
        {
            InputAndResultText = ResultFormatter.ToLatex(value, _answerForm, UseDisplayFractions);
        }

        // cycles the shown result between a decimal, an improper fraction and a mixed number, skipping
        // whichever of the three this value does not have
        //
        // the conversion is numeric, so a result that came out of a root or a pi has no fraction at all
        // and the key does nothing there, the same as pressing it while a formula is being typed
        private void ToggleAnswerForm()
        {
            if (!_isShowingResult) return;

            double value = _evaluator.LastAnswer;
            AnswerForm next = _answerForm;

            for (int step = 0; step < 3; step++)
            {
                next = NextAnswerForm(next);

                if (next == AnswerForm.Decimal) break;
                if (next == AnswerForm.Improper && ResultFormatter.HasFractionForm(value)) break;
                if (next == AnswerForm.Mixed && ResultFormatter.HasMixedForm(value)) break;
            }

            if (next == _answerForm) return;

            _answerForm = next;
            PublishResult(value);
        }

        private static AnswerForm NextAnswerForm(AnswerForm form)
        {
            if (form == AnswerForm.Decimal) return AnswerForm.Improper;
            if (form == AnswerForm.Improper) return AnswerForm.Mixed;

            return AnswerForm.Decimal;
        }

        private void ClearAll()
        {
            _isShowingResult = false;
            _inputManager.Clear();
            PublishInput();
            CalculationText = "";
        }

        private void Backspace()
        {
            // backspacing out of a result means going back to editing the formula behind it
            _isShowingResult = false;

            _inputManager.Backspace();
            PublishInput();
        }


        // === property changed ===

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
