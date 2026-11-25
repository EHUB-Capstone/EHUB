using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace EHub.IntegrationTests.Showcase;

[Collection("Sequential")]
public sealed class ShowcaseReportAuditMigrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task AuditMigration_PreservesLegacyRows_EnforcesForeignKeys_AndRollsBackCleanly()
    {
        var schema = "report_audit_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(factory.TestConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(factory.TestConnectionString) { SearchPath = schema, Pooling = false }.ConnectionString;
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection, options => options.UseVector()).Options);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
                CREATE TABLE users (id uuid PRIMARY KEY);
                CREATE TABLE showcase_moderation_rules (id uuid PRIMARY KEY, phrase text NOT NULL, status varchar(20) NOT NULL);
                CREATE TABLE showcase_moderation_rule_proposals (id uuid PRIMARY KEY, phrase text NOT NULL);
                CREATE TABLE showcase_reports (id uuid PRIMARY KEY, note text NULL);
                """);
            var userId = Guid.NewGuid(); var ruleId = Guid.NewGuid(); var proposalId = Guid.NewGuid(); var reportId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO users(id) VALUES ({userId})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO showcase_moderation_rules(id, phrase, status) VALUES ({ruleId}, 'Keep custom phrase', 'Inactive')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO showcase_moderation_rule_proposals(id, phrase) VALUES ({proposalId}, 'Keep proposal')");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO showcase_reports(id, note) VALUES ({reportId}, 'Keep report')");
            var migrations = db.Database.GetMigrations().ToArray();
            var target = migrations.Single(x => x.EndsWith("AddShowcaseReportAuditTrail", StringComparison.Ordinal));
            var previous = migrations[Array.IndexOf(migrations, target) - 1];
            var migrator = db.GetService<IMigrator>();
            var up = migrator.GenerateScript(previous, target);
            var down = migrator.GenerateScript(target, previous);
            await db.Database.ExecuteSqlRawAsync(up);
            (await db.Database.SqlQueryRaw<int>("""
                SELECT COUNT(*)::int AS "Value" FROM showcase_moderation_rules
                WHERE status = 'Inactive' AND created_by_user_id IS NULL AND created_at_utc IS NULL
                    AND updated_by_user_id IS NULL AND updated_at_utc IS NULL
                """).SingleAsync()).Should().Be(1);
            (await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM showcase_reports WHERE content_snapshot IS NULL AND note = 'Keep report'").SingleAsync()).Should().Be(1);
            (await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM showcase_moderation_rule_proposals WHERE review_note IS NULL").SingleAsync()).Should().Be(1);
            var now = new DateTime(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE showcase_moderation_rules SET created_by_user_id = {userId}, updated_by_user_id = {userId}, created_at_utc = {now}, updated_at_utc = {now} WHERE id = {ruleId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE showcase_moderation_rule_proposals SET review_note = {new string('a', 1000)} WHERE id = {proposalId}");
            var snapshot = "Original report content\nwith Vietnamese accents: bò";
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE showcase_reports SET content_snapshot = {snapshot} WHERE id = {reportId}");
            (await db.Database.SqlQuery<string>($"SELECT content_snapshot AS \"Value\" FROM showcase_reports WHERE id = {reportId}").SingleAsync()).Should().Be(snapshot);
            Func<Task> invalidUser = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE showcase_moderation_rules SET created_by_user_id = {Guid.NewGuid()} WHERE id = {ruleId}");
            (await invalidUser.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
            Func<Task> longNote = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE showcase_moderation_rule_proposals SET review_note = {new string('a', 1001)} WHERE id = {proposalId}");
            (await longNote.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.StringDataRightTruncation);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM users WHERE id = {userId}");
            (await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM showcase_moderation_rules WHERE created_by_user_id IS NULL AND updated_by_user_id IS NULL AND created_at_utc IS NOT NULL AND updated_at_utc IS NOT NULL").SingleAsync()).Should().Be(1);
            await db.Database.ExecuteSqlRawAsync(down);
            (await db.Database.SqlQueryRaw<string>("SELECT status AS \"Value\" FROM showcase_moderation_rules").SingleAsync()).Should().Be("Inactive");
            (await db.Database.SqlQueryRaw<string>("SELECT phrase AS \"Value\" FROM showcase_moderation_rule_proposals").SingleAsync()).Should().Be("Keep proposal");
            (await db.Database.SqlQueryRaw<string>("SELECT note AS \"Value\" FROM showcase_reports").SingleAsync()).Should().Be("Keep report");
            await db.Database.ExecuteSqlRawAsync(up);
        }
        finally
        {
            // Only the uniquely generated schema inside the disposable test database is removed.
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
