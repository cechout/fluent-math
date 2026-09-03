using Calculator_WinUI.Classes;
using Calculator_WinUI.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Calculator_WinUI.ViewModels
{
    public class StandardViewModel : INotifyPropertyChanged
    {
        private Calculate _calculator = new Calculate();

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
                    OnPropertyChanged(); // Sagt dem XAML: "Aktualisiere den Text!"
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


        // commands
        public ICommand InputCommand { get; }
        public ICommand CalculateCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand BackspaceCommand { get; }


        // constructor
        public StandardViewModel()
        {
            // Hier verknüpfen wir die Commands mit den Methoden
            InputCommand = new RelayCommand<string>(AddToTextBox);
            CalculateCommand = new RelayCommand<object>(_ => CalculateResult());
            ClearCommand = new RelayCommand<object>(_ => ClearAll());
            BackspaceCommand = new RelayCommand<object>(_ => Backspace());
        }


        // methods
        private void AddToTextBox(string sign)
        {
            if (InputAndResultText == "0" && sign != ".")
            {
                InputAndResultText = "";
            }
            InputAndResultText += sign;
        }

        private void CalculateResult()
        {
            CalculationText = InputAndResultText + "=";
            InputAndResultText = "(" + InputAndResultText + ")";

            InputAndResultText = _calculator.PreCalculate(InputAndResultText);

            if (InputAndResultText == "Error")
            {
                CalculationText = "";
            }
        }

        private void ClearAll()
        {
            InputAndResultText = "0";
            CalculationText = "";
        }

        private void Backspace()
        {
            if (InputAndResultText.Length <= 1)
            {
                InputAndResultText = "0";
            }
            else
            {
                InputAndResultText = InputAndResultText.Remove(InputAndResultText.Length - 1);
            }
        }


        // INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
