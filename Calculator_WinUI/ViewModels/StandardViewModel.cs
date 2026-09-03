using Calculator_WinUI.Engines;
using Calculator_WinUI.Models;
using Microsoft.UI.Xaml;
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

        // the last successful result, kept so an operator pressed straight after = can continue from it
        private double _lastAnswer;

        // true while the display shows a result instead of the formula being typed; the next keypress
        // decides whether that result is dropped or carried into the next calculation
        private bool _isShowingResult;


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


        // === shift layer ===

        // the second keyboard layer is not a separate panel; each shiftable key is two buttons stacked in
        // the same grid cell, and these two properties swap which of them is visible
        private Visibility _normalVisibility = Visibility.Visible;
        public Visibility NormalVisibility
        {
            get => _normalVisibility;
            set
            {
                _normalVisibility = value;
                OnPropertyChanged();
            }
        }

        private Visibility _shiftVisibility = Visibility.Collapsed;
        public Visibility ShiftVisibility
        {
            get => _shiftVisibility;
            set
            {
                _shiftVisibility = value;
                OnPropertyChanged();
            }
        }


        // === commands ===

        public ICommand InputCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand BackspaceCommand { get; }


        // === constructor ===

        public StandardViewModel()
        {
            InputCommand = new RelayCommand<string>(AddToTextBox);
            CalculateCommand = new RelayCommand<object>(_ => CalculateResult());
            ClearCommand = new RelayCommand<object>(_ => ClearAll());
            BackspaceCommand = new RelayCommand<object>(_ => Backspace());
        }


        // === input handling ===

        // three kinds of parameter arrive here: a "cmd_" keyword for anything structural, a bare operator,
        // and anything else, which is treated as a digit or a decimal point
        //
        // several keys in the XAML send a cmd_ value that has no arm yet (cmd_pi, cmd_e, cmd_more,
        // cmd_paren_open, cmd_paren_close and the inverse trig keys); they fall through the switch and do
        // nothing, which is why those buttons are currently dead rather than broken
        private void AddToTextBox(string sign)
        {
            // shift only swaps the keyboard layer, it must never disturb the input or a shown result
            if (sign == "cmd_shift")
            {
                ToggleShift();
                return;
            }

            if (_isShowingResult) BeginInputAfterResult(sign);

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

                    case "cmd_sin":
                        _inputManager.StartFunction("sin");
                        break;

                    case "cmd_cos":
                        _inputManager.StartFunction("cos");
                        break;

                    case "cmd_tan":
                        _inputManager.StartFunction("tan");
                        break;

                    case "cmd_frac":
                        _inputManager.StartFraction();
                        break;

                    case "cmd_log":
                        _inputManager.StartLogarithm(customBase: true);
                        break;

                    case "cmd_ln":
                        _inputManager.StartFunction("ln");
                        break;
                }
            }
            else if (sign == "+" || sign == "-" || sign == "*" || sign == "/")
            {
                _inputManager.AddOperator(sign);
            }
            else
            {
                _inputManager.AddNumber(sign);
            }

            // the engine has no change notification of its own, so the display is republished after
            // every single keypress
            InputAndResultText = _inputManager.GetLatexString();
        }

        // after = the display holds a result rather than the formula that produced it, so the next key
        // has to say what happens to it: an operator carries it into the next calculation, an arrow key
        // goes back to editing the old formula, anything else starts over
        private void BeginInputAfterResult(string sign)
        {
            _isShowingResult = false;

            if (sign.StartsWith("cmd_nav_")) return; // the tree still holds the formula that was evaluated

            if (sign == "+" || sign == "-" || sign == "*" || sign == "/")
            {
                _inputManager.SeedWithValue(ResultFormatter.ToPlainString(_lastAnswer));
                return;
            }

            _inputManager.Clear();
        }

        // the second keyboard layer is two buttons stacked in the same cell, so switching layers is
        // purely a matter of which of the two is visible
        private void ToggleShift()
        {
            if (NormalVisibility == Visibility.Visible)
            {
                NormalVisibility = Visibility.Collapsed;
                ShiftVisibility = Visibility.Visible;
            }
            else
            {
                NormalVisibility = Visibility.Visible;
                ShiftVisibility = Visibility.Collapsed;
            }
        }

        // = deliberately leaves the tree alone; only the display switches over to the result, so a
        // Math ERROR can be corrected instead of retyped from scratch
        private void CalculateResult()
        {
            CalculationText = _inputManager.GetLatexString() + "=";

            EvaluationResult result = _evaluator.Evaluate(_inputManager.RootTokens);
            if (result.IsSuccess)
            {
                _lastAnswer = result.Value;
                InputAndResultText = ResultFormatter.ToLatex(result.Value);
                _isShowingResult = true;
            }
            else
            {
                InputAndResultText = ResultFormatter.ErrorToLatex(result.Error);
            }
        }

        private void ClearAll()
        {
            _isShowingResult = false;
            _inputManager.Clear();
            InputAndResultText = _inputManager.GetLatexString();
            CalculationText = "";
        }

        private void Backspace()
        {
            // backspacing out of a result means going back to editing the formula behind it
            _isShowingResult = false;

            _inputManager.Backspace();
            InputAndResultText = _inputManager.GetLatexString();
        }


        // === property changed ===

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
