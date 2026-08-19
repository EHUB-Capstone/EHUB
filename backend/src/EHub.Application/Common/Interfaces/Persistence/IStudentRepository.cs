using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Persistence;

public interface IStudentRepository
{
    Task<Student?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Student?> GetUnlinkedByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Student student,
        CancellationToken cancellationToken = default);

    void Update(Student student);
}
