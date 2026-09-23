export const CHECKPOINT_END_AFTER_START_ERROR = 'End date and time must be after the start date and time.';

export function checkpointDateParts(value: string, defaultTime: string) {
  const [date = '', time] = value.split('T');
  const [hour = '', minute = ''] = (time || defaultTime).split(':');
  return { date, hour, minute };
}

export function parseCheckpointLocalDateTime(value: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value);
  if (!match) return null;
  const [, year, month, day, hour, minute] = match.map(Number);
  if (month < 1 || month > 12 || day < 1 || day > 31 || hour > 23 || minute > 59) return null;
  const result = new Date(year, month - 1, day, hour, minute);
  return result.getFullYear() === year && result.getMonth() === month - 1 &&
    result.getDate() === day && result.getHours() === hour && result.getMinutes() === minute
    ? result : null;
}

export function checkpointDateRangeError(startValue: string, endValue: string): string {
  const start = parseCheckpointLocalDateTime(startValue);
  const end = parseCheckpointLocalDateTime(endValue);
  return start && end && end <= start ? CHECKPOINT_END_AFTER_START_ERROR : '';
}

export function formatCheckpointDateTime(value?: string | Date | null): string {
  if (!value) return '—';
  const date = typeof value === 'string' ? new Date(value) : value;
  if (Number.isNaN(date.getTime())) return '—';
  const two = (part: number) => String(part).padStart(2, '0');
  return `${two(date.getDate())}/${two(date.getMonth() + 1)}/${date.getFullYear()} ${two(date.getHours())}:${two(date.getMinutes())}`;
}
