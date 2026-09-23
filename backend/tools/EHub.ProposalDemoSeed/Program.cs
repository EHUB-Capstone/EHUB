using System.Text.Json;
using System.Text.RegularExpressions;
using EHub.Contracts.ProjectProposals;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace EHub.ProposalDemoSeed;

public static partial class Program
{
    private const string ApiUserSecretsId = "415ef8b0-723d-41fd-af0f-dc71893cd13f";
    private const string SeedClassCode = "AI-DEMO-HISTORY";
    private const string SnapshotSchemaVersion = "project-proposal-snapshot-v1";
    private const string SeedChangeNote = "AI demo historical seed v1.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> Main(string[] args)
    {
        if (!args.Contains("--apply", StringComparer.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        try
        {
            EnsureDevelopmentEnvironment();
            var backendRoot = FindBackendRoot();
            var configuration = BuildConfiguration(backendRoot);
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
            EnsureLoopbackDatabase(connectionString);

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, options =>
                    options.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .Options;
            await using var context = new AppDbContext(dbOptions);

            if (!await context.Database.CanConnectAsync())
                throw new InvalidOperationException("The configured local database is not reachable.");
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
                throw new InvalidOperationException("The local database has pending migrations. Start EHub.Api in Development once, then rerun this tool.");

            var outcome = await ApplyAsync(context);
            Console.WriteLine("Local AI demo history is ready.");
            Console.WriteLine($"Created: {outcome.CreatedClasses} class, {outcome.CreatedTeams} teams, {outcome.CreatedProjects} projects, {outcome.CreatedProposals} proposals, {outcome.CreatedVersions} submission versions.");
            Console.WriteLine($"Available historical candidates with marker {SeedClassCode}: {outcome.TotalCandidateCount}.");
            Console.WriteLine("No API key was used or printed, and no external AI provider was called.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Seed failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task<SeedOutcome> ApplyAsync(AppDbContext context)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();

        var actor = await context.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => new { user.Id })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No local user exists. Initialize the development database first.");
        var semester = await context.Semesters
            .OrderByDescending(item => item.Year)
            .ThenByDescending(item => item.Term)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No semester exists. Initialize the development database first.");
        var course = await context.Courses
            .OrderBy(item => item.Code)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No course exists. Initialize the development database first.");

        var createdClasses = 0;
        var historyClass = await context.Classes
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.SemesterId == semester.Id && item.ClassCode == SeedClassCode);
        if (historyClass?.IsDeleted == true)
            throw new InvalidOperationException($"A deleted {SeedClassCode} class already exists. Restore or permanently clean it before seeding.");
        if (historyClass == null)
        {
            var nextIndex = (await context.Classes
                .IgnoreQueryFilters()
                .Where(item => item.SemesterId == semester.Id && item.CourseId == course.Id)
                .MaxAsync(item => (int?)item.ClassIndex) ?? 0) + 1;
            historyClass = new Class
            {
                ClassCode = SeedClassCode,
                Slug = $"ai-demo-history-{SlugPart(semester.Code)}",
                ClassIndex = nextIndex,
                SemesterId = semester.Id,
                CourseId = course.Id,
                Status = ClassStatus.Draft,
                Room = "LOCAL-SEED",
                CreatedById = actor.Id
            };
            context.Classes.Add(historyClass);
            createdClasses++;
        }

        var createdTeams = 0;
        var createdProjects = 0;
        var createdProposals = 0;
        var createdVersions = 0;
        foreach (var (definition, index) in SeedDefinitions.Select((item, index) => (item, index)))
        {
            var team = await context.Teams
                .SingleOrDefaultAsync(item => item.ClassId == historyClass.Id && item.TeamCode == definition.TeamCode);
            if (team == null)
            {
                team = new Team
                {
                    ClassId = historyClass.Id,
                    TeamCode = definition.TeamCode,
                    TeamName = definition.TeamName,
                    Description = "Synthetic local-only history for AI proposal comparison.",
                    Status = TeamStatus.Active,
                    CreatedById = actor.Id
                };
                context.Teams.Add(team);
                createdTeams++;
            }

            var project = await context.Projects.SingleOrDefaultAsync(item => item.TeamId == team.Id);
            if (project == null)
            {
                project = new Project
                {
                    TeamId = team.Id,
                    Name = $"[AI-DEMO] {definition.Snapshot.StartupName}",
                    Description = definition.Snapshot.Tagline,
                    Problem = definition.Snapshot.Problem,
                    Solution = definition.Snapshot.Solution,
                    TargetUsers = definition.Snapshot.TargetCustomers,
                    StartupField = definition.StartupField,
                    BusinessModel = definition.Snapshot.BusinessModel,
                    Technology = definition.Snapshot.Technology,
                    Status = ProjectStatus.Approved,
                    IsHighPotential = false,
                    SubmittedAt = DateTime.UtcNow.AddDays(-120 - index),
                    CreatedById = actor.Id
                };
                context.Projects.Add(project);
                createdProjects++;
            }
            else if (!project.Name.StartsWith("[AI-DEMO]", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Team {definition.TeamCode} already owns a non-demo project; seed stopped without committing.");
            }

            var proposal = await context.ProjectProposals.SingleOrDefaultAsync(item => item.ProjectId == project.Id);
            if (proposal == null)
            {
                proposal = ToProposal(definition.Snapshot, project.Id, team.Id, historyClass.Id, actor.Id, index);
                context.ProjectProposals.Add(proposal);
                createdProposals++;
            }

            var hasSeedVersion = await context.ProjectProposalVersions.AnyAsync(version =>
                version.ProjectProposalId == proposal.Id
                && version.Purpose == ProjectProposalVersionPurpose.Submission
                && version.ChangeNote == SeedChangeNote);
            if (!hasSeedVersion)
            {
                var nextVersion = (await context.ProjectProposalVersions
                    .Where(version => version.ProjectProposalId == proposal.Id)
                    .MaxAsync(version => (int?)version.VersionNumber) ?? 0) + 1;
                context.ProjectProposalVersions.Add(new ProjectProposalVersion
                {
                    ProjectProposalId = proposal.Id,
                    VersionNumber = nextVersion,
                    SnapshotJson = JsonSerializer.Serialize(definition.Snapshot, JsonOptions),
                    SnapshotSchemaVersion = SnapshotSchemaVersion,
                    Purpose = ProjectProposalVersionPurpose.Submission,
                    ChangeNote = SeedChangeNote,
                    ChangedById = actor.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-120 - index)
                });
                createdVersions++;
            }
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        var totalCandidateCount = await context.ProjectProposalVersions.CountAsync(version =>
            version.Purpose == ProjectProposalVersionPurpose.Submission
            && version.ChangeNote == SeedChangeNote);
        return new SeedOutcome(
            createdClasses,
            createdTeams,
            createdProjects,
            createdProposals,
            createdVersions,
            totalCandidateCount);
    }

    private static ProjectProposal ToProposal(
        ProjectProposalSnapshotDto snapshot,
        Guid projectId,
        Guid teamId,
        Guid classId,
        Guid actorId,
        int index)
    {
        var submittedAt = DateTime.UtcNow.AddDays(-120 - index);
        return new ProjectProposal
        {
            ProjectId = projectId,
            TeamId = teamId,
            ClassId = classId,
            Title = snapshot.Title,
            StartupName = snapshot.StartupName,
            Tagline = snapshot.Tagline,
            Problem = snapshot.Problem,
            Solution = snapshot.Solution,
            TargetCustomers = snapshot.TargetCustomers,
            ValueProposition = snapshot.ValueProposition,
            MarketSize = snapshot.MarketSize,
            Competitors = snapshot.Competitors,
            BusinessModel = snapshot.BusinessModel,
            RevenueModel = snapshot.RevenueModel,
            MarketingStrategy = snapshot.MarketingStrategy,
            Technology = snapshot.Technology,
            FinancialPlan = snapshot.FinancialPlan,
            Roadmap = snapshot.Roadmap,
            TeamIntroduction = snapshot.TeamIntroduction,
            Status = ProjectProposalStatus.Approved,
            SubmittedAt = submittedAt,
            ApprovedAt = submittedAt.AddDays(2),
            CreatedById = actorId,
            UpdatedById = actorId
        };
    }

    private static IConfigurationRoot BuildConfiguration(string backendRoot)
    {
        var apiDirectory = Path.Combine(backendRoot, "src", "EHub.Api");
        return new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(ApiUserSecretsId)
            .AddEnvironmentVariables()
            .Build();
    }

    private static void EnsureDevelopmentEnvironment()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This seed tool only runs with ASPNETCORE_ENVIRONMENT=Development.");
    }

    private static void EnsureLoopbackDatabase(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var hosts = (builder.Host ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts.Length == 0 || hosts.Any(host => !IsLoopbackHost(host)))
            throw new InvalidOperationException("Refusing to seed because the configured database host is not loopback/local.");
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
        || string.Equals(host, "::1", StringComparison.Ordinal);

    private static string FindBackendRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "EHub.slnx")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new InvalidOperationException("Cannot locate backend/EHub.slnx.");
    }

    private static string SlugPart(string value) =>
        SlugRegex().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');

    private static void PrintHelp()
    {
        Console.WriteLine("Seeds six synthetic historical proposals into the configured local Development database.");
        Console.WriteLine("Run from backend/: dotnet run --project tools/EHub.ProposalDemoSeed -- --apply");
        Console.WriteLine("The command is idempotent and refuses non-loopback database hosts.");
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();

    private sealed record SeedOutcome(
        int CreatedClasses,
        int CreatedTeams,
        int CreatedProjects,
        int CreatedProposals,
        int CreatedVersions,
        int TotalCandidateCount);

    private sealed record SeedDefinition(
        string TeamCode,
        string TeamName,
        string StartupField,
        ProjectProposalSnapshotDto Snapshot);

    private static IReadOnlyList<SeedDefinition> SeedDefinitions { get; } =
    [
        new("AIH-01", "AI History RoomHub", "PropTech", Snapshot(
            "Nền tảng tìm phòng trọ sinh viên đã xác minh", "RoomHub", "Phòng trọ đáng tin cậy quanh trường",
            "Sinh viên mất nhiều thời gian tìm phòng trọ gần trường vì thông tin nằm rải rác trong nhiều nhóm mạng xã hội, giá thuê thiếu minh bạch, hình ảnh không được kiểm chứng và có nguy cơ gặp tin giả hoặc môi giới không đáng tin cậy.",
            "RoomHub xây dựng sàn phòng trọ dành cho sinh viên, xác minh chủ nhà và địa chỉ, cho phép lọc theo khoảng cách đến trường, ngân sách, tiện ích, thời hạn thuê và đánh giá thực tế từ người đã ở.",
            "Sinh viên đại học cần thuê phòng gần trường và chủ nhà muốn tiếp cận đúng nhóm người thuê đáng tin cậy.",
            "Giảm thời gian tìm kiếm và rủi ro lừa đảo nhờ danh sách phòng đã xác minh, bộ lọc theo nhu cầu sinh viên và thông tin chi phí minh bạch.",
            "Thu phí dịch vụ từ chủ nhà cho tin xác minh và gói hiển thị ưu tiên; sinh viên được sử dụng chức năng tìm kiếm cơ bản miễn phí.",
            "React, ASP.NET Core, PostgreSQL, bản đồ và dịch vụ xác minh nội dung.",
            "Thử nghiệm tại một khu vực quanh trường, mở rộng dữ liệu chủ nhà, bổ sung đánh giá và triển khai sang các trường lân cận.")),
        new("AIH-02", "AI History MateStay", "PropTech", Snapshot(
            "Ghép bạn cùng phòng phù hợp cho sinh viên", "MateStay", "Ở cùng người hợp với thói quen của bạn",
            "Nhiều sinh viên chọn bạn cùng phòng qua bài đăng ngắn nên không biết trước mức ngân sách, lịch sinh hoạt, thói quen vệ sinh và quy tắc sử dụng không gian chung, dẫn đến mâu thuẫn và phải chuyển chỗ ở sớm.",
            "MateStay tạo hồ sơ nhu cầu và ghép cặp sinh viên dựa trên ngân sách, khu vực, giờ ngủ, lịch học, mức độ sạch sẽ, sở thích và các quy tắc sống chung; hai bên có thể trao đổi trước khi xác nhận.",
            "Sinh viên muốn tìm bạn ở ghép trong ký túc xá hoặc phòng trọ gần trường.",
            "Tăng khả năng sống chung lâu dài bằng tiêu chí tương thích minh bạch thay vì lựa chọn ngẫu nhiên từ bài đăng mạng xã hội.",
            "Mô hình freemium; thu phí xác minh danh tính và gói kết nối ưu tiên, đồng thời hợp tác giới thiệu với ký túc xá và chủ nhà.",
            "Ứng dụng web, bộ luật ghép cặp có trọng số, xác minh tài khoản và hệ thống trò chuyện an toàn.",
            "Khảo sát tiêu chí tương thích, chạy pilot với sinh viên năm nhất, đo tỷ lệ kết nối thành công rồi mở rộng sang nhiều khu vực.")),
        new("AIH-03", "AI History SafeDorm", "Campus Services", Snapshot(
            "Quản lý an toàn và sự cố tại khu trọ sinh viên", "SafeDorm", "Báo sự cố nhanh, theo dõi xử lý minh bạch",
            "Sinh viên thuê trọ khó báo cáo kịp thời các vấn đề như hỏng điện nước, mất an ninh, thiết bị xuống cấp hoặc nguy cơ cháy nổ; yêu cầu thường gửi qua tin nhắn riêng và không có trạng thái xử lý rõ ràng.",
            "SafeDorm cho phép cư dân gửi sự cố kèm ảnh và vị trí, tự phân loại mức độ ưu tiên, chuyển việc đến chủ nhà hoặc kỹ thuật viên và theo dõi thời gian phản hồi đến khi hoàn tất.",
            "Sinh viên đang ở phòng trọ hoặc ký túc xá, chủ nhà và đơn vị quản lý khu lưu trú.",
            "Tạo một kênh quản lý sự cố có bằng chứng, thời hạn và trách nhiệm rõ ràng, giúp môi trường lưu trú của sinh viên an toàn hơn.",
            "Thu phí thuê bao theo số phòng từ đơn vị quản lý và cung cấp gói báo cáo an toàn định kỳ.",
            "Ứng dụng di động, lưu trữ ảnh, thông báo thời gian thực và bảng điều khiển quản lý SLA.",
            "Pilot tại một khu trọ, chuẩn hóa danh mục sự cố, tích hợp mạng lưới kỹ thuật viên và mở rộng cho ký túc xá.")),
        new("AIH-04", "AI History MealRescue", "FoodTech", Snapshot(
            "Điều phối thực phẩm dư thừa cho tổ chức cộng đồng", "MealRescue", "Biến suất ăn còn tốt thành hỗ trợ thiết thực",
            "Nhà hàng và căn tin phải bỏ đi thực phẩm còn an toàn vào cuối ngày trong khi các nhóm thiện nguyện gần đó không biết số lượng, thời gian nhận và điều kiện vận chuyển để điều phối kịp thời.",
            "MealRescue cho phép đơn vị thực phẩm đăng lô đồ ăn dư theo thời hạn, xác minh tổ chức tiếp nhận, ghép chuyến lấy hàng và lưu bằng chứng bàn giao trước khi thực phẩm hết hạn sử dụng.",
            "Nhà hàng, căn tin, tiệm bánh, ngân hàng thực phẩm và tổ chức thiện nguyện địa phương.",
            "Giảm lãng phí thực phẩm và tạo quy trình quyên góp có thể theo dõi, ưu tiên thời gian và đảm bảo đúng đơn vị tiếp nhận.",
            "Thu phí quản lý từ doanh nghiệp tham gia và tài trợ CSR; tổ chức thiện nguyện sử dụng miễn phí.",
            "Web app, định vị, tối ưu tuyến lấy hàng, thông báo thời gian thực và nhật ký bàn giao.",
            "Kết nối mười cửa hàng thử nghiệm, chuẩn hóa an toàn thực phẩm, đo lượng thực phẩm cứu được rồi mở rộng theo quận.")),
        new("AIH-05", "AI History CampusGo", "Mobility", Snapshot(
            "Theo dõi xe buýt nội khuôn viên theo thời gian thực", "CampusGo", "Biết chính xác khi nào xe sẽ đến",
            "Sinh viên thường chờ xe buýt nội khuôn viên mà không biết vị trí hiện tại, thời gian đến dự kiến hoặc tình trạng quá tải, khiến họ đi trễ và khó lựa chọn tuyến thay thế trong giờ cao điểm.",
            "CampusGo hiển thị vị trí xe theo thời gian thực, dự báo thời gian đến từng trạm, cảnh báo thay đổi tuyến và thu thập mức độ đông để gợi ý chuyến phù hợp cho sinh viên.",
            "Sinh viên, nhân viên trường và bộ phận vận hành phương tiện trong khuôn viên.",
            "Giảm thời gian chờ và nâng cao độ tin cậy của việc di chuyển trong trường bằng dữ liệu hành trình trực tiếp.",
            "Trường trả phí thuê bao vận hành theo số xe; người dùng cuối truy cập miễn phí.",
            "GPS, bản đồ số, xử lý luồng vị trí, dự báo ETA và ứng dụng web đáp ứng.",
            "Gắn thiết bị cho hai xe thử nghiệm, đo sai số ETA, bổ sung dữ liệu đông khách và mở rộng toàn bộ tuyến.")),
        new("AIH-06", "AI History StudyMate", "EdTech", Snapshot(
            "Kết nối gia sư đồng trang lứa theo môn học", "StudyMate", "Đúng môn, đúng lịch, đúng người hướng dẫn",
            "Sinh viên gặp khó ở một học phần thường tìm gia sư qua bài đăng tự phát nên khó xác minh năng lực, lịch rảnh và mức độ phù hợp với cách học, trong khi sinh viên học tốt chưa có kênh hỗ trợ bạn học chính thức.",
            "StudyMate xác minh kết quả môn học của gia sư, ghép người học theo môn, mục tiêu, lịch rảnh và hình thức học; hệ thống hỗ trợ đặt buổi, phản hồi và theo dõi tiến bộ.",
            "Sinh viên cần học kèm và sinh viên có kết quả tốt muốn trở thành gia sư đồng trang lứa.",
            "Cung cấp lựa chọn gia sư minh bạch theo đúng học phần trong cộng đồng trường, giúp tiết kiệm chi phí và tăng khả năng hoàn thành môn học.",
            "Thu phí giao dịch nhỏ cho mỗi buổi học và cung cấp gói quản lý chương trình hỗ trợ học tập cho nhà trường.",
            "React, ASP.NET Core, lịch đặt buổi, video meeting và thuật toán ghép theo tiêu chí.",
            "Thử nghiệm với ba môn nền tảng, đánh giá kết quả học, bổ sung hệ thống uy tín và mở rộng theo khoa."))
    ];

    private static ProjectProposalSnapshotDto Snapshot(
        string title,
        string startupName,
        string tagline,
        string problem,
        string solution,
        string targetCustomers,
        string valueProposition,
        string businessModel,
        string technology,
        string roadmap) => new()
        {
            Title = title,
            StartupName = startupName,
            Tagline = tagline,
            Problem = problem,
            Solution = solution,
            TargetCustomers = targetCustomers,
            ValueProposition = valueProposition,
            MarketSize = "Thị trường ban đầu tập trung vào sinh viên và các đơn vị dịch vụ quanh trường; quy mô sẽ được kiểm chứng qua pilot và dữ liệu sử dụng thực tế.",
            Competitors = "Các lựa chọn hiện tại gồm nhóm mạng xã hội, bảng tính và quy trình thủ công; lợi thế đề xuất là dữ liệu có cấu trúc và luồng xử lý chuyên biệt.",
            BusinessModel = businessModel,
            RevenueModel = businessModel,
            MarketingStrategy = "Tiếp cận qua câu lạc bộ sinh viên, đối tác trong trường, chương trình giới thiệu và thử nghiệm theo từng lớp hoặc khu vực.",
            Technology = technology,
            FinancialPlan = "Ưu tiên chi phí cho phát triển MVP, hạ tầng và vận hành pilot; chỉ mở rộng sau khi đạt chỉ số sử dụng và giữ chân đã xác định.",
            Roadmap = roadmap,
            TeamIntroduction = "Nhóm synthetic phục vụ kiểm thử local; không đại diện cho người dùng hoặc dự án thật."
        };
}
