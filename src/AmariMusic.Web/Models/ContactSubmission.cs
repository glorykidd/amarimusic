namespace AmariMusic.Models;

public class ContactSubmission
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ViewedAt { get; set; }
    public string? AdminReply { get; set; }
    public DateTime? RepliedAt { get; set; }
}
