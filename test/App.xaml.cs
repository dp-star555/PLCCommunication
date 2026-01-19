using DeviceCommunicationBase;
using DeviceCommunicationBase.Stream;
using DryIoc;
using Prism.DryIoc;
using Prism.Ioc;
using System.Windows;

namespace test
{
  
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : PrismApplication
    {
        /// <summary>
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册本项目的视图
            containerRegistry.RegisterSingleton<CommPortManager>();
            containerRegistry.RegisterSingleton<CommunicationDeviceManager>();
            containerRegistry.RegisterSingleton<MainWindow>();

         
        }

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

    }
}
