#define Base 
//#define INOV_Small
#define EPSON

using CommunicationBase;
using DeviceCommunicationBase.DeviceCommunication_Mitsubishi3E;
using DryIoc;
using PLCCommunication_Base.Modbus;
using Prism.Ioc;
using System;
using System.Diagnostics;
using System.Windows;

namespace test
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(IContainerProvider container)
        {
            InitializeComponent();
            mitsubis3E_Device = container.Resolve<Mitsubis3E_Device>("test_3E");

        }
        ModbusTCP_Device deviceCommunication;
        Mitsubis3E_Device mitsubis3E_Device;

        private async void button_Click(object sender, RoutedEventArgs e)
        {
#if Base || EPSON
            deviceCommunication = new ModbusTCP_Device()
            {
                IPAddress = "127.0.0.1"//"192.168.1.128"
            };
            deviceCommunication.Port = 502;
            deviceCommunication.SetNum(8000, 8000, 20000, 8000);
#endif
#if INOV_Small
            deviceCommunication = new ModbusTCP_INOV_Small/*ModbusTCP_Device*/()
            {
                IPAddress = "127.0.0.1"//"192.168.1.128"
            };
#endif

            deviceCommunication.ValueDecoder = new WordValueDecoder();

#if Base
#endif
#if INOV_Small
            DataTable dt = CSVHelper.CSV2DataTable(@"C:\Users\ZDH-XMXT-257\Desktop\Config_PLCDataParam_System.csv");
            foreach (DataRow item in dt.Rows)
            {
                if (item[1].ToString() == "")
                {
                    continue;
                }
                ModbusDataPoint mpp = new ModbusDataPoint()
                {
                    Name = item[2].ToString(),
                    Input = item[1].ToString(),
                    DataType = (DataType)Enum.Parse(typeof(DataType), item[3].ToString()),
                    StrLength = Convert.ToInt32(item[4])
                };
                mpp.OnValueChanged += (val) =>
                {
                    Console.WriteLine($"{mpp.Input} : {val.ToString()}");
                };
                deviceCommunication.AddDataPoint(mpp);
            }
#endif

#if !EPSON
            ModbusDataPoint md = new ModbusDataPoint()
            {
                Name = "BOOL数据",
#if Base
                Input = "000100",
#endif
#if INOV_Small
                Input = "M100",
#endif
                DataType = DataType.BIT
            };

            ModbusDataPoint md2 = new ModbusDataPoint()
            {
                Name = "INT16数据",
#if Base
                Input = "407999",
#endif
#if INOV_Small
                Input = "D7999",
#endif
                DataType = DataType.INT16
            };
            ModbusDataPoint d0 = new ModbusDataPoint()
            {
                Name = "INT16数据_0",
#if Base
                Input = "400000",
#endif
#if INOV_Small
                Input = "D0",
#endif
                DataType = DataType.INT16
            };
            ModbusDataPoint i16 = new ModbusDataPoint()
            {
                Name = "INT32数据",
#if Base
                Input = "400010",
#endif
#if INOV_Small
                Input = "D10",
#endif
                DataType = DataType.INT32
            };
            ModbusDataPoint i162 = new ModbusDataPoint()
            {
                Name = "INT32数据_1",
#if Base
                Input = "400011",
#endif
#if INOV_Small
                Input = "D11",
#endif
                DataType = DataType.INT32
            };
            ModbusDataPoint db = new ModbusDataPoint()
            {
                Name = "ASCII数据",
#if Base
                Input = "400020",
#endif
#if INOV_Small
                Input = "D20",
#endif
                DataType = DataType.ASCII,
                StrLength = 6
            };
            md.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{val.BOOL}");
            };
            md2.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{val.INT16}");
            };
            i16.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{val.INT32}");
            };
            i162.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{val.INT32}");
            };
            db.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{val.STRING}");
            };
            d0.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{d0.Input} : {val.ToString()}");
            };
            deviceCommunication.AddDataPoint(md2);
            deviceCommunication.AddDataPoint(md);
            deviceCommunication.AddDataPoint(i16);
            deviceCommunication.AddDataPoint(i162);
            deviceCommunication.AddDataPoint(db);
            deviceCommunication.AddDataPoint(d0);

