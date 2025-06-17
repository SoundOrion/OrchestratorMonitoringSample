using StartOrchestratorAppBody.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StartOrchestratorAppBody.Orchestrators;

public class DagOrchestrator
{
    [Function("DagOrchestrator")]
    public async Task Run([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<DagInput>();
        var jobs = input.Jobs.ToDictionary(j => j.Id);

        var jobStatusDict = input.Jobs.ToDictionary(
            j => j.Id,
            j => new JobStatus
            {
                Id = j.Id,
                Name = j.Name,
                Progress = 0,
                Started = false,
                Finished = false,
                Running = false,
                LastUpdated = null
            });

        string lastFinishedJob = "";

        while (jobStatusDict.Values.Any(j => !j.Finished))
        {
            var completed = jobStatusDict.Values
                .Where(j => j.Finished)
                .Select(j => j.Id)
                .ToHashSet();

            var running = jobStatusDict.Values
                .Where(j => j.Running)
                .Select(j => j.Id)
                .ToHashSet();

            // Check all jobs and start those ready
            foreach (var job in jobs.Values)
            {
                if (!completed.Contains(job.Id) &&
                    !running.Contains(job.Id) &&
                    IsJobReady(job, completed))
                {
                    var status = jobStatusDict[job.Id];
                    status.Running = true;
                    status.Started = true;

                    _ = RunJob(context, job, status).ContinueWith(t =>
                    {
                        var (jobId, success) = t.Result;
                        var s = jobStatusDict[jobId];
                        s.Running = false;
                        s.Finished = success;
                        s.LastUpdated = context.CurrentUtcDateTime;
                    });
                }
            }

            context.SetCustomStatus(new CustomDagStatus
            {
                Jobs = jobStatusDict.Values.ToList(),
                Finished = completed.ToList(),
                Running = running.ToList(),
                LastJob = lastFinishedJob,
                Completed = false
            });

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);
        }

        context.SetCustomStatus(new CustomDagStatus
        {
            Jobs = jobStatusDict.Values.ToList(),
            Finished = jobStatusDict.Values.Where(j => j.Finished).Select(j => j.Id).ToList(),
            Running = new List<string>(),
            LastJob = lastFinishedJob,
            Completed = true
        });
    }

    private bool IsJobReady(JobNode job, HashSet<string> completed)
    {
        if (job.DependsOn == null || job.DependsOn.Count == 0)
            return true;

        var logic = job.DependsOnLogic?.Trim().ToUpperInvariant() ?? "AND";

        return logic switch
        {
            "OR" => job.DependsOn.Any(dep => completed.Contains(dep)),
            _ => job.DependsOn.All(dep => completed.Contains(dep)),
        };
    }

    private async Task<(string jobId, bool success)> RunJob(TaskOrchestrationContext context, JobNode job, JobStatus jobStatus)
    {
        await context.CallActivityAsync<bool>("StartExternalJobActivity", job.StartApiUrl);

        while (true)
        {
            var progress = await context.CallActivityAsync<JobProgress>("CheckExternalJobProgressActivity", job.ProgressApiUrl);

            jobStatus.Progress = progress.Progress;
            jobStatus.Started = progress.Started;
            jobStatus.Finished = progress.Finished;
            jobStatus.LastUpdated = context.CurrentUtcDateTime;

            if (progress.Progress >= 100)
                return (job.Id, true);

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);
        }
    }
}

public class ConditionalRoute
{
    public string ConditionJobId { get; set; }
    public string ExpectedOutcome { get; set; } // "Success" or "Failed"
    public List<string> TargetJobIds { get; set; }
}

public class JobNode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string StartApiUrl { get; set; }
    public string ProgressApiUrl { get; set; }
    public List<string> DependsOn { get; set; } = new();
    public string DependsOnLogic { get; set; } = "AND"; // "AND" or "OR"
}

public class DagInput
{
    public List<JobNode> Jobs { get; set; } = new();
}

public class JobStatus
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Progress { get; set; }
    public bool Started { get; set; }
    public bool Finished { get; set; }
    public bool Running { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string Error { get; set; }
}

public class CustomDagStatus
{
    public List<JobStatus> Jobs { get; set; } = new();
    public List<string> Finished { get; set; } = new();
    public List<string> Running { get; set; } = new();
    public string LastJob { get; set; }
    public bool Completed { get; set; }
    public string Error { get; set; }
}
