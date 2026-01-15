using DeviceCommunicationBase.DeviceCommunication_Mitsubishi3E;
using DeviceCommunicationBase;
using DryIoc;
using Prism.DryIoc;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
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
            containerRegistry.RegisterInstance<ICommPort>(new HPSocketPort_Client() { }, "1");
            containerRegistry.RegisterSingleton<Mitsubis3E_Device, Mitsubis3E_Device>("test_3E");
            containerRegistry.RegisterSingleton<MainWindow>();

         
        }

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

    }
}
