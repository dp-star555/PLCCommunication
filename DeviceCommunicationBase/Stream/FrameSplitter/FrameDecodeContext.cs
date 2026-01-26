using ImTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceCommunicationBase.Stream
{
    public class FrameDecodeContext
    {
        private readonly byte[] mData;
        private int mPosition;

        // 所有的变量存储（长度、校验结果等）
        private readonly Dictionary<string, object> mVars = new Dictionary<string, object>();

        public FrameDecodeContext(byte[] data)
        {
            mData = data;
            mPosition = 0;
        }

        public int StartIndex { get; set; } = 0; // 数据起始位置

        public bool IsFirstConst => false; //是否以及经过第一个常量定位

        public int Position => mPosition; // 当前消耗了多少字节

        // --- 核心改造：试读 ---
        public byte[] ReadBytes(int count)
        {
            // 如果数据不够，抛出专用异常，通知 Splitter 等待
            if (mPosition + count > mData.Length)
                throw new DataNotEnoughException();

            byte[] res = new byte[count];
            Array.Copy(mData, mPosition, res, 0, count);
            mPosition += count;
            return res;
        }

        // ... SetVar, GetVar, Mark 等逻辑保持不变 ...
        public void SetVar(string name, object val) => mVars[name] = val;
        public T GetVar<T>(string name)
        {
            if (mVars.TryGetValue(name, out var v)) return (T)Convert.ChangeType(v, typeof(T));
            return default;
        }

        public int GetIndex(ReadOnlySpan<byte> pattern) 
        {
            var span = new ReadOnlySpan<byte>(mData, mPosition, mData.Length - mPosition);
            int rel = span.IndexOf(pattern);
            return rel < 0 ? -1 : mPosition + rel;
        }
    }

    // 定义两个核心异常
    public class DataNotEnoughException : Exception { }
    public class ProtocolMismatchException : Exception { }
}
