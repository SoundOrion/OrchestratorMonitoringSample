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

public class StartJobMonitorFunction
{
    private readonly ILogger<StartJobMonitorFunction> _logger;

    public StartJobMonitorFunction(ILogger<StartJobMonitorFunction> logger)
    {
        _logger = logger;
    }

    [OpenApiOperation("StartJobMonitor", tags: new[] { "Job" })]
    [OpenApiRequestBody("application/json", typeof(JobMonitorOrchestratorInput), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Accepted, contentType: "application/json", bodyType: typeof(object), Description = "Accepted")]
    [Function("StartJobMonitorFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "start-job-monitor")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        // リクエストをデシリアライズ
        var input = await req.ReadFromJsonAsync<JobMonitorOrchestratorInput>();

        // バリデーション：ジョブが存在するか
        if (input?.Jobs == null || input.Jobs.Count == 0)
        {
            var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
            await badReq.WriteStringAsync("No jobs provided.");
            return badReq;
        }

        // バリデーション：各ジョブのURLが空でないか
        foreach (var job in input.Jobs)
        {
            if (string.IsNullOrWhiteSpace(job.StartApiUrl) || string.IsNullOrWhiteSpace(job.ProgressApiUrl))
            {
                var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                await badReq.WriteStringAsync("One or more jobs have missing URLs.");
                return badReq;
            }
        }

        // Orchestrator 起動
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync("JobMonitorOrchestrator", input);
        _logger.LogInformation("Started orchestration with ID = {instanceId}", instanceId);

        // 管理URL含むレスポンスを返す（statusQueryUri, terminatePostUriなど）
        var statusResponse = await client.CreateCheckStatusResponseAsync(req, instanceId);

        return statusResponse;
    }
}