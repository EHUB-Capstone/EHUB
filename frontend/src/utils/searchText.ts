/**
 * Lower-cases text and strips Vietnamese diacritics so "bu" matches "Bùi" and "ma" matches "Mạnh".
 * Vietnamese IMEs (Telex/VNI) produce accent-less or half-accented text while typing, so an
 * accent-sensitive match makes results flicker between hit and miss on every keystroke.
 */
export const normalizeSearchText = (value: unknown): string => String(value ?? '')
  .normalize('NFD')
  .replace(/[̀-ͯ]/g, '')
  .replace(/đ/gi, 'd')
  .toLowerCase()
  .trim();

/** True when every whitespace-separated term of `query` appears in at least one of `fields`. */
export const matchesSearchQuery = (query: string, fields: unknown[]): boolean => {
  const terms = normalizeSearchText(query).split(/\s+/).filter(Boolean);
  if (terms.length === 0) return true;
  const haystack = fields.map(normalizeSearchText).join(' ');
  return terms.every((term) => haystack.includes(term));
};
