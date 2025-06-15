using Microsoft.Azure.Functions.Worker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StartOrchestratorAppBody.Activities;

public class StartExternalJobActivity
{
    private readonly IHttpClientFactory _httpClientFactory;

    public StartExternalJobActivity(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [Function("StartExternalJobActivity")]
    public async Task<bool> Run([ActivityTrigger] string startApiUrl)
    {
        var client = _httpClientFactory.CreateClient();
        var resp = await client.PostAsync(startApiUrl, null);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        var result = JsonDocument.Parse(json);
        return result.RootElement.GetProperty("started").GetBoolean();
    }
}