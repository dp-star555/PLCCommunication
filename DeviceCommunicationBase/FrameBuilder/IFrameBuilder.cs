using DeviceCommunicationBase;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase
{
    /// <summary>构建上下文：输出缓冲 + Mark 定位点</summary>
    public sealed class FrameBuildContext
    {
        private readonly List<byte> mBuffer = new List<byte>();
        private readonly Dictionary<string, int> mMarks = new Dictionary<string, int>();

        public int Position => mBuffer.Count;

        public IReadOnlyList<byte> Buffer => mBuffer;

        public byte[] ToArray() => mBuffer.ToArray();

        public void Mark(string name)
        {
            mMarks[name] = mBuffer.Count;
        }

        public int GetMark(string name)
        {
            if (!mMarks.TryGetValue(name, out var pos))
                throw new InvalidOperationException($"Mark not found: {name}");
            return pos;
        }

        public (byte[] Data, int Length) Slice(string fromMark, string toMark)
        {
            int s = GetMark(fromMark);
            int e = GetMark(toMark);
            if (e < s) throw new InvalidOperationException($"Invalid mark range: {fromMark}->{toMark}");
            int len = e - s;
            byte[] result = new byte[len];
            mBuffer.CopyTo(s, result, 0, len);
            return (result, len);
        }

        public int SliceLength(string fromMark, string toMark)
        {
            int s = GetMark(fromMark);
            int e = GetMark(toMark);
            if (e < s) throw new InvalidOperationException($"Invalid mark range: {fromMark}->{toMark}");
            return e - s;
        }

        public void WriteByte(byte v) => mBuffer.Add(v);

        public void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
                mBuffer.Add(bytes[i]);
        }

        public int Reserve(int count)
        {
            int pos = mBuffer.Count;
            for (int i = 0; i < count; i++)
                mBuffer.Add(0);
            return pos;
        }

        public void Patch(int pos, byte[] value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                mBuffer[pos + i] = value[i];
            }
        }
    }
    /// <summary>
    /// 构建器模组接口
    /// </summary>
    public interface IFrameModule
    {
        int Length { get; }                 // 模组长度，-1 表示不定长
        void Emit(FrameBuildContext ctx);   // 第一遍：顺序写（或占位 / Mark）
        void Fixup(FrameBuildContext ctx);  // 第二遍：回填（Length/CRC 用）
    }

    /// <summary>
    /// 标签模组：用于标记位置起始
    /// </summary>
    public sealed class MarkModule : IFrameModule
    {
        private readonly string mName;
        public MarkModule(string name) => mName = name;

        public int Length => 0;

        public void Emit(FrameBuildContext ctx) => ctx.Mark(mName);
        public void Fixup(FrameBuildContext ctx) { }
    }
    /// <summary>
    /// 固定的一些功能码
    /// </summary>
    public sealed class ConstBytesModule : IFrameModule
    {
        private readonly byte[] mBytes;
        public ConstBytesModule(byte[] bytes) => mBytes = bytes;

        public int Length => mBytes.Length;

        public void Emit(FrameBuildContext ctx) => ctx.WriteBytes(mBytes);
        public void Fixup(FrameBuildContext ctx) { }
    }
    /// <summary>
    /// 数据模组：用于写入可变长度的数据
    /// </summary>
    public sealed class VarBytesModule : IFrameModule
    {
        private readonly Func<byte[]> mGetBytes;

        public VarBytesModule(Func<byte[]> getBytes)
        {
            mGetBytes = getBytes;
        }

        public int Length => mGetBytes().Length;

        public void Emit(FrameBuildContext ctx)
        {
            ctx.WriteBytes(mGetBytes());
        }

        public void Fixup(FrameBuildContext ctx) { }
    }
    /// <summary>
    /// 长度模组：用于回填某段数据的长度
    /// </summary>
    public sealed class LengthModule : IFrameModule
    {
        private readonly string mFromMark;
        private readonly string mToMark;
        private readonly bool mLittleEndian;
        private int mPatchPos;
        private int mLength;

        public LengthModule(string fromMark, string toMark, int byteNum = 2, bool littleEndian = true)
        {
            mFromMark = fromMark;
            mToMark = toMark;
            mLength = byteNum;
            mLittleEndian = littleEndian;
        }

        public int Length => mLength;

        public void Emit(FrameBuildContext ctx)
        {
            mPatchPos = ctx.Reserve(Length);
        }

        public void Fixup(FrameBuildContext ctx)
        {
            var  length = ctx.SliceLength(mFromMark, mToMark);
            byte[] bytes = new byte[Length];
            switch (Length)
            {
                case 1:
                    bytes[0] = (byte)(length & 0xFF);
                    break;
                case 2:
                    if (mLittleEndian)
                    {
                        bytes[0] = (byte)(length & 0xFF);
                        bytes[1] = (byte)(length >> 8);
                    }
                    else
                    {
                        bytes[0] = (byte)(length >> 8);
                        bytes[1] = (byte)(length & 0xFF);
                    }
                    break;
                case 4:
                    if (mLittleEndian)
                    {
                        bytes[0] = (byte)(length & 0xFF);
                        bytes[1] = (byte)(length >> 8);
                        bytes[2] = (byte)(length >> 16);
                        bytes[3] = (byte)(length >> 24);
                    }
                    else
                    {
                        bytes[0] = (byte)(length >> 24);
                        bytes[1] = (byte)(length >> 16);
                        bytes[2] = (byte)(length >> 8);
                        bytes[3] = (byte)(length & 0xFF);
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Length overflow: {length}, range={mFromMark}->{mToMark}");
            }
            ctx.Patch(mPatchPos, bytes);
        }
    }

    /// <summary>
    /// 写入 24-bit 无符号整数（Little-Endian），占 3 字节：
    /// b0 = v[7:0], b1 = v[15:8], b2 = v[23:16]
    /// 适用于 MC 3E 协议的 Address 字段（3 bytes LE）。
    /// </summary>
    public sealed class U24LEModule : IFrameModule
    {
        private readonly uint _value;

        /// <summary>
        /// 使用 int 构造（会检查范围）。
        /// </summary>
        public U24LEModule(int value)
        {
            if (value < 0 || value > 0x00FF_FFFF)
                throw new ArgumentOutOfRangeException(nameof(value), "U24 value must be in range [0, 0x00FFFFFF].");
            _value = (uint)value;
        }

        /// <summary>
        /// 使用 uint 构造（会检查范围）。
        /// </summary>
        public U24LEModule(uint value)
        {
            if (value > 0x00FF_FFFF)
                throw new ArgumentOutOfRangeException(nameof(value), "U24 value must be in range [0, 0x00FFFFFF].");
            _value = value;
        }

        public int Length => 3;

        public void Emit(FrameBuildContext ctx)
        {
            // Little-Endian 3 bytes
            ctx.WriteByte((byte)(_value & 0xFF));
            ctx.WriteByte((byte)((_value >> 8) & 0xFF));
            ctx.WriteByte((byte)((_value >> 16) & 0xFF));
        }

        public void Fixup(FrameBuildContext ctx)
        {
        }
    }




    /// <summary>
    /// 用于校验的类型
    /// </summary>
    public enum CheckType
    {
        CRC16,
        SUM8,
        XOR8
    }

    /// <summary>
    /// 用于校验的模组，可以配置校验类型
    /// </summary>
    public sealed class CheckModule : IFrameModule
    {
        private readonly string mFromMark;
        private readonly string mToMark;
        private int mPatchPos;
        private int mLength;
        private CheckType mCheckType;

        public CheckModule(string fromMark, string toMark, CheckType checkType, int byteNum)
        {
            mFromMark = fromMark;
            mToMark = toMark;
            switch (checkType)
            {
                case CheckType.CRC16:
                    mLength = 2;
                    break;
                case CheckType.SUM8:
                    mLength = 1;
                    break;
                case CheckType.XOR8:
                    mLength = 1;
                    break;
                default:
                    mLength = byteNum;
                    break;
            }
        }
        public int Length => mLength;
        public void Emit(FrameBuildContext ctx)
        {
            mPatchPos = ctx.Reserve(Length);
        }
        public void Fixup(FrameBuildContext ctx)
        {
            var (data, length) = ctx.Slice(mFromMark, mToMark);
            ushort crc = 0;//校验的代码实现 Crc16.ComputeChecksum(span);
            byte[] bytes = new byte[2];
            bytes[0] = (byte)(crc & 0xFF);
            bytes[1] = (byte)(crc >> 8);
            ctx.Patch(mPatchPos, bytes);
        }
    }

    /// <summary>
    /// 构建器主体
    /// </summary>
    public sealed class FrameComposer
    {
        private readonly List<IFrameModule> mModules = new List<IFrameModule>();

        public FrameComposer Add(IFrameModule module)
        {
            mModules.Add(module);
            return this;
        }

        public byte[] Build()
        {
            var ctx = new FrameBuildContext();

            // 第1遍：写入
            foreach (var m in mModules)
                m.Emit(ctx);

            // 第2遍：回填
            foreach (var m in mModules)
                m.Fixup(ctx);

            return ctx.ToArray();
        }
    }

}
