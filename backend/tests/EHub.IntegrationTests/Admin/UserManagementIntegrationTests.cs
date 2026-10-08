using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Admin.Users.ManageUsers;
using EHub.Contracts.Users;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Identity;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EHub.IntegrationTests.Admin;

public sealed class UserManagementIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer =
        new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("user_management_test_db")
            .WithUsername("user_management_test_user")
            .WithPassword("user_management_test_password")
            .Build();

    private AppDbContext _context = null!;
    private Guid _adminId;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;
        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        var adminRole = new Role { Name = SystemRoles.Admin };
        var lecturerRole = new Role { Name = SystemRoles.Lecturer };
        var mentorRole = new Role { Name = SystemRoles.Mentor };
        var studentRole = new Role { Name = SystemRoles.Student };
        var admin = new User
        {
            FullName = "User Management Admin",
            Email = "user-management-admin@example.com",
            NormalizedEmail = "user-management-admin@example.com",
            PasswordHash = "integration-test-only",
            Status = UserStatus.Active
        };

        await _context.Roles.AddRangeAsync(adminRole, lecturerRole, mentorRole, studentRole);
        await _context.Users.AddAsync(admin);
        await _context.UserRoles.AddAsync(new UserRole
        {
            UserId = admin.Id,
            RoleId = adminRole.Id,
            User = admin,
            Role = adminRole
        });
        await _context.SaveChangesAsync();
        _adminId = admin.Id;
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task UpdateUserAsync_ReplacesRoleAssignment_WhenRoleChanges()
    {
        var handler = new UserManagementHandler(
            _context,
            new TestCurrentUser(_adminId),
            new BCryptPasswordHasher());
        var unique = Guid.NewGuid().ToString("N");

        var createResult = await handler.CreateUserAsync(new SaveManagedUserRequest
        {
            Name = "Role Change User",
            Email = $"role-change-{unique}@example.com",
            Password = "Temporary123",
            Role = "LECTURER",
            Status = "APPROVED"
        });

        createResult.IsSuccess.Should().BeTrue();
        _context.ChangeTracker.Clear();

        var updateResult = await handler.UpdateUserAsync(
            createResult.Value.Id,
            new SaveManagedUserRequest
            {
                Name = createResult.Value.Name,
                Email = createResult.Value.Email,
                Role = "MENTOR",
                MentorType = "Academic",
                Status = createResult.Value.Status
            });

        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value.Role.Should().Be("MENTOR");

        _context.ChangeTracker.Clear();
        var assignedRoles = await _context.UserRoles
            .Where(userRole => userRole.UserId == createResult.Value.Id)
            .Select(userRole => userRole.Role.Name)
            .ToListAsync();
        assignedRoles.Should().Equal(SystemRoles.Mentor);
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsClassAndGroupFromCurrentSemester()
    {
        var handler = new UserManagementHandler(
            _context,
            new TestCurrentUser(_adminId),
            new BCryptPasswordHasher());
        var unique = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var email = $"current-semester-{unique}@example.com";

        var createResult = await handler.CreateUserAsync(new SaveManagedUserRequest
        {
            Name = "Current Semester Student",
            Email = email,
            Password = "Temporary123",
            Role = "STUDENT",
            Status = "APPROVED",
            StudentId = $"SE{unique}",
            ProgramGroup = "BIT",
            Major = MajorCodes.BIT_SE
        });
        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.Role.Should().Be("STUDENT");
        var lecturerResult = await handler.CreateUserAsync(new SaveManagedUserRequest
        {
            Name = "Current Semester Lecturer",
            Email = $"current-semester-lecturer-{unique}@example.com",
            Password = "Temporary123",
            Role = "LECTURER",
            Status = "APPROVED"
        });
        lecturerResult.IsSuccess.Should().BeTrue();

        var semester = new Semester
        {
            Code = $"FA{unique[..4]}",
            Name = $"Fall {unique}",
            Term = SemesterTerm.Fall,
            Year = 2099,
            Status = SemesterStatus.Active,
            CreatedBy = _adminId
        };
        var course = new Course
        {
            Code = $"U{unique[..7]}",
            Name = $"User Management {unique}",
            Status = CourseStatus.Active,
            CreatedBy = _adminId
        };
        var currentClass = new Class
        {
            ClassCode = $"UM-{unique}",
            Slug = $"um-{unique.ToLowerInvariant()}",
            ClassIndex = 1,
            SemesterId = semester.Id,
            Semester = semester,
            CourseId = course.Id,
            Course = course,
            PrimaryLecturerId = lecturerResult.Value.Id,
            ScheduleJson = "[{\"dayOfWeek\":1,\"slotNumber\":1,\"room\":\"UM-101\"}]",
            Status = ClassStatus.Active,
            CreatedById = _adminId,
            CreatedBy = _adminId
        };
        var student = await _context.Students.SingleAsync(item => item.UserId == createResult.Value.Id);
        var enrollment = new ClassStudent
        {
            ClassId = currentClass.Id,
            Class = currentClass,
            StudentId = student.Id,
            Student = student,
            SemesterId = semester.Id,
            CourseId = course.Id,
            EnrollmentStatus = EnrollmentStatus.Active,
            CountsTowardCourseSemesterLimit = true,
            MajorCodeAtEnrollment = MajorCodes.BIT_SE
        };
        var team = new Team
        {
            ClassId = currentClass.Id,
            Class = currentClass,
            TeamCode = $"G-{unique}",
            TeamName = $"Group {unique}",
            Status = TeamStatus.Active,
            CreatedById = _adminId,
            CreatedBy = _adminId
        };

        _context.Semesters.Add(semester);
        _context.Courses.Add(course);
        _context.Classes.Add(currentClass);
        _context.ClassStudents.Add(enrollment);
        _context.Teams.Add(team);
        _context.TeamMembers.Add(new TeamMember
        {
            TeamId = team.Id,
            Team = team,
            ClassId = currentClass.Id,
            StudentId = student.Id,
            ClassStudent = enrollment,
            RoleInTeam = TeamMemberRole.Leader,
            CountsTowardActiveTeam = true,
            CreatedById = _adminId
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await handler.GetUsersAsync(1, 10, email, "STUDENT", "APPROVED", null);

        result.IsSuccess.Should().BeTrue();
        var user = result.Value.Users.Should().ContainSingle().Subject;
        user.Semester.Should().Be(semester.Code);
        user.Class.Should().Be(currentClass.ClassCode);
        user.GroupName.Should().Be(team.TeamName);
        user.MentorType.Should().BeNull("only mentors have a mentor type");
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsMentorType_AndFiltersByIt()
    {
        var handler = new UserManagementHandler(_context, new TestCurrentUser(_adminId), new BCryptPasswordHasher());
        var unique = Guid.NewGuid().ToString("N")[..8];
        async Task<Guid> CreateMentorAsync(string name, MentorType type)
        {
            var created = await handler.CreateUserAsync(new SaveManagedUserRequest
            {
                Name = name, Email = $"{name.ToLowerInvariant().Replace(' ', '-')}-{unique}@example.com",
                Password = "Temporary123", Role = "MENTOR", MentorType = type.ToString(), Status = "APPROVED"
            });
            created.IsSuccess.Should().BeTrue();
            _context.ChangeTracker.Clear();
            _context.ChangeTracker.Clear();
            return created.Value.Id;
        }
        var industry = await CreateMentorAsync("Filter Industry", MentorType.Enterprise);
        var lecturer = await CreateMentorAsync("Filter Lecturer", MentorType.Academic);

        var all = await handler.GetUsersAsync(1, 100, unique, "MENTOR", null, null);
        var industryOnly = await handler.GetUsersAsync(1, 100, unique, "MENTOR", null, "Enterprise");
        var lecturerOnly = await handler.GetUsersAsync(1, 100, unique, "MENTOR", null, "academic");
        var invalid = await handler.GetUsersAsync(1, 100, unique, "MENTOR", null, "Unknown");

        all.Value.Users.Should().HaveCount(2);
        all.Value.Users.Single(item => item.Id == industry).MentorType.Should().Be("Enterprise");
        all.Value.Users.Single(item => item.Id == lecturer).MentorType.Should().Be("Academic");
        industryOnly.Value.Users.Should().ContainSingle().Which.Id.Should().Be(industry);
        lecturerOnly.Value.Users.Should().ContainSingle().Which.Id.Should().Be(lecturer);
        invalid.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUserAsync_RequiresAValidMentorType_ForMentorsOnly()
    {
        var handler = new UserManagementHandler(_context, new TestCurrentUser(_adminId), new BCryptPasswordHasher());
        var unique = Guid.NewGuid().ToString("N")[..8];
        SaveManagedUserRequest Request(string role, string? mentorType) => new()
        {
            Name = $"Typed {role}", Email = $"typed-{role.ToLowerInvariant()}-{mentorType ?? "none"}-{unique}@example.com",
            Password = "Temporary123", Role = role, MentorType = mentorType, Status = "APPROVED"
        };

        (await handler.CreateUserAsync(Request("MENTOR", null))).IsFailure.Should().BeTrue("a mentor must say which kind they are");
        (await handler.CreateUserAsync(Request("MENTOR", "Unknown"))).IsFailure.Should().BeTrue();
        var enterprise = await handler.CreateUserAsync(Request("MENTOR", "enterprise"));
        var academic = await handler.CreateUserAsync(Request("MENTOR", "Academic"));
        var lecturer = await handler.CreateUserAsync(Request("LECTURER", null));

        enterprise.IsSuccess.Should().BeTrue();
        enterprise.Value.MentorType.Should().Be("Enterprise");
        academic.Value.MentorType.Should().Be("Academic");
        lecturer.IsSuccess.Should().BeTrue("other roles do not need a mentor type");
        lecturer.Value.MentorType.Should().BeNull();
        _context.ChangeTracker.Clear();
        (await _context.MentorProfiles.AsNoTracking().SingleAsync(item => item.UserId == academic.Value.Id)).Type.Should().Be(MentorType.Academic);
        (await _context.MentorProfiles.AsNoTracking().SingleAsync(item => item.UserId == enterprise.Value.Id)).Type.Should().Be(MentorType.Enterprise);
    }

    [Fact]
    public async Task UpdateUserAsync_RequiresATypeWhenSwitchingToMentor_AndNeverChangesAnExistingMentorType()
    {
        var handler = new UserManagementHandler(_context, new TestCurrentUser(_adminId), new BCryptPasswordHasher());
        var unique = Guid.NewGuid().ToString("N")[..8];
        var lecturer = await handler.CreateUserAsync(new SaveManagedUserRequest
        {
            Name = "Becomes Mentor", Email = $"becomes-mentor-{unique}@example.com", Password = "Temporary123", Role = "LECTURER", Status = "APPROVED"
        });
        _context.ChangeTracker.Clear();
        SaveManagedUserRequest AsMentor(string? type) => new()
        {
            Name = lecturer.Value.Name, Email = lecturer.Value.Email, Role = "MENTOR", MentorType = type, Status = "APPROVED"
        };

        (await handler.UpdateUserAsync(lecturer.Value.Id, AsMentor(null))).IsFailure.Should().BeTrue("switching to mentor needs a type");
        _context.ChangeTracker.Clear();
        (await handler.UpdateUserAsync(lecturer.Value.Id, AsMentor("Academic"))).IsSuccess.Should().BeTrue();
        _context.ChangeTracker.Clear();

        (await handler.UpdateUserAsync(lecturer.Value.Id, AsMentor("Enterprise"))).IsFailure.Should().BeTrue("the type of an existing mentor is not changed here");
        _context.ChangeTracker.Clear();
        (await handler.UpdateUserAsync(lecturer.Value.Id, AsMentor(null))).IsSuccess.Should().BeTrue("editing other fields keeps the current type");
        _context.ChangeTracker.Clear();
        (await _context.MentorProfiles.AsNoTracking().SingleAsync(item => item.UserId == lecturer.Value.Id)).Type.Should().Be(MentorType.Academic);
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => new[] { SystemRoles.Admin };
        public bool IsAuthenticated => true;
    }
}
