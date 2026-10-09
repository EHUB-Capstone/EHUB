export const USER_DIRECTORY_MAX_PAGE_SIZE = 100;

export const buildApprovedLecturerQuery = (page = 1) => ({
  page,
  limit: USER_DIRECTORY_MAX_PAGE_SIZE,
  role: 'LECTURER' as const,
  status: 'APPROVED' as const,
});

export const normalizeLecturerOptions = (users: any[] = []) => users
  .map(user => ({
    ...user,
    _id: user._id || user.id,
    name: user.name || user.fullName,
  }))
  .filter(user => Boolean(user._id && user.name));
