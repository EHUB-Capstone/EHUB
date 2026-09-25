using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class TeamFormationInvitationConfiguration : IEntityTypeConfiguration<TeamFormationInvitation>
{
    public void Configure(EntityTypeBuilder<TeamFormationInvitation> builder)
    {
        builder.ToTable("team_formation_invitations", table =>
            table.HasCheckConstraint("CK_team_formation_invitations_status", "status IN ('Pending', 'Accepted', 'Declined')"));
        builder.HasKey(item => new { item.FormationId, item.StudentId });
        builder.Property(item => item.FormationId).HasColumnName("formation_id");
        builder.Property(item => item.ClassId).HasColumnName("class_id");
        builder.Property(item => item.StudentId).HasColumnName("student_id");
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.RespondedAtUtc).HasColumnName("responded_at_utc");
        builder.Property(item => item.ReservationReleasedAtUtc).HasColumnName("reservation_released_at_utc");
        builder.HasIndex(item => new { item.ClassId, item.StudentId })
            .IsUnique().HasFilter("reservation_released_at_utc IS NULL");
        builder.HasIndex(item => new { item.StudentId, item.ReservationReleasedAtUtc });
        builder.HasOne(item => item.Formation).WithMany(formation => formation.Invitations)
            .HasForeignKey(item => item.FormationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ClassStudent).WithMany()
            .HasForeignKey(item => new { item.ClassId, item.StudentId }).OnDelete(DeleteBehavior.Restrict);
    }
}
