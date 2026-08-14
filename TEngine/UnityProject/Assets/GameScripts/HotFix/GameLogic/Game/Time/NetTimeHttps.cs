using System;
using System.Net;
using System.Threading.Tasks;


namespace GameLogic
{
    public partial class NetTimeSystem : MonoSingleton<NetTimeSystem>
    {
        private string[] _httpsUrl = new[] {"https://connectivitycheck.gstatic.com/generate_204"};

        private string[] _homeHttpsUrl = new[] { "https://connect.rom.miui.com/generate_204" };

        private async void GenerateNetTimeByHttps()
        {
            string[] httpsUrl = _isHomeNet ? _homeHttpsUrl : _httpsUrl;
            foreach (var url in httpsUrl)
            {
                await GenerateNetTimeByHttps(url);
            }
        }

        private async Task GenerateNetTimeByHttps(string url)
        {
            try
            {
                var request = WebRequest.Create(url);
                request.Timeout = RequestTimeOut;
                request.Credentials = CredentialCache.DefaultCredentials;
                var response = await request.GetResponseAsync();
                var headerCollection = response.Headers;
                foreach (var h in headerCollection.AllKeys)
                {
                    if (h == "Date")
                    {
                        var dateTime = DateTime.Parse(headerCollection[h]);
                        SetNetTime(url, dateTime.ToLocalTime());
                    }
                }
            }
            catch (Exception e)
            {
            }
        }
    }
}