using System;
using System.Windows.Input;

namespace Calculator_WinUI.ViewModels
{
    // minimal ICommand used by every button binding
    //
    // no button in the app is ever disabled, so CanExecute is hardwired to true and CanExecuteChanged
    // is never raised; the event only exists because ICommand requires it
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
