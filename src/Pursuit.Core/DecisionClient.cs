using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pursuit.Core
{
    public interface IDecisionClient
    {
        Task<DecisionResponse> DecideAsync(Snapshot snapshot, CancellationToken cancellationToken);
    }
    public sealed class DecisionClient : IDecisionClient, IDisposable
    {
        private readonly HttpClient client;
        public DecisionClient(string endpoint = "http://127.0.0.1:8765/")
        {
            var address = new Uri(endpoint);
            if (!address.IsLoopback || address.Scheme != "http") throw new ArgumentException("decision endpoint must be loopback HTTP");
            client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = address, Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 1024 * 1024 };
        }
        public async Task<DecisionResponse> DecideAsync(Snapshot snapshot, CancellationToken cancellationToken)
        {
            using (var content = new StringContent(Wire.Serialize(snapshot), Encoding.UTF8, "application/json"))
            using (var response = await client.PostAsync("decide", content, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return Wire.Deserialize<DecisionResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
        }
        public void Dispose() => client.Dispose();
    }
}
