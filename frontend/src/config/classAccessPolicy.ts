export const classRouteAccess = Object.freeze({
  lecturerArea: Object.freeze(['LECTURER']),
  classDetail: Object.freeze(['ADMIN', 'LECTURER']),
});

export const canAccessClassRoute = (
  role: string,
  allowedRoles: readonly string[],
): boolean => allowedRoles.includes(role.trim().toUpperCase());
