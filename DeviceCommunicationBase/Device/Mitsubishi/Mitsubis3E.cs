using CommunicationBase;
using DeviceCommunicationBase;
using DeviceCommunicationBase.Stream;
using HPSocket;
using Prism.Ioc;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static PLCCommunication_Base.Mitsubishi3E.Mc3EBinaryPacketBuilder;

namespace PLCCommunication_Base.Mitsubishi3E
{
    /// <summary>
    /// 数据块描述
    /// </summary>
    class AddressBlock
    {
        public int StartAddress { get; set; }
        public int Length { get; set; }
        public int EndAddress => StartAddress + Length - 1;
    }

    public class Mitsubis3E_Device : DeviceCommunication
    {
        IContainerProvider  containerProvider;
        public Mitsubis3E_Device(IContainerProvider container):base(container) 
        {
            containerProvider=  container ;
            // 当拆出一帧完整包时：交付给等待方
            mSplitter.FrameCompleted += frame =>
            {
                byte[] data = frame.ToArray();
                if (mCurrentTcs == null)
                    return;                 // 没人在等：迟到包/残留包 -> 丢弃
                                            // B. 优先检查是否是异步请求
                if (mCurrentTcs != null && !mCurrentTcs.Task.IsCompleted)
                {
                    // 异步模式：设置 Task 结果，让 await 继续运行
                    mCurrentTcs.TrySetResult(data);
                }

            };

        }

        // SLMP 协议限制：单个块或单次读取通常最大不超过 960 个字 (视具体PLC型号和指令而定，这里保守设为 960)
        const int MAX_BLOCK_SIZE = 960;
        const int MAX_BLOCK_SIZE_BOOL = 960 * 16;

        /// <summary>
        /// 用于生成运行唯一码的锚
        /// </summary>
        int mGeneration;

        /// <summary>
        /// 用于交互的端口
        /// </summary>
        ICommPort mClient;

        /// <summary>
        /// 用于端口的自动解包器
        /// </summary>
        private readonly Mc3EBinaryFrameSplitter mSplitter = new Mc3EBinaryFrameSplitter( maxFrameLength: 4096);

        /// <summary>
        /// 用于记录所属的所有数据点
        /// </summary>
        Dictionary<string, ICommunicationDataPoint> mNameIndex = new Dictionary<string, ICommunicationDataPoint>();

        /// <summary>
        /// 数据集合
        /// </summary>
        Dictionary<E_McDeviceCode, Array> mValueGroup = new Dictionary<E_McDeviceCode, Array>();
        /// <summary>
        /// 输入的原始数据
        /// </summary>
        Dictionary<E_McDeviceCode, List<int>> mReadData_Base = new Dictionary<E_McDeviceCode, List<int>>();
        /// <summary>
        /// 用于回调进行索引的字典
        /// </summary>
        Dictionary<E_McDeviceCode, Dictionary<int, List<Mc3EDataPoint>>> mEventMap = new Dictionary<E_McDeviceCode, Dictionary<int, List<Mc3EDataPoint>>>();
        /// <summary>
        /// 用于排序后的读取列表
        /// </summary>
        Dictionary<E_McDeviceCode, List<AddressBlock>> mReadList = new Dictionary<E_McDeviceCode, List<AddressBlock>>();

        /// <summary>
        /// 允许的连续空位
        /// </summary>
        public ushort HoleThreshold { get; set; } = 480;
        /// <summary>
        /// 初始化数据数组长度
        /// </summary>
        /// <param name="code"></param>
        /// <param name="num"></param>
        public void ConfigValueArray(E_McDeviceCode code, ushort num)
        {
            if (!mValueGroup.ContainsKey(code))
            {
                switch (code)
                {
                    case E_McDeviceCode.M:
                    case E_McDeviceCode.X:
                    case E_McDeviceCode.Y:
                        mValueGroup.Add(code, new bool[num]);
                        break;
                    case E_McDeviceCode.D:
                    case E_McDeviceCode.R:
                        mValueGroup.Add(code, new byte[num * 2]);
                        break;
                    default:
                        break;
                }
            }
           else
            {
                throw new Exception($"数据数组 {code} 已经被初始化，无法重复初始化。");
            }
            if (!mReadData_Base.ContainsKey(code))
            {
                mReadData_Base.Add(code, new List<int>());
            }
            if (!mEventMap.ContainsKey(code))
            {
                mEventMap.Add(code, new Dictionary<int, List<Mc3EDataPoint>>());
            }
        }

