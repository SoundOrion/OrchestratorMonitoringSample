using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using StartOrchestratorAppBody.Models;

namespace StartOrchestratorAppBody.Orchestrators;

public class JobMonitorOrchestrator
{
    [Function("JobMonitorOrchestrator")]
    public async Task<string> Run([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<JobMonitorRequest>();
        var started = await context.CallActivityAsync<bool>("StartExternalJobActivity", input.StartApiUrl);

        if (!started)
            return "Job start failed";

        while (true)
        {
            var progress = await context.CallActivityAsync<JobProgress>("CheckExternalJobProgressActivity", input.ProgressApiUrl);

            context.SetCustomStatus(new { progress = progress.Progress, started = progress.Started });

            if (!progress.Started)
                return progress.Progress >= 100 ? "Completed" : "Job not started or aborted";
            if (progress.Progress >= 100)
                return "Completed";

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(3), CancellationToken.None);
        }
    }
}

public class JobMonitorRequest
{
    public string StartApiUrl { get; set; }
    public string ProgressApiUrl { get; set; }
}