#else
            ModbusDataPoint md = new ModbusDataPoint()
            {
                Name = "BOOL数据",
                Input = "000512",
                DataType = DataType.BIT
            };

            ModbusDataPoint md2 = new ModbusDataPoint()
            {
                Name = "INT16数据",
                Input = "400032",
                DataType = DataType.INT16
            };

            ModbusDataPoint md3 = new ModbusDataPoint()
            {
                Name = "in_BOOL数据",
                Input = "100512",
                DataType = DataType.BIT
            };

            ModbusDataPoint md4 = new ModbusDataPoint()
            {
                Name = "in_INT16数据",
                Input = "300032",
                DataType = DataType.INT16
            };

            md.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{val.BOOL}");
            };
            md2.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{val.INT16}");
            };
            md3.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{val.BOOL}");
            };
            md4.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{val.INT16}");
            };
            deviceCommunication.AddDataPoint(md2);
            deviceCommunication.AddDataPoint(md);
            deviceCommunication.AddDataPoint(md3);
            deviceCommunication.AddDataPoint(md4);
#endif

            deviceCommunication.SortOrderReadList();

           await deviceCommunication.Connect();
            DeviceCommunication.StartCallBackRunner();

        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {

            deviceCommunication.StartAutoRead();

        }

        private void button1复制__C__Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("halow:" + deviceCommunication["INT32数据"].GetValue().ToString());
            
        }

        private void button1复制__C_1_Click(object sender, RoutedEventArgs e)
        {
            deviceCommunication["INT32数据"].SetValue(12345);
        }
        Stopwatch sw = new Stopwatch();
        private void button1复制__C_1复制__C__Click(object sender, RoutedEventArgs e)
        {
            sw.Restart();
            deviceCommunication.Write(
                (deviceCommunication["BOOL数据"], !deviceCommunication["BOOL数据"].GetValue().BOOL),
                (deviceCommunication["INT32数据"], deviceCommunication["INT32数据"].GetValue().INT32 + 1),
                (deviceCommunication["INT32数据_1"], deviceCommunication["INT32数据_1"].GetValue().INT32 + 1),
                (deviceCommunication["INT16数据"], deviceCommunication["INT16数据"].GetValue().INT16 + 1));
            Console.WriteLine("时间: " + sw.ElapsedMilliseconds);
        }

        private void button1复制__C_1复制__C_复制__C__Click(object sender, RoutedEventArgs e)
        {
            sw.Restart();
            deviceCommunication.Write(
                (deviceCommunication["BOOL数据"], !deviceCommunication["BOOL数据"].GetValue().BOOL),
                (deviceCommunication["INT16数据"], deviceCommunication["INT16数据"].GetValue().INT16 + 1));
            Console.WriteLine("时间: " + sw.ElapsedMilliseconds);

        }

        private void button1复制__C_1复制__C_1_Click(object sender, RoutedEventArgs e)
        {

            mitsubis3E_Device.Config("1");
            mitsubis3E_Device.ConfigValueArray(McDeviceCode.M, 1500);
            mitsubis3E_Device.ConfigValueArray(McDeviceCode.D, 1500);

            Mc3EDataPoint mp1 = new Mc3EDataPoint()
            {
                Name = "测试D100",
                Input = "D100",
                DataType = DataType.UINT32
            };
            mp1.OnValueChanged += (val) => 
            {
                Console.WriteLine($"{mp1.Input} : {val.UINT32}");
            };
            Mc3EDataPoint mp2 = new Mc3EDataPoint()
            {
                Name = "测试D500",
                Input = "D500",
                DataType = DataType.INT16
            };
            mp2.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{mp2.Input} : {val.INT16}");
            };
            Mc3EDataPoint mp3 = new Mc3EDataPoint()
            {
                Name = "测试D900",
                Input = "D900",
                DataType = DataType.DOUBLE
            };
            mp3.OnValueChanged += (val) =>
            {
                Console.WriteLine($"{mp3.Input} : {val.DOUBLE}");
            };
            mitsubis3E_Device.AddDataPoint(mp1);
            mitsubis3E_Device.AddDataPoint(mp2);
            mitsubis3E_Device.AddDataPoint(mp3);

            //mitsubis3E_Device.AddDataPoint(new Mc3EDataPoint()
            //{
            //    Name = "测试BOOL",
            //    Input = "D200",
            //    DataType = DataType.UINT16
            //});
            mitsubis3E_Device.SortOrderReadList();
            mitsubis3E_Device.Connect();
            mitsubis3E_Device.StartAutoRead();

            DeviceCommunication.StartCallBackRunner();

        }

        private async void button1复制__C_1复制__C_1复制__C_复制__C__Click(object sender, RoutedEventArgs e)
        {
            sw.Restart();
            mitsubis3E_Device.Write(
                (mitsubis3E_Device["测试D900"], mitsubis3E_Device["测试D900"].GetValue().DOUBLE + 1),
                (mitsubis3E_Device["测试D500"], mitsubis3E_Device["测试D500"].GetValue().INT16 + 1),
                (mitsubis3E_Device["测试D100"], mitsubis3E_Device["测试D100"].GetValue().UINT32 + 1)
                );
            Console.WriteLine("时间: " + sw.ElapsedMilliseconds);

        }
    }
}
