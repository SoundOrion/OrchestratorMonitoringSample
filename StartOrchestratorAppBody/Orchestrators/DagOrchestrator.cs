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
                LastUpdated = null,
                Error = null
            });

        string lastFinishedJob = "";
        var pendingTasks = new Dictionary<string, Task<(string jobId, bool success, string error)>>();

        while (jobStatusDict.Values.Any(j => !j.Finished && string.IsNullOrEmpty(j.Error)))
        {
            var completed = jobStatusDict.Values
                .Where(j => j.Finished)
                .Select(j => j.Id)
                .ToHashSet();

            var running = jobStatusDict.Values
                .Where(j => j.Running)
                .Select(j => j.Id)
                .ToHashSet();

            var failed = jobStatusDict.Values
                .Where(j => !j.Finished && !j.Running && !string.IsNullOrEmpty(j.Error) && !j.Error.StartsWith("Skipped"))
                .Select(j => j.Id)
                .ToHashSet();

            var skipped = jobStatusDict.Values
                .Where(j => !j.Finished && !j.Running && j.Error?.StartsWith("Skipped") == true)
                .Select(j => j.Id)
                .ToHashSet();

            bool newJobsAdded = false;

            foreach (var job in jobs.Values)
            {
                var status = jobStatusDict[job.Id];

                if (completed.Contains(job.Id) || running.Contains(job.Id) || !string.IsNullOrEmpty(status.Error))
                    continue;

                if (input.ConditionalRoutes.Count != 0)
                {
                    var relatedRoutes = input.ConditionalRoutes
                        .Where(r => r.TargetJobIds.Contains(job.Id));

                    if (relatedRoutes.Any())
                    {
                        bool anyConditionEvaluated = false;
                        bool conditionMatched = false;

                        foreach (var route in relatedRoutes)
                        {
                            if (!jobStatusDict.TryGetValue(route.ConditionJobId, out var condJob))
                                continue;

                            if (!condJob.Finished && string.IsNullOrEmpty(condJob.Error))
                                continue;

                            anyConditionEvaluated = true;

                            var match = route.ExpectedOutcome.ToUpperInvariant() switch
                            {
                                "SUCCESS" => condJob.Finished && string.IsNullOrEmpty(condJob.Error),
                                "FAILED" => !condJob.Finished && !string.IsNullOrEmpty(condJob.Error),
                                _ => false
                            };

                            if (match)
                            {
                                conditionMatched = true;
                                break;
                            }
                        }

                        if (anyConditionEvaluated && !conditionMatched)
                        {
                            status.Error = "Skipped: ConditionalRoute not matched";
                            status.LastUpdated = context.CurrentUtcDateTime;
                            continue;
                        }

                        if (!anyConditionEvaluated)
                            continue;
                    }
                }

                if (job.DependsOn.Any(dep => failed.Contains(dep)))
                {
                    status.Error = "Skipped due to failed dependency";
                    status.LastUpdated = context.CurrentUtcDateTime;
                    continue;
                }

                if (IsJobReady(job, completed, skipped))
                {
                    status.Running = true;
                    status.Started = true;
                    status.LastUpdated = context.CurrentUtcDateTime;

                    pendingTasks[job.Id] = RunJob(context, job, status);
                    newJobsAdded = true;
                }
            }

            if (newJobsAdded)
            {
                context.SetCustomStatus(new CustomDagStatus
                {
                    Jobs = jobStatusDict.Values.ToList(),
                    Finished = completed.ToList(),
                    Running = running.ToList(),
                    LastJob = lastFinishedJob,
                    Completed = false
                });
            }

            if (pendingTasks.Any())
            {
                var finishedTask = await Task.WhenAny(pendingTasks.Values);
                var result = await finishedTask;

                if (jobStatusDict.TryGetValue(result.jobId, out var s))
                {
                    s.Running = false;
                    s.Finished = result.success;
                    s.Error = result.error;
                    s.LastUpdated = context.CurrentUtcDateTime;
                    if (result.success) lastFinishedJob = result.jobId;
                }

                pendingTasks.Remove(result.jobId);

                context.SetCustomStatus(new CustomDagStatus
                {
                    Jobs = jobStatusDict.Values.ToList(),
                    Finished = jobStatusDict.Values.Where(j => j.Finished).Select(j => j.Id).ToList(),
                    Running = jobStatusDict.Values.Where(j => j.Running).Select(j => j.Id).ToList(),
                    LastJob = lastFinishedJob,
                    Completed = false
                });
            }

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);
        }

        context.SetCustomStatus(new CustomDagStatus
        {
            Jobs = jobStatusDict.Values.ToList(),
            Finished = jobStatusDict.Values.Where(j => j.Finished).Select(j => j.Id).ToList(),
            Running = new List<string>(),
            LastJob = lastFinishedJob,
            Completed = jobStatusDict.Values.All(j => j.Finished || !string.IsNullOrEmpty(j.Error))
        });
    }

    private bool IsJobReady(JobNode job, HashSet<string> completed, HashSet<string> skipped)
    {
        if (job.DependsOn == null || job.DependsOn.Count == 0)
            return true;

        var logic = job.DependsOnLogic?.Trim().ToUpperInvariant() ?? "AND";

        // すべての依存が「完了」または「スキップ」または「まだ未実行」などをチェック
        var completedOrSkipped = completed.Union(skipped).ToHashSet();

        switch (logic)
        {
            case "OR":
                // どれか1つが completed なら実行OK
                return job.DependsOn.Any(dep => completed.Contains(dep));
            default: // AND
                     // 全てが完了またはスキップされているか
                return job.DependsOn.All(dep => completedOrSkipped.Contains(dep));
        }
    }

    private async Task<(string jobId, bool success, string error)> RunJob(TaskOrchestrationContext context, JobNode job, JobStatus jobStatus)
    {
        try
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
                    return (job.Id, true, null);

                await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(2), CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            jobStatus.Error = ex.Message;
            jobStatus.Finished = false;
            jobStatus.LastUpdated = context.CurrentUtcDateTime;
            return (job.Id, false, ex.Message);
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
    public List<ConditionalRoute> ConditionalRoutes { get; set; } = new(); // Optional
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
