namespace SharedModels;

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

/// <summary>
/// DAG
/// </summary>
public class JobNode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string StartApiUrl { get; set; }
    public string ProgressApiUrl { get; set; }
    public List<string> DependsOn { get; set; } = new();

    public string DependsOnLogic { get; set; } = "AND"; // "AND" or "OR"
}


public class ConditionalRoute
{
    public string ConditionJobId { get; set; }       // 条件となるジョブ
    public string ExpectedOutcome { get; set; }      // "Success" or "Failed"
    public List<string> TargetJobIds { get; set; }   // 条件に合致したときに有効になるジョブ
}

public class DagInput
{
    public List<JobNode> Jobs { get; set; } = new();
    public List<ConditionalRoute> ConditionalRoutes { get; set; } = new(); // 追加
}
