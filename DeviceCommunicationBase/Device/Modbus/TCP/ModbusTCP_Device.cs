using CommunicationBase;
using NModbus;
using PLCCommunication_Base.Modbus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PLCCommunication_Base.Modbus
{
    /// <summary>
    /// Modbus设备,这里pc设备只做主站使用，只考虑了主站的情况
    /// 默认的基础Modbus设备：没有输入转换器的情况下只运行输入转换后的modbus地址，无法兼容多种品牌。请使用对应封住或者自实现转换器
    /// </summary>
    public class ModbusTCP_Device : DeviceCommunication
    {
        public virtual void SetNum(ushort coilNum, ushort discreteInputsNum, ushort holdingRegistersNum, ushort inputRegistersNum ) 
        {
            mCoils = new bool[coilNum];
            mDiscreteInputs = new bool[discreteInputsNum ];
            mHoldingRegisters = new ushort[holdingRegistersNum ];
            mInputRegisters = new ushort[inputRegistersNum ];
        }

        readonly int CoilsMaxLengthOnce = 1920;
        readonly int RegistersMaxLengthOnce = 125;

        bool mIsConnected;
        private Dictionary<string, ICommunicationDataPoint> mNameIndex = new Dictionary<string, ICommunicationDataPoint>();
        private Dictionary<string, ICommunicationDataPoint> mAddressIndex = new Dictionary<string, ICommunicationDataPoint>();

        /// <summary>
        /// 用于快速索引的地图
        /// </summary>
        Dictionary<ModbusArea, Dictionary<int, List<ModbusDataPoint>>> mPointMap = new Dictionary<ModbusArea, Dictionary<int, List<ModbusDataPoint>>>()
        {
                { ModbusArea.Coil,new Dictionary<int,List< ModbusDataPoint>>()},
                { ModbusArea.DiscreteInput,new Dictionary<int, List< ModbusDataPoint>>() },
                { ModbusArea.HoldingRegister,new Dictionary<int, List< ModbusDataPoint>>() },
                { ModbusArea.InputRegister,new Dictionary<int, List< ModbusDataPoint>>() }
        };

        TcpClient tcpClient = null;
        IModbusMaster master;
        SemaphoreSlim mIOLock = new SemaphoreSlim(1, 1);
        /// <summary>
        /// 默认使用Modbus的原始地址
        /// </summary>
        ModbusAddressConverter_Base mBaseConveter = new ModbusAddressConverter_Base();

        /// <summary>
        /// 是否是大端模式(默认与系统一致)
        /// </summary>
        protected DataEndianKind mDataEndian { get; set; }

        //用于缓存数据的数组
        protected bool[] mCoils;
        protected bool[] mDiscreteInputs;
        protected ushort[] mHoldingRegisters;
        protected ushort[] mInputRegisters;

        /// <summary>
        /// 批量读取的数据的绑定列表
        /// key：指示数据寄存器的名称，根据连接器（有（汇川X,Y,M,R,D等等）或（西门子I,Q,DB,W,D等等））
        /// value：指示着对应寄存器将要批量读取的地址的起始与结束的地址
        /// </summary>
        Dictionary<ModbusArea, List<DataInfo>> ReadList = new Dictionary<ModbusArea, List<DataInfo>>()
        {
                { ModbusArea.Coil,new List<DataInfo>() },
                { ModbusArea.DiscreteInput,new List<DataInfo>() },
                { ModbusArea.HoldingRegister,new List<DataInfo>() },
                { ModbusArea.InputRegister,new List<DataInfo>() }
        };

        /// <summary>
        /// 基础的批量读取的数据的绑定列表（未排序合并）
        /// </summary>
        Dictionary<ModbusArea, List<int>> BaseReadList = new Dictionary<ModbusArea, List<int>>()
        { 
                { ModbusArea.Coil,new List<int>() },
                { ModbusArea.DiscreteInput,new List<int>() },
                { ModbusArea.HoldingRegister,new List<int>() },
                { ModbusArea.InputRegister,new List<int>() }
        };

        int mGeneration;
        public string IPAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 502;
        public byte SlaveID { get; set; } = 1;
        public string PortsConfigPath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "ConfigData_ModbusTCP");

        public override bool CanAutoRead { get; set; } = true;

        public override DeviceProtocolType CommunicationType { get { return DeviceProtocolType.ModbusTCP; } }

        public override ICommunicationDataPoint this[string name]
        {
            get
            {
                if (mNameIndex.ContainsKey(name))
                {
                    return mNameIndex[name];
                }
                else
                {
                    if (mAddressIndex.ContainsKey(name))
                    {
                        return mAddressIndex[name];
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public void AddDataPoint(ModbusDataPoint point)
        {
            ModbusDecodeData decodeData;
            IInputConverter<ModbusDecodeData> addressConverter = InputConverter as IInputConverter<ModbusDecodeData>;
            if (addressConverter == null)
            {
                addressConverter = mBaseConveter;
            }

            if (!mNameIndex.ContainsKey(point.Name))
            {
                mNameIndex.Add(point.Name, point);
            }
            if (!mAddressIndex.ContainsKey(point.Input))
            {
                mAddressIndex.Add(point.Input, point);
            }

            decodeData = point.DecodeData = addressConverter.Decode(point.Input);

            point.Panel = this;
            int mbAddress = decodeData.Address + point.GetLength();
            int arrayLength = 0;
            //比较地址区域，确保地址合法
            switch (decodeData.Area)
            {
                case ModbusArea.Coil:
                    arrayLength = mCoils.Length;
                    break;
                case ModbusArea.DiscreteInput:
                    arrayLength = mDiscreteInputs.Length;
                    break;
                case ModbusArea.HoldingRegister:
                    arrayLength = mHoldingRegisters.Length;
                    break;
                case ModbusArea.InputRegister:
                    arrayLength = mInputRegisters.Length;
                    break;
                default:
                    break;
            }
            if (mbAddress > arrayLength)
            {
                throw new Exception($"当前地址{point.Name}{point.Input},MODBUS:{mbAddress} > ArrrayL:{arrayLength}");
            }

            int dl = point.GetLength();
            for (int i = 0; i < dl; i++)
            {
                int address = decodeData.Address + i;
                if (!BaseReadList[decodeData.Area].Contains(address))
                {
                    BaseReadList[decodeData.Area].Add(address);
                }
            }

            int st = point.DecodeData.Address;
            ModbusArea area = point.DecodeData.Area;
            for (int i = 0; i < point.GetLength(); i++)
            {
                if (!mPointMap[area].ContainsKey(st + i))
                {
                    mPointMap[area].Add(st + i, new List<ModbusDataPoint>());
                    mPointMap[area][st + i].Add(point);
                }
                else
                {
                    mPointMap[area][st + i].Add(point);
                }
            }
        }

        public void SortOrderReadList()
        {
            // 清空 ReadList，重新生成
            foreach (var area in ReadList.Keys.ToList())
            {
                ReadList[area].Clear();
            }

            // 遍历每个 Modbus 区域
            foreach (var areaKvp in BaseReadList)
            {
                ModbusArea area = areaKvp.Key;
                List<int> baseList = areaKvp.Value;

                if (baseList == null || baseList.Count == 0)
                {
                    continue;
                }
                int maxLen = GetMaxLengthForArea(area);
                ReadList[area] = Partition(baseList, maxLen);
            }
        }

        /// <summary>
        /// 对地址进行动态规划分段：
        /// 1. 优先：最少发送次数（段数最少）
        /// 2. 次优先：总长度之和最小（总空洞最少）
        /// 单条指令最大连续长度 maxSpan（默认 125）
        /// </summary>
        /// <param name="addresses">要访问的离散地址列表（int，内部会排序、去重）</param>
        /// <param name="maxSpan">单条指令最大连续长度（End-Start+1）</param>
        /// <returns>分段后的 DataInfo 列表（按地址升序）</returns>
        public static List<DataInfo> Partition(List<int> addresses, int maxSpan = 125)
        {
            if (addresses == null) throw new ArgumentNullException(nameof(addresses));
            if (maxSpan <= 0) throw new ArgumentOutOfRangeException(nameof(maxSpan));

            // 空列表直接返回空结果
            if (addresses.Count == 0)
                return new List<DataInfo>();

            // 1. 排序 + 去重
            var addrs = addresses
                .OrderBy(a => a)
                .ToArray();

            int n = addrs.Length;

            // DP 数组：
            // commands[i]：从 i 开始到结尾，最少需要多少条指令
            // totalSpan[i]：在上述“最少指令方案”下，总长度之和
            var commands = new int[n + 1];
            var totalSpan = new int[n + 1];
            var nextIndex = new int[n + 1]; // 回溯用，记录从 i 最优跳到哪

            // 终止状态：从 n 开始（超出末尾）不需要任何指令、长度
            commands[n] = 0;
            totalSpan[n] = 0;
            nextIndex[n] = -1;

            // 2. 自底向上 DP，从最后一个地址往前算
            for (int i = n - 1; i >= 0; i--)
            {
                int bestCmd = int.MaxValue;
                int bestSpan = int.MaxValue;
                int bestNext = -1;

                int startAddr = addrs[i];

                // 枚举以 i 为起点的所有合法 [i..j] 段
                for (int j = i; j < n; j++)
                {
                    int endAddr = addrs[j];
                    int spanLen = endAddr - startAddr + 1; // 连续长度

                    // 超出单条指令最大长度，后面的 j 更大，只会更长，直接 break
                    if (spanLen > maxSpan)
                        break;

                    // 当前这段用 1 条指令，后面从 j+1 开始
                    int cmdCandidate = 1 + commands[j + 1];
                    int spanCandidate = spanLen + totalSpan[j + 1];

                    // 比较优劣：先比指令数，再比总长度
                    bool better =
                        cmdCandidate < bestCmd ||
                        (cmdCandidate == bestCmd && spanCandidate < bestSpan);

                    if (better)
                    {
                        bestCmd = cmdCandidate;
                        bestSpan = spanCandidate;
                        bestNext = j + 1;
                    }
                }

                commands[i] = bestCmd;
                totalSpan[i] = bestSpan;
                nextIndex[i] = bestNext;
            }

            // 3. 回溯构造分段结果
            var result = new List<DataInfo>();
            int index = 0;
            while (index < n)
            {
                int next = nextIndex[index];
                if (next <= index || next == -1)
                {
                    throw new InvalidOperationException("DP 回溯失败，nextIndex 状态异常。");
                }

                int start = addrs[index];
                int end = addrs[next - 1];
                ushort uStart = (ushort)start;
                ushort uEnd = (ushort)end;

                result.Add(new DataInfo
                {
                    StartAddress = uStart,
                    EndAddress = uEnd,
                    DataLengh = (ushort)(uEnd - uStart + 1)
                });

                index = next;
            }

            return result;
        }

        private int GetMaxLengthForArea(ModbusArea area)
        {
            switch (area)
            {
                case ModbusArea.Coil:
                case ModbusArea.DiscreteInput:
                    return CoilsMaxLengthOnce;  
                case ModbusArea.HoldingRegister:
                case ModbusArea.InputRegister:
                    return RegistersMaxLengthOnce;
                default:
                    return RegistersMaxLengthOnce;
            }
        }

        Stopwatch sw = new Stopwatch();
        public override async Task ReadAsync(CancellationToken ct = default)
        {
            sw.Restart();
            foreach (var areaDic in ReadList) //此时遍历的是不同功能码
            {
                foreach (var readInfo in areaDic.Value)
                {
                    ct.ThrowIfCancellationRequested();

                    switch (areaDic.Key)
                    {
                        case ModbusArea.Coil:
                            bool[] coils;
                            await mIOLock.WaitAsync();//线程锁
                            try
                            {
                                coils = await master.ReadCoilsAsync(SlaveID, readInfo.StartAddress, readInfo.DataLengh);
                            }
                            finally
                            {
                                mIOLock.Release();
                            }
                            ProcessBoolArea(ModbusArea.Coil, mCoils, readInfo.StartAddress, coils);
                            break;
                        case ModbusArea.DiscreteInput:
                            bool[] inputs;
                            await mIOLock.WaitAsync();//线程锁
                            try
                            {
                                inputs = await master.ReadInputsAsync(SlaveID, readInfo.StartAddress, readInfo.DataLengh);
                            }
                            finally
                            {
                                mIOLock.Release();
                            }
                            ProcessBoolArea(ModbusArea.DiscreteInput, mDiscreteInputs, readInfo.StartAddress, inputs);
                            break;
                        case ModbusArea.HoldingRegister:
                            ushort[] holdingRegisters;
                            await mIOLock.WaitAsync();//线程锁
                            try
                            {
                                holdingRegisters = await master.ReadHoldingRegistersAsync(SlaveID, readInfo.StartAddress, readInfo.DataLengh);
                            }
                            finally
                            {
                                mIOLock.Release();
                            }
                            ProcessUshortArea(ModbusArea.HoldingRegister, mHoldingRegisters, readInfo.StartAddress, holdingRegisters);
                            break;
                        case ModbusArea.InputRegister:
                            ushort[] inputRegisters;
                            await mIOLock.WaitAsync();//线程锁
                            try
                            {
                                inputRegisters = await master.ReadInputRegistersAsync(SlaveID, readInfo.StartAddress, readInfo.DataLengh);
                            }
                            finally
                            {
                                mIOLock.Release();
                            }
                            ProcessUshortArea(ModbusArea.InputRegister, mInputRegisters, readInfo.StartAddress, inputRegisters);
                            break;
                        default:
                            break;
                    }
                }
            }
            Console.WriteLine("全遍历时间：" + sw.ElapsedMilliseconds);
        }

        public override Task<DeviceValue> ReadAsync(ICommunicationDataPoint dp, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 计算合并发送的数据片段
        /// </summary>
        /// <param name="pvs"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        private List<WriteFragment> BuildMergedWriteFragments((ICommunicationDataPoint dp, object value)[] pvs)
        {
            List<WriteFragment> allFragments = new List<WriteFragment>();
            foreach (var (dp, val) in pvs)
            {
                // 类型检查与转换
                if (!(dp is ModbusDataPoint point)) continue;
                var decode = point.DecodeData;
                if (decode.Area == ModbusArea.DiscreteInput || decode.Area == ModbusArea.InputRegister)
                {
                    throw new InvalidOperationException($"不支持写入只读区域: {point.Name} ({decode.Area})");
                }
                var fragment = new WriteFragment
                {
                    StartAddress = decode.Address,
                    Area = decode.Area
                };
                if (decode.Area == ModbusArea.Coil)
                {
                    // 线圈：直接转 bool
                    bool boolVal = Convert.ToBoolean(val);
                    fragment.CoilData = new List<bool> { boolVal };
                }
                else // HoldingRegister
                {
                    byte[] hv = ValueDecoder.Code(val, dp.DataType, (ushort)(dp.GetLength() * 2), mDataEndian);
                    int shortCount = (hv.Length + 1) / 2;
                    if (shortCount > point.GetLength())
                    {
                        throw new Exception("输入数据超出，原数据的长度！");
                    }
                    ushort[] destShorts = new ushort[shortCount];
                    Buffer.BlockCopy(hv, 0, destShorts, 0, hv.Length);
                    fragment.RegisterData = new List<ushort>(destShorts);
                }
                allFragments.Add(fragment);
            }

            // 按区域分组 (Coil 和 Register 分开处理)
            var groups = allFragments.GroupBy(f => f.Area);
            var mergedList = new List<WriteFragment>();

            foreach (var group in groups)
            {
                ModbusArea area = group.Key;

                // 按起始地址从小到大
                var sortedList = group.OrderBy(f => f.StartAddress).ToList();
                // 连续地址合并
                if (sortedList.Count == 0) continue;

                WriteFragment current = sortedList[0];

                for (int i = 1; i < sortedList.Count; i++)
                {
                    var next = sortedList[i];

                    // 判断是否连续：下一段起始 == 当前段结束 + 1
                    // 写入必须严格连续，不能有空洞
                    if (next.StartAddress == current.EndAddress + 1)
                    {
                        // 合并数据
                        if (area == ModbusArea.Coil)
                        {
                            current.CoilData.AddRange(next.CoilData);
                        }
                        else
                        {
                            current.RegisterData.AddRange(next.RegisterData);
                        }
                    }
                    else if (next.StartAddress < current.EndAddress + 1)
                    {
                        int offset = next.StartAddress - current.StartAddress;

                        if (area == ModbusArea.Coil)
                        {
                            for (global::System.Int32 j = 0; j < next.CoilData.Count; j++)
                            {
                                if (current.CoilData.Count > (offset + j))
                                {
                                    current.CoilData[offset + j] = next.CoilData[j];
                                }
                                else
                                {
                                    current.CoilData.Add(next.CoilData[j]);
                                }
                            }
                        }
                        else
                        {
                            for (global::System.Int32 j = 0; j < next.RegisterData.Count; j++)
                            {
                                if (current.RegisterData.Count > (offset + j))
                                {
                                    current.RegisterData[offset + j] = next.RegisterData[j];
                                }
                                else
                                {
                                    current.RegisterData.Add(next.RegisterData[j]);
                                }
                            }
                        }

                    }
                    else
                    {
                        // 不连续，归档当前段，开始新段
                        mergedList.Add(current);
                        current = next;
                    }
                }
                mergedList.Add(current);
            }
            return mergedList;
        }

        public override void Write(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            if (pvs == null || pvs.Length == 0) return  ;
            var merged = BuildMergedWriteFragments(pvs);
            foreach (var task in merged)
            {
                mIOLock.Wait();
                try
                {
                    if (task.Area == ModbusArea.Coil)
                    {
                        master.WriteMultipleCoils(SlaveID, task.StartAddress, task.CoilData.ToArray());
                    }
                    else if (task.Area == ModbusArea.HoldingRegister)
                    {
                        master.WriteMultipleRegisters(SlaveID, task.StartAddress, task.RegisterData.ToArray());
                    }
                }
                finally{mIOLock.Release(); }  
            }

        }

        public override void Write(ICommunicationDataPoint dp, object value)
        {
            ModbusDataPoint point = (ModbusDataPoint)dp;
            mIOLock.Wait();
            try
            {
                switch (point.DecodeData.Area)
                {
                    case ModbusArea.Coil:
                        master.WriteSingleCoil(SlaveID, point.DecodeData.Address, (bool)value);
                        break;
                    case ModbusArea.HoldingRegister:
                        byte[] hv = ValueDecoder.Code(value, dp.DataType, (ushort)(dp.GetLength() * 2), mDataEndian);
                        int shortCount = (hv.Length + 1) / 2;
                        if (shortCount > point.GetLength())
                        {
                            throw new Exception("输入数据超出，原数据的长度！");
                        }
                        ushort[] destShorts = new ushort[shortCount];
                        Buffer.BlockCopy(hv, 0, destShorts, 0, hv.Length);
                        master.WriteMultipleRegisters(SlaveID, point.DecodeData.Address, destShorts);
                        break;
                    default:
                        throw new Exception($"写入{Name}设备，不支持当前点位{point.Name}:{point.Input} 类型：{point.DecodeData.Area}");
                }
            }
            finally { mIOLock.Release(); }
        }

        public override async Task WriteAsync(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            if (pvs == null || pvs.Length == 0) return  ;
            var merged = BuildMergedWriteFragments(pvs);
            foreach (var task in merged)
            {
                await mIOLock.WaitAsync();
                try
                {
                    if (task.Area == ModbusArea.Coil)
                    {
                        await master.WriteMultipleCoilsAsync(SlaveID, task.StartAddress, task.CoilData.ToArray());
                    }
                    else if (task.Area == ModbusArea.HoldingRegister)
                    {
                        await master.WriteMultipleRegistersAsync(SlaveID, task.StartAddress, task.RegisterData.ToArray());
                    }
                }
                finally { mIOLock.Release(); }
            }
        }

        public override async Task WriteAsync(ICommunicationDataPoint dp, object value)
        {
            ModbusDataPoint point = (ModbusDataPoint)dp;
            await mIOLock.WaitAsync();
            try
            {
                switch (point.DecodeData.Area)
                {
                    case ModbusArea.Coil:
                        await master.WriteSingleCoilAsync(SlaveID, point.DecodeData.Address, (bool)value);
                        break;
                    case ModbusArea.HoldingRegister:
                        byte[] hv = ValueDecoder.Code(value, dp.DataType, (ushort)(dp.GetLength() * 2), mDataEndian);
                        int shortCount = (hv.Length + 1) / 2;
                        if (shortCount > point.GetLength())
                        {
                            throw new Exception("输入数据超出，原数据的长度！");
                        }
                        ushort[] destShorts = new ushort[shortCount];
                        Buffer.BlockCopy(hv, 0, destShorts, 0, hv.Length);
                        await master.WriteMultipleRegistersAsync(SlaveID, point.DecodeData.Address, destShorts);
                        break;
                    default:
                        break;
                }
            }
            finally { mIOLock.Release(); }
        }

        public override async Task Connect()
        {
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress, Port);

            var factory = new ModbusFactory();
            master = factory.CreateMaster(tcpClient);
            mIsConnected = true;
        }

        public override void Disconnect()
        {
            master?.Dispose();
            master = null;
            tcpClient?.Close();
            tcpClient = null;
            mIsConnected = false;
        }

        private void ProcessBoolArea(ModbusArea area,bool[] buffer,ushort startAddress, bool[] newValues)  
        {
            int gen = Interlocked.Increment(ref mGeneration);

            for (int i = 0; i < newValues.Length; i++)
            {
                ushort addr = (ushort)(startAddress + i);

                bool oldV = buffer[addr];
                bool newV = newValues[i];

                if (oldV != newV)
                {
                    buffer[addr] = newV;
                    if (mPointMap[area].ContainsKey(addr))
                    {
                        foreach (var item in mPointMap[area][addr])
                        {
                            if (item.LastGeneration != gen)
                            {
                                DeviceValue val = this.DecodeValue(item);
                                Enqueue(item.GetValChangeDel(), val);
                                item.LastGeneration = gen;
                            }
                        }
                    }
                }
            }
        }

        private void ProcessUshortArea(ModbusArea area, ushort[] buffer, ushort startAddress, ushort[] newValues)
        {
            int gen = Interlocked.Increment(ref mGeneration);
            //List<int> changeIndexs = new List<int>();
            Span<bool> changed = stackalloc bool[newValues.Length];
            bool hasAnyChange = false;
            for (int i = 0; i < newValues.Length; i++)
            {
                ushort addr = (ushort)(startAddress + i );
                ushort oldV = buffer[addr];
                ushort newV = newValues[i];

                if (oldV != newV)
                {
                    buffer[addr] = newV;
                    //changeIndexs.Add(addr);
                    changed[i] = true;
                    hasAnyChange = true;
                }
                else
                {
                    changed[i] = false;
                }
            }

            // 如果没有任何数据变化，直接返回，跳过第二个循环
            if (!hasAnyChange) return;

            for (int i = 0; i < changed.Length; i++)
            {
                if (changed[i])
                {
                    ushort addr = (ushort)(startAddress + i);

                    if (mPointMap[area].TryGetValue(addr, out List<ModbusDataPoint> points))
                    {
                        foreach (var item in points)
                        {
                            if (item.LastGeneration != gen)
                            {
                                DeviceValue val = this.DecodeValue(item);
                                Enqueue(item.GetValChangeDel(), val);
                                item.LastGeneration = gen;
                            }
                        }
                    }
                }
            }

            //foreach (var item in changeIndexs)
            //{
            //    if (mPointMap[area].ContainsKey(item))
            //    {
            //        ModbusDataPoint mp = mPointMap[area][item];
            //        if (mp.LastGeneration != gen)
            //        {
            //            DeviceValue val = this.DecodeValue(mp);
            //            mp.TriggerChanged(val);
            //            mp.LastGeneration = gen;
            //        }
            //    }
            //}
        }

        public DeviceValue DecodeValue(ModbusDataPoint mp) 
        {
            switch (mp.DecodeData.Area)
            {
                case ModbusArea.Coil:
                    return new DeviceValue()
                    {
                        BOOL = mCoils[mp.DecodeData.Address]
                    };
                case ModbusArea.DiscreteInput:
                    return new DeviceValue()
                    {
                        BOOL = mDiscreteInputs[mp.DecodeData.Address]
                    };
                case ModbusArea.HoldingRegister:
                    var targetRegisters = mHoldingRegisters.AsSpan(mp.DecodeData.Address, mp.GetLength());
                    ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(targetRegisters);
                    return ValueDecoder.Decode(bytes,
                                                mp.DataType,
                                                mDataEndian);
                case ModbusArea.InputRegister:
                    var inputRegisters = mInputRegisters.AsSpan(mp.DecodeData.Address, mp.GetLength());
                    ReadOnlySpan<byte> irbytes = MemoryMarshal.AsBytes(inputRegisters);
                    return ValueDecoder.Decode(irbytes,
                                                mp.DataType,
                                                mDataEndian);
                default:
                    return new DeviceValue();
            }
        }
    }

    public class DataInfo
    {
        public ushort StartAddress;
        public ushort EndAddress;
        public ushort DataLengh;

        /// <summary>
        /// 判断两个地址区间是否有交集
        /// </summary>
        /// <param name="other">另一个地址区间</param>
        /// <param name="intervalLength">允许的间隔长度</param>
        /// <returns></returns>
        public bool HasOverlap(DataInfo other, int intervalLength = 1)
        {
            // 通过比较起始地址和结束地址来判断是否有交集
            return (this.StartAddress <= other.EndAddress + intervalLength && other.StartAddress <= this.EndAddress + intervalLength);
        }

        /// <summary>
        /// 合并两个地址区间
        /// </summary>
        /// <param name="other"></param>
        public void Merge(DataInfo other)
        {
            this.StartAddress = Math.Min(this.StartAddress, other.StartAddress);
            this.EndAddress = Math.Max(this.EndAddress, other.EndAddress);
            this.DataLengh = (ushort)(EndAddress - StartAddress + 1);
        }

        public DataInfo Clone()
        {
            return new DataInfo
            {
                StartAddress = this.StartAddress,
                EndAddress = this.EndAddress,
                DataLengh = this.DataLengh
            };
        }

    }

    /// <summary>
    /// 暂存待写入的片段
    /// </summary>
    public class WriteFragment
    {
        public ushort StartAddress;
        public ModbusArea Area;

        // 寄存器数据 
        public List<ushort> RegisterData;

        // 线圈数据 
        public List<bool> CoilData;

        // 计算结束地址 (起始 + 长度 - 1)
        public int EndAddress => StartAddress + (RegisterData?.Count ?? CoilData.Count) - 1;
    }


}
