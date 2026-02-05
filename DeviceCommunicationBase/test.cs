using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace DeviceCommunicationBase
{
    public static class IntEx
    {
        public enum Endian
        {
            Little,
            Big
        }

        /// <summary>
        /// 将 int 转为指定字节数的字节数组（支持大小端）
        /// </summary>
        /// <param name="value">源整数</param>
        /// <param name="byteCount">字节数：1 / 2 / 3 / 4</param>
        /// <param name="endian">字节序是否是小端</param>
        public static byte[] ToBytes(
            this int value,
            int byteCount,
            bool isLE = true)
        {
            if (byteCount < 1 || byteCount > 4)
                throw new ArgumentOutOfRangeException(nameof(byteCount), "byteCount must be between 1 and 4.");

            // 范围校验（防止高位被静默截断）
            int maxValue = (byteCount == 4) ? int.MaxValue : (1 << (byteCount * 8)) - 1;
            if (value < 0 || value > maxValue)
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"Value {value} exceeds range for {byteCount} bytes.");

            var bytes = new byte[byteCount];

            if (isLE)
            {
                for (int i = 0; i < byteCount; i++)
                    bytes[i] = (byte)((value >> (8 * i)) & 0xFF);
            }
            else
            {
                for (int i = 0; i < byteCount; i++)
                    bytes[byteCount - 1 - i] = (byte)((value >> (8 * i)) & 0xFF);
            }

            return bytes;
        }

        /// <summary>
        /// 将 int 转为指定字节数的字节数组（支持大小端）
        /// </summary>
        /// <param name="value">源整数</param>
        /// <param name="byteCount">字节数：1 / 2 / 3 / 4</param>
        /// <param name="endian">字节序是否是小端</param>
        public static byte[] ToBytes(
            this ushort value,
            ushort byteCount,
            bool isLE = true)
        {
            if (byteCount < 1 )
                throw new ArgumentOutOfRangeException(nameof(byteCount), "byteCount must be between 1 and 4.");

            // 范围校验（防止高位被静默截断）
            int maxValue = (byteCount == 2) ? ushort.MaxValue : (1 << (byteCount * 8)) - 1;
            if (value < 0 || value > maxValue)
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"Value {value} exceeds range for {byteCount} bytes.");

            var bytes = new byte[byteCount];

            if (isLE)
            {
                for (int i = 0; i < byteCount; i++)
                    bytes[i] = (byte)((value >> (8 * i)) & 0xFF);
            }
            else
            {
                for (int i = 0; i < byteCount; i++)
                    bytes[byteCount - 1 - i] = (byte)((value >> (8 * i)) & 0xFF);
            }

            return bytes;
        }

        public static ushort ToUInt16(this byte[] value, int startIndex, bool isLE = true)
        {
            // 1. 边界检查
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (startIndex < 0 || startIndex + 2 > value.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex), "数组长度不足或索引越界");

            // 2. 转换逻辑
            if (isLE)
            {
                // 小端 Little Endian: 低字节在前 (Low Byte First)
                return (ushort)(value[startIndex] | (value[startIndex + 1] << 8));
            }
            else
            {
                // 大端 Big Endian: 高字节在前 (High Byte First)
                return (ushort)((value[startIndex] << 8) | value[startIndex + 1]);
            }
        }
        public static ushort ToUInt16(this byte[] value, bool isLE = true)
        {
            return ToUInt16(value, 0, isLE);
        }

        public static uint ToUInt(this ReadOnlySpan<byte> value, int startIndex, int byteCount, bool isLE = true)
        {
            if (byteCount < 1 || byteCount > 4)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            if (startIndex < 0 || startIndex + byteCount > value.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            uint result = 0;

            if (isLE)
            {
                for (int i = 0; i < byteCount; i++)
                    result |= (uint)value[startIndex + i] << (8 * i);
            }
            else
            {
                for (int i = 0; i < byteCount; i++)
                    result = (result << 8) | value[startIndex + i];
            }

            return result;
        }
        public static uint ToUInt(this ReadOnlySpan<byte> value,  bool isLE = true)
        {
            return value.ToUInt(0, value.Length,isLE);

        }


        /// <summary>
        /// 将Byte数组转为Bool数组
        /// 逻辑：Hex显示的 1为True，0为False
        /// 例如：0x10 -> [true, false], 0x01 -> [false, true]
        /// </summary>
        public static bool[] ToHexBoolsUnsafe(this byte[] data)
        {
            if (data == null || data.Length == 0) return Array.Empty<bool>();

            int count = data.Length;
            // 1. 预分配内存，避免动态扩容
            bool[] result = new bool[count * 2];

            unsafe
            {
                // 2. 防止GC移动内存，获取原始指针
                fixed (byte* pSource = data)
                fixed (bool* pDest = result)
                {
                    byte* ptrSrc = pSource;
                    bool* ptrDest = pDest;

                    // 3. 指针遍历
                    for (int i = 0; i < count; i++)
                    {
                        // 逻辑：检查高4位是否为1 (对应Hex左边那位)
                        // 0x10 是二进制 0001 0000
                        *ptrDest = (*ptrSrc & 0x10) != 0;

                        // 逻辑：检查低4位是否为1 (对应Hex右边那位)
                        // 0x01 是二进制 0000 0001
                        *(ptrDest + 1) = (*ptrSrc & 0x01) != 0;

                        // 指针移动
                        ptrSrc++;      // 源指针移动 1 byte
                        ptrDest += 2;  // 目标指针移动 2 bools (2 bytes)
                    }
                }
            }

            return result;
        }

        public static int FindBytes(this byte[] src, byte[] pattern, int start = 0,int dataCount = -1)
        {
            //边界检查
            if (src == null || pattern == null || pattern.Length == 0) return -1;
            if (dataCount < pattern.Length) return -1; // 有效数据还没特征码长，肯定找不到

            // 创建切片 (Span)
            ReadOnlySpan<byte> searchSpace = dataCount < 0 ? src.AsSpan(start): src.AsSpan(start, dataCount);
            ReadOnlySpan<byte> target = pattern.AsSpan();

            int relativeIndex = searchSpace.IndexOf(target);
            // 转换回绝对索引
            return relativeIndex == -1 ? -1 : start + relativeIndex;
        }

    }
    public static class CommonUnilty
    {

        public static unsafe void ArrayCompare_OneByOne(byte[] b1, byte[] b2, out List<int> discrpantIndex)
        {
            if (b1 == null || b2 == null)
            {
                throw new Exception("输入的两个数组不能为空。");
            }
            if (b1.Length != b2.Length)
            {
                throw new Exception("输入的两个数组个数不相等。");
            }
            discrpantIndex = new List<int>();
            for (int j = 0; j < b1.Length; j++)
            {
                if (b1[j] != b2[j])
                {
                    discrpantIndex.Add(j);
                }
            }
        }

        public static unsafe void ArrayCompare_OneByOne(bool[] b1, bool[] b2, out List<int> discrpantIndex)
        {
            if (b1 == null || b2 == null)
            {
                throw new Exception("输入的两个数组不能为空。");
            }
            if (b1.Length != b2.Length)
            {
                throw new Exception("输入的两个数组个数不相等。");
            }
            discrpantIndex = new List<int>();
            for (int j = 0; j < b1.Length; j++)
            {
                if (b1[j] != b2[j])
                {
                    discrpantIndex.Add(j);
                }
            }
        }


        public static unsafe List<int> ArrayCompare_Ptr(ReadOnlySpan<ushort> b1, ReadOnlySpan<ushort> b2)
        {
            if (b1 == null || b2 == null)
            {
                throw new Exception("输入的两个数组不能为空。");
            }
            if (b1.Length != b2.Length)
            {
                throw new Exception("输入的两个数组个数不相等。");
            }
            List<int> discrpantIndex = new List<int>();
            fixed (ushort* b1_Ptr = b1, b2_Ptr = b2)
            {
                ArrayCompare_64(b1_Ptr, b2_Ptr, b1.Length, 0, discrpantIndex);
            }
            return discrpantIndex;
        }

        public static unsafe List<int> ArrayCompare_Ptr(ReadOnlySpan<byte> b1, ReadOnlySpan<byte> b2)
        {
            if (b1.Length == 0 || b2.Length == 0)
            {
                throw new Exception("输入的两个数组不能为空。");
            }
            if (b1.Length != b2.Length)
            {
                throw new Exception("输入的两个数组个数不相等。");
            }
            List<int> discrpantIndex = new List<int>();
            fixed (byte* b1_Ptr = b1, b2_Ptr = b2)
            {
                ArrayCompare_64(b1_Ptr, b2_Ptr, b1.Length, 0, discrpantIndex);
            }
            return discrpantIndex;
        }

        public static unsafe List<int> ArrayCompare_Ptr(ReadOnlySpan<bool> b1, ReadOnlySpan<bool> b2)
        {
            if (b1 == null || b2 == null)
            {
                throw new Exception("输入的两个数组不能为空。");
            }
            if (b1.Length != b2.Length)
            {
                throw new Exception("输入的两个数组个数不相等。");
            }
            List<int> discrpantIndex = new List<int>();
            //bool数组在实际存储中：每个bool占用1个字节
            fixed (bool* b1_Ptr = b1, b2_Ptr = b2)
            {
                ArrayCompare_64((byte*)b1_Ptr, (byte*)b2_Ptr, b1.Length, 0, discrpantIndex);
            }
            return discrpantIndex;
        }


        //以最大范围进行批量对比，数据不够时用循环对比
        unsafe static void ArrayCompare_64(ushort* b1, ushort* b2, int length, int indexOffset, List<int> discrpantIndex)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            //以一个ushort指针进行
            ushort* lastAddress = b1 + length;
            //计算最后一个对比数据的地址
            ushort* lastAddressMinus64 = lastAddress - 4;//此处是64位
            int index = indexOffset;
            while (b1 <= lastAddressMinus64)
            {
                ulong xor = (*(ulong*)(b1) ^ *(ulong*)(b2));
                if (xor != 0)
                {
                    ulong mark = 0xFFFFUL;
                    for (int i = 0; i < 4; i++)
                    {
                        if ((xor & mark) != 0)
                        {
                            discrpantIndex.Add(index + i);
                        }
                        mark <<= 16;
                    }
                }
                index += 4;
                b1 += 4;
                b2 += 4;
            }

            while (b1 < lastAddress)
            {
                if (*b1 != *b2) discrpantIndex.Add(index);
                index++;
                b1++;
                b2++;
            }
        }

        unsafe static void ArrayCompare_64(byte* b1, byte* b2, int length, int indexOffset, List<int> discrpantIndex)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            byte* lastAddress = b1 + length;
            byte* lastAddressMinus64 = lastAddress - 8;//此处是64位
            int index = indexOffset;
            while (b1 <= lastAddressMinus64)
            {
                ulong xor = (*(ulong*)(b1) ^ *(ulong*)(b2));
                if (xor != 0)
                {
                    ulong mask = 0xFFUL;
                    for (int i = 0; i < 8; i++)
                    {
                        if ((xor & mask) != 0) discrpantIndex.Add(index + i);
                        mask <<= 8;
                    }
                }
                index += 8;
                b1 += 8;
                b2 += 8;
            }

            while (b1 < lastAddress)
            {
                if (*b1 != *b2) discrpantIndex.Add(index);
                index++;
                b1++;
                b2++;
            }
        }

        /// <summary>
        /// 异步等待结果，超时弹异常
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task"></param>
        /// <param name="timeoutMs"></param>
        /// <returns></returns>
        /// <exception cref="TimeoutException"></exception>
        public static async Task<T> TaskWaitAsync<T>(this Task<T> task, int timeoutMs) 
        {
            var delayTask = Task.Delay(timeoutMs);
            var completedTask = await Task.WhenAny(task, delayTask);

            if (completedTask == delayTask)
            {
                throw new TimeoutException("Task Wait time out.");
            }

            return await task; // 重新 await 以获取结果或抛出原任务的异常
        }
    }

    /// <summary>
    /// 平移缓冲
    /// 高性能的数据存储，用于减少数据复制此时
    /// </summary>
    public sealed class SlidingBuffer
    {
        private byte[] mBuffer;
        private int mRead;   // 已消费起点
        private int mWrite;  // 已写入末尾

        public SlidingBuffer(int capacity = 2048)
        {
            mBuffer = new byte[Math.Max(16, capacity)];
        }

        public int Count => mWrite - mRead;

        /// <summary>
        /// 追加数据
        /// </summary>
        /// <param name="data"></param>
        public void Append(ReadOnlySpan<byte> data)
        {
            EnsureCapacity(mWrite + data.Length);
            data.CopyTo(mBuffer.AsSpan(mWrite));
            mWrite += data.Length;
        }

        /// <summary>
        /// 当前有效区间视图
        /// </summary>
        public ReadOnlySpan<byte> Span => mBuffer.AsSpan(mRead, mWrite - mRead);

        /// <summary>
        /// 丢弃前 N 字节
        /// </summary>
        /// <param name="count"></param>
        public void Consume(int count)
        {
            if (count <= 0) return;
            if (count > Count) count = Count;

            mRead += count;
            CompactIfNeeded();
        }

        /// <summary>
        /// 拷贝部分数据
        /// </summary>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public byte[] CopyFrame(int start, int length)
        {
            if (start < 0 || length < 0 || start + length > Count)
                throw new ArgumentOutOfRangeException();

            var dst = new byte[length];
            Span.Slice(start, length).CopyTo(dst);
            return dst;
        }

        /// <summary>
        /// 将有效数据平移到数组起点
        /// </summary>
        private void CompactIfNeeded()
        {
            if (mRead == 0) return;

            // 当已消费超过一半，做一次平移
            if (mRead > mBuffer.Length / 2)
            {
                int remain = mWrite - mRead;
                Array.Copy(mBuffer, mRead, mBuffer, 0, remain);
                mRead = 0;
                mWrite = remain;
            }
        }

        /// <summary>
        /// 自动扩容机制
        /// </summary>
        /// <param name="required"></param>
        private void EnsureCapacity(int required)
        {
            if (required <= mBuffer.Length) return;

            int newSize = mBuffer.Length * 2;
            while (newSize < required) newSize *= 2;
            Array.Resize(ref mBuffer, newSize);
        }

        /// <summary>
        /// 清空内存
        /// </summary>
        public void Clear()
        {
            mRead = 0;
            mWrite = 0;
        }
    }
}
