using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class MentoringFeedback : AuditableEntity
{
    public Guid MentoringSessionId { get; set; }
    public virtual MentoringSession MentoringSession { get; set; } = null!;
    public Guid StudentUserId { get; set; }
    public virtual User StudentUser { get; set; } = null!;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}
