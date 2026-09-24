using FluentMath.Engines;
using FluentMath.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FluentMath.ViewModels
{
    // the only translator between the keypad and the input engine
    //
    // every button on the standard and the scientific page is bound to the same InputCommand and
    // identifies itself through its CommandParameter, so adding a key is a XAML change plus one arm in
    // the switch below; the pages themselves stay free of input logic, and each has a ViewModel of its own
    public class CalculatorViewModel : INotifyPropertyChanged
    {
        // === fields ===

        private readonly MathInputManager _inputManager = new MathInputManager();
        private readonly MathEvaluator _evaluator = new MathEvaluator();

        // the calculator setup; shared with the settings page, which is why it is handed in rather than
        // owned, and read at the moment it matters rather than copied
        private readonly CalculatorSettings _settings;

        // true while the display shows a result instead of the formula being typed; the next keypress
        // decides whether that result is dropped or carried into the next calculation
        private bool _isShowingResult;

        // which shape the shown result is in; the S to D key cycles it, every = starts over at the form the
        // settings open a result in
        private AnswerForm _answerForm;

        // whether a fraction is shown mixed rather than improper; every = starts over at the settings, and
        // the shift of S to D swaps it
        private bool _mixedFraction;

        // the result on screen, a pair included; LastAnswer on the evaluator only keeps its first value
        private EvaluationResult _result;

        // the power of ten the ENG view writes the result over
        private int _engineeringExponent;


        // === display properties ===

        // both hold LaTeX, not plain text; ScientificPage feeds them straight to KaTeX
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
        //
        // it has to be a copy: = deliberately leaves the tree alone so a Math ERROR can be corrected, and
        // MathInputManager clears its root list in place, so a reference would come back empty the moment
        // the next calculation starts
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
        //
        // while a formula is being typed these are the live tree and the live cursor rather than a copy:
        // the display is rebuilt on every keystroke anyway, and the caret is matched by list identity,
        // which is the only thing that tells one empty slot from another
        //
        // a result replaces the tokens and drops the caret; a failed evaluation replaces both with a line
        // of text, because an error message is not a formula
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

            // always raised, never guarded on a change: while typing the list is the same object every
            // time and only its contents move
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

        // the settings own the mode, so it survives a trip to another page; this pair exists so the keypad
        // can set it and the indicator above the display can follow it
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

        // the three letters a Casio prints for the unit; the selector in the caret bar carries them, so
        // the label is what the user reads off the selector rather than an indicator next to it
        public string AngleModeLabel
        {
            get
            {
                if (CurrentAngleMode == AngleMode.Radians) return "RAD";
                if (CurrentAngleMode == AngleMode.Gradians) return "GRA";

                return "DEG";
            }
        }

        // cycle is what the single selector button sends, since a button that shows the current unit can
        // only offer the next one; the three direct keys stay handled for the tests that press them, the
        // settings page writes the unit into the settings itself
        private void SetAngleMode(string sign)
        {
            if (sign == "cmd_angle_cycle") { CurrentAngleMode = NextAngleMode(CurrentAngleMode); }
            else if (sign == "cmd_angle_rad") { CurrentAngleMode = AngleMode.Radians; }
            else if (sign == "cmd_angle_gra") { CurrentAngleMode = AngleMode.Gradians; }
            else { CurrentAngleMode = AngleMode.Degrees; }
        }

        // degrees, radians, gradians and round again, the order the units are listed in on a Casio setup
        private static AngleMode NextAngleMode(AngleMode current)
        {
            if (current == AngleMode.Degrees) return AngleMode.Radians;
            if (current == AngleMode.Radians) return AngleMode.Gradians;

            return AngleMode.Degrees;
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


        // === trig panel layers ===

        // the trig panel carries four grids of the same six keys and shows one of them; these two latches
        // pick which, exactly the way the shift key picks a keypad layer, and they are plain bools for
        // the same reason
        //
        // --- latches ---
        private bool _isTrigInverseLatched;      // sin becomes sin to the minus one
        private bool _isTrigHyperbolicLatched;   // sin becomes sinh

        public bool IsTrigInverseLatched => _isTrigInverseLatched;

        public bool IsTrigInverseUnlatched => !_isTrigInverseLatched;

        public bool IsTrigHyperbolicLatched => _isTrigHyperbolicLatched;

        public bool IsTrigHyperbolicUnlatched => !_isTrigHyperbolicLatched;

        // --- which grid is up ---
        // one property per grid rather than one binding that reads both latches, because a function
        // binding does not reliably re-evaluate when the second property it reads is the one that moved
        public bool ShowTrigPlain => !_isTrigInverseLatched && !_isTrigHyperbolicLatched;

        public bool ShowTrigInverse => _isTrigInverseLatched && !_isTrigHyperbolicLatched;

        public bool ShowTrigHyperbolic => !_isTrigInverseLatched && _isTrigHyperbolicLatched;

        public bool ShowTrigInverseHyperbolic => _isTrigInverseLatched && _isTrigHyperbolicLatched;

        // the page calls this when the panel closes, whether a function was pressed or the panel was
        // dismissed; a latch that outlived its panel would open the next one on a layer nobody chose
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

        // a setup of its own with every setting at its default, for a caller that has no shared one
        public CalculatorViewModel() : this(new CalculatorSettings()) { }

        public CalculatorViewModel(CalculatorSettings settings)
        {
            _settings = settings;

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
        // the panels send the same parameters as the keys below them, so nothing about a key being in a
        // panel rather than on the pad reaches this far
        private void AddToTextBox(string sign)
        {
            // shift only swaps the keyboard layer, it must never disturb the input or a shown result
            if (sign == "cmd_shift")
            {
                ToggleShift();
                return;
            }

            // the same holds for the two latches inside the trig panel
            if (sign == "cmd_trig_inv" || sign == "cmd_trig_hyp")
            {
                ToggleTrigLatch(sign);
                return;
            }

            // the angle unit is a mode rather than an input, so like shift it must leave both the
            // formula and a shown result exactly where they are
            if (sign.StartsWith("cmd_angle_"))
            {
                SetAngleMode(sign);
                return;
            }

            // --- revisit: keys drawn before they compute ---
            // the history and memory keys in the header are drawn ahead of the history list and the
            // variable store they need
            // they return here rather than falling out of the switch below, because the fall-through
            // reaches BeginInputAfterResult first, which clears a shown result and leaves the display as
            // a bare 0 with the formula gone
            // the trigger is the branch that implements them; a name leaves this set as it lands, and
            // Vocabulary.NotImplemented in the test project is held against it
            if (NotImplementedKeys.Contains(sign)) return;

            // a view key changes how the result is shown and leaves the formula behind it alone, so it is
            // handled before BeginInputAfterResult the way the mode keys are
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

            // an empty formula is shown as a 0, so a key that reads an operand has to find one standing
            // there; without this the 0 on screen has nothing behind it and x squared opens on an empty
            // box instead, which reads as the 0 having been deleted
            // only at the root, since an empty slot shows a box rather than a 0 and promises nothing
            //
            // the mixed fraction is the exception: its empty template is what the key is there for, and a
            // 0 lifted into the whole part would put the cursor past the slot that is typed first
            // a marker of the °′″ key stands behind the 0 the same way, which is how 0°39′ is typed
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

        // what the decimal point key is labelled with; the key always types a dot, which the display draws
        // as the mark the settings ask for
        public string DecimalMarkLabel => _settings.DecimalMarkText;

        // the settings page writes into the settings directly, while a page kept alive across the visit
        // still shows the labels it read before it; the page calls this on its way back into view
        public void RefreshSettingLabels()
        {
            OnPropertyChanged(nameof(CurrentAngleMode));
            OnPropertyChanged(nameof(AngleModeLabel));
            OnPropertyChanged(nameof(DecimalMarkLabel));
        }

        // the input line is the only one that can be clicked, so it is the only one that asks for the
        // addresses that make a click resolvable back into a cursor position
        private void PublishInput()
        {
            InputAndResultText = _inputManager.GetLatexString(withCursor: true, withAddresses: true,
                displayFractions: UseDisplayFractions);

            // an empty formula shows a zero rather than nothing, so the display is never blank; the caret
            // then stands behind that zero the same way it stands behind a typed digit
            if (_inputManager.RootTokens.Count == 0)
            {
                List<MathToken> zero = new List<MathToken> { new MathToken(TokenType.Number, "0") };
                PublishInputDisplay(zero, zero, 1, null);
                return;
            }

            PublishInputDisplay(_inputManager.RootTokens,
                _inputManager.ActiveTokens, _inputManager.ActiveCursorIndex, null);
        }

        // whether a point in the display is a place the cursor can be aimed at
        //
        // the zero on an empty formula is drawn and not typed: it holds one position rather than two,
        // so a click on either side of it would leave the cursor exactly where it already stands, and a
        // preview of that click would be promising a move that cannot happen
        public bool CanPlaceCursor => _inputManager.RootTokens.Count > 0;

        // a click in the display rather than a keypress; the address is written by the renderer and
        // checked by the input manager, so an unusable one simply changes nothing
        public void PlaceCursor(string address)
        {
            if (!CanPlaceCursor) return;

            // after = the display holds the result and not the formula that produced it, so the address
            // was worked out against the result; seeding it is what puts those very tokens into the
            // manager and makes the address mean the place it looked like it meant
            //
            // a result whose seeded shape is not the one that was drawn, a scientific form against the
            // plain string it is seeded from, keeps the cursor where the seed left it rather than
            // dropping the click on the floor
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
                if (TakesTheOperandBefore(sign)) _inputManager.EncloseIfCompound();
                return;
            }

            _inputManager.Clear();
        }

        // the keys that take the operand on their left as a whole, where a result written as more than one
        // operand has to go in brackets to stay one; EXP is a times sign and does not
        private static bool TakesTheOperandBefore(string sign)
        {
            return (ContinuesFromResult(sign) && sign != "cmd_exp") || sign == "cmd_npr" || sign == "cmd_ncr";
        }

        // the next calculation continues from exactly what the display is showing, digits or fraction,
        // rather than from an Ans token; watching the number stay put is what makes it read as the same
        // calculation carrying on instead of a new one
        //
        // a pair carries on as its first value, the one Ans holds as well; a decimal carries on as the
        // digits the number format wrote, with the full value behind them
        //
        // an exact form carries on as real roots and a real π, so the next calculation is exact again, and
        // a recurring decimal, which cannot be typed, as the fraction it stands for
        // an angle in degrees, minutes and seconds carries on as real markers and stays an angle, with the
        // full value behind the rounded seconds
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
                || sign == "cmd_frac_mixed"
                || sign == "cmd_exp"
                || sign.StartsWith(PrefixCommand);
        }

        // what every decimal prefix key sends, followed by the name of its prefix
        private const string PrefixCommand = "cmd_prefix_";

        // nPr, nCr and the division with remainder stand between two operands the way the arithmetic
        // signs do, so a shown result is carried on as their left one, the way a Casio writes AnsC
        private static bool IsOperator(string sign)
        {
            return sign == "+" || sign == "-" || sign == "*" || sign == "/"
                || sign == "cmd_npr" || sign == "cmd_ncr" || sign == "cmd_div_r";
        }

        // the keys that are drawn but compute nothing, see the revisit tag in AddToTextBox
        //
        // the two header keys, which are waiting on a history list and a variable store rather than on a
        // token
        private static readonly HashSet<string> NotImplementedKeys = new HashSet<string>
        {
            "cmd_history", "cmd_memory"
        };

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
        //
        // says whether there is a result, which a view key pressed during input needs to know
        private bool CalculateResult()
        {
            // the history line shows the formula the way it was read, with a bracket pair around a product
            // that binds tighter than the division in front of it; the tree itself stays as it was typed
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

        // FACT, the result as a product of prime powers; pressed again it goes back to the decimal
        //
        // pressed during input it evaluates first, which saves the = a Casio needs there; a result with no
        // prime factors, a fraction, a negative number or 0, is the Math ERROR the Casio manual names
        // a pair has none either and is factorised by its first value, the one it carries on as
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
        // the first press of ENG writes the power that leaves one to three digits in front of the point,
        // the first press of the shift one power further up, 0.123×10³ for 123; every press after that
        // steps three powers down or up, until the mantissa would need a power of its own
        // pressed during input it evaluates first; a pair turns into its first value, as it does for FACT
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

        // the °′″ key on a result, which switches between degrees, minutes and seconds and the decimal the
        // way it does on a Casio; during input it types a marker instead, see AddSexagesimalMarker
        //
        // a pair turns into its first value, as it does for FACT and ENG, and a value past the largest
        // angle the form writes is left as it is
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

        // deg, the shown result back in decimal degrees from whichever view it was in; pressed during input
        // it evaluates first
        private void ShowDecimalDegrees()
        {
            if (!_isShowingResult && !CalculateResult()) return;

            _answerForm = AnswerForm.Decimal;
            PublishResult();
        }

        // cycles the shown result through its exact form, its recurring decimal and its decimal, the way
        // S⇔D does on a Casio: 7/3, 2.3 with a bar, 2.333333333; skipping whichever this value does not
        // have, so √2/2 only switches with its decimal; a pair switches both values together, and has a
        // form when either of them does
        //
        // the exact form comes from the exact value, so a result that came out of a logarithm or e has only
        // the fraction the numeric search finds for it, if any
        // pressed during input it evaluates first and then switches, which saves the = a Casio needs
        private void ToggleAnswerForm()
        {
            if (!_isShowingResult && !CalculateResult()) return;

            AnswerForm[] cycle = { ExactForm(), AnswerForm.Recurring, AnswerForm.Decimal };
            int at = _answerForm == AnswerForm.Improper || _answerForm == AnswerForm.Mixed
                ? 0
                : Array.IndexOf(cycle, _answerForm);

            // the prime factors, the ENG view and degrees, minutes and seconds are left for the decimal, from
            // where the cycle starts over
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

        // the shift of S⇔D, which a Casio labels a b/c ⇔ d/c: the fraction swaps between improper and mixed,
        // and is shown in the new form from whichever view the result was in
        //
        // a result without a fraction is left alone, and a proper fraction has no mixed form and stays as
        // it is; pressed during input it evaluates first, like the key it is the shift of
        private void SwapFractionForm()
        {
            if (!_isShowingResult && !CalculateResult()) return;
            if (!HasForm(ResultFormatter.HasFractionForm)) return;

            _mixedFraction = !_mixedFraction;
            _answerForm = ExactForm();
            PublishResult();
        }

        // the exact form in the fraction form currently chosen; a value with no mixed form, a proper
        // fraction or a root, is shown improper, which is the one form it has
        private AnswerForm ExactForm()
        {
            return _mixedFraction && HasForm(AnswerForm.Mixed) ? AnswerForm.Mixed : AnswerForm.Improper;
        }

        // the form a new result opens in: an angle in degrees, minutes and seconds as one; otherwise, with
        // exact first, the exact form when the value has one, and the decimal when it has none
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
