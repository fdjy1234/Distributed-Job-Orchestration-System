namespace SkiJobControl.Core.Models;

public class Job
{
    public string JobId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? WorkerId { get; set; }
    public DateTime UpdateTime { get; set; }
}
