namespace BlazorProgressUI;

public class DagJobConfig
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string StartApiUrl { get; set; }
    public string ProgressApiUrl { get; set; }
    public string[] DependsOn { get; set; } = Array.Empty<string>();

    public string DependsOnLogic { get; set; } = "AND";
    public List<ConditionalRoute> ConditionalRoutes { get; set; } = new();
}

public class ConditionalRoute
{
    public string ConditionJobId { get; set; }
    public string ExpectedOutcome { get; set; } // "Success" or "Failed"
    public List<string> TargetJobIds { get; set; }
}