import type { TeachingStaffDto } from '../types/subjects';
import { matchesSearchQuery } from './searchText.ts';

export type StaffStatusFilter = 'ALL' | 'ACTIVE' | 'INACTIVE' | 'NEEDS_INFORMATION';

export type StaffRoleFilter = 'ALL' | TeachingStaffDto['role'] | 'ENTERPRISE_MENTOR' | 'ACADEMIC_MENTOR';

export interface TeachingStaffFilters {
  search: string;
  role: StaffRoleFilter;
  status: StaffStatusFilter;
}

export type StaffKind = 'LECTURER' | 'ENTERPRISE_MENTOR' | 'ACADEMIC_MENTOR' | 'MENTOR';

export function getStaffKind(member: Pick<TeachingStaffDto, 'role' | 'mentorType'>): StaffKind {
  if (member.role === 'LECTURER') return 'LECTURER';
  if (member.mentorType === 'Enterprise') return 'ENTERPRISE_MENTOR';
  if (member.mentorType === 'Academic') return 'ACADEMIC_MENTOR';
  return 'MENTOR';
}

export function filterTeachingStaff(
  staff: TeachingStaffDto[],
  filters: TeachingStaffFilters,
): TeachingStaffDto[] {
  return staff.filter((member) => {
    const matchesRole = filters.role === 'ALL'
      || member.role === filters.role
      || getStaffKind(member) === filters.role;
    const matchesStatus = filters.status === 'ALL'
      || (filters.status === 'NEEDS_INFORMATION' && member.isIncomplete)
      || (filters.status === 'ACTIVE' && !member.isIncomplete && member.status === 'Active')
      || (filters.status === 'INACTIVE' && !member.isIncomplete && member.status === 'Inactive');
    const matchesSearch = matchesSearchQuery(filters.search, [
      member.name,
      member.email,
      ...(member.missingFields ?? []),
      ...member.assignments.flatMap(item => [item.classCode, item.subjectCode]),
    ]);

    return matchesRole && matchesStatus && matchesSearch;
  });
}

export function paginateTeachingStaff<T>(items: T[], requestedPage: number, pageSize: number) {
  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));
  const page = Math.min(Math.max(1, requestedPage), totalPages);
  const startIndex = (page - 1) * pageSize;

  return {
    items: items.slice(startIndex, startIndex + pageSize),
    page,
    pageSize,
    totalPages,
    rangeStart: items.length === 0 ? 0 : startIndex + 1,
    rangeEnd: Math.min(startIndex + pageSize, items.length),
    totalItems: items.length,
  };
}
