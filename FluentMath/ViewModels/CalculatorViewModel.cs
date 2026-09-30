using FluentMath.Engines;
using FluentMath.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FluentMath.ViewModels
{
    // the calculator ViewModel:
    // the only translator between the keypad and the input engine; every key sends its CommandParameter
    // through InputCommand, so a new key is a XAML change plus one arm in the switch below
    public class CalculatorViewModel : INotifyPropertyChanged
    {
        // === fields ===

        private readonly MathInputManager _inputManager = new MathInputManager();
        private readonly MathEvaluator _evaluator = new MathEvaluator();

        // shared with the settings page, so handed in and read when it matters, never copied
        private readonly CalculatorSettings _settings;

        // the display shows a result; the next key drops it or carries it on
        private bool _isShowingResult;

        // the shape of the shown result; S⇔D cycles it, every = starts at the settings form
        private AnswerForm _answerForm;

        // a fraction shown mixed rather than improper; SHIFT S⇔D swaps it, every = starts at the settings
        private bool _mixedFraction;

        // the result on screen, a pair included; LastAnswer on the evaluator only keeps its first value
        private EvaluationResult _result;

        // the power of ten the ENG view writes the result over
        private int _engineeringExponent;


        // === display properties ===

        // both hold LaTeX; (only the tests read it, the display draws the tokens)
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

        // the same snapshot as CalculationText, as tokens
        // (a copy; the manager clears its root list in place when the next calculation starts)
        private IReadOnlyList<MathToken> _calculationTokens = new List<MathToken>();
        public IReadOnlyList<MathToken> CalculationTokens
        {
            get => _calculationTokens;
            set
            {
                _calculationTokens = value;
                OnPropertyChanged();
            }
        }

        // what the input line draws, and where the caret stands in it
        // while typing, the live tree and cursor (the caret is matched by list identity, which tells two
        // empty slots apart); a result drops the caret, an error replaces both with a line of text
        private IReadOnlyList<MathToken> _inputTokens = new List<MathToken>();
        public IReadOnlyList<MathToken> InputTokens => _inputTokens;

        public IReadOnlyList<MathToken> CaretTokens { get; private set; }
        public int CaretIndex { get; private set; }
        public string InputErrorText { get; private set; }

        private void PublishInputDisplay(IReadOnlyList<MathToken> tokens,
            IReadOnlyList<MathToken> caretTokens, int caretIndex, string errorText)
        {
            _inputTokens = tokens;
            CaretTokens = caretTokens;
            CaretIndex = caretIndex;
            InputErrorText = errorText;

            // always raised; while typing the list is the same object and only its contents move
            OnPropertyChanged(nameof(InputTokens));
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

        // the settings own the mode; this lets the keypad set it and the selector follow it
        public AngleMode CurrentAngleMode
        {
            get => _settings.AngleMode;
            set
            {
                if (_settings.AngleMode == value) return;

                _settings.AngleMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AngleModeLabel));
            }
        }

        // the three letters a Casio prints for the unit, on the selector in the caret bar
        public string AngleModeLabel
        {
            get
            {
                if (CurrentAngleMode == AngleMode.Radians) return "RAD";
                if (CurrentAngleMode == AngleMode.Gradians) return "GRA";

                return "DEG";
            }
        }

        // the selector sends cycle; the three direct keys stay for the tests that press them
        private void SetAngleMode(string sign)
        {
            if (sign == "cmd_angle_cycle") { CurrentAngleMode = NextAngleMode(CurrentAngleMode); }
            else if (sign == "cmd_angle_rad") { CurrentAngleMode = AngleMode.Radians; }
            else if (sign == "cmd_angle_gra") { CurrentAngleMode = AngleMode.Gradians; }
            else { CurrentAngleMode = AngleMode.Degrees; }
        }

        // degrees, radians, gradians and round again, the Casio setup order
        private static AngleMode NextAngleMode(AngleMode current)
        {
            if (current == AngleMode.Degrees) return AngleMode.Radians;
            if (current == AngleMode.Radians) return AngleMode.Gradians;

            return AngleMode.Degrees;
        }


        // === shift layer ===

        // each shiftable key is two buttons in one cell; these flags swap which is visible
        // (plain bools, x:Bind converts them, so the ViewModel stays free of WinUI and testable)
        private bool _isShiftLayer;

        public bool IsNormalLayer => !_isShiftLayer;

        public bool IsShiftLayer => _isShiftLayer;


        // === trig panel layers ===

        // the two latches pick which of the four grids of the trig panel is up, like shift picks a layer
        //
        // --- latches ---
        private bool _isTrigInverseLatched;      // sin becomes sin⁻¹
        private bool _isTrigHyperbolicLatched;   // sin becomes sinh

        public bool IsTrigInverseLatched => _isTrigInverseLatched;

        public bool IsTrigInverseUnlatched => !_isTrigInverseLatched;

        public bool IsTrigHyperbolicLatched => _isTrigHyperbolicLatched;

        public bool IsTrigHyperbolicUnlatched => !_isTrigHyperbolicLatched;

        // --- which grid is up ---
        // one property per grid; a function binding does not reliably re-evaluate on its second argument
        public bool ShowTrigPlain => !_isTrigInverseLatched && !_isTrigHyperbolicLatched;

        public bool ShowTrigInverse => _isTrigInverseLatched && !_isTrigHyperbolicLatched;

        public bool ShowTrigHyperbolic => !_isTrigInverseLatched && _isTrigHyperbolicLatched;

        public bool ShowTrigInverseHyperbolic => _isTrigInverseLatched && _isTrigHyperbolicLatched;

        // called when the panel closes, so the next one opens on the plain layer
        public void ResetTrigLatches()
        {
            if (!_isTrigInverseLatched && !_isTrigHyperbolicLatched) return;

            _isTrigInverseLatched = false;
            _isTrigHyperbolicLatched = false;

            PublishTrigLayers();
        }

        private void ToggleTrigLatch(string sign)
        {
            if (sign == "cmd_trig_inv") { _isTrigInverseLatched = !_isTrigInverseLatched; }
            else { _isTrigHyperbolicLatched = !_isTrigHyperbolicLatched; }

            PublishTrigLayers();
        }

        private void PublishTrigLayers()
        {
            OnPropertyChanged(nameof(IsTrigInverseLatched));
            OnPropertyChanged(nameof(IsTrigInverseUnlatched));
            OnPropertyChanged(nameof(IsTrigHyperbolicLatched));
            OnPropertyChanged(nameof(IsTrigHyperbolicUnlatched));

            OnPropertyChanged(nameof(ShowTrigPlain));
            OnPropertyChanged(nameof(ShowTrigInverse));
            OnPropertyChanged(nameof(ShowTrigHyperbolic));
            OnPropertyChanged(nameof(ShowTrigInverseHyperbolic));
        }


        // === commands ===

        public ICommand InputCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand BackspaceCommand { get; }
        public ICommand ToggleAnswerFormCommand { get; }


        // === constructor ===

        // a setup of its own at the defaults, for a caller without a shared one
        public CalculatorViewModel() : this(new CalculatorSettings()) { }

        public CalculatorViewModel(CalculatorSettings settings)
        {
            _settings = settings;

            InputCommand = new RelayCommand<string>(AddToTextBox);
            CalculateCommand = new RelayCommand<object>(_ => CalculateResult());
            ClearCommand = new RelayCommand<object>(_ => ClearAll());
            BackspaceCommand = new RelayCommand<object>(_ => Backspace());
            ToggleAnswerFormCommand = new RelayCommand<object>(_ => ToggleAnswerForm());

            // the starting display from the engine, so the cursor stands right before the first key
            PublishInput();
        }


        // === input handling ===

        // three kinds of parameter: a "cmd_" keyword for anything structural, a bare operator, and a digit
        // or decimal point; (a panel key sends the same as a pad key)
        private void AddToTextBox(string sign)
        {
            // shift only swaps the layer and never touches the input or a shown result
            if (sign == "cmd_shift")
            {
                ToggleShift();
                return;
            }

            // the same for the two trig latches
            if (sign == "cmd_trig_inv" || sign == "cmd_trig_hyp")
            {
                ToggleTrigLatch(sign);
                return;
            }

            // and for the angle unit, a mode rather than an input
            if (sign.StartsWith("cmd_angle_"))
            {
                SetAngleMode(sign);
                return;
            }

            // --- revisit: keys drawn before they compute ---
            // the history and memory keys wait on the history list and the variable store
            // they return here, since the fall-through would clear a shown result to a bare 0
            // trigger: the branch that implements one takes it out of this set and Vocabulary.NotImplemented
            if (NotImplementedKeys.Contains(sign)) return;

            // a view key changes how the result is shown and leaves the formula alone, like the mode keys
            if (sign == "cmd_prime")
            {
                ShowPrimeFactors();
                return;
            }

            if (sign == "cmd_eng" || sign == "cmd_eng_back")
            {
                ShowEngineering(towardsSmallerPowers: sign == "cmd_eng");
                return;
            }

            if (sign == "cmd_frac_swap")
            {
                SwapFractionForm();
                return;
            }

            if (sign == "cmd_degrees")
            {
                ShowDecimalDegrees();
                return;
            }

            // the °′″ key is a view key on a result only; during input it types a marker
            if (sign == "cmd_dms" && _isShowingResult)
            {
                ToggleSexagesimal();
                return;
            }

            if (_isShowingResult) BeginInputAfterResult(sign);

            // an empty formula shows a 0, so a key that reads an operand finds it standing there (only at
            // the root; an empty slot shows a box); not the mixed fraction, whose empty template is the point
            // a °′″ marker stands behind the 0 the same way, which is how 0°39′ is typed
            if (((ContinuesFromResult(sign) && sign != "cmd_frac_mixed") || sign == "cmd_dms")
                && _inputManager.RootTokens.Count == 0)
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

                    // the power with 2 prefilled; Right steps back out, so typing continues after it
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

                    case "cmd_dms":
                        _inputManager.AddSexagesimalMarker();
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

                    case "cmd_sec":
                        _inputManager.StartFunction("sec");
                        break;

                    case "cmd_csc":
                        _inputManager.StartFunction("csc");
                        break;

                    case "cmd_cot":
                        _inputManager.StartFunction("cot");
                        break;

                    case "cmd_asec":
                        _inputManager.StartFunction("arcsec");
                        break;

                    case "cmd_acsc":
                        _inputManager.StartFunction("arccsc");
                        break;

                    case "cmd_acot":
                        _inputManager.StartFunction("arccot");
                        break;

                    case "cmd_sech":
                        _inputManager.StartFunction("sech");
                        break;

                    case "cmd_csch":
                        _inputManager.StartFunction("csch");
                        break;

                    case "cmd_coth":
                        _inputManager.StartFunction("coth");
                        break;

                    case "cmd_asech":
                        _inputManager.StartFunction("arsech");
                        break;

                    case "cmd_acsch":
                        _inputManager.StartFunction("arcsch");
                        break;

                    case "cmd_acoth":
                        _inputManager.StartFunction("arcoth");
                        break;

                    case "cmd_abs":
                        _inputManager.StartFunction("abs");
                        break;

                    case "cmd_floor":
                        _inputManager.StartFunction("floor");
                        break;

                    case "cmd_ceil":
                        _inputManager.StartFunction("ceil");
                        break;

                    case "cmd_int":
                        _inputManager.StartFunction("int");
                        break;

                    case "cmd_intg":
                        _inputManager.StartFunction("intg");
                        break;

                    // the functions of two arguments open in the first; Right walks on into the second
                    case "cmd_gcd":
                        _inputManager.StartFunction("gcd");
                        break;

                    case "cmd_lcm":
                        _inputManager.StartFunction("lcm");
                        break;

                    case "cmd_ranint":
                        _inputManager.StartFunction("ranint");
                        break;

                    case "cmd_rndfix":
                        _inputManager.StartFunction("rndfix");
                        break;

                    case "cmd_rnd":
                        _inputManager.StartFunction("rnd");
                        break;

                    case "cmd_rand":
                        _inputManager.AddRandom();
                        break;

                    // nPr and nCr stand between n and r as an operator, written with the letter a Casio uses
                    case "cmd_npr":
                        _inputManager.AddOperator("P");
                        break;

                    case "cmd_div_r":
                        _inputManager.AddOperator("÷R");
                        break;

                    case "cmd_pol":
                        _inputManager.StartFunction("pol");
                        break;

                    case "cmd_rec":
                        _inputManager.StartFunction("rec");
                        break;

                    case "cmd_ncr":
                        _inputManager.AddOperator("C");
                        break;

                    // the calculus structures open in their first slot, and x is the variable they run over
                    case "cmd_integral":
                        _inputManager.StartLargeOperator(LargeOperatorKind.Integral);
                        break;

                    case "cmd_derivative":
                        _inputManager.StartDerivative();
                        break;

                    case "cmd_sum":
                        _inputManager.StartLargeOperator(LargeOperatorKind.Sum);
                        break;

                    case "cmd_product":
                        _inputManager.StartLargeOperator(LargeOperatorKind.Product);
                        break;

                    case "cmd_x":
                        _inputManager.AddVariable();
                        break;

                    // the eleven decimal prefixes, each a postfix under the name its key carries
                    default:
                        if (sign.StartsWith(PrefixCommand))
                        {
                            _inputManager.AddPostfix(sign.Substring(PrefixCommand.Length));
                        }
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

                    case "cmd_frac_mixed":
                        _inputManager.StartMixedFraction();
                        break;

                    case "cmd_pow_e":
                        _inputManager.StartPowerOfE();
                        break;

                    // log is the common logarithm, as on an FX-991; a chosen base is its own key
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

            // the engine has no change notification, so the display is republished after every key
            PublishInput();
        }

        // set by the display from its layout style; the engine knows no display
        public bool UseDisplayFractions { get; set; }

        // the decimal key label; the key always types a dot, drawn as the mark the settings ask for
        public string DecimalMarkLabel => _settings.DecimalMarkText;

        // a cached page calls this on its way back, since the settings page writes the settings directly
        public void RefreshSettingLabels()
        {
            OnPropertyChanged(nameof(CurrentAngleMode));
            OnPropertyChanged(nameof(AngleModeLabel));
            OnPropertyChanged(nameof(DecimalMarkLabel));
        }

        // the input line is the only clickable one, so only it asks for addresses
        private void PublishInput()
        {
            InputAndResultText = _inputManager.GetLatexString(withCursor: true, withAddresses: true,
                displayFractions: UseDisplayFractions);

            // an empty formula shows a 0 with the caret behind it, so the display is never blank
            if (_inputManager.RootTokens.Count == 0)
            {
                List<MathToken> zero = new List<MathToken> { new MathToken(TokenType.Number, "0") };
                PublishInputDisplay(zero, zero, 1, null);
                return;
            }

            PublishInputDisplay(_inputManager.RootTokens,
                _inputManager.ActiveTokens, _inputManager.ActiveCursorIndex, null);
        }

        // whether the display is worth aiming at; the drawn 0 of an empty formula holds one position only
        public bool CanPlaceCursor => _inputManager.RootTokens.Count > 0;

        // a click in the display; an address the input manager cannot use changes nothing
        public void PlaceCursor(string address)
        {
            if (!CanPlaceCursor) return;

            // after = the address was worked out against the result, so the result is seeded first
            // (a seed shaped unlike the drawing keeps the cursor where the seed left it)
            bool seeded = false;
            if (_isShowingResult)
            {
                SeedWithShownResult();
                seeded = true;
            }

            if (!_inputManager.SetCursorPosition(address) && !seeded) return;

            // clicking into the formula is editing it, the same way an arrow key after = is
            _isShowingResult = false;
            PublishInput();
        }

        // the key after =: an operator carries the result on, an arrow key edits the old formula,
        // anything else starts over
        private void BeginInputAfterResult(string sign)
        {
            _isShowingResult = false;

            if (sign.StartsWith("cmd_nav_")) return; // the tree still holds the formula that was evaluated

            if (IsOperator(sign) || ContinuesFromResult(sign))
            {
                SeedWithShownResult();
                if (TakesTheOperandBefore(sign)) _inputManager.EncloseIfCompound();
                return;
            }

            _inputManager.Clear();
        }

        // the keys that take the operand on their left as a whole, so a compound result goes in brackets
        // (not EXP, a times sign)
        private static bool TakesTheOperandBefore(string sign)
        {
            return (ContinuesFromResult(sign) && sign != "cmd_exp") || sign == "cmd_npr" || sign == "cmd_ncr";
        }

        // the next calculation continues from exactly what the display shows, not from an Ans token
        //
        // a pair as its first value; a decimal as the written digits with the full value behind them; an
        // exact form as real roots and π; a recurring decimal as its fraction; an angle as real markers
        private void SeedWithShownResult()
        {
            MathValue value = _result.FirstValue;

            if (_answerForm == AnswerForm.PrimeFactors && ResultFormatter.TryPrimeFactors(value.Value, out var factors))
            {
                _inputManager.SeedWithTokens(ResultFormatter.PrimeFactorTokens(factors));
                return;
            }

            List<MathToken>? angle = _answerForm == AnswerForm.Sexagesimal
                ? ResultFormatter.SexagesimalTokens(value.Value, asInput: true)
                : null;

            if (angle != null)
            {
                _inputManager.SeedWithTokens(angle, value);
                return;
            }

            AnswerForm form = _answerForm == AnswerForm.Recurring ? ExactForm() : _answerForm;

            if (form == AnswerForm.Improper || form == AnswerForm.Mixed)
            {
                if (ResultFormatter.TryFraction(value, out long numerator, out long denominator))
                {
                    if (form == AnswerForm.Mixed && Math.Abs(numerator) > denominator)
                    {
                        _inputManager.SeedWithMixedFraction(numerator / denominator,
                            Math.Abs(numerator % denominator), denominator);
                        return;
                    }

                    _inputManager.SeedWithFraction(numerator, denominator);
                    return;
                }

                List<MathToken>? exact = ResultFormatter.ExactFormTokens(value, asInput: true);
                if (exact != null)
                {
                    _inputManager.SeedWithTokens(exact);
                    return;
                }
            }

            WrittenDecimal written = _answerForm == AnswerForm.Engineering
                ? ResultFormatter.Engineering(value.Value, _engineeringExponent, _settings.NumberFormat, _settings.UsePrefixes)
                : ResultFormatter.Write(value.Value, _settings.NumberFormat);

            if (written.Exponent is not int exponent)
            {
                _inputManager.SeedWithValue(written.Digits, value);
                return;
            }

            // the digits stand for the value over the power, so that is what they carry
            MathValue power = new MathValue(Math.Pow(10, exponent),
                ExactValue.Power(ExactValue.FromInteger(10), ExactValue.FromInteger(exponent)));
            MathValue mantissa = value / power;

            if (written.Prefix != null) _inputManager.SeedWithPrefix(written.Digits, mantissa, written.Prefix);
            else _inputManager.SeedWithScientific(written.Digits, mantissa, exponent);
        }

        // the keys that read an operand to their left; on a shown result they carry it on, as 5 = x²
        // does on a Casio
        // (a key missing here clears the result to a bare 0; CommandVocabularyTests presses every key on one)
        private static bool ContinuesFromResult(string sign)
        {
            return sign == "cmd_fact"
                || sign == "cmd_inv"
                || sign == "cmd_percent"
                || sign == "cmd_pow_2"
                || sign == "cmd_pow_n"
                || sign == "cmd_frac"
                || sign == "cmd_frac_mixed"
                || sign == "cmd_exp"
                || sign.StartsWith(PrefixCommand);
        }

        // what every decimal prefix key sends, followed by the name of its prefix
        private const string PrefixCommand = "cmd_prefix_";

        // nPr, nCr and the division with remainder stand between two operands like the arithmetic signs
        private static bool IsOperator(string sign)
        {
            return sign == "+" || sign == "-" || sign == "*" || sign == "/"
                || sign == "cmd_npr" || sign == "cmd_ncr" || sign == "cmd_div_r";
        }

        // the keys drawn but computing nothing, see the revisit tag in AddToTextBox
        private static readonly HashSet<string> NotImplementedKeys = new HashSet<string>
        {
            "cmd_history", "cmd_memory"
        };

        private void ToggleShift()
        {
            _isShiftLayer = !_isShiftLayer;

            OnPropertyChanged(nameof(IsNormalLayer));
            OnPropertyChanged(nameof(IsShiftLayer));
        }

        // = leaves the tree alone, so a Math ERROR can be corrected rather than retyped
        // (returns whether there is a result, for a view key pressed during input)
        private bool CalculateResult()
        {
            // the history line shows the formula as read, with brackets around a product that binds
            // tighter than the division before it
            List<MathToken> asRead = MathEvaluator.CloneWithImpliedBrackets(_inputManager.RootTokens);

            string readLatex = asRead.Count == 0
                ? "0"
                : LatexHelper.GetListLatex(asRead, new LatexRenderContext(null, false, UseDisplayFractions));

            CalculationText = readLatex + "=";
            CalculationTokens = asRead;

            _evaluator.AngleMode = _settings.AngleMode;
            _evaluator.NumberFormat = _settings.NumberFormat;

            EvaluationResult result = _evaluator.Evaluate(_inputManager.RootTokens);
            if (!result.IsSuccess)
            {
                ShowError(result.Error);
                return false;
            }

            _evaluator.LastAnswer = result.FirstValue;
            _result = result;
            _mixedFraction = _settings.MixedFirst;
            _answerForm = FirstAnswerForm();
            PublishResult();
            _isShowingResult = true;

            return true;
        }

        private void PublishResult()
        {
            NumberFormat format = _settings.NumberFormat;
            List<MathToken> tokens;

            if (_answerForm == AnswerForm.Engineering)
            {
                WrittenDecimal written = ResultFormatter.Engineering(_result.Value, _engineeringExponent,
                    format, _settings.UsePrefixes);

                InputAndResultText = ResultFormatter.ToLatex(written);
                tokens = ResultFormatter.ToTokens(written);
            }
            else
            {
                InputAndResultText = ResultFormatter.ToLatex(_result, _answerForm, UseDisplayFractions, format);
                tokens = ResultFormatter.ToTokens(_result, _answerForm, UseDisplayFractions, format);
            }

            PublishInputDisplay(tokens, null, 0, null);
        }

        // the tree stays as it was, so the arrow keys go back into the formula that failed
        private void ShowError(EvaluationError error)
        {
            _isShowingResult = false;

            InputAndResultText = ResultFormatter.ErrorToLatex(error);
            PublishInputDisplay(new List<MathToken>(), null, 0, ResultFormatter.ErrorToText(error));
        }

        // FACT, the result as a product of prime powers; pressed again, back to the decimal
        // (during input it evaluates first; no prime factors is a Math ERROR, a pair uses its first value)
        private void ShowPrimeFactors()
        {
            if (!_isShowingResult && !CalculateResult()) return;

            if (_answerForm == AnswerForm.PrimeFactors)
            {
                _answerForm = AnswerForm.Decimal;
                PublishResult();
                return;
            }

            if (!ResultFormatter.TryPrimeFactors(_result.Value, out _))
            {
                ShowError(EvaluationError.Domain);
                return;
            }

            _result = EvaluationResult.Success(_result.FirstValue);
            _answerForm = AnswerForm.PrimeFactors;
            PublishResult();
        }

        // ENG and its shift, the result over a power of ten that is a multiple of three
        //
        // the first ENG leaves one to three digits before the point, the first shift one power further up
        // (0.123×10³ for 123); every press after steps three powers, until the mantissa needs a power itself
        // (during input it evaluates first; a pair uses its first value)
        private void ShowEngineering(bool towardsSmallerPowers)
        {
            if (!_isShowingResult && !CalculateResult()) return;

            double value = _result.Value;

            if (_answerForm != AnswerForm.Engineering)
            {
                int standard = ResultFormatter.EngineeringExponent(value);
                int first = towardsSmallerPowers ? standard : standard + 3;

                _engineeringExponent = ResultFormatter.CanWriteEngineering(value, first) ? first : standard;
                _result = EvaluationResult.Success(_result.FirstValue);
                _answerForm = AnswerForm.Engineering;
                PublishResult();
                return;
            }

            int next = _engineeringExponent + (towardsSmallerPowers ? -3 : 3);
            if (!ResultFormatter.CanWriteEngineering(value, next)) return;

            _engineeringExponent = next;
            PublishResult();
        }

        // the °′″ key on a result, between degrees, minutes and seconds and the decimal
        // (a pair uses its first value; a value past the largest angle the form writes stays as it is)
        private void ToggleSexagesimal()
        {
            if (_answerForm == AnswerForm.Sexagesimal)
            {
                _answerForm = AnswerForm.Decimal;
                PublishResult();
                return;
            }

            if (!ResultFormatter.HasSexagesimalForm(_result.Value)) return;

            _result = EvaluationResult.Success(_result.FirstValue);
            _answerForm = AnswerForm.Sexagesimal;
            PublishResult();
        }

        // deg, the result back in decimal degrees from any view; during input it evaluates first
        private void ShowDecimalDegrees()
        {
            if (!_isShowingResult && !CalculateResult()) return;

            _answerForm = AnswerForm.Decimal;
            PublishResult();
        }

        // S⇔D: cycles the result through its exact form, recurring decimal and decimal (7/3, 2.3 with a
        // bar, 2.333333333), skipping a form it lacks; a pair switches both values together
        // (a result of a logarithm or e has only the fraction the numeric search finds; during input it
        // evaluates first)
        private void ToggleAnswerForm()
        {
            if (!_isShowingResult && !CalculateResult()) return;

            AnswerForm[] cycle = { ExactForm(), AnswerForm.Recurring, AnswerForm.Decimal };
            int at = _answerForm == AnswerForm.Improper || _answerForm == AnswerForm.Mixed
                ? 0
                : Array.IndexOf(cycle, _answerForm);

            // the other views go to the decimal, where the cycle starts over
            if (at < 0)
            {
                _answerForm = AnswerForm.Decimal;
                PublishResult();
                return;
            }

            for (int step = 1; step < cycle.Length; step++)
            {
                AnswerForm next = cycle[(at + step) % cycle.Length];
                if (!HasForm(next)) continue;

                _answerForm = next;
                PublishResult();
                return;
            }
        }

        // SHIFT S⇔D, a b/c ⇔ d/c on a Casio: swaps the fraction between improper and mixed, from any view
        // (no fraction, or a proper one, stays as it is; during input it evaluates first)
        private void SwapFractionForm()
        {
            if (!_isShowingResult && !CalculateResult()) return;
            if (!HasForm(ResultFormatter.HasFractionForm)) return;

            _mixedFraction = !_mixedFraction;
            _answerForm = ExactForm();
            PublishResult();
        }

        // the exact form in the chosen fraction form; without a mixed form it is improper
        private AnswerForm ExactForm()
        {
            return _mixedFraction && HasForm(AnswerForm.Mixed) ? AnswerForm.Mixed : AnswerForm.Improper;
        }

        // the form a new result opens in: an angle as one; with exact first the exact form if any, else
        // the decimal
        private AnswerForm FirstAnswerForm()
        {
            if (_result.Kind == ResultKind.Single && _result.FirstValue.IsSexagesimal
                && ResultFormatter.HasSexagesimalForm(_result.Value))
            {
                return AnswerForm.Sexagesimal;
            }

            if (!_settings.ExactFirst || !HasForm(AnswerForm.Improper)) return AnswerForm.Decimal;

            return ExactForm();
        }

        private bool HasForm(AnswerForm form)
        {
            if (form == AnswerForm.Improper) return HasForm(ResultFormatter.HasExactForm);
            if (form == AnswerForm.Mixed) return HasForm(ResultFormatter.HasMixedForm);
            if (form == AnswerForm.Recurring) return _settings.RecurringDecimals && HasForm(ResultFormatter.HasRecurringForm);

            return form == AnswerForm.Decimal;
        }

        private bool HasForm(Func<MathValue, bool> hasForm)
        {
            if (hasForm(_result.FirstValue)) return true;

            return _result.Kind != ResultKind.Single && hasForm(_result.SecondValue);
        }

        private void ClearAll()
        {
            _isShowingResult = false;
            _inputManager.Clear();
            PublishInput();
            CalculationText = "";
            CalculationTokens = new List<MathToken>();
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
