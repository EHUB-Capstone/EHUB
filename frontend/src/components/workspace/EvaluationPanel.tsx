// @ts-nocheck
import { useCallback, useEffect, useMemo, useState } from 'react';
import { ClipboardCheck, Loader2, Sparkles, History, Layers3 } from 'lucide-react';
import toast from 'react-hot-toast';
import { evaluationApi } from '../../api/evaluationApi';
import RubricForm from './RubricForm';
import { useAuth } from '../../hooks/useAuth';
import { MentorEvaluationCard } from '../evaluation/PerformanceLevelBadge';
import PerformanceLevelBadge from '../evaluation/PerformanceLevelBadge';

const CHECKPOINTS = [1, 2, 3, 4];

const roleLabel = (role) => ({
  ADMIN: 'Admin',
  LECTURER: 'Lecturer',
  MENTOR: 'Mentor',
  STUDENT: 'Student',
  USER: 'Student',
}[role] || role || 'User');

export default function EvaluationPanel({
  teamId,
  proposalId,
  pitchDeckId,
  isReadOnly = false,
  checkpointNumber,
  embedded = false,
}) {
  const { user } = useAuth();
  const [selectedCheckpoint, setSelectedCheckpoint] = useState(() => Number(checkpointNumber) || 1);
  const [checkpointData, setCheckpointData] = useState(null);
  const [evaluations, setEvaluations] = useState([]);
  const [history, setHistory] = useState([]);
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const isStudent = user?.role === 'STUDENT' || user?.role === 'USER';
  const isMentor = user?.role === 'MENTOR';
  const canEdit = !isReadOnly && user?.role === 'LECTURER';
  const activeEvaluation = useMemo(
    () => evaluations.find((ev) => ev.lecturerId?._id === user?._id) || null,
    [evaluations, user?._id]
  );

  useEffect(() => {
    if (!checkpointNumber) return;
    // Keep the merged grading panel aligned with the checkpoint opened in the workspace.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setSelectedCheckpoint(Number(checkpointNumber));
  }, [checkpointNumber]);

  const fetchCheckpointData = useCallback(async () => {
    if (!teamId) return;
    try {
      setLoading(true);
      setError(null);
      const res = await evaluationApi.getCheckpointSummary(teamId, selectedCheckpoint);
      if (res.success) {
        setCheckpointData(res.data.checkpoint || null);
        setEvaluations(res.data.evaluations || []);
        setHistory(res.data.history || []);
        setSummary(res.data.summary || null);
      }
    } catch (err) {
      const message = err.response?.data?.message || err.response?.data?.error || 'Failed to load evaluations';
      setError(message);
      toast.error(message);
    } finally {
      setLoading(false);
    }
  }, [selectedCheckpoint, teamId]);

  useEffect(() => {
    // Fetching checkpoint data is the side effect owned by this panel.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchCheckpointData();
  }, [fetchCheckpointData]);

  const saveEvaluation = async (formData) => {
    try {
      setSaving(true);
      const payload = {
        ...formData,
        proposalId,
        pitchDeckId,
        checkpointNumber: selectedCheckpoint,
        checkpointTitle: checkpointData?.title,
      };

      let res;
      if (activeEvaluation) {
        res = await evaluationApi.updateCheckpointEvaluation(activeEvaluation._id, payload);
      } else {
        res = await evaluationApi.createCheckpointEvaluation(teamId, selectedCheckpoint, payload);
      }

      if (res.success) {
        const wasAlreadySubmitted = activeEvaluation?.status === 'SUBMITTED' || activeEvaluation?.status === 'PUBLISHED';
        toast.success(wasAlreadySubmitted
          ? 'Evaluation updated.'
          : formData.status === 'SUBMITTED'
            ? 'Official evaluation submitted.'
            : 'Draft saved.');
        await fetchCheckpointData();
      }
    } catch (err) {
      const message = err.response?.data?.message || err.response?.data?.error || 'Failed to save evaluation';
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="p-8 text-center text-slate-500 flex items-center justify-center gap-3">
        <Loader2 className="w-5 h-5 animate-spin text-primary" />
        Loading evaluations...
      </div>
    );
  }

  if (error) {
    return <div className="p-6 text-center text-red-500 bg-red-50 rounded-2xl border border-red-100">{error}</div>;
  }

  const checkpointTitle = checkpointData?.title || `Checkpoint ${selectedCheckpoint}`;
  const latestScore = summary?.averageScore ?? (evaluations[0]?.checkpointTotal || evaluations[0]?.weightedScore || 0);

  if (embedded) {
    return (
      <div className="space-y-4 p-4 sm:p-5">
        <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <div className="flex items-center gap-2 text-[11px] font-bold uppercase tracking-wider text-primary">
                <ClipboardCheck className="h-4 w-4" /> Checkpoint evaluation
              </div>
              <h2 className="mt-1 truncate text-lg font-black text-slate-900">{checkpointTitle}</h2>
              <p className="mt-1 text-xs leading-5 text-slate-500">
                {canEdit
                  ? 'Score this submission without leaving the workspace.'
                  : isMentor || isStudent
                    ? 'Review the published performance and feedback.'
                    : `${roleLabel(user?.role)} access is read-only.`}
              </p>
            </div>
            {activeEvaluation?.status && (
              <span className={`shrink-0 rounded-full px-2.5 py-1 text-[10px] font-bold ${activeEvaluation.status === 'SUBMITTED' || activeEvaluation.status === 'PUBLISHED'
                ? 'bg-emerald-50 text-emerald-700'
                : 'bg-amber-50 text-amber-700'
                }`}>
                {activeEvaluation.status}
              </span>
            )}
          </div>

          <div className="mt-4 grid grid-cols-2 gap-2">
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
              <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">Evaluations</p>
              <p className="mt-1 text-xl font-black text-slate-900">{summary?.evaluationCount ?? evaluations.length}</p>
            </div>
            {(isMentor || isStudent) ? (
              <div className="rounded-xl border border-slate-200 bg-white p-3">
                <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">Performance</p>
                <div className="mt-1">
                  {summary?.overallPerformance?.level && summary.overallPerformance.level !== 'Unscored'
                    ? <PerformanceLevelBadge level={summary.overallPerformance.level} label={summary.overallPerformance.label} size="sm" />
                    : <span className="text-sm font-semibold text-slate-400">Not scored</span>}
                </div>
              </div>
            ) : (
              <div className="rounded-xl border border-emerald-100 bg-emerald-50 p-3">
                <p className="text-[10px] font-semibold uppercase tracking-wider text-emerald-500">Average score</p>
                <p className="mt-1 text-xl font-black text-emerald-700">{Number(latestScore || 0).toFixed(2)} / 10</p>
              </div>
            )}
          </div>
        </div>

        {canEdit ? (
          <RubricForm
            key={`${selectedCheckpoint}-${activeEvaluation?._id || 'new'}`}
            initialData={activeEvaluation || {}}
            onSubmit={saveEvaluation}
            readOnly={saving}
            criteria={checkpointData?.rubrics || []}
            checkpointNumber={selectedCheckpoint}
            checkpointTitle={checkpointTitle}
            compact
          />
        ) : (
          <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="mb-3 flex items-center justify-between gap-3">
              <h3 className="font-bold text-slate-900">Evaluation summary</h3>
              <History className="h-4 w-4 text-slate-400" />
            </div>
            {evaluations.length === 0 ? (
              <p className="rounded-xl border border-dashed border-slate-200 bg-slate-50 p-5 text-center text-sm text-slate-500">
                No evaluation recorded for this checkpoint yet.
              </p>
            ) : (isMentor || isStudent) ? (
              <div className="space-y-3">
                {evaluations.map((evaluation) => (
                  <MentorEvaluationCard key={evaluation._id} evaluation={evaluation} />
                ))}
              </div>
            ) : (
              <div className="space-y-3">
                {evaluations.map((evaluation) => (
                  <article key={evaluation._id} className="rounded-xl border border-slate-200 bg-slate-50 p-3">
                    <div className="flex items-center justify-between gap-3">
                      <div className="min-w-0">
                        <p className="truncate text-sm font-semibold text-slate-900">{evaluation.lecturerId?.name || 'Evaluator'}</p>
                        <p className="text-xs text-slate-400">{evaluation.status || 'DRAFT'}</p>
                      </div>
                      <p className="text-xl font-black text-primary">{Number(evaluation.checkpointTotal || evaluation.weightedScore || 0).toFixed(2)}</p>
                    </div>
                    {evaluation.overallFeedback && <p className="mt-2 whitespace-pre-wrap text-sm text-slate-600">{evaluation.overallFeedback}</p>}
                  </article>
                ))}
              </div>
            )}
          </section>
        )}

        <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="mb-3 flex items-center gap-2">
            <History className="h-4 w-4 text-primary" />
            <h3 className="font-bold text-slate-900">Evaluation history</h3>
          </div>
          {history.length === 0 ? (
            <p className="text-sm text-slate-500">No history available yet.</p>
          ) : (
            <div className="space-y-2">
              {history.map((item) => (
                <div key={item._id} className="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <div className="flex items-center justify-between gap-2 text-xs text-slate-400">
                    <span className="font-bold uppercase tracking-wider">{item.action}</span>
                    <span>v{item.version}</span>
                  </div>
                  <p className="mt-1 text-sm font-semibold text-slate-900">{item.changedBy?.name || 'System'}</p>
                  <p className="text-xs text-slate-500">{new Date(item.createdAt).toLocaleString()}</p>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="bg-white rounded-2xl border border-slate-200/70 shadow-sm p-5 sm:p-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-primary">
              <ClipboardCheck className="w-4 h-4" /> Rubric evaluation workflow
            </div>
            <h2 className="mt-2 text-2xl font-black text-slate-900">{checkpointTitle}</h2>
            <p className="mt-1 text-sm text-slate-500">
              {isStudent
                ? 'View the official evaluation, comments, and history for each checkpoint.'
                : canEdit
                  ? 'Score the startup team, save drafts, and submit the official evaluation.'
                  : isMentor
                    ? 'Track team progress and performance direction. Exact scores are not shown.'
                    : 'View evaluation summary and history.'}
            </p>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
            {CHECKPOINTS.map((num) => (
              <button
                key={num}
                onClick={() => setSelectedCheckpoint(num)}
                className={`rounded-xl px-4 py-3 border text-sm font-semibold transition-all ${selectedCheckpoint === num
                  ? 'bg-primary text-white border-primary shadow-sm'
                  : 'bg-slate-50 text-slate-600 border-slate-200 hover:bg-slate-100'
                  }`}
              >
                CP {num}
              </button>
            ))}
          </div>
        </div>

        <div className="mt-5 grid grid-cols-1 md:grid-cols-3 gap-3">
          <div className="rounded-2xl bg-slate-50 border border-slate-200 p-4">
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-400">Evaluations</p>
            <p className="mt-1 text-2xl font-black text-slate-900">{summary?.evaluationCount ?? evaluations.length}</p>
          </div>
          <div className="rounded-2xl bg-blue-50 border border-blue-100 p-4">
            <p className="text-xs font-semibold uppercase tracking-wider text-blue-500">Submitted</p>
            <p className="mt-1 text-2xl font-black text-blue-700">{summary?.submittedCount ?? evaluations.filter((ev) => ev.status === 'SUBMITTED' || ev.status === 'PUBLISHED').length}</p>
          </div>
          {/* Average score hidden for Mentor/Student — show overall performance level instead */}
          {(isMentor || isStudent) ? (
            summary?.overallPerformance && summary.overallPerformance.level !== 'Unscored' ? (
              <div className="rounded-2xl bg-white border border-slate-200 p-4">
                <p className="text-xs font-semibold uppercase tracking-wider text-slate-400">Overall Performance</p>
                <div className="mt-2">
                  <PerformanceLevelBadge
                    level={summary.overallPerformance.level}
                    label={summary.overallPerformance.label}
                    size="lg"
                  />
                </div>
              </div>
            ) : null
          ) : (
            <div className="rounded-2xl bg-emerald-50 border border-emerald-100 p-4">
              <p className="text-xs font-semibold uppercase tracking-wider text-emerald-500">Average score</p>
              <p className="mt-1 text-2xl font-black text-emerald-700">{Number(latestScore || 0).toFixed(2)} / 10</p>
            </div>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
        <div className="xl:col-span-2 space-y-6">
          {canEdit ? (
            <div className="space-y-4">
              <div className="rounded-2xl bg-white border border-slate-200/70 shadow-sm p-5 sm:p-6">
                <div className="flex items-center justify-between gap-3 mb-4">
                  <div>
                    <h3 className="text-lg font-bold text-slate-900">Rubric scoring form</h3>
                    <p className="text-sm text-slate-500">Select a level or input a score from 0 to 10.</p>
                  </div>
                  {activeEvaluation?.status && (
                    <span className={`px-3 py-1 rounded-full text-xs font-bold ${activeEvaluation.status === 'SUBMITTED'
                      ? 'bg-emerald-50 text-emerald-700'
                      : 'bg-amber-50 text-amber-700'
                      }`}>
                      {activeEvaluation.status}
                    </span>
                  )}
                </div>

                <RubricForm
                  key={`${selectedCheckpoint}-${activeEvaluation?._id || 'new'}`}
                  initialData={activeEvaluation || {}}
                  onSubmit={saveEvaluation}
                  readOnly={saving}
                  criteria={checkpointData?.rubrics || []}
                  checkpointNumber={selectedCheckpoint}
                  checkpointTitle={checkpointTitle}
                />
              </div>
            </div>
          ) : (
            <div className="rounded-2xl bg-white border border-slate-200/70 shadow-sm p-5 sm:p-6">
              <div className="flex items-start justify-between gap-3 mb-4">
                <div>
                  <h3 className="text-lg font-bold text-slate-900">Evaluation summary</h3>
                  <p className="text-sm text-slate-500">
                    {(isMentor || isStudent)
                      ? 'Performance levels only, no numeric scores.'
                      : `${roleLabel(user?.role)} access is read-only.`}
                  </p>
                </div>
                <div className="flex items-center gap-2 text-xs font-semibold text-slate-500">
                  <History className="w-4 h-4" /> History preserved
                </div>
              </div>

              {evaluations.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-8 text-center text-slate-500">
                  No evaluations recorded for this checkpoint yet.
                </div>
              ) : (isMentor || isStudent) ? (
                /* ── MENTOR / STUDENT VIEW: badges only, no numeric scores ── */
                <div className="space-y-4">
                  {evaluations.map((ev) => (
                    <MentorEvaluationCard key={ev._id} evaluation={ev} />
                  ))}
                </div>
              ) : (
                /* ── LECTURER / ADMIN VIEW: full numeric data ── */
                <div className="space-y-4">
                  {evaluations.map((ev) => (
                    <div key={ev._id} className="rounded-2xl border border-slate-200 bg-slate-50 p-4">
                      <div className="flex items-center justify-between gap-4 mb-3">
                        <div>
                          <p className="font-semibold text-slate-900">{ev.lecturerId?.name || 'Evaluator'}</p>
                          <p className="text-xs text-slate-400">{ev.evaluatorRole} · {new Date(ev.updatedAt).toLocaleString()}</p>
                        </div>
                        <div className="text-right">
                          <span className={`inline-flex rounded-full px-2 py-0.5 text-[11px] font-bold mb-1 ${ev.status === 'SUBMITTED' || ev.status === 'PUBLISHED' ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700'}`}>
                            {ev.status || 'DRAFT'}
                          </span>
                          <p className="text-2xl font-black text-primary">{Number(ev.checkpointTotal || ev.weightedScore || 0).toFixed(2)}</p>
                          <p className="text-xs text-slate-400">/ 10</p>
                        </div>
                      </div>
                      <div className="grid grid-cols-2 md:grid-cols-4 gap-2 mb-3 text-xs">
                        {(ev.rubricScores || []).map((item) => (
                          <div key={item.criterionKey} className="rounded-xl bg-white border border-slate-200 p-2">
                            <p className="text-slate-500 truncate">{item.criterionName}</p>
                            <p className="font-bold text-slate-900">{Number(item.score || 0).toFixed(1)}</p>
                            {item.comment && <p className="mt-1 text-slate-600 whitespace-pre-wrap">{item.comment}</p>}
                          </div>
                        ))}
                      </div>
                      {ev.overallFeedback && (
                        <div className="space-y-2 text-sm">
                          {ev.overallFeedback && <p className="text-slate-600 whitespace-pre-wrap"><span className="font-semibold text-slate-700">Overall:</span> {ev.overallFeedback}</p>}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        <div className="space-y-6">
          <div className="rounded-2xl bg-white border border-slate-200/70 shadow-sm p-5 sm:p-6">
            <div className="flex items-center gap-2 mb-4">
              <Layers3 className="w-4 h-4 text-primary" />
              <h3 className="font-bold text-slate-900">Current checkpoint summary</h3>
            </div>
            {checkpointData ? (
              <div className="space-y-3 text-sm text-slate-600">
                <p>{checkpointData.shortDescription}</p>
                <div className="rounded-xl bg-slate-50 border border-slate-200 p-3">
                  <p className="text-xs uppercase tracking-wider text-slate-400 font-semibold">Criteria count</p>
                  <p className="mt-1 font-bold text-slate-900">{checkpointData.rubrics?.length || 0} criteria</p>
                </div>
                <div className="rounded-xl bg-slate-50 border border-slate-200 p-3">
                  <p className="text-xs uppercase tracking-wider text-slate-400 font-semibold">Submission window</p>
                  <p className="mt-1 font-bold text-slate-900">Weight total: 100%</p>
                </div>
              </div>
            ) : (
              <p className="text-sm text-slate-500">Rubric configuration unavailable.</p>
            )}
          </div>

          <div className="rounded-2xl bg-white border border-slate-200/70 shadow-sm p-5 sm:p-6">
            <div className="flex items-center gap-2 mb-4">
              <History className="w-4 h-4 text-primary" />
              <h3 className="font-bold text-slate-900">Evaluation history</h3>
            </div>
            {history.length === 0 ? (
              <p className="text-sm text-slate-500">No history available yet.</p>
            ) : (
              <div className="space-y-3 max-h-[440px] overflow-y-auto pr-1">
                {history.map((item) => (
                  <div key={item._id} className="rounded-xl border border-slate-200 bg-slate-50 p-3">
                    <div className="flex items-center justify-between gap-2">
                      <p className="text-xs font-bold uppercase tracking-wider text-slate-400">{item.action}</p>
                      <p className="text-xs text-slate-400">v{item.version}</p>
                    </div>
                    <p className="mt-1 text-sm font-semibold text-slate-900">{item.changedBy?.name || 'System'}</p>
                    <p className="text-xs text-slate-500">{new Date(item.createdAt).toLocaleString()}</p>
                    {item.note && <p className="mt-2 text-sm text-slate-600">{item.note}</p>}
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="rounded-2xl bg-white border border-slate-200/70 shadow-sm p-5 sm:p-6">
            <div className="flex items-center gap-2 mb-4">
              <Sparkles className="w-4 h-4 text-primary" />
              <h3 className="font-bold text-slate-900">Access and workflow</h3>
            </div>
            <div className="space-y-3 text-sm text-slate-600">
              <p>Lecturers can create drafts, score rubric criteria, and submit the official evaluation.</p>
              <p>Mentors can review and comment, but cannot finalize the score.</p>
              <p>Students can view published evaluations and feedback history.</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
