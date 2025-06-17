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
    public async Task Run([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        try
        {
            var input = context.GetInput<JobMonitorOrchestratorInput>();
            var jobs = input.Jobs ?? new List<JobMonitorRequest>();

            for (int i = input.ResumeFromIndex; i < jobs.Count; i++)
            {
                var job = jobs[i];
                var started = await context.CallActivityAsync<bool>("StartExternalJobActivity", job.StartApiUrl);

                while (true)
                {
                    var progress = await context.CallActivityAsync<JobProgress>("CheckExternalJobProgressActivity", job.ProgressApiUrl);

                    context.SetCustomStatus(new
                    {
                        currentJob = job.Name,
                        currentIndex = i,
                        progress = progress.Progress,
                        started = progress.Started,
                        finished = progress.Finished,
                    });

                    //if (progress.Finished)
                    //    break;

                    if (!progress.Started)
                        break;

                    if (progress.Progress >= 100)
                        break;

                    await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(3), CancellationToken.None);
                }
            }

            context.SetCustomStatus(new { completed = true });
        }
        catch (Exception ex)
        {
            context.SetCustomStatus(new { error = ex.Message });
            throw; // throwÇµÇ»Ç¢Ç∆FailedÇ…Ç»ÇÁÇ»Ç¢Ç™ÅAÉçÉOÇ∆ÇµÇƒÇÕécÇ∑
        }
    }
}

public class JobMonitorOrchestratorInput
{
    public List<JobMonitorRequest> Jobs { get; set; } = new();
    public int ResumeFromIndex { get; set; } = 0;
}
public class JobMonitorRequest
{
    public string Name { get; set; }
    public string StartApiUrl { get; set; }
    public string ProgressApiUrl { get; set; }
}