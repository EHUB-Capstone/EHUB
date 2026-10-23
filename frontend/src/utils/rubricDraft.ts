import type { RubricRow } from './rubricFormState.ts';

// Unsaved rubric edits are kept per browser tab (sessionStorage) so a reload or crash does not lose them.
// This is separate from the "Save draft" button, which stores a DRAFT evaluation on the server.
export const RUBRIC_DRAFT_PREFIX = 'ehub:rubric-draft:';
const RUBRIC_DRAFT_VERSION = 1;

export type DraftStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem' | 'key' | 'length'>;

export interface RubricDraft {
  version: number;
  // Sync key of the server version the edits were made against.
  baselineKey: string;
  savedAt: number;
  rows: Array<{ criterionKey: string; score: number | string; comment: string }>;
  feedback: string;
  overrides: Record<string, string>;
}

export type DraftRestoreAction = 'discard' | 'restore' | 'offer';

export const rubricDraftKey = (
  userId: string,
  teamId: string,
  checkpointNumber: number | string,
  evaluationId?: string | null,
): string => `${RUBRIC_DRAFT_PREFIX}${userId}:${teamId}:${checkpointNumber}:${evaluationId || 'new'}`;

// Storage can be missing or throw (private mode, blocked site data), and the form must work without it.
export const getBrowserStorage = (): DraftStorage | null => {
  try {
    return typeof window === 'undefined' ? null : window.sessionStorage;
  } catch {
    return null;
  }
};

export const saveRubricDraft = (storage: DraftStorage | null, key: string, draft: RubricDraft): void => {
  if (!storage) return;
  try {
    storage.setItem(key, JSON.stringify(draft));
  } catch {
    // Quota or blocked storage: losing the safety net must not break grading.
  }
};

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

export const loadRubricDraft = (storage: DraftStorage | null, key: string): RubricDraft | null => {
  if (!storage) return null;
  try {
    const raw = storage.getItem(key);
    if (!raw) return null;
    const parsed: unknown = JSON.parse(raw);
    if (!isRecord(parsed) || parsed.version !== RUBRIC_DRAFT_VERSION) return null;
    if (typeof parsed.baselineKey !== 'string' || typeof parsed.savedAt !== 'number') return null;
    if (typeof parsed.feedback !== 'string' || !isRecord(parsed.overrides) || !Array.isArray(parsed.rows)) return null;
    const rows = parsed.rows.every((row) => isRecord(row) && typeof row.criterionKey === 'string'
      && (typeof row.score === 'number' || typeof row.score === 'string') && typeof row.comment === 'string');
    const overrides = Object.values(parsed.overrides).every((value) => typeof value === 'string');
    return rows && overrides ? parsed as unknown as RubricDraft : null;
  } catch {
    return null;
  }
};

export const clearRubricDraft = (storage: DraftStorage | null, key: string): void => {
  if (!storage) return;
  try {
    storage.removeItem(key);
  } catch {
    // Nothing to clean up if storage is unavailable.
  }
};

// Used on logout so the next user of this tab never sees another lecturer's unsaved scores.
export const clearAllRubricDrafts = (storage: DraftStorage | null): void => {
  if (!storage) return;
  try {
    const keys: string[] = [];
    for (let index = 0; index < storage.length; index += 1) {
      const key = storage.key(index);
      if (key?.startsWith(RUBRIC_DRAFT_PREFIX)) keys.push(key);
    }
    keys.forEach((key) => storage.removeItem(key));
  } catch {
    // Nothing to clean up if storage is unavailable.
  }
};

// Draft values are matched by criterion key, so a rubric that changed since the draft was written cannot misalign rows.
export const applyDraftToRows = (rows: RubricRow[], draft: RubricDraft): RubricRow[] => {
  const byKey = new Map(draft.rows.map((row) => [row.criterionKey, row]));
  return rows.map((row) => {
    const saved = byKey.get(row.criterionKey as string);
    return saved ? { ...row, score: saved.score, comment: saved.comment } : row;
  });
};

// discard - the draft equals the server version, nothing to restore;
// restore - made against this very server version, safe to put back;
// offer   - the server version changed since the draft was written: let the lecturer choose.
export const decideDraftRestore = ({
  draftBaselineKey, currentSyncKey, draftSnapshot, serverSnapshot,
}: {
  draftBaselineKey: string;
  currentSyncKey: string;
  draftSnapshot: string;
  serverSnapshot: string;
}): DraftRestoreAction => {
  if (draftSnapshot === serverSnapshot) return 'discard';
  return draftBaselineKey === currentSyncKey ? 'restore' : 'offer';
};
