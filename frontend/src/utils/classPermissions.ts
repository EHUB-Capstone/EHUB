type EntityReference = string | { id?: string | null; _id?: string | null } | null | undefined;

export interface ClassPermissionUser {
  id?: string | null;
  _id?: string | null;
  role?: string | null;
  roles?: string[] | null;
}

export interface ClassPermissionTarget {
  primaryLecturerId?: string | null;
  lectureId?: EntityReference;
}

const normalizeRole = (role?: string | null) => role?.trim().toUpperCase() || '';

const entityId = (reference: EntityReference): string => {
  if (!reference) return '';
  if (typeof reference === 'string') return reference.trim().toLowerCase();
  return String(reference.id || reference._id || '').trim().toLowerCase();
};

export const hasClassRole = (user: ClassPermissionUser | null | undefined, role: string): boolean => {
  const expected = normalizeRole(role);
  if (normalizeRole(user?.role) === expected) return true;
  return (user?.roles || []).some(candidate => normalizeRole(candidate) === expected);
};

export const canManageClass = (
  user: ClassPermissionUser | null | undefined,
  targetClass: ClassPermissionTarget | null | undefined,
): boolean => {
  if (hasClassRole(user, 'ADMIN')) return true;
  if (!hasClassRole(user, 'LECTURER')) return false;

  const userId = entityId(user);
  const lecturerId = entityId(targetClass?.primaryLecturerId || targetClass?.lectureId);
  return Boolean(userId && lecturerId && userId === lecturerId);
};
