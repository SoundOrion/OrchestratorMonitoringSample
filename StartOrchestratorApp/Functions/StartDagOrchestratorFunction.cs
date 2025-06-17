using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using SharedModels;
using System.Net;

namespace StartOrchestratorApp.Functions;

public class StartDagOrchestratorFunction
{
    private readonly ILogger<StartDagOrchestratorFunction> _logger;

    public StartDagOrchestratorFunction(ILogger<StartDagOrchestratorFunction> logger)
    {
        _logger = logger;
    }

    [Function("StartDagOrchestratorFunction")]
    public async Task<HttpResponseData> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = "start-dag")] HttpRequestData req,
    [DurableClient] DurableTaskClient client)
    {
        var input = await req.ReadFromJsonAsync<DagInput>();

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("DagOrchestrator", input);
        _logger.LogInformation("Started orchestration with ID = {instanceId}", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
