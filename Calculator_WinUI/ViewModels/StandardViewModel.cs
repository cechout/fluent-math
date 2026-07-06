using Calculator_WinUI.Classes;
using Calculator_WinUI.Engines;
using Calculator_WinUI.Models;
using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Calculator_WinUI.ViewModels
{
    public class StandardViewModel : INotifyPropertyChanged
    {
        private Calculate _calculator = new Calculate();
        private readonly MathInputManager _inputManager = new MathInputManager();

        // ui properties 
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

        // toggle visibility of secondary buttons
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

        // commands
        public ICommand InputCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand BackspaceCommand { get; }


        // constructor
        public StandardViewModel()
        {
            InputCommand = new RelayCommand<string>(AddToTextBox);
            CalculateCommand = new RelayCommand<object>(_ => CalculateResult());
            ClearCommand = new RelayCommand<object>(_ => ClearAll());
            BackspaceCommand = new RelayCommand<object>(_ => Backspace());
        }


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
            // old logic
            else if (sign == "+" || sign == "-" || sign == "*" || sign == "/")
            {
                _inputManager.AddOperator(sign);
            }
            else
            {
                _inputManager.AddNumber(sign);
            }

            // update UI
            InputAndResultText = _inputManager.GetLatexString();
        }

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


        // INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}