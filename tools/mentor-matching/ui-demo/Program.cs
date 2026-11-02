using System.Net;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Mentoring.TrainedRanking;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Contracts.Mentors;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Infrastructure.Persistence.Seed;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

// This tool has no Program class: global::Program is the real EHub.Api entry point.
internal static class UiDemoEntryPoint
{
    internal const string AdminEmail = "admin.ai-demo@example.test";
    // Disposable fixture credential for this loopback-only, newly created database.
    internal const string AdminPassword = "AiDemoOnly@2026!";
    private const string DemoMarker = "[AI DEMO100]";
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        var stage = "arguments";
        try
        {
            var options = Arguments.Read(args);
            var repository = FindRepositoryRoot();
            var output = Path.GetFullPath(options.Output);
            var allowedOutput = Path.GetFullPath(Path.Combine(repository, "artifacts", "mentor-ui-demo"));
            if (!output.Equals(allowedOutput, StringComparison.OrdinalIgnoreCase) &&
                !output.StartsWith(allowedOutput + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Output must be within this repository's ignored artifacts/mentor-ui-demo directory.");
            Directory.CreateDirectory(output);
            stage = "input validation";
            var profileBytes = await File.ReadAllBytesAsync(options.Profiles);
            var projectsBytes = await File.ReadAllBytesAsync(options.Projects);
            var modelBytes = await File.ReadAllBytesAsync(options.Model);
            _ = MentorRankingModel.FromJson(Encoding.UTF8.GetString(modelBytes));
            var profiles = Encoding.UTF8.GetString(profileBytes).TrimStart('\uFEFF').Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonSerializer.Deserialize<ProfileRow>(line, Json) ?? throw new InvalidDataException()).ToArray();
            var itProfileBytes = options.ItProfiles is null ? null : await File.ReadAllBytesAsync(options.ItProfiles);
            var itProfiles = itProfileBytes is null ? [] :
                JsonSerializer.Deserialize<ProfileRow[]>(itProfileBytes, Json) ?? throw new InvalidDataException();
            using var projectsDocument = JsonDocument.Parse(projectsBytes);
            var projectsRoot = projectsDocument.RootElement;
            var projects = (projectsRoot.ValueKind == JsonValueKind.Array ? projectsRoot : projectsRoot.GetProperty("projects"))
                .Deserialize<ProjectRow[]>(Json) ?? throw new InvalidDataException();
            if (profiles.Length != 30 || profiles.Select(x => x.MentorRef).Distinct().Count() != 30 ||
                profiles.Any(x => !Regex.IsMatch(x.MentorRef, "^M[0-9]{3}$") || x.MentorType != "Enterprise" ||
                    x.Profile is null || x.Profile.MentorType != "Business") ||
                projects.Length != 100 || projects.Select(x => x.Reference).Distinct().Count() != 100 ||
                projects.Any(x => string.IsNullOrWhiteSpace(x.Reference) || string.IsNullOrWhiteSpace(x.Title) ||
                    x.Title.Length > 200 || string.IsNullOrWhiteSpace(x.Description) || x.Description.Length > 4000))
                throw new InvalidDataException("Expected exactly 30 distinct Enterprise profiles and 100 complete projects.");
            if (options.ItProfiles is not null && (itProfiles.Length != 10 ||
                itProfiles.Select(row => row.MentorRef).Distinct(StringComparer.Ordinal).Count() != 10 ||
                itProfiles.Any(row => !Regex.IsMatch(row.MentorRef, "^IT[0-9]{3}$") || row.MentorType != "Academic" ||
                    row.Profile is null || row.Profile.MentorType != "IT" || string.IsNullOrWhiteSpace(row.Profile.Experience))))
                throw new InvalidDataException("Expected exactly 10 distinct synthetic Academic/IT profiles.");
            var allProfiles = profiles.Concat(itProfiles).ToArray();

            stage = "isolated PostgreSQL container";
            Console.WriteLine("Starting dedicated loopback PostgreSQL container for AI DEMO100.");
            await using var postgres = new PostgreSqlBuilder("pgvector/pgvector:0.8.6-pg16")
                .WithName("ehub-ai-demo100-" + Guid.NewGuid().ToString("N")[..12])
                .WithDatabase("ehub_ai_demo100")
                .WithUsername("ehub_demo")
                .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(24)))
                .WithCreateParameterModifier(parameters =>
                {
                    if (parameters.HostConfig?.PortBindings is null) return;
                    foreach (var bindings in parameters.HostConfig.PortBindings.Values)
                        foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
                }).Build();
            await postgres.StartAsync();
            var configuration = BuildConfiguration(postgres.GetConnectionString(), options.Model);
            // The real API reads JWT settings while registering authentication,
            // before the factory's later configuration callback. Supply the same
            // disposable settings to that initial read and the options binders.
            // These variables exist only in this dedicated demo process.
            foreach (var setting in configuration)
                Environment.SetEnvironmentVariable(setting.Key.Replace(":", "__"), setting.Value);
            stage = "real API host";
            await using var factory = new UiDemoFactory(configuration, postgres.GetConnectionString(), repository);
            factory.UseKestrel(server => server.ListenLocalhost(5226));
            factory.StartServer();
            stage = "migrations and isolated seed";
            var seed = await SeedAsync(factory.Services, allProfiles, projects);
            var ready = new
            {
                schema = "ehub-ai-demo100-ready-v2", isDemo = true, environment = "Testing",
                apiUrl = "http://127.0.0.1:5226", frontendUrl = "http://127.0.0.1:5174/admin/subjects",
                semesterId = seed.SemesterId, semesterCode = "FA2026", classes = seed.Classes,
                projectCount = projects.Length, mentorCount = allProfiles.Length,
                enterpriseMentorCount = profiles.Length, academicMentorCount = itProfiles.Length, assignmentsAfterSeed = 0,
                syntheticAdminEmail = AdminEmail,
                modelSha256 = Hash(modelBytes), profilesSha256 = Hash(profileBytes), projectsSha256 = Hash(projectsBytes),
                itProfilesSha256 = itProfileBytes is null ? null : Hash(itProfileBytes),
                note = "Dedicated disposable PostgreSQL container; real API/auth/ranker; no assignments saved until admin accepts."
            };
            await File.WriteAllTextAsync(Path.Combine(output, "ready.json"), JsonSerializer.Serialize(ready, Json), new UTF8Encoding(false));
            if (options.Verify)
            {
                stage = "real HTTP verification";
                var verification = await VerifyAsync(factory.Services, seed.SemesterId, itProfiles.Length > 0);
                await File.WriteAllTextAsync(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(verification, Json), new UTF8Encoding(false));
            }
            Console.WriteLine(JsonSerializer.Serialize(new { ready = true, projects = 100, enterpriseMentors = 30,
                academicMentors = itProfiles.Length, apiUrl = "http://127.0.0.1:5226", semester = "FA2026", output }, Json));
            using var stop = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
            System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += _ => stop.Cancel();
            try { await Task.Delay(Timeout.Infinite, stop.Token); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            return 0;
        }
        catch (Exception error)
        {
            // Never echo configuration, connection strings, token bodies or professional profile text.
            Console.Error.WriteLine($"AI DEMO100 failed during {stage}: {error.GetType().Name}.");
            if (error is HttpRequestException httpError)
                Console.Error.WriteLine($"HTTP status: {httpError.StatusCode}; transport error: {httpError.InnerException?.GetType().Name ?? "none"}.");
            if (stage == "real API host")
            {
                var message = error.Message;
                if (!Regex.IsMatch(message, "password|secret|token|connection|authorization", RegexOptions.IgnoreCase))
                    Console.Error.WriteLine(message[..Math.Min(message.Length, 1200)]);
            }
            return 1;
        }
    }

    private static Dictionary<string, string?> BuildConfiguration(string connection, string model) => new()
    {
        ["ConnectionStrings:DefaultConnection"] = connection,
        ["Jwt:Issuer"] = "EHub.AiDemo100", ["Jwt:Audience"] = "EHub.AiDemo100.Client",
        ["Jwt:Secret"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
        ["Jwt:AccessTokenExpirationMinutes"] = "120", ["Jwt:RefreshTokenExpirationDays"] = "1",
        ["Mentoring:MatchingProvider"] = "Trained", ["Mentoring:TrainedModelPath"] = Path.GetFullPath(model),
        ["AdminSeed:Email"] = AdminEmail, ["AdminSeed:Password"] = AdminPassword,
        ["AdminSeed:FullName"] = DemoMarker + " Admin",
        ["Google:ClientId"] = "demo-unused.apps.googleusercontent.com",
        ["Cloudinary:CloudName"] = "unused-demo", ["Cloudinary:ApiKey"] = "unused-demo", ["Cloudinary:ApiSecret"] = "unused-demo",
        ["R2:AccountId"] = "unused-demo", ["R2:AccessKeyId"] = "unused-demo", ["R2:SecretAccessKey"] = "unused-demo", ["R2:BucketName"] = "unused-demo",
        ["Frontend:BaseUrl"] = "http://127.0.0.1:5174", ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
        ["Cors:AllowedOrigins:1"] = "http://127.0.0.1:5174", ["Cors:AllowedOrigins:2"] = "http://localhost:5174",
        ["RegistrationOtp:HashKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
        ["Email:Provider"] = "Console", ["Serilog:MinimumLevel:Default"] = "Warning"
    };

    private static async Task<SeedResult> SeedAsync(IServiceProvider services, ProfileRow[] profiles, ProjectRow[] projects)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await db.Database.MigrateAsync();
        await DatabaseSeeder.SeedAllAsync(db, config, hasher);
        var admin = await db.Users.SingleAsync(user => user.NormalizedEmail == AdminEmail);
        var roles = await db.Roles.ToDictionaryAsync(role => role.Name);
        foreach (var item in await db.Semesters.ToArrayAsync()) item.Status = SemesterStatus.Planned;
        // Persist deactivation before activating the demo semester: PostgreSQL
        // enforces that only one semester may have Active status.
        await db.SaveChangesAsync();
        var semester = await db.Semesters.SingleAsync(item => item.Code == "FA2026");
        semester.Name = DemoMarker + " 100 dự án giả lập";
        semester.Status = SemesterStatus.Active;
        var course = await db.Courses.SingleAsync(item => item.Code == "EXE101");
        var lecturer = new User { FullName = DemoMarker + " Giảng viên", Email = "lecturer.ai-demo@example.test",
            NormalizedEmail = "lecturer.ai-demo@example.test", PasswordHash = hasher.Hash(Guid.NewGuid().ToString("N")) };
        lecturer.UserRoles.Add(new UserRole { User = lecturer, Role = roles[SystemRoles.Lecturer] });
        db.Users.Add(lecturer);
        db.SemesterStaffAssignments.Add(new SemesterStaffAssignment { Semester = semester, User = lecturer,
            Role = SemesterStaffRole.Lecturer, Status = SemesterStaffStatus.Active, CreatedBy = admin.Id });
        foreach (var row in profiles)
        {
            var source = row.Profile!;
            var user = new User { FullName = (row.MentorType == "Academic" ? "Mentor IT " : "Mentor ") + row.MentorRef,
                Email = row.MentorRef.ToLowerInvariant() + "@ai-demo.example.test",
                NormalizedEmail = row.MentorRef.ToLowerInvariant() + "@ai-demo.example.test", PasswordHash = hasher.Hash(Guid.NewGuid().ToString("N")) };
            user.UserRoles.Add(new UserRole { User = user, Role = roles[SystemRoles.Mentor] });
            var profile = new MentorProfile { User = user, Type = Enum.Parse<MentorType>(row.MentorType),
                Bio = source.Bio, Experience = source.Experience, Expertise = source.Expertise ?? [],
                StartupDomains = source.StartupDomains ?? [], TechnologySkills = source.TechnologySkills ?? [], Tags = source.Tags ?? [],
                Status = MentorProfileStatus.Active, CreatedBy = admin.Id };
            // Structured historical guidance remains empty unless supplied as verified source data.
            db.MentorProfiles.Add(profile);
            db.SemesterStaffAssignments.Add(new SemesterStaffAssignment { Semester = semester, User = user,
                Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active, CreatedBy = admin.Id });
        }
        var classes = new List<Class>();
        for (var index = 1; index <= 10; index++)
        {
            var classroom = new Class { Semester = semester, Course = course, PrimaryLecturer = lecturer,
                ClassCode = $"AI-DEMO100-{index:00}", Slug = $"ai-demo100-{index:00}", ClassIndex = index,
                Status = ClassStatus.Active, ScheduleJson = "[]", Room = DemoMarker, CreatedById = admin.Id };
            classroom.ClassLecturers.Add(new ClassLecturer { Class = classroom, Lecturer = lecturer, IsPrimary = true, AssignedById = admin.Id });
            db.Classes.Add(classroom);
            classes.Add(classroom);
        }
        for (var index = 0; index < projects.Length; index++)
        {
            var row = projects[index];
            var team = new Team { Class = classes[index / 10], TeamCode = $"P{index + 1:000}",
                TeamName = row.Title, Description = row.Description, CreatedById = admin.Id, Status = TeamStatus.Active };
            team.Project = new Project { Team = team, Name = row.Title, Description = row.Description,
                CreatedById = admin.Id, Status = ProjectStatus.Draft };
            db.Teams.Add(team);
        }
        await db.SaveChangesAsync();
        if (await db.Teams.CountAsync() != 100 || await db.Projects.CountAsync() != 100 ||
            await db.MentorProfiles.CountAsync() != profiles.Length ||
            await db.MentorProfiles.CountAsync(profile => profile.Type == MentorType.Enterprise) != 30 ||
            await db.MentorProfiles.CountAsync(profile => profile.Type == MentorType.Academic) != profiles.Length - 30 ||
            await db.MentorAssignments.AnyAsync()) throw new InvalidDataException("Seed reconciliation failed.");
        return new SeedResult(semester.Id, classes.Select(item => new ClassSeed(item.Id, item.ClassCode)).ToArray());
    }

    private static async Task<object> VerifyAsync(IServiceProvider services, Guid semesterId, bool includesItMentors)
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = new Uri("http://127.0.0.1:5226"), Timeout = TimeSpan.FromSeconds(65) };
        var payload = new PreviewMentorAllocationRequest { SemesterId = semesterId, Strategy = MentorAllocationStrategies.Ai, Seed = 28 };
        using var unauthenticated = await client.PostAsJsonAsync("/api/admin/mentors/allocations/preview", payload);
        if (unauthenticated.StatusCode != HttpStatusCode.Unauthorized) throw new InvalidDataException("Expected 401.");
        using var login = await client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = AdminEmail, Password = AdminPassword });
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json))?.Data ?? throw new InvalidDataException();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        using var previewResponse = await client.PostAsJsonAsync("/api/admin/mentors/allocations/preview", payload);
        previewResponse.EnsureSuccessStatusCode();
        var preview = (await previewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>(Json))?.Data ?? throw new InvalidDataException();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.MentorAssignments.CountAsync();
        var profileTypes = await db.MentorProfiles.AsNoTracking().ToDictionaryAsync(profile => profile.Id, profile => profile.Type);
        var enterprise = preview.Assignments.Where(row => row.MentorType == nameof(MentorType.Enterprise)).ToArray();
        var academic = preview.Assignments.Where(row => row.MentorType == nameof(MentorType.Academic)).ToArray();
        if (preview.TeamCount != 100 || preview.Strategy != MentorAllocationStrategies.Ai ||
            enterprise.Length != 100 || academic.Length != (includesItMentors ? 100 : 0) ||
            preview.Assignments.Count != (includesItMentors ? 200 : 100) || !preview.CanCommit ||
            preview.Assignments.Select(row => (row.TeamId, row.MentorType)).Distinct().Count() != preview.Assignments.Count ||
            preview.Assignments.Any(row => !profileTypes.TryGetValue(row.MentorProfileId, out var type) || row.MentorType != type.ToString()) ||
            preview.UnfilledEnterpriseCount != 0 || preview.UnfilledAcademicCount != (includesItMentors ? 0 : 100) ||
            saved != 0) throw new InvalidDataException("Preview reconciliation failed.");
        return new { passed = true, unauthenticatedStatus = 401, loginSucceeded = true, projects = preview.TeamCount,
            proposedEnterprise = enterprise.Length, proposedAcademic = academic.Length, proposedTotal = preview.Assignments.Count,
            unfilledEnterprise = preview.UnfilledEnterpriseCount,
            needsReviewEnterprise = enterprise.Count(row => row.FitScore == 0), needsReviewAcademic = academic.Count(row => row.FitScore == 0),
            unfilledAcademic = preview.UnfilledAcademicCount, assignmentsBeforeAcceptance = saved, modelProvider = "Trained" };
    }

    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "backend", "EHub.slnx"))) return directory.FullName;
        throw new InvalidDataException("Run the demo from within the E-HUB repository.");
    }

    private sealed record Arguments(string Profiles, string Projects, string Model, string Output, bool Verify, string? ItProfiles)
    {
        internal static Arguments Read(string[] args)
        {
            if (!args.Contains("--demo100", StringComparer.Ordinal)) throw new ArgumentException("Explicit --demo100 is required.");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var switches = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < args.Length; index++)
            {
                if (args[index] is "--demo100" or "--verify") { if (!switches.Add(args[index])) throw new ArgumentException(); continue; }
                if (args[index] is not ("--profiles" or "--projects" or "--model" or "--output" or "--it-profiles") || index + 1 >= args.Length ||
                    !values.TryAdd(args[index], args[++index])) throw new ArgumentException();
            }
            return new Arguments(values["--profiles"], values["--projects"], values["--model"], values["--output"],
                switches.Contains("--verify"), values.GetValueOrDefault("--it-profiles"));
        }
    }
    private sealed record ProfileRow(string MentorRef, string MentorType, ProfessionalProfile? Profile);
    private sealed record ProfessionalProfile(string MentorType, string? Bio, string? Experience, string[]? Expertise,
        string[]? StartupDomains, string[]? TechnologySkills, string[]? Tags);
    private sealed record ProjectRow(string? ProjectId, string Title, string Description)
    {
        public string? Id { get; init; }
        public string Reference => ProjectId ?? Id ?? string.Empty;
    }
    private sealed record ClassSeed(Guid Id, string Code);
    private sealed record SeedResult(Guid SemesterId, ClassSeed[] Classes);
}

