using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClassCheckpointSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "class_checkpoint_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checkpoint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reopen_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_reopened_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_checkpoint_schedules", x => x.id);
                    table.CheckConstraint("CK_class_checkpoint_schedules_date_range", "start_date_utc < end_date_utc");
                    table.ForeignKey(
                        name: "FK_class_checkpoint_schedules_checkpoints_checkpoint_id",
                        column: x => x.checkpoint_id,
                        principalTable: "checkpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_checkpoint_schedules_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_class_checkpoint_schedules_checkpoint_id_start_date_utc_end~",
                table: "class_checkpoint_schedules",
                columns: new[] { "checkpoint_id", "start_date_utc", "end_date_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_class_checkpoint_schedules_class_id_checkpoint_id",
                table: "class_checkpoint_schedules",
                columns: new[] { "class_id", "checkpoint_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "class_checkpoint_schedules");
        }
    }
}
