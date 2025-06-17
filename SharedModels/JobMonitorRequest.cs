namespace SharedModels;

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
}

public class DagInput
{
    public List<JobNode> Jobs { get; set; } = new();
}
