using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Admin.Mentors;

internal static class MentorDraftFields
{
    // Column names as they appear in the mentor workbook, so the admin knows what to fill in.
    public static IReadOnlyCollection<string> GetMissing(MentorImportDraft mentor)
    {
        var fields = new List<string>();
        if (string.IsNullOrWhiteSpace(mentor.Email)) fields.Add(mentor.Type == MentorType.Academic ? "Email công việc" : "Email");
        if (mentor.Type == MentorType.Enterprise)
        {
            if (mentor.DateOfBirth is null) fields.Add("Ngày tháng năm sinh");
            if (string.IsNullOrWhiteSpace(mentor.Phone)) fields.Add("SDT");
            if (string.IsNullOrWhiteSpace(mentor.ContractType)) fields.Add("Loại HĐ");
            if (string.IsNullOrWhiteSpace(mentor.EducationLevel)) fields.Add("Trình độ học vấn");
            if (string.IsNullOrWhiteSpace(mentor.CurrentAddress)) fields.Add("Địa chỉ hiện nay");
            if (string.IsNullOrWhiteSpace(mentor.JobTitle)) fields.Add("Vị trí, Chức danh");
            if (string.IsNullOrWhiteSpace(mentor.Organization)) fields.Add("Công ty");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(mentor.Department)) fields.Add("Phòng ban trực tiếp");
            if (string.IsNullOrWhiteSpace(mentor.JobTitle)) fields.Add("Chức danh (VN)");
        }
        return fields;
    }
}