        public void Config(string url) 
        {
            string name="1";
            mClient= containerProvider.Resolve<CommPortManager>().GetPort(name);

            mClient.FrameSplitter = mSplitter;
            mClient.OnDisconnect += (sender) =>
            {
                // 如果连接断开了，通知等待的任务抛出异常
                mCurrentTcs?.TrySetException(new Exception("Socket disconnected unexpectedly."));
            };
        }

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
                    return null;
                }
            }
        }

        public override bool CanAutoRead { get; } = true;

        public override DeviceProtocolType CommunicationType { get; } = DeviceProtocolType.MC3E;

        public override Task Connect()
        {
            if (!mClient.IsOpen)
            {
                mClient.Connect();
            }
            return Task.CompletedTask;
        }

        public override void Disconnect()
        {
            if (mClient.IsOpen)
            {
                mClient.Disconnect();
            }
        }

        // 正则
        static readonly Regex mAddrRegex = new Regex(@"^(?<Type>[A-Za-z]{1,2})(?<Addr>[0-9A-Fa-f]+)(?<Suffix>H)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public void AddDataPoint(ICommunicationDataPoint point)
        {
            Mc3EDataPoint dp = point as Mc3EDataPoint;
            if (dp == null)
            {
                throw new Exception($"{point.Name}:{point.Input} 地址不是Mc3EDataPoint类似，无法进行转换。");
            }

            // 正则格式校验与拆分
            var match = mAddrRegex.Match(dp.Input);
            if (!match.Success)
            {
                throw new FormatException($"地址格式错误: {point.Name}:{point.Input}。应为 '字母+数字' 格式。");
            }

            string typeStr = match.Groups["Type"].Value;
            string addrStr = match.Groups["Addr"].Value;
            bool hasHexSuffix = match.Groups["Suffix"].Success; // 检查是否有 H 后缀
            int addrNum = dp.GetLength();

            E_McDeviceCode dc;

            if (!mNameIndex.ContainsKey(point.Name))
            {
                mNameIndex.Add(point.Name, point);
            }

            if (!Enum.TryParse(typeStr, out dc))
            {
                throw new FormatException($"地址格式错误: {point.Name}:{point.Input}。{typeStr}不被支持。");
            }
            else
            {
                if (!mValueGroup.ContainsKey(dc))
                {
                    throw new FormatException($"地址格式错误: {point.Name}:{point.Input}。{typeStr}对应的内存1缓存没有被初始化。");
                }
                else
                {
                    ushort address = 1;
                    if (hasHexSuffix)//16进制需要转化到10进制参与计算
                    {
                        address = Convert.ToUInt16(addrStr, 16);
                    }
                    else
                    {
                        address = Convert.ToUInt16(addrStr);
                    }


                    dp.DecodeData.Area = dc;
                    dp.DecodeData.Address = address;

                    int arrayLength = 0;
                    arrayLength = mValueGroup[dc].Length;

                    int oAddress = address + addrNum;
                    if (oAddress > arrayLength)
                    {
                        throw new Exception($"当前地址{point.Name}{point.Input},（地址:{address} + 长度{addrNum}） > ArrrayL:{arrayLength}");
                    }

                    point.Panel = this;
                    //将数据点都加入原始数据的列表中
                    for (int i = 0; i < addrNum; i++)
                    {
                        //添加用于查找的基础数据
                        int add = address + i;
                        if (!mReadData_Base[dc].Contains(add))
                        {
                            mReadData_Base[dc].Add(add);
                        }

                        //添加用于回调的索引数据
                        if (!mEventMap[dc].ContainsKey(add))
                        {
                            mEventMap[dc].Add(add, new List<Mc3EDataPoint>());
                        }
                        mEventMap[dc][add].Add(dp);
                    }
                }
            }
        }

        public void SortOrderReadList()
        {
            // 清空 ReadList，重新生成
            foreach (var item in mReadList)
            {
                item.Value.Clear();
            }

            // 遍历每个区域
            foreach (var areaKvp in mReadData_Base)
            {
                E_McDeviceCode code = areaKvp.Key;
                List<int> baseList = areaKvp.Value;

                if (baseList == null || baseList.Count == 0)
                {
                    continue;
                }

                // 获取所有地址：去重 + 排序 
                var sortedAddresses = areaKvp.Value.Distinct().OrderBy(a => a).ToList();

                if (sortedAddresses.Count == 0) continue;

                var blocks = new List<AddressBlock>();

                // 初始化第一个块
                AddressBlock currentBlock = new AddressBlock
                {
                    StartAddress = sortedAddresses[0],
                    Length = 1
                };

                // 遍历后续地址进行聚类
                for (int i = 1; i < sortedAddresses.Count; i++)
                {
                    int currentAddr = sortedAddresses[i];
                    int prevAddr = sortedAddresses[i - 1];

                    // 判断是否连续：当前地址 == 前一个地址 + 1
                    bool isConsecutive = false;

                    // 判断是否超长：当前块长度是否已达上限
                    bool isFull = false;

                    switch (code)
                    {
                        case E_McDeviceCode.M:
                        case E_McDeviceCode.X:
                        case E_McDeviceCode.Y:
                        case E_McDeviceCode.B:
                            isConsecutive = (currentAddr <= prevAddr + HoleThreshold * 4);
                            isFull = currentBlock.Length >= MAX_BLOCK_SIZE_BOOL;
                            break;
                        case E_McDeviceCode.D:
                        case E_McDeviceCode.W:
                        case E_McDeviceCode.R:
                            isConsecutive = (currentAddr <= prevAddr + HoleThreshold);
                            isFull = currentBlock.Length >= MAX_BLOCK_SIZE;
                            break;
                        default:
                            break;
                    }

                    if (isConsecutive && !isFull)
                    {
                        // 连续且未满 -> 延长当前块
                        currentBlock.Length += currentAddr  - prevAddr;
                    }
                    else
                    {
                        // 不连续 或 已满 -> 结束当前块，开启新块
                        blocks.Add(currentBlock);

                        currentBlock = new AddressBlock
                        {
                            StartAddress = currentAddr,
                            Length = 1
                        };
                    }
                }

                // 添加最后一个块
                blocks.Add(currentBlock);

                // 保存结果
                if (!mReadList.ContainsKey(code))
                {
                    mReadList.Add(code, new List<AddressBlock>());
                }
                mReadList[code] = blocks;

            }
        }

        // 用于生成并匹配请求ID ，（发一个等一个），这是最安全的做法。
        private readonly SemaphoreSlim mLock = new SemaphoreSlim(1, 1);
        private TaskCompletionSource<byte[]> mCurrentTcs;

        /// <summary>
        /// 用于生成请求报文
        /// </summary>
        Mc3EComposerBuilder mBuilder = new Mc3EComposerBuilder();

        public override async Task ReadAsync(CancellationToken ct = default)
        {
            // 遍历所有已分类和排序的读取块
            foreach (var kvp in mReadList)
            {
                E_McDeviceCode code = kvp.Key;
                List<AddressBlock> blocks = kvp.Value;

                foreach (var block in blocks)
                {
                    // 1. 构建读取报文
                    byte[] request = mBuilder.BuildRead(code, block.StartAddress, block.Length);

                    // 2. 发送并等待响应
                    try
                    {
                        byte[] response = await mClient.WriteRequestAsync(request);// SendAndWaitAsync(request);
                        ushort successCode = response.ToUInt16(9);
                        if (successCode != 0)//读取失败
                        {
                            throw new Exception($"获取异常代码：{successCode}");
                        }
                        else
                        {
                            // 3. 解析数据并填充到 mValueGroup
                            switch (code)
                            {
                                case E_McDeviceCode.M:
                                case E_McDeviceCode.X:
                                case E_McDeviceCode.Y:
                                case E_McDeviceCode.B:
                                    byte[] bv = new byte[response.Length - 11];
                                    Array.Copy(response, 11, bv, 0, bv.Length);
                                    bool[] values = bv.ToHexBoolsUnsafe();
                                    //进行比较,复制，触发变更
                                    ProcessData_Bool(code,(bool[])mValueGroup[code], block.StartAddress, values);
                                    break;
                                case E_McDeviceCode.D:
                                case E_McDeviceCode.W:
                                case E_McDeviceCode.R:
                                    ProcessData_Byte(code, (byte[])mValueGroup[code], block.StartAddress, response,11);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    catch (TimeoutException ex)
                    {
                        // 处理超时
                    }
                    catch (Exception ex)
                    {
                        // 处理其他错误
                    }

                }
            }
        }
        public override async Task<DeviceValue> ReadAsync(ICommunicationDataPoint dp, CancellationToken ct = default)
        {
            //手动读取单个点，不进行自动回调，与数据更新

            Mc3EDataPoint mp = dp as Mc3EDataPoint;
            E_McDeviceCode code = mp.DecodeData.Area;

            int startAddr = mp.DecodeData.Address;

            int readLength = mp.GetLength();

            // 1. 构建读取报文
            byte[] request = mBuilder.BuildRead(code, startAddr, readLength);

            // 2. 发送并等待响应
            byte[] response = await mClient.WriteRequestAsync(request);//SendAndWaitAsync(request);
            ushort successCode = response.ToUInt16(9);
            if (successCode != 0)//读取失败
            {
                throw new Exception($"获取异常代码：{successCode}");
            }
            else
            {
                // 3. 解析数据并填充到 mValueGroup
                switch (code)
                {
                    case E_McDeviceCode.M:
                    case E_McDeviceCode.X:
                    case E_McDeviceCode.Y:
                    case E_McDeviceCode.B:
                        byte[] bv = new byte[response.Length - 11];
                        Array.Copy(response, 11, bv, 0, bv.Length);
                        bool[] values = bv.ToHexBoolsUnsafe();
                        //进行比较,复制，触发变更
                        ProcessData_Bool(code, (bool[])mValueGroup[code], startAddr, values);
                        break;
                    case E_McDeviceCode.D:
                    case E_McDeviceCode.W:
                    case E_McDeviceCode.R:
                        ProcessData_Byte(code, (byte[])mValueGroup[code], startAddr, response, 11);
                        break;
                    default:
                        break;
                }
            }

            return dp.GetValue();
        }

        public override async void Write(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            // 1. 数据准备 (逻辑与 Async 版本完全共用，为了 DRY 原则，建议封装数据准备逻辑)
            List<(Mc3EDataPoint dp, byte[] data)> wordItems = new List<(Mc3EDataPoint dp, byte[] data)>();
            List<(Mc3EDataPoint dp, byte[] data)> bitItems = new List<(Mc3EDataPoint dp, byte[] data)>();

            foreach (var dp0 in pvs)
            {
                Mc3EDataPoint dp = dp0.dp as Mc3EDataPoint;
                object value = dp0.value;
                switch (dp.DataType)
                {
                    case DataType.BIT:
                        bitItems.Add((dp, new byte[] { (bool)(value) ? (byte)1 : (byte)0 }));
                        break;
                    case DataType.BYTE:
                        break;
                    case DataType.INT16:
                    case DataType.UINT16:
                    case DataType.INT32:
                    case DataType.UINT32:
                    case DataType.SINGLE:
                    case DataType.DOUBLE:
                    case DataType.UTF32:
                    case DataType.ASCII:
                        byte[] d = ValueDecoder.Code(value, dp.DataType, (ushort)(dp.GetLength() * 2));
                        wordItems.Add((dp, d));
                        break;
                    default:
                        break;
                }
            }

            // 2. 构建报文
            byte[] request = mBuilder.BuildWrite(wordItems, bitItems);

            byte[] response = await mClient.WriteRequestAsync(request); // 这里直接阻塞，没有 Task，没有死锁

            // 4. 校验结果
            ushort successCode = response.ToUInt16(9);
            if (successCode != 0)
            {
                throw new Exception($"写入失败，PLC返回异常代码：{successCode:X2}");
            }
        }

        public override void Write(ICommunicationDataPoint dp, object value)
        {
            Write((dp, value));
        }

        public override async Task WriteAsync(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            List<(Mc3EDataPoint dp, byte[] data)> wordItems = new List<(Mc3EDataPoint dp, byte[] data)>();
            List<(Mc3EDataPoint dp, byte[] data)> bitItems = new List<(Mc3EDataPoint dp, byte[] data)>();
            foreach (var dp0 in pvs)
            {
                Mc3EDataPoint dp = dp0.dp as Mc3EDataPoint;
                object value = dp0.value;
                switch (dp.DataType)
                {
                    case DataType.BIT:
                        bitItems.Add((dp, new byte[] { (bool)(value) ? (byte)1 : (byte)0 }));
                        break;
                    case DataType.BYTE:
                        break;
                    case DataType.INT16:
                    case DataType.UINT16:
                    case DataType.INT32:
                    case DataType.UINT32:
                    case DataType.SINGLE:
                    case DataType.DOUBLE:
                    case DataType.UTF32:
                    case DataType.ASCII:
                        byte[] d = ValueDecoder.Code(value, dp.DataType, (ushort)(dp.GetLength() * 2));
                        wordItems.Add((dp, d));
                        break;
                    default:
                        break;
                }
            }
            // 1. 构建读取报文
            byte[] request = mBuilder.BuildWrite(wordItems, bitItems);

            // 2. 发送并等待响应
            byte[] response = await mClient.WriteRequestAsync(request);//SendAndWaitAsync(request);
            ushort successCode = response.ToUInt16(9);
            if (successCode != 0)//读取失败
            {
                throw new Exception($"读取失败,获取异常代码：{successCode}");
            }
        }

        public override async Task WriteAsync(ICommunicationDataPoint dp, object value)
        {
            await WriteAsync((dp, value));
        }

        public DeviceValue DecodeValue(Mc3EDataPoint mp)
        {
            //if (!mChangedMge.IsChannelRunning)
            //{
            //    throw new Exception("自动读取没有开启，无法支持内存解码操作");
            //}
            switch (mp.DecodeData.Area)
            {
                case E_McDeviceCode.M:
                case E_McDeviceCode.X:
                case E_McDeviceCode.Y:
                case E_McDeviceCode.B:
                    return ((bool[])mValueGroup[mp.DecodeData.Area])[mp.DecodeData.Address];
                case E_McDeviceCode.D:
                case E_McDeviceCode.W:
                case E_McDeviceCode.R:
                    ReadOnlySpan<byte> bytes = ((byte[])mValueGroup[mp.DecodeData.Area]).AsSpan(mp.DecodeData.Address * 2, mp.GetLength() * 2);
                    return ValueDecoder.Decode(bytes,mp.DataType,0);
                default:
                    break;
            }
            return new DeviceValue();
        }

        /// <summary>
        /// 执行比较并将b1同步到b2
        /// b2,需要拆分
        /// </summary>
        /// <param name="bl"></param>
        /// <param name="b2"></param>
        private void ProcessData_Bool(E_McDeviceCode code, bool[] bl, int offest1, bool[] b2)
        {
            int gen = Interlocked.Increment(ref mGeneration);

            ReadOnlySpan<bool> bv1 = new ReadOnlySpan<bool>(bl, offest1, b2.Length);
            ReadOnlySpan<bool> bv2 = new ReadOnlySpan<bool>(b2);
            List<int> differentIndex = CommonUnilty.ArrayCompare_Ptr(bv1, bv2);
            Array.Copy(b2, 0, mValueGroup[code], offest1, b2.Length);

            foreach (var item in differentIndex)
            {
                int addr = offest1 + item;

                if (!mEventMap[code].TryGetValue(addr, out var points))
                    continue;
                foreach (var mEventMap in points)
                {
                    if (mEventMap.LastGeneration != gen)
                    {
                        DeviceValue val = this.DecodeValue(mEventMap);
                        mChangedMge.Enqueue(mEventMap.GetValChangeDel_Light(), val,E_CallBackWeight.Light);
                        mChangedMge.Enqueue(mEventMap.GetValChangeDel_Heavy(), val, E_CallBackWeight.Heavy);
                        mEventMap.LastGeneration = gen;
                    }
                }
            }
        }

        private void ProcessData_Byte(E_McDeviceCode code, byte[] bl, int offest1, byte[] b2, int offest2)
        {
            int gen = Interlocked.Increment(ref mGeneration);

            int length = b2.Length - offest2;
            ReadOnlySpan<byte> bv1 = new ReadOnlySpan<byte>(bl, offest1 * 2, length);
            ReadOnlySpan<byte> bv2 = new ReadOnlySpan<byte>(b2, offest2, length);
            List<int> differentIndex = CommonUnilty.ArrayCompare_Ptr(bv1, bv2);
            Array.Copy(b2, offest2, mValueGroup[code], offest1 * 2, length);

            foreach (var item in differentIndex)
            {
                //绑定的最小单位是ushort，所以触发回调时要除2
                int addr = offest1 + item / 2;

                if (!mEventMap[code].TryGetValue(addr, out var points))
                    continue;
                foreach (var mEventMap in points)
                {
                    if (mEventMap.LastGeneration != gen)
                    {
                        DeviceValue val = this.DecodeValue(mEventMap);
                        mChangedMge.Enqueue(mEventMap.GetValChangeDel_Light(), val, E_CallBackWeight.Light);
                        mChangedMge.Enqueue(mEventMap.GetValChangeDel_Heavy(), val, E_CallBackWeight.Heavy);
                        mEventMap.LastGeneration = gen;
                    }
                }
            }
        }

    }

    /// <summary>
    /// 收发代码生成工具
    /// </summary>
    sealed class Mc3EBinaryPacketBuilder
    {
        public class Mc3EComposerBuilder
        {
            // 配置项
            private byte[] SubHeader = { 0x50, 0x00 }; // 固定为 0x0050 (请求)
            public byte NetworkNo { get; set; } = 0x00;
            public byte PcNo { get; set; } = 0xFF;
            public byte[] IoNo { get; set; } = { 0xFF, 0x03 };// 0x03FF;
            public byte StationNo { get; set; } = 0x00;
            public byte[] MonitorTimer { get; set; } = { 0x10, 0x00 };//0x0010;
            private byte[] ReadCommd = { 0x01, 0x04 }; // 批量读块
            private byte[] WriteCommd = { 0x06, 0x14 }; // 随机写入

            /// <summary>
            /// 使用 Composer 自动生成读取报文
            /// 批量读取
            /// </summary>
            public byte[] BuildRead(E_McDeviceCode code, int startAddr, int points)
            {
                // 根据设备类型决定子命令
                bool isBit = IsBitDevice(code);
                byte[] subCmd = isBit ? new byte[] { 0x01,0x00 } : new byte[] { 0x00, 0x00 };

                var composer = new FrameComposer();

                // --- 1. 3E 帧头 (Header) ---
                composer.Add(new ConstBytesModule(SubHeader))                       // SubHeader (Req)
                        .Add(new ConstBytesModule(new byte[]{ NetworkNo }))         // Network
                        .Add(new ConstBytesModule(new byte[] { PcNo }))             // PC
                        .Add(new ConstBytesModule(IoNo))                            // IO
                        .Add(new ConstBytesModule(new byte[] { StationNo }));       // Station

                // --- 2. 长度自计算区域 ---
                // MC协议规定：长度字段 = 从 MonitorTimer 开始到最后的所有字节数
                // 定义：计算从 "DataStart" 到 "DataEnd" 的长度，填入2字节(小端字节序)
                composer.Add(new LengthModule("DataStart", "DataEnd", 2, true));    //Length

                // --- 3. 数据体 (Command Body) ---
                composer.Add(new MarkModule( MarkType.Length,"DataStart"))                           // <--- 长度计算起点

                        .Add(new ConstBytesModule(MonitorTimer))                    // Timer
                        .Add(new ConstBytesModule(ReadCommd))                       // Command (Batch Read)
                        .Add(new ConstBytesModule(subCmd))                          // SubCommand
                        .Add(new ConstBytesModule(startAddr.ToBytes(3)))            // Address (3 Bytes)
                        .Add(new ConstBytesModule(new byte[] { (byte)code }))       // Device Code
                        .Add(new ConstBytesModule(points.ToBytes(2)))               // Points

                        .Add(new MarkModule(MarkType.Length, "DataEnd"));                            // <--- 长度计算终点

                // --- 4. 生成 ---
                return composer.Build();
            }

            /// <summary>
            /// 使用 Composer 自动生成写入报文
            /// </summary>
            public byte[] BuildWrite(List<(Mc3EDataPoint dp, byte[] data)> wordItems, List<(Mc3EDataPoint dp, byte[] data)> bitItems)
            {
                if ((wordItems == null && bitItems == null) || 
                    (wordItems.Count == 0 && bitItems.Count == 0)) 
                    return null;

                // 检查数量上限 (根据实际PLC型号调整，这里假设一般限制)
                if (wordItems.Count + bitItems.Count > 192) throw new ArgumentException("随机写入点数过多，最多一次写入192字，请分包处理");


                // 1) 构建报文
                var composer = new FrameComposer();

                // --- Header ---
                composer.Add(new ConstBytesModule(SubHeader))
                        .Add(new ConstBytesModule(new byte[] { NetworkNo }))
                        .Add(new ConstBytesModule(new byte[] { PcNo }))
                        .Add(new ConstBytesModule(IoNo))
                        .Add(new ConstBytesModule(new byte[] { StationNo }));

                // --- Length ---
                composer.Add(new LengthModule("DataStart", "DataEnd", 2, true));

                // --- Body ---
                composer.Add(new MarkModule(MarkType.Length, "DataStart"))
                        .Add(new ConstBytesModule(MonitorTimer))
                        .Add(new ConstBytesModule(WriteCommd))                 // 0x1406
                        .Add(new ConstBytesModule(new byte[] { 0x00, 0x00 })) // SubCommand (一般为 0x0000)

                        //// 块数（按常见实现：1字节）
                        .Add(new ConstBytesModule(wordItems.Count.ToBytes(1)))           // Word block count
                        .Add(new ConstBytesModule(bitItems.Count.ToBytes(1)));           // Bit  block count

                // 3) 追加 Word blocks
                foreach (var it in wordItems)
                {
                    AddBlockToComposer(composer, it.dp, it.data);
                }

                // 4) 追加 Bit blocks
                foreach (var it in bitItems)
                {
                    AddBlockToComposer(composer, it.dp, it.data);
                }

                composer.Add(new MarkModule(MarkType.Length, "DataEnd"));

                return composer.Build();
            }

            /// <summary>
            /// 添加单个块
            /// </summary>
            private void AddBlockToComposer(FrameComposer composer, Mc3EDataPoint dp, byte[] data)
            {
                // 计算字数 (Word Count)
                // 假设 data.Length 已经是偶数 (2字节=1字)
                int wordCount = data.Length / 2;

                // 校验：1406 单块最大 255 字
                if (wordCount > 255)
                    throw new ArgumentException($"1406指令单块数据不能超过255个字 (Code:{dp.DecodeData.Area}, Addr:{dp.DecodeData.Address})");

                if (wordCount == 0) return;

                // 结构：[Code(1)] + [Address(3)] + [Point(1)] + [Data(N)]
                composer.Add(new ConstBytesModule(dp.DecodeData.Address.ToBytes(3)))        // 1. Address
                        .Add(new ConstBytesModule(new byte[] { (byte)dp.DecodeData.Area })) // 2. Code
                        .Add(new ConstBytesModule(wordCount.ToBytes(2)))          // 3. Point 
                        .Add(new ConstBytesModule(data));                                   // 4. Data
            }

            private bool IsBitDevice(E_McDeviceCode code)
            {
                return code == E_McDeviceCode.M || code == E_McDeviceCode.X || code == E_McDeviceCode.Y || code == E_McDeviceCode.B;
            }
        }
    }
}
