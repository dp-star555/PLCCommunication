using DeviceCommunicationBase;
using DeviceCommunicationBase.Stream;
using DryIoc;
using PLCCommunication_Base.Mitsubishi3E;
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
            containerRegistry.Register<Mitsubis3E_Device>("test_3E");

            containerRegistry.RegisterSingleton<CommunicationDeviceMge>();
            containerRegistry.RegisterSingleton<MainWindow>();
        }

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

    }
}
