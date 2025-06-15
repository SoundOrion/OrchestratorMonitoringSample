using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using StartOrchestratorApp.Models;
using System.Net;

namespace StartOrchestratorApp.Functions;

public class StartJobMonitorFunction
{
    private readonly ILogger<StartJobMonitorFunction> _logger;

    public StartJobMonitorFunction(ILogger<StartJobMonitorFunction> logger)
    {
        _logger = logger;
    }

    [OpenApiOperation("StartJobMonitor", tags: new[] { "Job" })]
    [OpenApiRequestBody("application/json", typeof(JobMonitorRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Accepted, contentType: "application/json", bodyType: typeof(object), Description = "Accepted")]
    [Function("StartJobMonitorFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "start-job-monitor")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        // 認証/監査/バリデーション処理をここで
        var input = await req.ReadFromJsonAsync<JobMonitorRequest>();

        // 必要なバリデーション例
        if (string.IsNullOrWhiteSpace(input.StartApiUrl) || string.IsNullOrWhiteSpace(input.ProgressApiUrl))
        {
            var badReq = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badReq.WriteStringAsync("Invalid input");
            return badReq;
        }

        // Orchestrator起動＋管理エンドポイント取得
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("JobMonitorOrchestrator", input);
        _logger.LogInformation("Started orchestration with ID = {instanceId}", instanceId);

        // Durable Functionsの管理エンドポイントを取得（Status/Terminateなど全部含む）
        var statusResponse = await client.CreateCheckStatusResponseAsync(req, instanceId);

        return statusResponse;
    }
}