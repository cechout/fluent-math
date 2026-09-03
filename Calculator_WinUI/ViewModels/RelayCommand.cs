using System;
using System.Windows.Input;

namespace Calculator_WinUI.ViewModels
{
    /// <summary>
    /// this class is used to create commands for buttons in the view, it implements the ICommand interface
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action<T> execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }
    }
}
