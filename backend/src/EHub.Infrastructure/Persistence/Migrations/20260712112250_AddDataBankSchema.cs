using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataBankSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_bank_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_bank_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_bank_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "data_bank_columns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    column_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    data_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    aliases = table.Column<string[]>(type: "text[]", nullable: false),
                    normalized_aliases = table.Column<string[]>(type: "text[]", nullable: false),
                    is_system_field = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_data_bank_columns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_bank_export_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selected_columns = table.Column<string[]>(type: "text[]", nullable: false),
                    column_order = table.Column<string[]>(type: "text[]", nullable: false),
                    header_aliases_json = table.Column<string>(type: "jsonb", nullable: false),
                    filters_json = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_data_bank_export_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_bank_export_templates_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "data_bank_import_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sheet_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    header_row = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    rows_inserted = table.Column<int>(type: "integer", nullable: false),
                    rows_updated = table.Column<int>(type: "integer", nullable: false),
                    rows_skipped = table.Column<int>(type: "integer", nullable: false),
                    columns_added = table.Column<string[]>(type: "text[]", nullable: false),
                    columns_ignored = table.Column<string[]>(type: "text[]", nullable: false),
                    conflicts_json = table.Column<string>(type: "jsonb", nullable: true),
                    analysis_json = table.Column<string>(type: "jsonb", nullable: true),
                    column_mappings_json = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    committed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rolled_back_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_data_bank_import_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_bank_import_batches_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_data_bank_import_batches_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "academic_datasets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dataset_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dynamic_fields_json = table.Column<string>(type: "jsonb", nullable: false),
                    last_import_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_academic_datasets", x => x.id);
                    table.ForeignKey(
                        name: "FK_academic_datasets_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_datasets_data_bank_import_batches_last_import_batc~",
                        column: x => x.last_import_batch_id,
                        principalTable: "data_bank_import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_datasets_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_datasets_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "data_bank_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_json = table.Column<string>(type: "jsonb", nullable: false),
                    student_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    dataset_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_bank_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_bank_snapshots_data_bank_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "data_bank_import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_data_bank_snapshots_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "data_bank_field_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dataset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    new_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    imported_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    imported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_bank_field_histories", x => x.id);
                    table.ForeignKey(
                        name: "FK_data_bank_field_histories_academic_datasets_dataset_id",
                        column: x => x.dataset_id,
                        principalTable: "academic_datasets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_data_bank_field_histories_data_bank_import_batches_import_b~",
                        column: x => x.import_batch_id,
                        principalTable: "data_bank_import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_data_bank_field_histories_users_imported_by_id",
                        column: x => x.imported_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_academic_datasets_class_id",
                table: "academic_datasets",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_academic_datasets_class_id_dataset_type",
                table: "academic_datasets",
                columns: new[] { "class_id", "dataset_type" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_datasets_class_id_project_id_dataset_type",
                table: "academic_datasets",
                columns: new[] { "class_id", "project_id", "dataset_type" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_datasets_class_id_student_id_dataset_type",
                table: "academic_datasets",
                columns: new[] { "class_id", "student_id", "dataset_type" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_datasets_dataset_type",
                table: "academic_datasets",
                column: "dataset_type");

            migrationBuilder.CreateIndex(
                name: "IX_academic_datasets_last_import_batch_id",
                table: "academic_datasets",
                column: "last_import_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_academic_datasets_project_id",
                table: "academic_datasets",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_academic_datasets_student_id",
                table: "academic_datasets",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_audit_logs_action_created_at",
                table: "data_bank_audit_logs",
                columns: new[] { "action", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_audit_logs_created_at",
                table: "data_bank_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_audit_logs_entity_entity_id",
                table: "data_bank_audit_logs",
                columns: new[] { "entity", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_audit_logs_user_id_created_at",
                table: "data_bank_audit_logs",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_columns_data_type",
                table: "data_bank_columns",
                column: "data_type");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_columns_is_active",
                table: "data_bank_columns",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_columns_normalized_key",
                table: "data_bank_columns",
                column: "normalized_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_export_templates_owner_id",
                table: "data_bank_export_templates",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_export_templates_owner_id_name",
                table: "data_bank_export_templates",
                columns: new[] { "owner_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_field_histories_dataset_id",
                table: "data_bank_field_histories",
                column: "dataset_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_field_histories_dataset_id_field_key_imported_at",
                table: "data_bank_field_histories",
                columns: new[] { "dataset_id", "field_key", "imported_at" });

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_field_histories_entity_type_imported_at",
                table: "data_bank_field_histories",
                columns: new[] { "entity_type", "imported_at" });

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_field_histories_field_key",
                table: "data_bank_field_histories",
                column: "field_key");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_field_histories_import_batch_id",
                table: "data_bank_field_histories",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_field_histories_import_batch_id_imported_at",
                table: "data_bank_field_histories",
                columns: new[] { "import_batch_id", "imported_at" });

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_field_histories_imported_at",
                table: "data_bank_field_histories",
                column: "imported_at");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_field_histories_imported_by_id",
                table: "data_bank_field_histories",
                column: "imported_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_import_batches_class_id",
                table: "data_bank_import_batches",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_import_batches_class_id_created_at",
                table: "data_bank_import_batches",
                columns: new[] { "class_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_import_batches_class_id_file_checksum",
                table: "data_bank_import_batches",
                columns: new[] { "class_id", "file_checksum" });

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_import_batches_created_at",
                table: "data_bank_import_batches",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_import_batches_file_checksum",
                table: "data_bank_import_batches",
                column: "file_checksum");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_import_batches_status",
                table: "data_bank_import_batches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_import_batches_uploaded_by_id",
                table: "data_bank_import_batches",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_import_batches_uploaded_by_id_created_at",
                table: "data_bank_import_batches",
                columns: new[] { "uploaded_by_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_snapshots_created_at",
                table: "data_bank_snapshots",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_snapshots_created_by_id",
                table: "data_bank_snapshots",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_data_bank_snapshots_import_batch_id",
                table: "data_bank_snapshots",
                column: "import_batch_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_bank_audit_logs");

            migrationBuilder.DropTable(
                name: "data_bank_columns");

            migrationBuilder.DropTable(
                name: "data_bank_export_templates");

            migrationBuilder.DropTable(
                name: "data_bank_field_histories");

            migrationBuilder.DropTable(
                name: "data_bank_snapshots");

            migrationBuilder.DropTable(
                name: "academic_datasets");

            migrationBuilder.DropTable(
                name: "data_bank_import_batches");
        }
    }
}
