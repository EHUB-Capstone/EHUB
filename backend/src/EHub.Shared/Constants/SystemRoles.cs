namespace EHub.Shared.Constants;

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Lecturer = "Lecturer";
    public const string Student = "Student";
    public const string Mentor = "Mentor";

    public static readonly string[] All =
    [
        Admin,
        Lecturer,
        Student,
        Mentor
    ];

    public static readonly string[] PublicRegisterRoles =
    [
        Lecturer,
        Student,
        Mentor
    ];

    public static bool IsPublicRegisterRole(string role)
    {
        for (int i = 0; i < PublicRegisterRoles.Length; i++)
        {
            if (PublicRegisterRoles[i] == role) return true;
        }
        return false;
    }
}
