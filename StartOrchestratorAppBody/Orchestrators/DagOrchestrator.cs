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

        var completed = new HashSet<string>();
        var running = new HashSet<string>();
        string lastFinishedJob = "";

        while (completed.Count < jobs.Count)
        {
            var ready = jobs.Values
                .Where(j =>
                    !completed.Contains(j.Id) &&
                    !running.Contains(j.Id) &&
                    IsJobReady(j, completed))
                .ToList();

            if (!ready.Any() && running.Count == 0)
            {
                throw new InvalidOperationException("実行可能なジョブがありません。DAGに循環依存があります。");
            }

            var jobTasks = new List<(string jobId, Task<(string jobId, bool success)> task)>();

            foreach (var job in ready)
            {
                running.Add(job.Id);
                var status = jobStatusDict[job.Id];
                status.Running = true;
                status.Started = true;

                var task = RunJob(context, job, status);
                jobTasks.Add((job.Id, task));
            }

            // 並列に監視
            var pending = jobTasks.ToDictionary(j => j.jobId, j => j.task);

            while (pending.Count > 0)
            {
                var completedJobs = new List<string>();

                foreach (var (jobId, task) in pending)
                {
                    if (task.IsCompleted)
                    {
                        var (id, success) = await task;
                        var status = jobStatusDict[id];

                        running.Remove(id);
                        status.Running = false;
                        status.Finished = success;
                        status.LastUpdated = context.CurrentUtcDateTime;
                        lastFinishedJob = id;
                        completed.Add(id);

                        if (!success)
                        {
                            status.Error = "Failed";
                            context.SetCustomStatus(new CustomDagStatus
                            {
                                Jobs = jobStatusDict.Values.ToList(),
                                Finished = completed.ToList(),
                                Running = running.ToList(),
                                LastJob = lastFinishedJob,
                                Completed = false,
                                Error = $"ジョブ {id} が失敗しました"
                            });
                            throw new Exception($"ジョブ {id} が失敗しました");
                        }

                        completedJobs.Add(jobId);
                    }
                }

                foreach (var id in completedJobs)
                {
                    pending.Remove(id);
                }

                // 進捗更新（Aの進捗など含めリアルタイムに反映）
                context.SetCustomStatus(new CustomDagStatus
                {
                    Jobs = jobStatusDict.Values.ToList(),
                    Finished = completed.ToList(),
                    Running = running.ToList(),
                    LastJob = lastFinishedJob,
                    Completed = false
                });

                if (pending.Count > 0)
                    await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(1), CancellationToken.None);
            }


            // 条件分岐ジョブの動的登録（必要であれば）
            foreach (var job in jobs.Values)
            {
                foreach (var route in job.ConditionalRoutes)
                {
                    if (completed.Contains(route.ConditionJobId))
                    {
                        var condJobStatus = jobStatusDict[route.ConditionJobId];
                        bool match = route.ExpectedOutcome == "Success" && condJobStatus.Finished ||
                                     route.ExpectedOutcome == "Failed" && !condJobStatus.Finished;

                        if (match)
                        {
                            foreach (var targetId in route.TargetJobIds)
                            {
                                if (!jobs.ContainsKey(targetId)) continue;
                                // jobs[targetId] は既に辞書にある。条件に応じて実行の対象となるだけ。
                            }
                        }
                    }
                }
            }

        }

        context.SetCustomStatus(new CustomDagStatus
        {
            Jobs = jobStatusDict.Values.ToList(),
            Finished = completed.ToList(),
            Running = running.ToList(),
            LastJob = lastFinishedJob,
            Completed = true
        });
    }

    private bool IsJobReady(JobNode job, HashSet<string> completed)
    {
        return job.DependsOnLogic?.ToUpperInvariant() switch
        {
            "OR" => job.DependsOn.Any(dep => completed.Contains(dep)),
            _ => job.DependsOn.All(dep => completed.Contains(dep)),
        };
    }

    // 実行＆ポーリング処理
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

            if (!progress.Started && progress.Progress == 0)
                return (job.Id, false);

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
    public List<ConditionalRoute> ConditionalRoutes { get; set; } = new();
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
