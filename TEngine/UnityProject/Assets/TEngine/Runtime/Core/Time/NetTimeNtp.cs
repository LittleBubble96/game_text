using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using TEngine;

namespace TEngine
{
    public partial class NetTimeSystem
    {
        public string[] UrlNtp = new[] {"time.apple.com", "pool.ntp.org", "time.windows.com"};
        //public string[] UrlNtp = new[] { "time1.google.com" };
        public string[] HomeUrlNtp = new[] { "ntp.aliyun.com", "ntp1.aliyun.com", "cn.pool.ntp.org" };

        //https://stackoverflow.com/questions/1193955/how-to-query-an-ntp-server-using-c
        public async void GenerateNetTimeByNtp()
        {
            string[] ntp = _isHomeNet ? HomeUrlNtp : UrlNtp;
            foreach (var url in ntp)
            {
                await GenerateNetTimeByNtp(url);
            }
        }

        private async Task GenerateNetTimeByNtp(string url)
        {
            try
            {
                var ntpData = new byte[48];
                ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

                var addresses = await Dns.GetHostAddressesAsync(url);

                var ipEndPoint = new IPEndPoint(addresses[0], 123);

                using (var listener = new Socket(ipEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
                {
                    await listener.ConnectAsync(ipEndPoint);
                    listener.ReceiveTimeout = RequestTimeOut;
                    await listener.SendAsync(ntpData, SocketFlags.None);
                    await listener.ReceiveAsync(ntpData, SocketFlags.None);
                    listener.Close();
                }

                const byte serverReplyTime = 40;
                ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);
                ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);
                intPart = SwapEndianness(intPart);
                fractPart = SwapEndianness(fractPart);

                var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
                var networkDateTime =
                    (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long) milliseconds);

                SetNetTime(url, networkDateTime.ToLocalTime());
            }
            catch (Exception e)
            {
                Log.Warning($"[NetTimeSystem] {e}");
                // ignored
            }
        }

        private uint SwapEndianness(ulong x)
        {
            return (uint) (((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }
    }
}