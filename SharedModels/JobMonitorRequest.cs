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