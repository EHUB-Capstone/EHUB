using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Evaluation : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;

    public Guid? SubmissionId { get; set; }
    public virtual Submission? Submission { get; set; }

    public Guid RubricId { get; set; }
    public virtual Rubric Rubric { get; set; } = null!;

    public Guid EvaluatorId { get; set; }
    public virtual User Evaluator { get; set; } = null!;

    public EvaluatorRole EvaluatorRole { get; set; } = EvaluatorRole.Lecturer;

    public decimal TotalScore { get; set; }
    public decimal MaxTotalScore { get; set; }

    public string? OverallFeedback { get; set; }
    public string? Strengths { get; set; }
    public string? Weaknesses { get; set; }
    public string? Suggestions { get; set; }

    public EvaluationStatus Status { get; set; } = EvaluationStatus.Draft;

    public DateTime? SubmittedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    // Navigation properties
    public virtual ICollection<EvaluationDetail> Details { get; set; } = new List<EvaluationDetail>();
    public virtual ICollection<EvaluationHistory> Histories { get; set; } = new List<EvaluationHistory>();
}
