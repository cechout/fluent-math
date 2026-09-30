using System;
using System.Windows.Input;

namespace FluentMath.ViewModels
{
    // the ICommand behind every key binding; CanExecute is always true and CanExecuteChanged never fires
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
