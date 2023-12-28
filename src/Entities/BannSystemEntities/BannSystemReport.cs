namespace AGC_Entbannungssystem.Entities;

public class BannSystemReport
{
    public string? reportId { get; set; }
    public ulong authorId { get; set; }
    public string? reason { get; set; }
    public long timestamp { get; set; }
    public bool active { get; set; }
}