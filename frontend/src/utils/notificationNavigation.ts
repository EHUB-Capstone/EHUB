interface NotificationNavigationSource {
  type?: string | null;
  link?: string | null;
  data?: {
    teamId?: string | null;
  } | null;
}

interface LecturerDirectionTarget {
  semester: string;
  year: number | string;
  classId: string;
  teamId?: string | null;
}

const normalizeNotificationType = (type?: string | null): string =>
  String(type || '').replaceAll('_', '').toUpperCase();

export const isProjectDirectionSubmittedNotification = (
  notification: NotificationNavigationSource,
): boolean => normalizeNotificationType(notification.type) === 'PROJECTDIRECTIONSUBMITTED';

export const getLegacyNotificationClassId = (
  notification: NotificationNavigationSource,
): string => {
  const match = notification.link?.match(/^\/classes\/([^/?#]+)$/i);
  return match?.[1] ? decodeURIComponent(match[1]) : '';
};

export const buildLecturerDirectionOverviewLink = ({
  semester,
  year,
  classId,
  teamId,
}: LecturerDirectionTarget): string => {
  const params = new URLSearchParams();
  params.set('semester', semester.trim().toUpperCase());
  params.set('year', String(year));
  params.set('tab', 'overview');
  params.set('classId', classId);
  if (teamId) params.set('teamId', teamId);
  return `/lecturer/classes?${params.toString()}`;
};
