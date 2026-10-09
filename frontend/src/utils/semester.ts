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

export function shortenSemesterCode(code: string | null | undefined): string {
  const normalizedCode = String(code ?? '').trim().toUpperCase();
  if (!normalizedCode) return '';

  const letters = normalizedCode.match(/[A-Z]/g)?.join('') ?? '';
  const digits = normalizedCode.match(/\d/g)?.join('') ?? '';
  return `${letters}${digits.length >= 2 ? digits.slice(-2) : digits}`;
}
