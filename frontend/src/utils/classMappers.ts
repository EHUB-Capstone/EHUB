import type {
  ApiEnvelope,
  ClassDto,
  ClassScheduleSlot,
  ClassViewModel,
} from '../types/classes';

export const unwrapApiData = <T = any>(response: ApiEnvelope<T> | T): T => {
  if (response && typeof response === 'object' && 'data' in response) {
    return (response as ApiEnvelope<T>).data;
  }
  return response as T;
};

const normalizeSchedules = (source: ClassDto): ClassScheduleSlot[] => {
  if (Array.isArray(source.schedules)) return source.schedules;
  if (!source.scheduleJson) return [];

  try {
    const parsed = JSON.parse(source.scheduleJson) as unknown;
    return Array.isArray(parsed) ? parsed as ClassScheduleSlot[] : [];
  } catch {
    return [];
  }
};

export const toClassViewModel = (source: ClassDto): ClassViewModel => ({
  ...source,
  _id: source.id,
  semester: source.semesterCode.slice(0, 2).toUpperCase(),
  schedules: normalizeSchedules(source),
  lectureId: source.primaryLecturerId
    ? {
        _id: source.primaryLecturerId,
        name: source.primaryLecturerName || 'Unknown lecturer',
        email: source.primaryLecturerEmail,
      }
    : null,
});
