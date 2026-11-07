using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <summary>Normalizes retired ChangesRequested records to Draft before the application removes that status.</summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261107090000_RemoveShowcaseRequestChanges")]
    public partial class RemoveShowcaseRequestChanges : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE showcase_projects SET status = 'Draft' WHERE status = 'ChangesRequested';");
            migrationBuilder.Sql("UPDATE showcase_review_histories SET from_status = 'Draft' WHERE from_status = 'ChangesRequested';");
            migrationBuilder.Sql("UPDATE showcase_review_histories SET to_status = 'Draft' WHERE to_status = 'ChangesRequested';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The former state cannot be inferred after it has been normalized to Draft.
        }
    }
}
