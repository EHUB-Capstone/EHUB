using EHub.Domain.Entities;
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
public sealed class ShowcaseAutoReportMigrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task Rollback_PreservesHumanReports_AndCanBeAppliedAgain()
    {
        // Use a separate disposable database: never roll back the shared scenario database.
        var databaseName = "showcase_rollback_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(factory.TestConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(factory.TestConnectionString) { Database = databaseName, Pooling = false }.ConnectionString;
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection, options => options.UseVector()).Options);
            var names = db.Database.GetMigrations().ToArray();
            var categoryIndex = Array.FindIndex(names, name => name.EndsWith("AddShowcaseViolationCategories", StringComparison.Ordinal));
            categoryIndex.Should().BeGreaterThan(0);
            await db.GetService<IMigrator>().MigrateAsync(names[categoryIndex - 1]);
            var customSpam = Guid.NewGuid();
            var customAdult = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO showcase_moderation_rules (id, phrase, description, reason, is_enabled) VALUES ({customSpam}, 'custom spam phrase', 'Keep custom description', 'Spam', false), ({customAdult}, 'custom adult phrase', 'Keep custom description', 'InappropriateContent', true)");
            await db.Database.MigrateAsync();
            var migratedSpam = await db.ShowcaseModerationRules.SingleAsync(x => x.Id == customSpam);
            migratedSpam.Category.Should().Be(ShowcaseViolationCategory.Spam);
            migratedSpam.Status.Should().Be(ShowcaseModerationRuleStatus.Inactive);
            migratedSpam.Description.Should().Be("Keep custom description");
            (await db.ShowcaseModerationRules.SingleAsync(x => x.Id == customAdult)).Category.Should().Be(ShowcaseViolationCategory.AdultContent);
            (await db.ShowcaseModerationRules.SingleAsync(x => x.Phrase == "cặc")).Category.Should().Be(ShowcaseViolationCategory.Profanity);
            (await db.ShowcaseModerationRules.SingleAsync(x => x.Phrase == "tao giết mày")).Category.Should().Be(ShowcaseViolationCategory.Threat);
            await db.ShowcaseModerationRules.Where(x => x.Id == customSpam || x.Id == customAdult).ExecuteDeleteAsync();
            var reporter = new User { FullName = "Migration reporter", Email = "migration@example.test", NormalizedEmail = "MIGRATION@EXAMPLE.TEST" };
            var page = new ShowcaseProject { ProjectLineageId = Guid.NewGuid() };
            var human = new ShowcaseReport { ShowcaseProjectId = page.Id, ReporterUserId = reporter.Id, Reason = ShowcaseReportReason.Spam, CreatedAt = DateTime.UtcNow };
            db.Users.Add(reporter); db.ShowcaseProjects.Add(page); db.ShowcaseReports.Add(human);
            db.ShowcaseReports.Add(new ShowcaseReport { ShowcaseProjectId = page.Id, Source = ShowcaseReportSource.System,
                ContentFingerprint = new string('A', 64), DetectionKey = new string('B', 64), Reason = ShowcaseReportReason.Harassment, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var initialIndex = Array.FindIndex(applied, name => name.EndsWith("AddShowcaseAutomaticReports", StringComparison.Ordinal));
            initialIndex.Should().BeGreaterThan(0);
            await db.GetService<IMigrator>().MigrateAsync(applied[initialIndex - 1]);
            (await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM showcase_reports").SingleAsync()).Should().Be(1);
            (await db.Database.SqlQueryRaw<Guid>("SELECT reporter_user_id AS \"Value\" FROM showcase_reports").SingleAsync()).Should().Be(reporter.Id);
            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();
            (await db.ShowcaseReports.SingleAsync()).Source.Should().Be(ShowcaseReportSource.User);
            (await db.ShowcaseModerationRules.CountAsync()).Should().Be(59);
        }
        finally
        {
            // Name is generated above, never taken from user input. FORCE only targets this test database.
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
