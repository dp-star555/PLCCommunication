using DeviceCommunicationBase;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Run();
        }
        public static void Run()
        {
            var client = new HPSocketPort_Client("127.0.0.1", 5000);

            // 预热 & 连接
            if (!client.Connect())
            {
                Console.WriteLine("连接失败");
                return;
            }

            // 测试参数
            const int frameCount = 1000;      // 压多少次
            const int payloadLen = 64;          // 每帧字节数，可改大一点试试看

            byte[] sendBuf = new byte[payloadLen];
            // 构造一个简单 payload，前几个字节带一个递增序号，用于简单检查
            for (int i = 0; i < payloadLen - 2; i++) sendBuf[i] = (byte)(i & 0xFF);
            sendBuf[payloadLen - 2] = (byte)(0x0D);
            sendBuf[payloadLen - 1] = (byte)(0x0A);
            byte[] recvBuf = new byte[1024];    // 接收 buffer，够大就行

            // 记录 GC 情况
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long memBefore = GC.GetTotalMemory(true);
            int gc0Before = GC.CollectionCount(0);
            int gc1Before = GC.CollectionCount(1);
            int gc2Before = GC.CollectionCount(2);

            var sw = Stopwatch.StartNew();

            for (int i = 0; i < frameCount; i++)
            {
                // 写
                if (!client.Write(sendBuf))
                {
                    Console.WriteLine($"第 {i} 次发送失败");
                    break;
                }

                // 读：按你的语义，这里拿到的是“这次之后的第一帧”
                int len = client.Read(recvBuf, timeoutMs: 1000);
                string textAscii = System.Text.Encoding.ASCII.GetString(recvBuf, 0, len);
                Console.WriteLine(textAscii);
                if (len <= 0)
                {
                    Console.WriteLine($"第 {i} 次接收超时");
                    break;
                }

                //// 简单检查一下返回的长度
                //if (len != payloadLen)
                //{
                //    Console.WriteLine($"第 {i} 次接收长度不匹配，期望={payloadLen} 实际={len}");
                //    break;
                //}

                // 也可以简单校验下一下内容（防止服务器乱回）
                // if (recvBuf[0] != sendBuf[0]) ...
            }

            sw.Stop();

            long memAfter = GC.GetTotalMemory(true);
            int gc0After = GC.CollectionCount(0);
            int gc1After = GC.CollectionCount(1);
            int gc2After = GC.CollectionCount(2);

            client.Disconnect();

            double totalMs = sw.Elapsed.TotalMilliseconds;
            double avgMs = totalMs / frameCount;
            double tps = frameCount / (totalMs / 1000.0);

            Console.WriteLine("==== 请求-应答模式测试结果 ====");
            Console.WriteLine($"总次数:         {frameCount}");
            Console.WriteLine($"总耗时:         {totalMs:F2} ms");
            Console.WriteLine($"平均每次:       {avgMs:F3} ms");
            Console.WriteLine($"吞吐量:         {tps:F2} 次/秒");
            Console.WriteLine();
            Console.WriteLine($"内存变化:       {memBefore / 1024.0 / 1024.0:F2} MB -> {memAfter / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"GC Gen0:        {gc0After - gc0Before}");
            Console.WriteLine($"GC Gen1:        {gc1After - gc1Before}");
            Console.WriteLine($"GC Gen2:        {gc2After - gc2Before}");
        }
    }
}
