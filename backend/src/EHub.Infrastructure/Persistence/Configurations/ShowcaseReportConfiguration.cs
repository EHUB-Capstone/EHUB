using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ShowcaseReportConfiguration : IEntityTypeConfiguration<ShowcaseReport>
{
    public void Configure(EntityTypeBuilder<ShowcaseReport> builder)
    {
        builder.ToTable("showcase_reports");
        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).HasColumnName("id");
        builder.Property(report => report.ShowcaseProjectId).HasColumnName("showcase_project_id");
        builder.Property(report => report.CommentId).HasColumnName("comment_id");
        builder.Property(report => report.ReporterUserId).HasColumnName("reporter_user_id");
        builder.Property(report => report.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(30);
        builder.Property(report => report.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(report => report.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(report => report.CreatedAt).HasColumnName("created_at");
        builder.Property(report => report.ResolvedByUserId).HasColumnName("resolved_by_user_id");
        builder.Property(report => report.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(report => report.ResolutionNote).HasColumnName("resolution_note").HasMaxLength(1000);
        builder.Property(report => report.Version).IsRowVersion().HasColumnName("xmin");

        // NULL comment ids identify a page report and must also be unique per reporter.
        builder.HasIndex(report => new { report.ReporterUserId, report.ShowcaseProjectId })
            .IsUnique().HasFilter("comment_id IS NULL");
        builder.HasIndex(report => new { report.ReporterUserId, report.CommentId })
            .IsUnique().HasFilter("comment_id IS NOT NULL");
        builder.HasIndex(report => new { report.Status, report.CreatedAt });
        builder.HasIndex(report => report.ShowcaseProjectId);
        builder.HasIndex(report => report.CommentId);
        builder.HasOne<ShowcaseProject>().WithMany().HasForeignKey(report => report.ShowcaseProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShowcaseComment>().WithMany().HasForeignKey(report => report.CommentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(report => report.ReporterUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(report => report.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
