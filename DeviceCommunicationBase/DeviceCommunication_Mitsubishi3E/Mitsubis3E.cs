using CommunicationBase;
using DeviceCommunicationBase.FrameSplitter;
using HPSocket;
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
using static DeviceCommunicationBase.DeviceCommunication_Mitsubishi3E.Mc3EBinaryPacketBuilder;

namespace DeviceCommunicationBase.DeviceCommunication_Mitsubishi3E
{
    /// <summary>
    /// MC 设备码（3E Binary）
    /// </summary>
    public enum McDeviceCode : byte
    {
        // bit
        M = 0x90,
        X = 0x9C,
        Y = 0x9D,
        B = 0xA0,
        // word
        D = 0xA8,
        W = 0xB4,
        R = 0xAF,
    }

    /// <summary>
    /// 解析后的数据
    /// </summary>
    public class MCDecodeData
    {
        public McDeviceCode Area;
        public ushort Address;
    }

    /// <summary>
    /// MC 链接地址数据
    /// </summary>
    public class Mc3EDataPoint : ICommunicationDataPoint
    {
        /// <summary>
        /// 数据点回调
        /// </summary>
        ValueChangedDelegate valueChanged;

        DeviceCommunication mPanel;

        public DeviceCommunication Panel { set { mPanel = value; } }
        public string Name { get; set; } = string.Empty;
        public MCDecodeData DecodeData { get; set; } = new MCDecodeData();
        public string Input { get; set; }

        public DataType DataType { get; set; }

        public int StrLength { get; set; }

        public ushort GetLength()
        {
            switch (DataType)
            {
                case DataType.BIT:
                case DataType.INT16:
                case DataType.UINT16:
                    return 1;
                case DataType.INT32:
                case DataType.UINT32:
                case DataType.SINGLE:
                    return 2;
                case DataType.DOUBLE:
                    return 4;
                case DataType.UTF32:
                    return (ushort)(StrLength * 2);
                case DataType.ASCII:
                    return (ushort)((StrLength + 2 - 1) / 2);
                default:
                    throw new Exception("当前类型无法对应长度");
            }
        }

        public int LastGeneration { get; set; }

        public event ValueChangedDelegate OnValueChanged
        {
            add
            {
                //防止重复添加
                if (valueChanged != null && valueChanged.GetInvocationList().Contains(value))
                {
                    return;
                }
                valueChanged += value;
            }
            remove
            {
                valueChanged -= value;
            }
        }

        public ValueChangedDelegate GetValChangeDel()
        {
            return valueChanged;
        }

        public DeviceValue GetValue()
        {
            //串行读取
            return ((Mitsubis3E_Device)mPanel).DecodeValue(this);
        }

        public void SetValue(object val)
        {
            ((Mitsubis3E_Device)mPanel).Write(this, val);
        }
    }

    /// <summary>
    /// 数据块描述
    /// </summary>
    public class AddressBlock
    {
        public int StartAddress { get; set; }
        public int Length { get; set; }

        public int EndAddress => StartAddress + Length - 1;
    }

    public class Mitsubis3E_Device : DeviceCommunication
    {
        public Mitsubis3E_Device()
        {
            // 当拆出一帧完整包时：交付给等待方
            _splitter.FrameCompleted += frame =>
            {
                TaskCompletionSource<byte[]> tcs = null;

                //_pendingTcs = null; // 半双工：一次只等一个响应
                tcs = mCurrentTcs;

                // 无人等待：可能是超时后的迟到包，直接丢弃
                if (tcs == null) return;

                tcs.TrySetResult(frame.ToArray());
            };
            mClient.FrameSplitter = _splitter;
            mClient.OnDisconnect += (sender) =>
            {
                // 如果连接断开了，通知等待的任务抛出异常
                mCurrentTcs?.TrySetException(new Exception("Socket disconnected unexpectedly."));
            };
        }

        // SLMP 协议限制：单个块或单次读取通常最大不超过 960 个字 (视具体PLC型号和指令而定，这里保守设为 960)
        const int MAX_BLOCK_SIZE = 960;
        const int MAX_BLOCK_SIZE_BOOL = 960 * 16;

        HPSocketPort_Client mClient = new HPSocketPort_Client();

        private readonly Mc3EBinaryFrameSplitter _splitter = new Mc3EBinaryFrameSplitter( maxFrameLength: 4096);

        private Dictionary<string, ICommunicationDataPoint> mNameIndex = new Dictionary<string, ICommunicationDataPoint>();

