export function generateCriterionKey(label: string): string {
  const words = label
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .split(/[^A-Za-z0-9]+/)
    .filter(Boolean);

  const key = words
    .map((word, index) => {
      const normalizedWord = word.toLowerCase();
      return index === 0
        ? normalizedWord
        : normalizedWord.charAt(0).toUpperCase() + normalizedWord.slice(1);
    })
    .join('');

  if (!key || /^[A-Za-z]/.test(key)) return key;
  return `criterion${key.charAt(0).toUpperCase()}${key.slice(1)}`;
}

export function resolveCriterionKey(currentKey: string, currentLabel: string, nextLabel: string): string {
  const currentGeneratedKey = generateCriterionKey(currentLabel);
  return !currentKey || currentKey === currentGeneratedKey
    ? generateCriterionKey(nextLabel)
    : currentKey;
}
