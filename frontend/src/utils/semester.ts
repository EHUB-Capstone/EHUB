export function formatSemesterCode(
  semester: string | null | undefined,
  year: number | string | null | undefined,
): string {
  const compactSemester = String(semester ?? '').trim().toUpperCase().replace(/\s+/g, '');
  const yearText = String(year ?? '').trim();

  if (!compactSemester) return yearText || '—';
  if (!yearText || compactSemester.endsWith(yearText)) return compactSemester;

  return `${compactSemester}${yearText}`;
}
