using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Infrastructure.Persistence.Seed;

public static class DataBankColumnSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (!await context.DataBankColumns.AnyAsync())
        {
            var columns = new[]
            {
                new DataBankColumn
                {
                    Key = "roll_number",
                    DisplayName = "Roll Number",
                    NormalizedKey = "roll_number",
                    DataType = DataBankColumnDataType.Text,
                    Aliases = new[] { "roll number", "student id", "mssv", "student code" },
                    NormalizedAliases = new[] { "rollnumber", "studentid", "mssv", "studentcode" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "student_name",
                    DisplayName = "Student Name",
                    NormalizedKey = "student_name",
                    DataType = DataBankColumnDataType.Text,
                    Aliases = new[] { "student name", "full name", "fullname", "tên sinh viên", "họ và tên" },
                    NormalizedAliases = new[] { "studentname", "fullname", "fullname", "tensinhvien", "hovaten" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "student_email",
                    DisplayName = "Student Email",
                    NormalizedKey = "student_email",
                    DataType = DataBankColumnDataType.Email,
                    Aliases = new[] { "student email", "email", "học viên email" },
                    NormalizedAliases = new[] { "studentemail", "email", "hocvienemail" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "class_code",
                    DisplayName = "Class Code",
                    NormalizedKey = "class_code",
                    DataType = DataBankColumnDataType.Text,
                    Aliases = new[] { "class code", "class", "lớp", "mã lớp" },
                    NormalizedAliases = new[] { "classcode", "class", "lop", "malop" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "team_code",
                    DisplayName = "Team Code",
                    NormalizedKey = "team_code",
                    DataType = DataBankColumnDataType.Text,
                    Aliases = new[] { "team code", "group code", "mã nhóm" },
                    NormalizedAliases = new[] { "teamcode", "groupcode", "manhom" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "team_name",
                    DisplayName = "Team Name",
                    NormalizedKey = "team_name",
                    DataType = DataBankColumnDataType.Text,
                    Aliases = new[] { "team name", "group name", "tên nhóm" },
                    NormalizedAliases = new[] { "teamname", "groupname", "tennhom" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "project_name",
                    DisplayName = "Project Name",
                    NormalizedKey = "project_name",
                    DataType = DataBankColumnDataType.Text,
                    Aliases = new[] { "project name", "startup name", "tên dự án", "đề tài" },
                    NormalizedAliases = new[] { "projectname", "startupname", "tenduan", "detai" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "startup_field",
                    DisplayName = "Startup Field",
                    NormalizedKey = "startup_field",
                    DataType = DataBankColumnDataType.Text,
                    Aliases = new[] { "startup field", "field", "domain", "lĩnh vực", "ngành" },
                    NormalizedAliases = new[] { "startupfield", "field", "domain", "linhvuc", "nganh" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "business_model",
                    DisplayName = "Business Model",
                    NormalizedKey = "business_model",
                    DataType = DataBankColumnDataType.Text,
                    Aliases = new[] { "business model", "model", "mô hình kinh doanh" },
                    NormalizedAliases = new[] { "businessmodel", "model", "mohinhkinhdoanh" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "technology",
                    DisplayName = "Technology Stack",
                    NormalizedKey = "technology",
                    DataType = DataBankColumnDataType.Text,
                    Aliases = new[] { "technology", "tech stack", "công nghệ" },
                    NormalizedAliases = new[] { "technology", "techstack", "congnghe" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "is_high_potential",
                    DisplayName = "Is High Potential",
                    NormalizedKey = "is_high_potential",
                    DataType = DataBankColumnDataType.Boolean,
                    Aliases = new[] { "is high potential", "potential", "tiềm năng" },
                    NormalizedAliases = new[] { "ishighpotential", "potential", "tiemnang" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "evaluation_score",
                    DisplayName = "Evaluation Score",
                    NormalizedKey = "evaluation_score",
                    DataType = DataBankColumnDataType.Number,
                    Aliases = new[] { "evaluation score", "score", "grade", "điểm", "điểm số" },
                    NormalizedAliases = new[] { "evaluationscore", "score", "grade", "diem", "diemso" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new DataBankColumn
                {
                    Key = "mentor_name",
                    DisplayName = "Mentor Name",
                    NormalizedKey = "mentor_name",
                    DataType = DataBankColumnDataType.Text,
                    Aliases = new[] { "mentor name", "mentor", "người hướng dẫn" },
                    NormalizedAliases = new[] { "mentorname", "mentor", "nguoihuongdan" },
                    IsSystemField = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            await context.DataBankColumns.AddRangeAsync(columns);
            await context.SaveChangesAsync();
        }
    }
}
