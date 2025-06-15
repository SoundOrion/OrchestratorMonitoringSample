using Microsoft.Azure.Functions.Worker;
using StartOrchestratorAppBody.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StartOrchestratorAppBody.Activities;

public class CheckExternalJobProgressActivity
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CheckExternalJobProgressActivity(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [Function("CheckExternalJobProgressActivity")]
    public async Task<JobProgress> Run([ActivityTrigger] string progressApiUrl)
    {
        var client = _httpClientFactory.CreateClient();
        var resp = await client.GetAsync(progressApiUrl);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        var result = JsonDocument.Parse(json);

        return new JobProgress
        {
            Started = result.RootElement.GetProperty("started").GetBoolean(),
            Progress = result.RootElement.GetProperty("progress").GetInt32()
        };
    }
}
