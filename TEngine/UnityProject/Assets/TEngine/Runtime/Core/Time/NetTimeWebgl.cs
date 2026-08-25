using System;
using System.Globalization;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace TEngine
{
    public partial class NetTimeSystem
    {
        private string[] _webglUrl = new[] {"https://connectivitycheck.gstatic.com/generate_204"};

        private string[] _homeWebglUrl = new[] { "https://connect.rom.miui.com/generate_204" };

        private bool _isRunningWebgl = false;

        private async void GenerateNetTimeByWebgl()
        {
            if (_isRunningWebgl)
            {
                return;
            }
            string[] httpsUrl = _isHomeNet ? _homeWebglUrl : _webglUrl;
            _isRunningWebgl = true;
            foreach (var url in httpsUrl)
            {
                await GenerateNetTimeByWebgl(url);
            }
            _isRunningWebgl = false;
        }

        private async Task GenerateNetTimeByWebgl(string url)
        {
            using var req = UnityWebRequest.Head(url);
            req.timeout = 5;
            await req.SendWebRequest().ToUniTask();

            if (req.result == UnityWebRequest.Result.Success)
            {
                string date = req.GetResponseHeader("Date");
                if (!string.IsNullOrEmpty(date))
                {
                    var utc = DateTime.ParseExact(
                        date,
                        "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
                    );
                    SetNetTime(url, utc.ToLocalTime());
                }
            }
            else
            {
                Log.Warning($"[NetTime] fail: {req.error}");
            }
        }
    }
}