internal sealed class UiDemoFactory(Dictionary<string, string?> settings, string connection, string repository)
    : WebApplicationFactory<global::Program>
{
    protected override IEnumerable<Assembly> GetTestAssemblies() => [typeof(UiDemoFactory).Assembly];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(Path.Combine(repository, "backend", "src", "EHub.Api"));
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.Sources.Clear();
            configuration.AddInMemoryCollection(settings);
        });
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            foreach (var hosted in services.Where(item => item.ServiceType == typeof(IHostedService) &&
                         item.ImplementationType?.Assembly == typeof(AppDbContext).Assembly).ToArray())
                services.Remove(hosted);
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection, postgres => postgres.UseVector()));
            Block<IEmailService>(services);
            Block<IGoogleAuthService>(services);
            Block<IImageStorageService>(services);
            Block<ISubmissionFileStorageService>(services);
            Block<ISubmissionObjectStorage>(services);
            services.RemoveAll<IMentorDocumentStorageService>();
            services.AddSingleton<IMentorDocumentStorageService, DemoMentorDocuments>();
            Block<IDocumentPreviewConverter>(services);
            Block<IMentorEmbeddingSearch>(services);
        });
    }
    private static void Block<T>(IServiceCollection services) where T : class
    {
        services.RemoveAll<T>();
        services.AddSingleton(DispatchProxy.Create<T, BlockedDemoProxy>());
    }
}

// Full CV files stay inside this disposable process; no external storage is contacted.
internal sealed class DemoMentorDocuments : IMentorDocumentStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> files = new();

    public async Task<Result<(string Url, string PublicId)>> UploadAsync(Stream content, string fileName,
        Guid mentorId, string kind, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var id = Guid.NewGuid().ToString("N");
        files[id] = buffer.ToArray();
        return Result.Success(($"https://demo-documents.example.test/{id}", id));
    }

    public Task<Result<byte[]>> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address) ||
            address.Host != "demo-documents.example.test" || !files.TryGetValue(address.AbsolutePath.Trim('/'), out var bytes))
            return Task.FromResult(Result.Failure<byte[]>(ErrorCodes.CommonNotFoundError, "Demo document was not found."));
        return Task.FromResult(Result.Success(bytes));
    }

    public Task DeleteAsync(string publicId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        files.TryRemove(publicId, out _);
        return Task.CompletedTask;
    }
}

public class BlockedDemoProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new InvalidOperationException("This isolated demo does not permit external email, authentication providers, storage or embedding calls.");
}
