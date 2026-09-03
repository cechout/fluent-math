using Calculator_WinUI.Classes;
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

        // the v1 string evaluator, carried over from the WPF version and currently unused; it will be
        // replaced by a real evaluator walking the token tree rather than being wired back up
        private Calculate _calculator = new Calculate();

        private readonly MathInputManager _inputManager = new MathInputManager();


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

                    case "cmd_shift":
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

        // there is no evaluator yet, so pressing equals only moves the input up into the history line
        private void CalculateResult()
        {
            CalculationText = InputAndResultText + "=";
        }

        private void ClearAll()
        {
            _inputManager.Clear();
            InputAndResultText = _inputManager.GetLatexString();
            CalculationText = "";
        }

        private void Backspace()
        {
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
