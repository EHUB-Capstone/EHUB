using EHub.Domain.Enums;
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
public sealed class ShowcaseViolationManagementMigrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task UpgradeAndRollback_PreserveRuleStatusesAndReports()
    {
        // The schema is generated for this test; the shared scenario schema and local database are never changed.
        var schema = "violation_migration_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(factory.TestConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(factory.TestConnectionString) { SearchPath = schema, Pooling = false }.ConnectionString;
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection, options => options.UseVector()).Options);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
                CREATE TABLE showcase_violation_categories (
                    code varchar(32) PRIMARY KEY, name_vi varchar(120) NOT NULL, name_en varchar(120) NOT NULL, sort_order int NOT NULL);
                CREATE TABLE showcase_moderation_rules (
                    id uuid PRIMARY KEY, phrase varchar(120) NOT NULL, description varchar(300) NOT NULL,
                    reason varchar(30) NOT NULL, category varchar(32) NOT NULL REFERENCES showcase_violation_categories(code), is_enabled boolean NOT NULL);
                CREATE TABLE showcase_reports (
                    id uuid PRIMARY KEY, category varchar(32) NULL REFERENCES showcase_violation_categories(code), reason varchar(30) NOT NULL, note text NULL);
                """);
            var oldCategories = new[] { "Profanity", "Insult", "Threat", "Defamation", "Racism", "AdultContent", "Spam" };
            for (var index = 0; index < oldCategories.Length; index++)
            {
                var code = oldCategories[index];
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO showcase_violation_categories(code, name_vi, name_en, sort_order) VALUES ({code}, {code}, {code}, {index + 1})");
            }
            var activeId = Guid.NewGuid();
            var inactiveId = Guid.NewGuid();
            var disabledDefaultId = Guid.Parse("946a4c91-263f-4cea-a3aa-000000000001");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO showcase_moderation_rules(id, phrase, description, reason, category, is_enabled) VALUES ({activeId}, 'active custom phrase', 'Preserve active', 'Harassment', 'Insult', true), ({inactiveId}, 'inactive custom phrase', 'Preserve inactive', 'Harassment', 'Insult', false), ({disabledDefaultId}, 'disabled default phrase', 'Keep administrator choice', 'Harassment', 'Insult', false)");
            var migrations = db.Database.GetMigrations().ToArray();
            var target = migrations.Single(x => x.EndsWith("AddShowcaseViolationManagement", StringComparison.Ordinal));
            var previous = migrations[Array.IndexOf(migrations, target) - 1];
            var migrator = db.GetService<IMigrator>();
            var up = migrator.GenerateScript(previous, target);
            var down = migrator.GenerateScript(target, previous);
            await db.Database.ExecuteSqlRawAsync(up);

            var rules = await db.ShowcaseModerationRules.AsNoTracking().Select(x => new { x.Id, x.Status, x.Description }).ToListAsync();
            rules.Should().HaveCount(3);
            rules.Single(x => x.Id == activeId).Status.Should().Be(ShowcaseModerationRuleStatus.Active);
            rules.Single(x => x.Id == inactiveId).Status.Should().Be(ShowcaseModerationRuleStatus.Inactive);
            rules.Single(x => x.Id == disabledDefaultId).Status.Should().Be(ShowcaseModerationRuleStatus.Inactive);
            rules.Single(x => x.Id == disabledDefaultId).Description.Should().Be("Keep administrator choice");
            (await db.ShowcaseViolationCategories.CountAsync()).Should().Be(10);
            (await db.ShowcaseViolationCategories.CountAsync(x => x.SupportsPhraseBlocking)).Should().Be(7);
            Func<Task> invalidStatus = () => db.Database.ExecuteSqlRawAsync("UPDATE showcase_moderation_rules SET status = 'Unknown'");
            (await invalidStatus.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
            var reportId = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO showcase_reports(id, category, reason, note) VALUES ({reportId}, 'Misinformation', 'Other', 'Keep this report')");

            await db.Database.ExecuteSqlRawAsync(down);
            (await db.Database.SqlQuery<bool>($"SELECT is_enabled AS \"Value\" FROM showcase_moderation_rules WHERE id = {activeId}").SingleAsync()).Should().BeTrue();
            (await db.Database.SqlQuery<bool>($"SELECT is_enabled AS \"Value\" FROM showcase_moderation_rules WHERE id = {inactiveId}").SingleAsync()).Should().BeFalse();
            (await db.Database.SqlQuery<bool>($"SELECT is_enabled AS \"Value\" FROM showcase_moderation_rules WHERE id = {disabledDefaultId}").SingleAsync()).Should().BeFalse();
            (await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM showcase_violation_categories").SingleAsync()).Should().Be(7);
            (await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM showcase_reports WHERE id = {reportId} AND category IS NULL AND note = 'Keep this report'").SingleAsync()).Should().Be(1);
            await db.Database.ExecuteSqlRawAsync(up);
            (await db.ShowcaseModerationRules.AsNoTracking().Where(x => x.Id == inactiveId).Select(x => x.Status).SingleAsync()).Should().Be(ShowcaseModerationRuleStatus.Inactive);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
