namespace BlazorProgressUI;

public class DagJobConfig
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string StartApiUrl { get; set; }
    public string ProgressApiUrl { get; set; }
    public string[] DependsOn { get; set; } = Array.Empty<string>();
}