        /// <summary>
        /// 数据集合
        /// </summary>
        Dictionary<McDeviceCode, Array> mValueGroup = new Dictionary<McDeviceCode, Array>();
        /// <summary>
        /// 输入的原始数据
        /// </summary>
        Dictionary<McDeviceCode, List<int>> mReadData_Base = new Dictionary<McDeviceCode, List<int>>();
        /// <summary>
        /// 用于回调进行索引的字典
        /// </summary>
        Dictionary<McDeviceCode, Dictionary<int, List<Mc3EDataPoint>>> mEventMap = new Dictionary<McDeviceCode, Dictionary<int, List<Mc3EDataPoint>>>();
        /// <summary>
        /// 用于排序后的读取列表
        /// </summary>
        Dictionary<McDeviceCode, List<AddressBlock>> mReadList = new Dictionary<McDeviceCode, List<AddressBlock>>();
        /// <summary>
        /// 允许的连续空位
        /// </summary>
        public ushort HoleThreshold { get; set; } = 480;
        /// <summary>
        /// 初始化数据数组长度
        /// </summary>
        /// <param name="code"></param>
        /// <param name="num"></param>
        public void ConfigValueArray(McDeviceCode code, ushort num)
        {
            if (!mValueGroup.ContainsKey(code))
            {
                switch (code)
                {
                    case McDeviceCode.M:
                    case McDeviceCode.X:
                    case McDeviceCode.Y:
                        mValueGroup.Add(code, new bool[num]);
                        break;
                    case McDeviceCode.D:
                    case McDeviceCode.R:
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

        public override bool CanAutoRead { get; set; } = true;

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
        static readonly Regex s_addrRegex = new Regex(@"^(?<Type>[A-Za-z]{1,2})(?<Addr>[0-9A-Fa-f]+)(?<Suffix>H)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public void AddDataPoint(ICommunicationDataPoint point)
        {
            Mc3EDataPoint dp = point as Mc3EDataPoint;
            if (dp == null)
            {
                throw new Exception($"{point.Name}:{point.Input} 地址不是Mc3EDataPoint类似，无法进行转换。");
            }

            // 正则格式校验与拆分
            var match = s_addrRegex.Match(dp.Input);
            if (!match.Success)
            {
                throw new FormatException($"地址格式错误: {point.Name}:{point.Input}。应为 '字母+数字' 格式。");
            }

            string typeStr = match.Groups["Type"].Value;
            string addrStr = match.Groups["Addr"].Value;
            bool hasHexSuffix = match.Groups["Suffix"].Success; // 检查是否有 H 后缀
            int addrNum = dp.GetLength();

            McDeviceCode dc;


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
                McDeviceCode code = areaKvp.Key;
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
                        case McDeviceCode.M:
                        case McDeviceCode.X:
                        case McDeviceCode.Y:
                        case McDeviceCode.B:
                            isConsecutive = (currentAddr <= prevAddr + HoleThreshold * 4);
                            isFull = currentBlock.Length >= MAX_BLOCK_SIZE_BOOL;
                            break;
                        case McDeviceCode.D:
                        case McDeviceCode.W:
                        case McDeviceCode.R:
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

        // 用于生成并匹配请求ID (虽然MC协议本身不像Modbus有明显TxId，但我们可以通过队列保证顺序)
        // 这里的实现假设是半双工模式（发一个等一个），这是最安全的做法。
        private readonly SemaphoreSlim mLock = new SemaphoreSlim(1, 1);
        private TaskCompletionSource<byte[]> mCurrentTcs;
        public override async Task ReadAsync(CancellationToken ct = default)
        {
            // 遍历所有已分类和排序的读取块
            foreach (var kvp in mReadList)
            {
                McDeviceCode code = kvp.Key;
                List<AddressBlock> blocks = kvp.Value;

                foreach (var block in blocks)
                {
                    // 1. 构建读取报文
                    byte[] request = mBuilder.BuildRead(code, block.StartAddress, block.Length);

                    // 2. 发送并等待响应
                    try
                    {
                        byte[] response = await SendAndWaitAsync(request);
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
                                case McDeviceCode.M:
                                case McDeviceCode.X:
                                case McDeviceCode.Y:
                                case McDeviceCode.B:
                                    byte[] bv = new byte[response.Length - 11];
                                    Array.Copy(response, 11, bv, 0, bv.Length);
                                    bool[] values = bv.ToHexBoolsUnsafe();
                                    //进行比较,复制，触发变更
                                    ProcessData_Bool(code,(bool[])mValueGroup[code], block.StartAddress, values);
                                    break;
                                case McDeviceCode.D:
                                case McDeviceCode.W:
                                case McDeviceCode.R:
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
        int mGeneration;

        /// <summary>
        /// 执行比较并将b1同步到b2
        /// b2,需要拆分
        /// </summary>
        /// <param name="bl"></param>
        /// <param name="b2"></param>
        void ProcessData_Bool(McDeviceCode code, bool[] bl,int offest1, bool[] b2) 
        {
            int gen = Interlocked.Increment(ref mGeneration);

            ReadOnlySpan<bool> bv1 = new ReadOnlySpan<bool>(bl, offest1,b2.Length);
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
                        Enqueue(mEventMap.GetValChangeDel(), val);
                        mEventMap.LastGeneration = gen;
                    }
                }
            }
        }

        void ProcessData_Byte(McDeviceCode code, byte[] bl, int offest1, byte[] b2, int offest2)
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
                int addr = offest1 + item  / 2;

                if (!mEventMap[code].TryGetValue(addr, out var points))
                    continue;
                foreach (var mEventMap in points)
                {
                    if (mEventMap.LastGeneration != gen)
                    {
                        DeviceValue val = this.DecodeValue(mEventMap);
                        Enqueue(mEventMap.GetValChangeDel(), val);
                        mEventMap.LastGeneration = gen;
                    }
                }
            }
        }

        // 用于生成请求报文
        Mc3EComposerBuilder mBuilder = new Mc3EComposerBuilder();

        public override async Task<DeviceValue> ReadAsync(ICommunicationDataPoint dp, CancellationToken ct = default)
        {
            //手动读取单个点，不进行自动回调，与数据更新

            Mc3EDataPoint mp = dp as Mc3EDataPoint;
            McDeviceCode code = mp.DecodeData.Area;

            int startAddr = mp.DecodeData.Address;

            int readLength = mp.GetLength();

            // 1. 构建读取报文
            byte[] request = mBuilder.BuildRead(code, startAddr, readLength);

            // 2. 发送并等待响应
            byte[] response = await SendAndWaitAsync(request);
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
                    case McDeviceCode.M:
                    case McDeviceCode.X:
                    case McDeviceCode.Y:
                    case McDeviceCode.B:
                        byte[] bv = new byte[response.Length - 11];
                        Array.Copy(response, 11, bv, 0, bv.Length);
                        bool[] values = bv.ToHexBoolsUnsafe();
                        //进行比较,复制，触发变更
                        ProcessData_Bool(code, (bool[])mValueGroup[code], startAddr, values);
                        break;
                    case McDeviceCode.D:
                    case McDeviceCode.W:
                    case McDeviceCode.R:
                        ProcessData_Byte(code, (byte[])mValueGroup[code], startAddr, response, 11);
                        break;
                    default:
                        break;
                }
            }

            return dp.GetValue();
        }

        public override void Write(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            throw new NotImplementedException();
        }

        public override void Write(ICommunicationDataPoint dp, object value)
        {
            throw new NotImplementedException();
        }

        public override async Task WriteAsync(params (ICommunicationDataPoint dp, object value)[] pvs)
        {
            List<(Mc3EDataPoint dp, byte[] data)> wordItems = new List<(Mc3EDataPoint dp, byte[] data)>() ;
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
            try
            {
                byte[] response = await SendAndWaitAsync(request);
                ushort successCode = response.ToUInt16(9);
                if (successCode != 0)//读取失败
                {
                    throw new Exception($"获取异常代码：{successCode}");
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

        public override Task WriteAsync(ICommunicationDataPoint dp, object value)
        {
            throw new NotImplementedException();
        }

        public DeviceValue DecodeValue(Mc3EDataPoint mp)
        {
            if (!IsChannelRunning)
            {
                throw new Exception("自动读取没有开启，无法支持内存解码操作");
            }
            switch (mp.DecodeData.Area)
            {
                case McDeviceCode.M:
                case McDeviceCode.X:
                case McDeviceCode.Y:
                case McDeviceCode.B:
                    return new DeviceValue()
                    {
                        BOOL = ((bool[])mValueGroup[mp.DecodeData.Area])[mp.DecodeData.Address]
                    };
                case McDeviceCode.D:
                case McDeviceCode.W:
                case McDeviceCode.R:
                    ReadOnlySpan<byte> bytes = ((byte[])mValueGroup[mp.DecodeData.Area]).AsSpan(mp.DecodeData.Address * 2, mp.GetLength() * 2);
                    
                    return ValueDecoder.Decode(bytes,mp.DataType,0);
                default:
                    break;
            }
            return new DeviceValue();
        }

        /// <summary>
        /// 发送数据并异步等待回复
        /// </summary>
        /// <param name="request">请求报文</param>
        /// <param name="timeoutMs">超时时间(毫秒)</param>
        /// <returns>回复报文</returns>
        private async Task<byte[]> SendAndWaitAsync(byte[] request, int timeoutMs = 3000)
        {
            // 1. 进入锁 (异步等待锁，不阻塞线程)
            await mLock.WaitAsync();

            try
            {
                // 2. 初始化 TCS
                // RunContinuationsAsynchronously 强制后续代码在线程池运行，防止阻塞 HPSocket 的回调线程
                mCurrentTcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

                // 3. 发送数据
                if (!mClient.Write(request))
                {
                    throw new Exception("Send data failed.");
                }

                // 4. 等待结果 OR 超时
                // 创建一个超时任务
                Task timeoutTask = Task.Delay(timeoutMs);

                // 等待 TCS 完成 或者 超时任务 完成 (看谁先完成)
                Task completedTask = await Task.WhenAny(mCurrentTcs.Task, timeoutTask);

                // 5. 判断结果
                if (completedTask == timeoutTask)
                {
                    // 如果是超时任务先完成，说明超时了
                    mCurrentTcs.TrySetCanceled(); // 取消 TCS
                    throw new TimeoutException($"Request timed out after {timeoutMs}ms");
                }

                // 如果是 TCS 先完成，获取结果
                return await mCurrentTcs.Task;
            }
            finally
            {
                // 6. 清理现场并释放锁 (非常重要，否则下次请求会死锁)
                mCurrentTcs = null;
                mLock.Release();
            }
        }

    }

    public sealed class Mc3EBinaryPacketBuilder
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
            public byte[] BuildRead(McDeviceCode code, int startAddr, int points)
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
                composer.Add(new MarkModule("DataStart"))                           // <--- 长度计算起点

                        .Add(new ConstBytesModule(MonitorTimer))                    // Timer
                        .Add(new ConstBytesModule(ReadCommd))                       // Command (Batch Read)
                        .Add(new ConstBytesModule(subCmd))                          // SubCommand
                        .Add(new ConstBytesModule(startAddr.ToBytes(3)))            // Address (3 Bytes)
                        .Add(new ConstBytesModule(new byte[] { (byte)code }))       // Device Code
                        .Add(new ConstBytesModule(points.ToBytes(2)))               // Points

                        .Add(new MarkModule("DataEnd"));                            // <--- 长度计算终点

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
                composer.Add(new MarkModule("DataStart"))
                        .Add(new ConstBytesModule(MonitorTimer))
                        .Add(new ConstBytesModule(WriteCommd))                 // 0x1406
                        .Add(new ConstBytesModule(new byte[] { 0x00, 0x00 }))  // SubCommand (一般为 0x0000)

                        // 块数（按常见实现：1字节）
                        .Add(new ConstBytesModule(wordItems.Count.ToBytes(2)))           // Word block count
                        .Add(new ConstBytesModule(bitItems.Count.ToBytes(2)));           // Bit  block count

                // 3) 追加 Word blocks
                foreach (var it in wordItems)
                {
                    composer.Add(new ConstBytesModule(it.dp.DecodeData.Address.ToBytes(3)))           // Address (3 bytes LE)
                            .Add(new ConstBytesModule(new byte[] { (byte)it.dp.DecodeData.Area })) // Device code(1)
                            .Add(new ConstBytesModule(it.dp.GetLength().ToBytes(2)))
                            .Add(new ConstBytesModule(it.data));                     // Data (2)
                }

                // 4) 追加 Bit blocks
                foreach (var it in bitItems)
                {
                    composer.Add(new ConstBytesModule(it.dp.DecodeData.Address.ToBytes(3)))
                            .Add(new ConstBytesModule(new byte[] { (byte)it.dp.DecodeData.Area }))
                            .Add(new ConstBytesModule(it.dp.GetLength().ToBytes(2)))
                            .Add(new ConstBytesModule(it.data));                     // Data (each bit=1 byte 00/01)
                }

                composer.Add(new MarkModule("DataEnd"));

                return composer.Build();
            }



            private bool IsBitDevice(McDeviceCode code)
            {
                return code == McDeviceCode.M || code == McDeviceCode.X || code == McDeviceCode.Y || code == McDeviceCode.B;
            }
        }
    }
}
