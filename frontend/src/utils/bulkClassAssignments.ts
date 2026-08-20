export interface ParsedClassPositions {
  positions: number[];
  error: string | null;
}

export function parseClassPositions(value: string, quantity: number): ParsedClassPositions {
  const normalized = value.trim();
  if (!normalized) return { positions: [], error: null };

  const positions: number[] = [];
  for (const token of normalized.split(',').map(item => item.trim()).filter(Boolean)) {
    const range = token.match(/^(\d+)\s*-\s*(\d+)$/);
    if (range) {
      const from = Number(range[1]);
      const to = Number(range[2]);
      if (from > to) return { positions: [], error: `Invalid descending range “${token}”.` };
      if (from < 1 || to > quantity) {
        return { positions: [], error: `Positions must be between 1 and ${quantity}.` };
      }
      for (let position = from; position <= to; position += 1) positions.push(position);
      continue;
    }
    if (!/^\d+$/.test(token)) return { positions: [], error: 'Use positions such as 1,3,5-8.' };
    positions.push(Number(token));
  }

  if (positions.some(position => position < 1 || position > quantity)) {
    return { positions: [], error: `Positions must be between 1 and ${quantity}.` };
  }
  if (new Set(positions).size !== positions.length) {
    return { positions: [], error: 'A position is repeated in this lecturer assignment.' };
  }
  return { positions, error: null };
}
