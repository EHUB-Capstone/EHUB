using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductFeedbackInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_feedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    ContextUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_feedbacks_users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_feedback_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductFeedbackId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CloudinaryPublicId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_feedback_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_feedback_attachments_product_feedbacks_ProductFeedb~",
                        column: x => x.ProductFeedbackId,
                        principalTable: "product_feedbacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_feedback_attachments_ProductFeedbackId",
                table: "product_feedback_attachments",
                column: "ProductFeedbackId");

            migrationBuilder.CreateIndex(
                name: "IX_product_feedbacks_IsArchived_CreatedAt",
                table: "product_feedbacks",
                columns: new[] { "IsArchived", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_product_feedbacks_ReporterUserId",
                table: "product_feedbacks",
                column: "ReporterUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_feedback_attachments");

            migrationBuilder.DropTable(
                name: "product_feedbacks");
        }
    }
}
