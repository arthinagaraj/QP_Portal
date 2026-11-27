namespace QpPortal.Api.Data;

public class Scrutiny
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public int ReviewerId { get; set; }
    public int AssignedBy { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
    public string? Remarks { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
