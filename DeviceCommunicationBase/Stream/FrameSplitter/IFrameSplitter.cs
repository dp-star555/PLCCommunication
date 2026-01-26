using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    /// <summary>
    /// 从连续字节流中拆分出完整帧的通用接口
    /// </summary>
    public interface IFrameSplitter
    {
        /// <summary>
        /// 追加收到的数据（可能是半包、粘包）
        /// </summary>
        void Feed(ReadOnlySpan<byte> data);

        /// <summary>
        /// 每拆出一帧完整包时触发
        /// </summary>
        event Action<ReadOnlyMemory<byte>> FrameCompleted;

        /// <summary>
        /// 清空内部缓冲
        /// </summary>
        void Reset();
    }

    public class FrameSplitter: IFrameSplitter
    {

        /// <summary>
        /// 最大允许扩容次数，防止异常数据打爆内存
        /// 基数是1024,
        /// 次数 1 -> 2 -> 3
        /// 扩容 2048 -> 4096 - > 8192
        /// </summary>
        public int MaxExpandCapacityCount { get; } = 2;

        private readonly List<IFrameModule> mModules = new List<IFrameModule>();
        private readonly List<MaskBlock> mBlocks = new List<MaskBlock>();

        /// <summary>
        /// 模组是否有变更
        /// </summary>
        private bool mIsModuleDirty = false;

        public FrameSplitter Add(IFrameModule module)
        {
            mModules.Add(module);
            mIsModuleDirty = true;
            return this;
        }

        public event Action<ReadOnlyMemory<byte>> FrameCompleted;

        //private int mBufferCount = 0;
        private byte[]  mBuffer =new byte[1024];
        private int mWrite;

        public void Feed(ReadOnlySpan<byte> data)
        {
            //如果是模块更新过，则重新生成掩码块
            if (mIsModuleDirty)
            {
                BlockCompile();
            }

            if (data.IsEmpty) return;
            //拷贝数据至本机内存
            EnsureCapacity(mWrite + data.Length);
            data.CopyTo(mBuffer.AsSpan(mWrite));
            mWrite += data.Length;

            while (mBuffer.Length > 0)
            {
                // 1. 创建扫描上下文 (用 Span 高效扫描)
                // 注意：这里只给它看，不让它改 Buffer
                var ctx = new FrameDecodeContext(mBuffer);
                bool frameFound = false;

                try
                {
                    // 2. 顺序遍历 Module 列表
                    foreach (var module in mModules)
                    {
                        module.Scan(ctx); // 每个 Module 只负责移动 ctx.Position
                    }

                    // 3. 如果循环走完了，说明匹配成功！
                    frameFound = true;
                    int totalLen = ctx.Position; // 这就是一帧的总长

                    // 4. 【数据分割】
                    byte[] frame = mBuffer.GetRange(0, totalLen).ToArray();
                    mBuffer.RemoveRange(0, totalLen); // 从缓冲移除

                    // 5. 抛出整包 (后续再由 Parser 做 Decode)
                    FrameCompleted?.Invoke(frame);
                }
                catch (ProtocolMismatchException)
                {
                    // 遇见 Const 不匹配 -> 说明开头是垃圾 -> 滑动窗口
                    mBuffer.RemoveAt(0);
                    continue;
                }
                catch (DataNotEnoughException)
                {
                    // 遇见 Length 或 Body 数据不够 -> 等待下一波
                    break;
                }
            }
        }
        public void Reset()
        {
            throw new NotImplementedException();
        }

        private void EnsureCapacity(int required)
        {
            if (required <= mBuffer.Length) return;

            int newSize = mBuffer.Length * 2;
            while (newSize < required) newSize *= 2;
            Array.Resize(ref mBuffer, newSize);
        }


        void BlockCompile()
        {
            mBlocks.Clear();

            ushort lengthNum = 0;
            bool isStartMark = false;

            foreach (var mod in mModules)
            {
                if (isStartMark)
                {
                    switch (mod)
                    {
                        case LengthModule lengthMod:
                            lengthNum = (ushort)lengthMod.Length;
                            continue;
                        case MarkModule markMod:
                            //找到Length的结尾标记
                            if ( mod is MarkModule mark2 && mark2.MarkType == MarkType.Length)
                            {
                                if (mBlocks.Last() is FixBlock lastBlock)
                                {
                                    lastBlock.Length = lengthNum;//将提前的数据长度写入当前空洞块
                                }
                                isStartMark = false;
                            }
                            continue;
                        default:
                            continue;
                    }
                }

                // 1. 获取模块特征
                byte[] constBytes = mod.GetConstData();
                int modLen = mod.Length;

                switch (mod)
                {
                    case ConstBytesModule constMod:
                        //连续的块自动融合
                        if (mBlocks.Count > 0 && mBlocks.Last() is SolidBlock lastSolid)
                        {
                            lastSolid.Length += (ushort)modLen;
                            lastSolid.Bytes.AddRange(constBytes);
                        }
                        else
                        {
                            mBlocks.Add(new SolidBlock { Bytes = constBytes.ToList() });
                        }
                        continue;
                    case LengthModule lengthMod:
                        lengthNum = (ushort)lengthMod.Length;
                        //连续的块自动融合
                        if (mBlocks.Count > 0 && mBlocks.Last() is FixBlock lastFixed)
                        {
                            lastFixed.Length += (ushort)modLen;
                        }
                        else
                        {
                            mBlocks.Add(new FixBlock { Length = (ushort)modLen });
                        }
                        continue;
                    case CheckModule checkMod:
                        //连续的块自动融合
                        if (mBlocks.Count > 0 && mBlocks.Last() is FixBlock lastFixed2)
                        {
                            lastFixed2.Length += (ushort)modLen;
                        }
                        else
                        {
                            mBlocks.Add(new FixBlock { Length = (ushort)modLen });
                        }
                        continue;
                    case MarkModule markMod:
                        //没有找到Length标记前，忽略其他标记模块
                        if (!isStartMark && mod is MarkModule mark && mark.MarkType == MarkType.Length)
                        {
                            isStartMark = true;
                            mBlocks.Add(new FixBlock { Length = 0 });//添加一个定长空洞块（有长度计数的空洞块永远都是定长的）
                            continue;
                        }
                        continue;
                    case VarBytesModule varMod:
                        mBlocks.Add(new VoidBlock());//添加一个不定长空洞块
                        continue;
                    default:
                        break;
                }
            }

        }

        void MatchLoop()
        {
            // 相对于 mBuffer[0] 的位置
            int cursor = 0;
            while (cursor < mWrite)
            {
                // 计算当前剩余的有效数据长度
                // 总有效量 - 当前游标 = 剩余待处理量
                int remainingLen = mWrite - cursor;

                int anchorIdx = mBuffer.FindBytes(mPivot,cursor, remainingLen);

                // 1. 快速定位主锚点
                int anchorIdx = mBuffer.FindBytes(mPivot);
                if (anchorIdx < 0) { /* Wait logic... */ break; }

                // 2. 倒推帧头
                int frameStart = anchorIdx - mPivotOffset;
                if (frameStart < 0)
                {
                    mBuffer.RemoveRange(0, anchorIndex + 1); // 错位了，滑动
                    continue;
                }

                // 3. 走地图校验
                var result = VerifyFrame(frameStart);

                if (result.IsMatch)
                {
                    // 切包...
                    byte[] frame = _buffer.GetRange(frameStart, result.TotalLength).ToArray();
                    _buffer.RemoveRange(0, frameStart + result.TotalLength);
                    // OnFrame(frame);
                }
                else if (result.IsWaiting)
                {
                    break; // 数据不够
                }
                else
                {
                    // 校验失败，滑动窗口
                    _buffer.RemoveRange(0, anchorIndex + 1);
                }
            }
        }
    }
}
