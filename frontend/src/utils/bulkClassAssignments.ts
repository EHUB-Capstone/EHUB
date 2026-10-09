export interface ParsedClassIndices {
  classIndices: number[];
  error: string | null;
}

export function parseClassIndices(value: string, startClassIndex: number, quantity: number): ParsedClassIndices {
  const normalized = value.trim();
  if (!normalized) return { classIndices: [], error: null };

  const lastClassIndex = startClassIndex + quantity - 1;
  if (!Number.isInteger(startClassIndex) || startClassIndex < 1 ||
      !Number.isInteger(quantity) || quantity < 1 || quantity > 100 || lastClassIndex > 999) {
    return { classIndices: [], error: 'Enter a valid starting class index and number of classes first.' };
  }

  const boundsError = `Class numbers must be between ${startClassIndex} and ${lastClassIndex}.`;
  const classIndices: number[] = [];
  for (const token of normalized.split(',').map(item => item.trim()).filter(Boolean)) {
    const range = token.match(/^(\d+)\s*-\s*(\d+)$/);
    if (range) {
      const from = Number(range[1]);
      const to = Number(range[2]);
      if (from > to) return { classIndices: [], error: `Invalid descending range “${token}”.` };
      if (from < startClassIndex || to > lastClassIndex) {
        return { classIndices: [], error: boundsError };
      }
      for (let classIndex = from; classIndex <= to; classIndex += 1) classIndices.push(classIndex);
      continue;
    }
    if (!/^\d+$/.test(token)) return { classIndices: [], error: 'Use class numbers separated by commas or ranges.' };
    classIndices.push(Number(token));
  }

  if (classIndices.some(classIndex => classIndex < startClassIndex || classIndex > lastClassIndex)) {
    return { classIndices: [], error: boundsError };
  }
  if (new Set(classIndices).size !== classIndices.length) {
    return { classIndices: [], error: 'A class number is repeated in this lecturer assignment.' };
  }
  return { classIndices, error: null };
}
