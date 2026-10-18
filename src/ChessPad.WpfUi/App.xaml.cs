using Autofac;
using ChessPad.ViewModels;
using ChessPad.WpfUi.Views;
using System.Windows;

namespace ChessPad.WpfUi
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IContainer _container;
        public App()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<MainWindow>().AsSelf();
            builder.RegisterType<ChessPadViewModel>().AsSelf();
            builder.RegisterType<ChessBoardViewModel>().AsSelf();
            builder.RegisterType<FieldViewModel>().AsSelf();
            builder.RegisterType<RowColumnLabelViewModel>().AsSelf();
            _container = builder.Build();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var window = _container.Resolve<MainWindow>();
            MainWindow = window;
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _container.Dispose();
            base.OnExit(e);
        }
    }
}
