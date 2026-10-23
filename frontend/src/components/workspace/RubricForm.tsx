// @ts-nocheck
import { useEffect, useMemo, useRef, useState } from 'react';
import { ChevronDown, ChevronUp, RotateCcw, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import {
  decideEvaluationSync, editableSnapshot, evaluationSyncKey, initialRows, normalizeCriteria, savedOverrides,
} from '../../utils/rubricFormState';
import {
  applyDraftToRows, clearRubricDraft, decideDraftRestore, getBrowserStorage, loadRubricDraft, rubricDraftKey, saveRubricDraft,
} from '../../utils/rubricDraft';

const DRAFT_WRITE_DELAY_MS = 500;
const formatDraftTime = (value) => new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

const evaluationStatusPresentation = {
  DRAFT: {
    label: 'Draft',
    className: 'border-amber-200 bg-amber-50 text-amber-700',
  },
  SUBMITTED: {
    label: 'Submitted',
    className: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  },
  PUBLISHED: {
    label: 'Published',
    className: 'border-blue-200 bg-blue-50 text-blue-700',
  },
  NOT_GRADED: {
    label: 'Not graded',
    className: 'border-slate-200 bg-slate-50 text-slate-500',
  },
};

export default function RubricForm({
  initialData,
  onSubmit,
  readOnly = false,
  hideSensitiveScores = false,
  criteria: customCriteria,
  members: customMembers = [],
  checkpointNumber,
  checkpointTitle,
  compact = false,
  draftScope,
}) {
  const criteria = useMemo(() => normalizeCriteria(customCriteria), [customCriteria]);

  // Unsaved edits survive a reload through sessionStorage. Without a scope (user + team) the feature is off.
  const draftStorage = draftScope?.userId && draftScope?.teamId ? getBrowserStorage() : null;
  const draftKey = draftStorage
    ? rubricDraftKey(draftScope.userId, draftScope.teamId, checkpointNumber, initialData?._id)
    : null;
  const [storedDraft] = useState(() => {
    const draft = draftKey ? loadRubricDraft(draftStorage, draftKey) : null;
    if (!draft) return null;
    const serverRows = initialRows(criteria, initialData);
    const rows = applyDraftToRows(serverRows, draft);
    const action = decideDraftRestore({
      draftBaselineKey: draft.baselineKey,
      currentSyncKey: evaluationSyncKey(initialData, criteria),
      draftSnapshot: editableSnapshot(rows, draft.feedback, draft.overrides),
      serverSnapshot: editableSnapshot(serverRows, initialData?.overallFeedback, savedOverrides(initialData)),
    });
    return action === 'discard' ? null : { action, draft, rows };
  });
  const restoredNow = storedDraft?.action === 'restore';
  const [restoredAt, setRestoredAt] = useState(() => (restoredNow ? storedDraft.draft.savedAt : null));
  const [draftOffer, setDraftOffer] = useState(() => (storedDraft?.action === 'offer' ? storedDraft : null));

  const [rubricScores, setRubricScores] = useState(() => (restoredNow ? storedDraft.rows : initialRows(criteria, initialData)));
  const [overallFeedback, setOverallFeedback] = useState(() => (restoredNow ? storedDraft.draft.feedback : initialData?.overallFeedback || ''));
  const [memberScoresExpanded, setMemberScoresExpanded] = useState(false);
  const [memberOverrides, setMemberOverrides] = useState(() => (restoredNow ? storedDraft.draft.overrides : savedOverrides(initialData)));
  const [attemptedSubmit, setAttemptedSubmit] = useState(false);
  const isSubmitted = initialData?.status === 'SUBMITTED' || initialData?.status === 'PUBLISHED';
  const savedSnapshot = useMemo(
    () => editableSnapshot(initialRows(criteria, initialData), initialData?.overallFeedback, savedOverrides(initialData)),
    [criteria, initialData],
  );
  const hasChanges = editableSnapshot(rubricScores, overallFeedback, memberOverrides) !== savedSnapshot;
  const evaluationStatus = String(initialData?.status || 'NOT_GRADED').toUpperCase();
  const statusPresentation = evaluationStatusPresentation[evaluationStatus]
    || evaluationStatusPresentation.NOT_GRADED;

  // Background refreshes (realtime, reconnect) hand us a new initialData object even when nothing changed.
  // Only replace what the lecturer is typing when it is safe; otherwise keep the edits and offer a reload.
  const syncKey = evaluationSyncKey(initialData, criteria);
  const currentSnapshot = editableSnapshot(rubricScores, overallFeedback, memberOverrides);
  const baselineRef = useRef({ key: syncKey, snapshot: savedSnapshot });
  const [hasNewerVersion, setHasNewerVersion] = useState(false);

  const applyServerVersion = () => {
    baselineRef.current = { key: syncKey, snapshot: savedSnapshot };
    setRubricScores(initialRows(criteria, initialData));
    setOverallFeedback(initialData?.overallFeedback || '');
    setMemberOverrides(savedOverrides(initialData));
    setAttemptedSubmit(false);
    setHasNewerVersion(false);
    setRestoredAt(null);
  };

  // Debounced write of unsaved edits. The latest payload is also flushed on unmount and page hide, so a reload
  // right after typing does not lose the last keystrokes.
  const latestDraftRef = useRef(null);
  const draftTimerRef = useRef(null);
  const cancelPendingDraft = () => {
    if (draftTimerRef.current !== null) clearTimeout(draftTimerRef.current);
    draftTimerRef.current = null;
    latestDraftRef.current = null;
  };
  const flushDraft = () => {
    if (draftTimerRef.current !== null) clearTimeout(draftTimerRef.current);
    draftTimerRef.current = null;
    if (latestDraftRef.current && draftKey) saveRubricDraft(draftStorage, draftKey, latestDraftRef.current);
    latestDraftRef.current = null;
  };
  const discardStoredDraft = () => {
    cancelPendingDraft();
    if (draftKey) clearRubricDraft(draftStorage, draftKey);
  };

  useEffect(() => {
    // While the lecturer has not answered the restore offer, the stored draft must stay untouched.
    if (!draftKey || draftOffer) return;
    if (!hasChanges) {
      discardStoredDraft();
      return;
    }
    latestDraftRef.current = {
      version: 1,
      baselineKey: baselineRef.current.key,
      savedAt: Date.now(),
      rows: rubricScores.map((row) => ({ criterionKey: row.criterionKey, score: row.score ?? '', comment: row.comment || '' })),
      feedback: overallFeedback,
      overrides: memberOverrides,
    };
    if (draftTimerRef.current !== null) clearTimeout(draftTimerRef.current);
    draftTimerRef.current = setTimeout(flushDraft, DRAFT_WRITE_DELAY_MS);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draftKey, draftOffer, hasChanges, currentSnapshot]);

  useEffect(() => {
    window.addEventListener('pagehide', flushDraft);
    return () => {
      window.removeEventListener('pagehide', flushDraft);
      flushDraft();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const discardRestoredDraft = () => {
    applyServerVersion();
    discardStoredDraft();
  };

  const restoreOfferedDraft = () => {
    setRubricScores(draftOffer.rows);
    setOverallFeedback(draftOffer.draft.feedback);
    setMemberOverrides(draftOffer.draft.overrides);
    setRestoredAt(draftOffer.draft.savedAt);
    setDraftOffer(null);
  };

  const discardOfferedDraft = () => {
    setDraftOffer(null);
    discardStoredDraft();
  };

  useEffect(() => {
    const action = decideEvaluationSync({
      baselineKey: baselineRef.current.key,
      incomingKey: syncKey,
      baselineSnapshot: baselineRef.current.snapshot,
      incomingSnapshot: savedSnapshot,
      currentSnapshot,
    });
    if (action === 'keep') return;
    if (action === 'notify') {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setHasNewerVersion(true);
      return;
    }
    applyServerVersion();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [syncKey, savedSnapshot, currentSnapshot]);

  const updateRow = (index, patch) => {
    if (readOnly) return;
    setRubricScores((previous) => previous.map((row, rowIndex) => rowIndex === index ? { ...row, ...patch } : row));
  };

  const handleScoreChange = (index, value) => {
    const normalized = value.replace(',', '.');
    if (!/^\d*(?:\.\d*)?$/.test(normalized)) return;
    updateRow(index, { score: normalized });
  };

  const scoreError = (item, required = false) => {
    if (item.score === '' || item.score == null) return required ? 'Enter a score.' : '';
    const score = Number(item.score);
    if (!Number.isFinite(score) || score < 0 || score > item.maxScore) {
      return `Enter a score from 0 to ${item.maxScore}.`;
    }
    return '';
  };

  const checkpointTotal = useMemo(() => rubricScores.reduce((sum, item) => {
    const score = Number(item.score);
    return sum + (item.score !== '' && Number.isFinite(score) && score >= 0 && score <= item.maxScore
      ? score * item.weight / 100 : 0);
  }, 0), [rubricScores]);

  const members = Array.isArray(customMembers) ? customMembers : [];
  const updateMemberScore = (studentId, value) => {
    if (readOnly) return;
    const normalized = value.replace(',', '.');
    if (!/^\d*(?:\.\d*)?$/.test(normalized)) return;
    setMemberOverrides((previous) => ({ ...previous, [studentId]: normalized }));
  };
  const resetMemberScore = (studentId) => setMemberOverrides((previous) => {
    const next = { ...previous };
    delete next[studentId];
    return next;
  });
  const memberScoreError = (studentId) => {
    if (!(studentId in memberOverrides)) return '';
    const value = memberOverrides[studentId];
    const score = Number(value);
    return value === '' || !Number.isFinite(score) || score < 0 || score > 10
      ? 'Enter a score from 0 to 10.'
      : '';
  };

  const handleSubmit = async (status) => {
    if (readOnly || !onSubmit) return;
    // Saving an unchanged draft or submitted evaluation is not an update; submitting a draft still is.
    if ((status === 'DRAFT' || isSubmitted) && !hasChanges) return;
    if (rubricScores.length === 0) {
      toast.error('No rubric criteria are configured for this checkpoint.');
      return;
    }
    const required = status === 'SUBMITTED';
    setAttemptedSubmit(required);
    if (rubricScores.some((item) => scoreError(item, required))) {
      toast.error(required
        ? 'Score every configured criterion before submitting the evaluation.'
        : 'Correct invalid scores before saving the draft.');
      return;
    }
    if (members.some((member) => memberScoreError(member.studentId))) {
      toast.error('Correct invalid individual member scores before saving.');
      return;
    }

    const saved = await onSubmit({
      checkpointNumber,
      checkpointTitle,
      rubricScores: rubricScores.map((item) => ({
        criterionKey: item.criterionKey,
        score: item.score === '' || item.score == null ? null : Number(item.score),
        comment: item.comment,
      })),
      overallFeedback,
      memberScoreOverrides: Object.entries(memberOverrides).map(([studentId, score]) => ({
        studentId,
        score: Number(score),
      })),
      status: isSubmitted ? 'SUBMITTED' : status,
    });
    // Only a successful save makes the browser copy redundant; on failure it stays as the lecturer's safety net.
    if (saved === true) discardStoredDraft();
  };

  const title = checkpointTitle && checkpointTitle !== `Checkpoint ${checkpointNumber}`
    ? checkpointTitle : `Checkpoint ${checkpointNumber}`;

  return (
    <div className={`rounded-2xl border border-slate-200 bg-white shadow-sm ${compact ? 'p-3 sm:p-4' : 'p-4 sm:p-5'}`}>
      <div className="mb-3 flex items-center justify-between gap-3">
        <div className="min-w-0">
          <h3 className="truncate text-sm font-bold text-slate-900">{title} · Enter scores</h3>
          <p className="text-xs text-slate-500">Type a score for each criterion.</p>
          <div className="mt-2 flex items-center gap-2">
            <span className="text-overline font-bold uppercase tracking-wider text-slate-400">Evaluation status</span>
            <span
              aria-label={`Evaluation status: ${statusPresentation.label}`}
              className={`inline-flex rounded-full border px-2.5 py-1 text-overline font-bold uppercase tracking-wide ${statusPresentation.className}`}
            >
              {statusPresentation.label}
            </span>
          </div>
        </div>
        {!hideSensitiveScores && (
          <div className="shrink-0 rounded-xl bg-blue-50 px-3 py-1.5 text-right">
            <span className="text-xs font-semibold text-blue-600">Total </span>
            <span className="text-lg font-black text-blue-700">{checkpointTotal.toFixed(2)}</span>
            <span className="text-xs text-blue-500"> / 10</span>
          </div>
        )}
      </div>

      {restoredAt !== null && (
        <div role="status" className="mb-3 flex flex-wrap items-center justify-between gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
          <span>Restored your unsaved changes from {formatDraftTime(restoredAt)}.</span>
          <button type="button" onClick={discardRestoredDraft} disabled={readOnly} className="shrink-0 rounded-lg border border-amber-200 bg-white px-3 py-1.5 font-semibold text-amber-700 hover:bg-amber-50 disabled:opacity-50">
            Discard
          </button>
        </div>
      )}

      {draftOffer && (
        <div role="status" className="mb-3 flex flex-wrap items-center justify-between gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
          <span>You have unsaved changes from {formatDraftTime(draftOffer.draft.savedAt)}, but this evaluation was updated since then.</span>
          <span className="flex shrink-0 gap-2">
            <button type="button" onClick={restoreOfferedDraft} disabled={readOnly} className="rounded-lg border border-amber-200 bg-white px-3 py-1.5 font-semibold text-amber-700 hover:bg-amber-50 disabled:opacity-50">
              Restore my changes
            </button>
            <button type="button" onClick={discardOfferedDraft} disabled={readOnly} className="rounded-lg border border-amber-200 bg-white px-3 py-1.5 font-semibold text-amber-700 hover:bg-amber-50 disabled:opacity-50">
              Discard
            </button>
          </span>
        </div>
      )}

      {hasNewerVersion && (
        <div role="status" className="mb-3 flex flex-wrap items-center justify-between gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
          <span>A newer version of this evaluation was saved elsewhere. Your unsaved edits are kept.</span>
          <button type="button" onClick={applyServerVersion} disabled={readOnly} className="shrink-0 rounded-lg border border-amber-200 bg-white px-3 py-1.5 font-semibold text-amber-700 hover:bg-amber-50 disabled:opacity-50">
            Load latest and discard my edits
          </button>
        </div>
      )}

      <div className="divide-y divide-slate-100 rounded-xl border border-slate-200">
        {rubricScores.length === 0 && (
          <p className="px-3 py-4 text-sm text-slate-500">No rubric criteria are configured for this checkpoint.</p>
        )}
        {rubricScores.map((item, index) => {
          const error = scoreError(item, attemptedSubmit);
          return (
            <div key={`${item.criterionKey}-${index}`} className="flex min-h-12 items-center gap-2 px-3 py-1.5 sm:gap-3">
              <div className="min-w-0 flex-1" title={item.description || undefined}>
                <label htmlFor={`rubric-score-${checkpointNumber}-${index}`} className="block truncate text-sm font-medium text-slate-800">
                  {item.criterionName}
                </label>
                <span className="text-caption text-slate-400">{item.weight}% weight</span>
              </div>
              {hideSensitiveScores ? (
                <span className="text-xs text-slate-500">Score hidden</span>
              ) : (
                <div className="flex w-28 shrink-0 flex-col items-end">
                  <div className={`flex w-full items-center rounded-lg border bg-white px-2 ${error ? 'border-red-400' : 'border-slate-200 focus-within:border-primary'}`}>
                    <input
                      id={`rubric-score-${checkpointNumber}-${index}`}
                      type="text"
                      inputMode="decimal"
                      autoComplete="off"
                      value={item.score ?? ''}
                      onChange={(event) => handleScoreChange(index, event.target.value)}
                      disabled={readOnly}
                      placeholder="0"
                      aria-invalid={Boolean(error)}
                      aria-describedby={error ? `rubric-error-${checkpointNumber}-${index}` : undefined}
                      className="min-w-0 w-full border-0 bg-transparent py-1.5 text-right text-sm font-bold text-slate-900 outline-none disabled:text-slate-400"
                    />
                    <span className="shrink-0 pl-1 text-xs text-slate-400">/ {item.maxScore}</span>
                  </div>
                  {error && <span id={`rubric-error-${checkpointNumber}-${index}`} className="text-right text-overline text-red-600">{error}</span>}
                </div>
              )}
            </div>
          );
        })}
      </div>

      <div className="mt-3 rounded-xl border border-slate-200 bg-slate-50/70">
        <button
          type="button"
          onClick={() => setMemberScoresExpanded((current) => !current)}
          aria-expanded={memberScoresExpanded}
          className="flex w-full items-center justify-between gap-3 px-3 py-2.5 text-left"
        >
          <span className="flex min-w-0 items-center gap-2">
            <Users className="h-4 w-4 shrink-0 text-primary" />
            <span>
              <span className="block text-xs font-semibold text-slate-700">Individual member scores</span>
              <span className="block text-caption text-slate-500">
                {Object.keys(memberOverrides).length === 0
                  ? `All ${members.length} members use the Total score`
                  : `${Object.keys(memberOverrides).length} custom score${Object.keys(memberOverrides).length === 1 ? '' : 's'}`}
              </span>
            </span>
          </span>
          {memberScoresExpanded
            ? <ChevronUp className="h-4 w-4 shrink-0 text-slate-500" />
            : <ChevronDown className="h-4 w-4 shrink-0 text-slate-500" />}
        </button>

        {memberScoresExpanded && (
          <div className="border-t border-slate-200 bg-white px-3 py-2">
            <div className="mb-2 flex items-center justify-between gap-3">
              <p className="text-caption text-slate-500">Default: Total {checkpointTotal.toFixed(2)} / 10</p>
              {Object.keys(memberOverrides).length > 0 && (
                <button
                  type="button"
                  disabled={readOnly}
                  onClick={() => setMemberOverrides({})}
                  className="inline-flex items-center gap-1 text-caption font-semibold text-primary-700 hover:text-primary-700/80 disabled:opacity-50"
                >
                  <RotateCcw className="h-3 w-3" /> Reset all
                </button>
              )}
            </div>
            {members.length === 0 ? (
              <p className="py-2 text-xs text-slate-500">No active team members found.</p>
            ) : (
              <div className="divide-y divide-slate-100">
                {members.map((member) => {
                  const isOverridden = member.studentId in memberOverrides;
                  const error = memberScoreError(member.studentId);
                  return (
                    <div key={member.studentId} className="flex min-h-12 items-center gap-3 py-2">
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium text-slate-800">{member.fullName}</p>
                        <p className="text-caption text-slate-400">
                          {member.rollNumber || 'Student'} · {isOverridden ? 'Custom score' : 'Uses Total'}
                        </p>
                      </div>
                      <div className="flex shrink-0 items-center gap-2">
                        <div className="flex flex-col items-end">
                          <div className={`flex w-24 items-center rounded-lg border bg-white px-2 ${error ? 'border-red-400' : 'border-slate-200 focus-within:border-primary'}`}>
                            <input
                              type="text"
                              inputMode="decimal"
                      autoComplete="off"
                              aria-label={`Score for ${member.fullName}`}
                              value={isOverridden ? memberOverrides[member.studentId] : checkpointTotal.toFixed(2)}
                              onChange={(event) => updateMemberScore(member.studentId, event.target.value)}
                              disabled={readOnly}
                              className="min-w-0 w-full border-0 bg-transparent py-1.5 text-right text-sm font-bold text-slate-900 outline-none disabled:text-slate-400"
                            />
                            <span className="pl-1 text-xs text-slate-400">/10</span>
                          </div>
                          {error && <span className="text-overline text-red-600">{error}</span>}
                        </div>
                        <button
                          type="button"
                          onClick={() => resetMemberScore(member.studentId)}
                          disabled={readOnly || !isOverridden}
                          aria-label={`Reset score for ${member.fullName}`}
                          className="rounded-md p-1.5 text-slate-400 hover:bg-slate-100 hover:text-primary-700 disabled:invisible"
                        >
                          <RotateCcw className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}
      </div>

      <div className="mt-3">
        <label htmlFor={`overall-feedback-${checkpointNumber}`} className="mb-1 block text-xs font-semibold text-slate-600">Overall feedback (optional)</label>
        <textarea
          id={`overall-feedback-${checkpointNumber}`}
          rows={compact ? 2 : 3}
          value={overallFeedback}
          onChange={(event) => setOverallFeedback(event.target.value)}
          disabled={readOnly}
          placeholder="General feedback for the team..."
          className="block w-full resize-none rounded-lg border border-slate-200 px-3 py-2 text-sm text-slate-700 outline-none focus:border-primary disabled:bg-slate-50"
        />
      </div>

      {rubricScores.length > 0 && (
        <details className="mt-2 text-xs text-slate-600">
          <summary className="cursor-pointer font-semibold">Criterion comments (optional)</summary>
          <div className="mt-2 space-y-2">
            {rubricScores.map((item, index) => (
              <label key={item.criterionKey} className="block">
                <span className="mb-1 block">{item.criterionName}</span>
                <input
                  type="text"
                  value={item.comment}
                  onChange={(event) => updateRow(index, { comment: event.target.value })}
                  disabled={readOnly}
                  className="w-full rounded-lg border border-slate-200 px-3 py-1.5 text-sm"
                />
              </label>
            ))}
          </div>
        </details>
      )}

      {onSubmit && !hideSensitiveScores && (
        <div className="mt-3 flex justify-end gap-2">
          {!isSubmitted && (
            <button type="button" disabled={readOnly || rubricScores.length === 0 || !hasChanges} title={hasChanges ? undefined : 'Make a change to save a new draft'} onClick={() => handleSubmit('DRAFT')} className="rounded-lg border border-slate-200 px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50">
              Save draft
            </button>
          )}
          <button type="button" disabled={readOnly || rubricScores.length === 0 || (isSubmitted && !hasChanges)} title={isSubmitted && !hasChanges ? 'Make a change to save a new version' : undefined} onClick={() => handleSubmit('SUBMITTED')} className="rounded-lg bg-primary px-3 py-2 text-xs font-semibold text-white hover:bg-primary/90 disabled:opacity-50">
            {readOnly ? 'Saving...' : isSubmitted ? 'Save changes' : 'Submit evaluation'}
          </button>
        </div>
      )}
    </div>
  );
}
