using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ProjectProposalConfiguration : IEntityTypeConfiguration<ProjectProposal>
{
    public void Configure(EntityTypeBuilder<ProjectProposal> builder)
    {
        builder.ToTable("project_proposals");

        builder.HasKey(pp => pp.Id);
        builder.Property(pp => pp.Id).HasColumnName("id");

        builder.Property(pp => pp.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(pp => pp.TeamId)
            .HasColumnName("team_id")
            .IsRequired();

        builder.Property(pp => pp.ClassId)
            .HasColumnName("class_id")
            .IsRequired();

        builder.Property(pp => pp.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(pp => pp.StartupName)
            .HasColumnName("startup_name")
            .HasMaxLength(200);

        builder.Property(pp => pp.Tagline)
            .HasColumnName("tagline")
            .HasMaxLength(300);

        builder.Property(pp => pp.Problem).HasColumnName("problem");
        builder.Property(pp => pp.Solution).HasColumnName("solution");
        builder.Property(pp => pp.TargetCustomers).HasColumnName("target_customers");
        builder.Property(pp => pp.ValueProposition).HasColumnName("value_proposition");
        builder.Property(pp => pp.MarketSize).HasColumnName("market_size");
        builder.Property(pp => pp.Competitors).HasColumnName("competitors");
        builder.Property(pp => pp.BusinessModel).HasColumnName("business_model");
        builder.Property(pp => pp.RevenueModel).HasColumnName("revenue_model");
        builder.Property(pp => pp.MarketingStrategy).HasColumnName("marketing_strategy");
        builder.Property(pp => pp.Technology).HasColumnName("technology");
        builder.Property(pp => pp.FinancialPlan).HasColumnName("financial_plan");
        builder.Property(pp => pp.Roadmap).HasColumnName("roadmap");
        builder.Property(pp => pp.TeamIntroduction).HasColumnName("team_introduction");

        builder.Property(pp => pp.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(pp => pp.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(pp => pp.ApprovedAt).HasColumnName("approved_at");
        builder.Property(pp => pp.RejectedAt).HasColumnName("rejected_at");

        builder.Property(pp => pp.CreatedById).HasColumnName("created_by_id");
        builder.Property(pp => pp.UpdatedById).HasColumnName("updated_by_id");

        // Indexes
        builder.HasIndex(pp => pp.ProjectId)
            .IsUnique();

        builder.HasIndex(pp => pp.TeamId);
        builder.HasIndex(pp => pp.ClassId);
        builder.HasIndex(pp => pp.Status);
        builder.HasIndex(pp => pp.CreatedById);
        builder.HasIndex(pp => pp.SubmittedAt);
        builder.HasIndex(pp => new { pp.ClassId, pp.Status });
        builder.HasIndex(pp => new { pp.TeamId, pp.Status });

        // Audit & Soft Delete properties configuration
        builder.Property(pp => pp.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(pp => pp.CreatedBy).HasColumnName("created_by");
        builder.Property(pp => pp.UpdatedAt).HasColumnName("updated_at");
        builder.Property(pp => pp.UpdatedBy).HasColumnName("updated_by");
        builder.Property(pp => pp.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(pp => pp.DeletedAt).HasColumnName("deleted_at");
        builder.Property(pp => pp.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(pp => !pp.IsDeleted);

        // Relationships configuration
        builder.HasOne(pp => pp.Project)
            .WithOne(p => p.ProjectProposal)
            .HasForeignKey<ProjectProposal>(pp => pp.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pp => pp.Team)
            .WithMany()
            .HasForeignKey(pp => pp.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pp => pp.Class)
            .WithMany()
            .HasForeignKey(pp => pp.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pp => pp.Creator)
            .WithMany()
            .HasForeignKey(pp => pp.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pp => pp.Updater)
            .WithMany()
            .HasForeignKey(pp => pp.UpdatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
