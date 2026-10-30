export interface AutoSizeInput {
  scrollHeight: number;
  lineHeight: number;
  verticalPadding: number;
  verticalBorder: number;
  minRows: number;
  maxRows: number;
}

export interface AutoSizeResult {
  height: number;
  overflowY: 'hidden' | 'auto';
}

// Grows a textarea with its content between minRows and maxRows; past maxRows it scrolls instead.
// scrollHeight includes padding but not the border, so the border is added back for border-box sizing.
export function autoSizeHeight({
  scrollHeight, lineHeight, verticalPadding, verticalBorder, minRows, maxRows,
}: AutoSizeInput): AutoSizeResult {
  const rowsToHeight = (rows: number) => rows * lineHeight + verticalPadding + verticalBorder;
  const minHeight = rowsToHeight(Math.max(1, minRows));
  const maxHeight = rowsToHeight(Math.max(minRows, maxRows));
  const contentHeight = scrollHeight + verticalBorder;
  if (contentHeight > maxHeight) return { height: maxHeight, overflowY: 'auto' };
  return { height: Math.max(minHeight, contentHeight), overflowY: 'hidden' };
}

// Chat-style sending: Enter sends, Shift+Enter adds a line. Never send while an input method (e.g. a Vietnamese
// keyboard) is still composing a character, or a half-typed word would be posted.
export const shouldSubmitOnEnter = ({ key, shiftKey, isComposing }: {
  key: string;
  shiftKey: boolean;
  isComposing: boolean;
}): boolean => key === 'Enter' && !shiftKey && !isComposing;
