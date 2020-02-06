using System;
using System.ComponentModel;
using System.Windows.Input;

namespace WpfBindingSample.Utils
{
    public class ViewModelCommand : ICommand
    {
        private readonly Func<bool> _CanExecute;
        private readonly Action _Execute;

        public ViewModelCommand(Action execute) : this(execute, null) { }

        public ViewModelCommand(Action execute, Func<bool> canExecute)
        {
            _Execute = execute;
            _CanExecute = canExecute ?? (() => true);
        }

        /// <summary>
        /// 実行可能かどうかを取得するプロパティ (get-only)
        /// </summary>
        public bool CanExecute => _CanExecute();


        /// <summary>
        /// コマンドを実行します
        /// </summary>
        public void Execute() => _Execute();

        /// <summary>
        /// 実行可能かどうかが変化した時に呼び出します
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged(this, null);
        }


        #region Interface Implements

        public event EventHandler CanExecuteChanged;

        bool ICommand.CanExecute(object parameter) => _CanExecute();

        void ICommand.Execute(object parameter) => _Execute();

        #endregion
    }
}
