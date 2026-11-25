using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ShowcaseModerationRuleProposalConfiguration : IEntityTypeConfiguration<ShowcaseModerationRuleProposal>
{
    public void Configure(EntityTypeBuilder<ShowcaseModerationRuleProposal> builder)
    {
        builder.ToTable("showcase_moderation_rule_proposals", table =>
        {
            table.HasCheckConstraint("ck_showcase_rule_proposals_status", "status IN ('Pending', 'Approved', 'Rejected')");
            table.HasCheckConstraint("ck_showcase_rule_proposals_category", "category IN ('Profanity', 'Insult', 'Threat', 'Defamation', 'Racism', 'AdultContent', 'Spam')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ShowcaseProjectId).HasColumnName("showcase_project_id");
        builder.Property(x => x.SourceReportId).HasColumnName("source_report_id");
        builder.Property(x => x.CommentId).HasColumnName("comment_id");
        builder.Property(x => x.Phrase).HasColumnName("phrase").HasMaxLength(120).IsRequired();
        builder.Property(x => x.PhraseKey).HasColumnName("phrase_key").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(32);
        builder.HasOne<ShowcaseViolationCategoryDefinition>().WithMany().HasForeignKey(x => x.Category).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ProposedByUserId).HasColumnName("proposed_by_user_id");
        builder.Property(x => x.ProposedAtUtc).HasColumnName("proposed_at_utc");
        builder.Property(x => x.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(x => x.ReviewedAtUtc).HasColumnName("reviewed_at_utc");
        builder.Property(x => x.CreatedRuleId).HasColumnName("created_rule_id");
        builder.Property(x => x.Version).IsRowVersion().HasColumnName("xmin");
        builder.HasIndex(x => x.PhraseKey).IsUnique().HasFilter("status = 'Pending'");
        builder.HasIndex(x => new { x.Status, x.ProposedAtUtc });
        builder.HasOne<ShowcaseProject>().WithMany().HasForeignKey(x => x.ShowcaseProjectId).OnDelete(DeleteBehavior.Cascade);
        // Report history may be pruned without removing a proposal or an approved rule.
        builder.HasOne<ShowcaseReport>().WithMany().HasForeignKey(x => x.SourceReportId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ShowcaseComment>().WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ProposedByUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ShowcaseModerationRule>().WithMany().HasForeignKey(x => x.CreatedRuleId).OnDelete(DeleteBehavior.SetNull);
    }
}
