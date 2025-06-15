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
        var input = context.GetInput<List<JobMonitorRequest>>();

        for (int i = 0; i < input.Count; i++)
        {
            var job = input[i];

            // Startバッチ呼び出し
            var started = await context.CallActivityAsync<bool>("StartExternalJobActivity", job.StartApiUrl);

            if (!started)
                return "Job start failed";

            while (true)
            {
                var progress = await context.CallActivityAsync<JobProgress>("CheckExternalJobProgressActivity", job.ProgressApiUrl);

                context.SetCustomStatus(new { progress = progress.Progress, started = progress.Started });

                if (progress.Progress >= 100)
                    break;

                await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(3), CancellationToken.None);
            }
        }

        return "All Jobs Completed";
    }
}

public class JobMonitorRequest
{
    public string StartApiUrl { get; set; }
    public string ProgressApiUrl { get; set; }
}