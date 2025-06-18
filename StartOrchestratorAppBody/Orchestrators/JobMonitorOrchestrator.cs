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
                try
                {
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
                            message = "Monitoring"
                        });

                        if (!progress.Started)
                        {
                            context.SetCustomStatus(new
                            {
                                currentJob = job.Name,
                                currentIndex = i,
                                error = "Job not started"
                            });
                            if (input.StopOnFailure) throw new Exception("Job not started.");
                            break;
                        }

                        if (progress.Progress >= 100 || progress.Finished)
                            break;

                        await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(3), CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    context.SetCustomStatus(new
                    {
                        currentJob = job.Name,
                        currentIndex = i,
                        error = $"Exception during job: {ex.Message}"
                    });

                    if (input.StopOnFailure)
                        throw; // 全体停止（設計方針通り）
                    else
                        continue; // 次のジョブへ進む
                }
            }

            context.SetCustomStatus(new
            {
                completed = true,
                lastIndex = jobs.Count - 1
            });
        }
        catch (Exception ex)
        {
            context.SetCustomStatus(new { error = ex.Message });
            throw;
        }
    }
}

public class JobMonitorOrchestratorInput
{
    public List<JobMonitorRequest> Jobs { get; set; } = new();
    public int ResumeFromIndex { get; set; } = 0;
    public bool StopOnFailure { get; set; } = true; // ★追加：失敗時に停止するか？
}
public class JobMonitorRequest
{
    public string Name { get; set; }
    public string StartApiUrl { get; set; }
    public string ProgressApiUrl { get; set; }
}