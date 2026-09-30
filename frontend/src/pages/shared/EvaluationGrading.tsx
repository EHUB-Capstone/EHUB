import { useCallback, useDeferredValue, useEffect, useMemo, useRef, useState } from 'react';
import { Navigate, useSearchParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import {
  Award, BookOpenCheck, CheckCircle2, ChevronDown, ChevronUp, ClipboardCheck, Edit3, Filter,
  EyeOff, Loader2, MessageSquareText, RefreshCw, Search, Send, Trophy, Users, X,
} from 'lucide-react';
import { checkpointApi } from '../../api/checkpointApi';
import { classApi } from '../../api/classApi';
import { evaluationApi } from '../../api/evaluationApi';
import { workspaceApi } from '../../api/workspaceApi';
import EvaluationPanel from '../../components/workspace/EvaluationPanel';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import EmptyState from '../../components/ui/EmptyState';
import ErrorState from '../../components/ui/ErrorState';
import { releaseFeatureFlags } from '../../config/releaseFeatureFlags';
import { useAuth } from '../../hooks/useAuth';
import Rankings from '../common/Rankings';
import type {
  CheckpointEvaluationSummary, CourseAssessmentEvaluation, CourseAssessmentEvaluationList, EvaluationCheckpoint,
  EvaluationGradingRecord, EvaluationGradingStatus, EvaluationTeam, EvaluationTeamMember,
} from '../../types/evaluationGrading';
import type { ClassRosterListResponse, ClassRosterStudent } from '../../types/classes';
import type { ApiEnvelope, WorkspaceOption } from '../../types/workspaceTools';
import type { WorkspaceCheckpointOverviewResponse } from '../../types/workspaceCheckpoints';
import { parseApiError } from '../../utils/apiError';
import {
  calculateWeightedCourseScore, canAccessEvaluationRankings, evaluationStatusLabel, filterEvaluationRecords, filterTeamsBySemester,
  resolveActiveEvaluationSemester, resolveEvaluationMemberScore,
  selectLatestOfficialEvaluation,
} from '../../utils/evaluationGrading';
import { parseWorkspaceSemester } from '../../utils/workspaceHub';

const roleDescription: Record<string, string> = {
  ADMIN: 'Review evaluation results across all accessible classes and teams.',
  LECTURER: 'Review and manage grading for teams in the classes you teach.',
  MENTOR: 'Review published team scores and written feedback for the teams you mentor.',
  STUDENT: 'Review published team and personal scores, plus written feedback for your team.',
};

const statusStyle: Record<EvaluationGradingStatus, string> = {
  DRAFT: 'border-amber-200 bg-amber-50 text-amber-700',
  SUBMITTED: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  PUBLISHED: 'border-blue-200 bg-blue-50 text-blue-700',
  NOT_GRADED: 'border-slate-200 bg-slate-50 text-slate-500',
};

const MAX_BULK_PUBLICATION_COUNT = 200;
type PublicationAction = 'publish' | 'unpublish';

interface PublicationConfirmation {
  action: PublicationAction;
  evaluationIds: string[];
  teamCount: number;
  scope: 'single' | 'filtered';
}

interface EvaluationWorkspaceDetail {
  team?: { teamCode?: string };
  members?: EvaluationTeamMember[];
  project?: { projectName?: string; description?: string | null } | null;
  proposal?: { projectName?: string; projectDescription?: string | null } | null;
}

interface TeamCheckpointOverview {
  team: EvaluationTeam;
  checkpoints: EvaluationCheckpoint[];
  assessments: CourseAssessmentEvaluation[];
}

interface SummaryJob {
  team: EvaluationTeam;
  checkpoint: EvaluationCheckpoint;
}

interface EvaluationTeamGroup {
  team: EvaluationTeam;
  recordsByCheckpoint: Map<number, EvaluationGradingRecord>;
  assessments: CourseAssessmentEvaluation[];
}

interface EvaluationClassGroup {
  classId: string;
  classCode: string;
  courseCode: string;
  semester: string;
  checkpoints: EvaluationCheckpoint[];
  assessments: CourseAssessmentEvaluation[];
  teams: EvaluationTeamGroup[];
}

async function mapWithConcurrency<T, R>(
  items: T[],
  limit: number,
  task: (item: T) => Promise<R>,
): Promise<PromiseSettledResult<R>[]> {
  const results: PromiseSettledResult<R>[] = new Array(items.length);
  let nextIndex = 0;
  const worker = async () => {
    while (nextIndex < items.length) {
      const index = nextIndex++;
      try {
        results[index] = { status: 'fulfilled', value: await task(items[index]) };
      } catch (reason) {
        results[index] = { status: 'rejected', reason };
      }
    }
  };
  await Promise.all(Array.from({ length: Math.min(limit, items.length) }, worker));
  return results;
}

async function loadClassRoster(classId: string): Promise<ClassRosterStudent[]> {
  const firstPage = await classApi.getStudents(classId, { page: 1, pageSize: 100 }) as ApiEnvelope<ClassRosterListResponse>;
  if (!firstPage.success) throw new Error(firstPage.message || 'Unable to load class roster.');
  const firstData = firstPage.data;
  const remainingPages = Array.from(
    { length: Math.max(0, Number(firstData?.totalPages || 1) - 1) },
    (_, index) => index + 2,
  );
  const remainingResponses = await Promise.all(remainingPages.map(async page => {
    const response = await classApi.getStudents(classId, { page, pageSize: 100 }) as ApiEnvelope<ClassRosterListResponse>;
    if (!response.success) throw new Error(response.message || 'Unable to load class roster.');
    return response.data?.items || [];
  }));
  return [...(firstData?.items || []), ...remainingResponses.flat()];
}

function normalizeStatus(value: string | undefined): Exclude<EvaluationGradingStatus, 'NOT_GRADED'> {
  const normalized = String(value || '').toUpperCase();
  return normalized === 'PUBLISHED' ? 'PUBLISHED' : normalized === 'SUBMITTED' ? 'SUBMITTED' : 'DRAFT';
}

function formatUpdatedAt(value?: string): string {
  if (!value) return 'Not updated yet';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 'Not updated yet' : date.toLocaleString();
}

function memberScore(record: EvaluationGradingRecord | undefined, studentId: string): {
  score: number | null;
  isOverridden: boolean;
} {
  if (!record?.evaluation) return { score: null, isOverridden: false };
  return resolveEvaluationMemberScore(record.evaluation, studentId);
}

function courseAssessmentMemberScore(assessment: CourseAssessmentEvaluation, studentId: string): {
  score: number | null;
  isOverridden: boolean;
} {
  const member = assessment.memberScores?.find(item => item.studentId === studentId);
  return {
    score: member?.score ?? null,
    isOverridden: Boolean(member?.isOverridden),
  };
}

function groupEvaluationRecords(
  records: EvaluationGradingRecord[],
  assessmentsByTeam: Map<string, CourseAssessmentEvaluation[]>,
): EvaluationClassGroup[] {
  const classGroups = new Map<string, EvaluationClassGroup>();
  records.forEach(record => {
    let classGroup = classGroups.get(record.team.classId);
    if (!classGroup) {
      classGroup = {
        classId: record.team.classId,
        classCode: record.team.classCode,
        courseCode: record.team.courseCode,
        semester: record.team.semester,
        checkpoints: [],
        assessments: assessmentsByTeam.get(record.team.teamId) || [],
        teams: [],
      };
      classGroups.set(record.team.classId, classGroup);
    }
    if (!classGroup.checkpoints.some(checkpoint => checkpoint.number === record.checkpoint.number)) {
      classGroup.checkpoints.push(record.checkpoint);
    }
    let teamGroup = classGroup.teams.find(group => group.team.teamId === record.team.teamId);
    if (!teamGroup) {
      teamGroup = { team: record.team, recordsByCheckpoint: new Map(), assessments: assessmentsByTeam.get(record.team.teamId) || [] };
      classGroup.teams.push(teamGroup);
    }
    teamGroup.recordsByCheckpoint.set(record.checkpoint.number, record);
  });

  return [...classGroups.values()]
    .map(group => ({
      ...group,
      checkpoints: [...group.checkpoints].sort((left, right) => left.number - right.number),
      teams: [...group.teams].sort((left, right) =>
        left.team.teamName.localeCompare(right.team.teamName, undefined, { numeric: true })),
    }))
    .sort((left, right) => left.classCode.localeCompare(right.classCode, undefined, { numeric: true }));
}

function weightedCourseScore(
  checkpoints: EvaluationCheckpoint[],
  recordsByCheckpoint: Map<number, EvaluationGradingRecord>,
  assessments: CourseAssessmentEvaluation[],
  studentId?: string,
): { score: number; complete: boolean } {
  const checkpointComponents = checkpoints.map(checkpointItem => {
    const record = recordsByCheckpoint.get(checkpointItem.number);
    const value = studentId ? memberScore(record, studentId).score : record?.evaluation?.checkpointTotal ?? null;
    return { score: value, weight: checkpointItem.courseWeight };
  });
  return calculateWeightedCourseScore([
    ...checkpointComponents,
    ...assessments.map(assessment => ({
      score: studentId ? courseAssessmentMemberScore(assessment, studentId).score : assessment.score,
      weight: assessment.weight,
    })),
  ]);
}

function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  return (parts.length > 1 ? `${parts[0][0]}${parts.at(-1)?.[0]}` : parts[0]?.slice(0, 2) || '?').toUpperCase();
}

function FeedbackDialog({
  record,
  onClose,
  isStudent,
}: {
  record: EvaluationGradingRecord;
  onClose: () => void;
  isStudent: boolean;
}) {
  const [criterionScoresExpanded, setCriterionScoresExpanded] = useState(false);
  const evaluation = record.evaluation;
  if (!evaluation) return null;
  const rubricComments = evaluation.rubricScores.filter(score => score.comment?.trim());
  const criterionScores = evaluation.rubricScores.filter(score => score.score !== null && score.score !== undefined);
  const canShowCriterionScores = isStudent && record.status === 'PUBLISHED' && criterionScores.length > 0;
  const criterionConfig = new Map((record.checkpoint.rubrics || []).map(criterion => [criterion.key, criterion]));

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-slate-950/50 p-3 backdrop-blur-sm sm:p-6" role="dialog" aria-modal="true" aria-labelledby="feedback-dialog-title">
      <div className="flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl">
        <header className="flex items-start justify-between gap-4 border-b border-slate-200 bg-gradient-to-r from-orange-50 via-white to-blue-50/60 px-5 py-4">
          <div className="min-w-0">
            <p className="text-[10px] font-bold uppercase tracking-wider text-primary">{record.team.classCode} · {record.team.teamName}</p>
            <h2 id="feedback-dialog-title" className="mt-1 truncate text-lg font-black text-slate-900">Checkpoint {record.checkpoint.number}: {record.checkpoint.title}</h2>
          </div>
          <button type="button" onClick={onClose} className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-slate-400 transition-colors hover:bg-white hover:text-slate-700" aria-label="Close feedback dialog"><X className="h-5 w-5" /></button>
        </header>

        <div className="overflow-y-auto p-5 sm:p-6">
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div className="rounded-xl border border-orange-100 bg-orange-50 p-3"><p className="text-[10px] font-bold uppercase tracking-wider text-orange-500">Team score</p>{evaluation.checkpointTotal === null || evaluation.checkpointTotal === undefined ? <p className="mt-1 text-sm font-bold text-slate-400">Pending publication</p> : <p className="mt-1 text-2xl font-black text-primary">{Number(evaluation.checkpointTotal).toFixed(2)}<span className="text-xs text-slate-400"> / 10</span></p>}</div>
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3"><p className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Status</p><span className={`mt-2 inline-flex rounded-full border px-2.5 py-1 text-xs font-bold ${statusStyle[record.status]}`}>{evaluationStatusLabel(record.status)}</span></div>
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3"><p className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Evaluated by</p><p className="mt-1 truncate text-sm font-bold text-slate-800">{evaluation.lecturerId?.name || 'Lecturer'}</p></div>
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3"><p className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Updated</p><p className="mt-1 text-xs font-semibold leading-5 text-slate-700">{formatUpdatedAt(evaluation.updatedAt)}</p></div>
          </div>

          {canShowCriterionScores && (
            <section className="mt-5 overflow-hidden rounded-2xl border border-orange-200 bg-orange-50/30">
              <button
                type="button"
                onClick={() => setCriterionScoresExpanded(current => !current)}
                aria-expanded={criterionScoresExpanded}
                aria-controls="student-criterion-score-details"
                className="flex w-full items-center justify-between gap-4 px-4 py-3.5 text-left transition-colors hover:bg-orange-50"
              >
                <span className="flex min-w-0 items-center gap-3">
                  <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white text-primary shadow-sm"><ClipboardCheck className="h-4 w-4" /></span>
                  <span className="min-w-0">
                    <span className="block text-sm font-bold text-slate-900">Criterion score details</span>
                    <span className="block text-xs text-slate-500">{criterionScores.length} scored {criterionScores.length === 1 ? 'criterion' : 'criteria'}</span>
                  </span>
                </span>
                {criterionScoresExpanded ? <ChevronUp className="h-4 w-4 shrink-0 text-slate-500" /> : <ChevronDown className="h-4 w-4 shrink-0 text-slate-500" />}
              </button>
              {criterionScoresExpanded && (
                <div id="student-criterion-score-details" className="divide-y divide-orange-100 border-t border-orange-100 bg-white px-4">
                  {criterionScores.map(score => {
                    const config = criterionConfig.get(score.criterionKey);
                    return (
                      <div key={score.criterionKey} className="flex items-center justify-between gap-4 py-3">
                        <div className="min-w-0">
                          <p className="text-sm font-semibold text-slate-800">{score.criterionName}</p>
                          {config && <p className="mt-0.5 text-[11px] text-slate-400">Weight {Number(config.weight)}%</p>}
                        </div>
                        <span className="shrink-0 rounded-xl bg-primary-50 px-3 py-1.5 text-sm font-black tabular-nums text-primary">
                          {Number(score.score).toFixed(2)}{config ? <span className="ml-1 text-[10px] text-slate-400">/ {Number(config.maxScore)}</span> : null}
                        </span>
                      </div>
                    );
                  })}
                </div>
              )}
            </section>
          )}

          <section className={`${canShowCriterionScores ? 'mt-4' : 'mt-5'} rounded-2xl border border-slate-200 p-4`}>
            <div className="flex items-center gap-2"><MessageSquareText className="h-4 w-4 text-primary" /><h3 className="font-bold text-slate-900">Overall Feedback</h3></div>
            <p className={`mt-3 whitespace-pre-wrap text-sm leading-6 ${evaluation.overallFeedback ? 'text-slate-700' : 'italic text-slate-400'}`}>{evaluation.overallFeedback || 'No overall feedback was provided.'}</p>
          </section>

          <section className="mt-4 rounded-2xl border border-slate-200 p-4">
            <div className="flex items-center gap-2"><ClipboardCheck className="h-4 w-4 text-primary" /><h3 className="font-bold text-slate-900">Rubric Comments</h3></div>
            {rubricComments.length === 0 ? <p className="mt-3 text-sm italic text-slate-400">No rubric comments were provided.</p> : (
              <div className="mt-3 divide-y divide-slate-100">
                {rubricComments.map(score => <div key={score.criterionKey} className="py-3 first:pt-0 last:pb-0"><div className="flex items-center justify-between gap-3"><p className="text-sm font-bold text-slate-800">{score.criterionName}</p>{!isStudent && score.score !== null && score.score !== undefined && <span className="shrink-0 rounded-lg bg-primary-50 px-2 py-1 text-xs font-black text-primary">{Number(score.score).toFixed(2)}</span>}</div><p className="mt-1 whitespace-pre-wrap text-sm leading-6 text-slate-600">{score.comment}</p></div>)}
              </div>
            )}
          </section>
        </div>
      </div>
    </div>
  );
}

function CourseAssessmentDialog({
  team,
  assessment,
  onClose,
  onSaved,
}: {
  team: EvaluationTeam;
  assessment: CourseAssessmentEvaluation;
  onClose: () => void;
  onSaved: () => void;
}) {
  const members = team.members || [];
  const [score, setScore] = useState(assessment.score?.toString() ?? '');
  const [memberScores, setMemberScores] = useState<Record<string, { value: string; customized: boolean }>>(() =>
    Object.fromEntries(members.map(member => {
      const saved = assessment.memberScores?.find(item => item.studentId === member.studentId);
      return [member.studentId, {
        value: (saved?.score ?? assessment.score)?.toString() ?? '',
        customized: Boolean(saved?.isOverridden),
      }];
    })),
  );
  const [saving, setSaving] = useState(false);
  const numericScore = Number(score);
  const isScoreValid = (value: string) => value.trim() !== '' && Number.isFinite(Number(value)) && Number(value) >= 0 && Number(value) <= 10;
  const isValid = isScoreValid(score) && members.every(member => isScoreValid(memberScores[member.studentId]?.value ?? ''));
  const updateTeamScore = (value: string) => {
    setScore(value);
    setMemberScores(current => Object.fromEntries(members.map(member => {
      const existing = current[member.studentId];
      return [member.studentId, existing?.customized ? existing : { value, customized: false }];
    })));
  };
  const updateMemberScore = (studentId: string, value: string) => {
    setMemberScores(current => ({ ...current, [studentId]: { value, customized: true } }));
  };
  const save = async () => {
    if (!isValid) return;
    try {
      setSaving(true);
      const response = await evaluationApi.saveCourseAssessment(team.teamId, assessment.assessmentId, {
        score: numericScore,
        memberScores: members.map(member => ({
          studentId: member.studentId,
          score: Number(memberScores[member.studentId].value),
        })),
      });
      if (!response.success) throw new Error(response.message || 'Unable to save assessment score.');
      toast.success(`${assessment.name} score saved`);
      onSaved();
    } catch (error: unknown) {
      toast.error(parseApiError(error, 'Unable to save assessment score.').message);
    } finally {
      setSaving(false);
    }
  };
  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-slate-950/50 p-4 backdrop-blur-sm" role="dialog" aria-modal="true" aria-labelledby="course-assessment-dialog-title">
      <div className="flex max-h-[92vh] w-full max-w-lg flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl">
        <div className="flex items-start justify-between gap-4 border-b border-slate-100 p-5">
          <div><p className="text-[10px] font-bold uppercase tracking-wider text-primary">{team.classCode} · {team.teamName}</p><h2 id="course-assessment-dialog-title" className="mt-1 text-lg font-black text-slate-900">{assessment.name}</h2><p className="mt-1 text-sm text-slate-500">This assessment contributes {Number(assessment.weight).toFixed(1)}% to the course total.</p></div>
          <button type="button" onClick={onClose} className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-slate-400 hover:bg-slate-100 hover:text-slate-700" aria-label="Close assessment grading dialog"><X className="h-5 w-5" /></button>
        </div>
        <div className="overflow-y-auto p-5">
          <label className="block text-sm font-bold text-slate-700">Team score (0-10)<input autoFocus aria-label={`${assessment.name} team score`} type="number" inputMode="decimal" min="0" max="10" step="0.01" value={score} onChange={event => updateTeamScore(event.target.value)} className="mt-2 w-full appearance-none rounded-xl border border-slate-200 px-3 py-2.5 text-lg font-bold outline-none [appearance:textfield] focus:border-primary focus:ring-2 focus:ring-primary/20 [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none" /></label>
          {score && !isScoreValid(score) && <p className="mt-2 text-xs font-medium text-red-600">Enter a team score from 0 to 10.</p>}
          <section className="mt-5 border-t border-slate-100 pt-5" aria-labelledby="member-assessment-scores">
            <h3 id="member-assessment-scores" className="text-sm font-black text-slate-800">Member scores</h3>
            <p className="mt-1 text-xs text-slate-500">Each member starts with the team score. Adjust a member only when an individual score is needed.</p>
            <div className="mt-3 space-y-3">
              {members.map(member => {
                const value = memberScores[member.studentId]?.value ?? '';
                return <label key={member.studentId} className="grid grid-cols-[minmax(0,1fr)_7rem] items-center gap-3 rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5"><span className="min-w-0"><span className="block truncate text-sm font-bold text-slate-800" title={member.fullName}>{member.fullName}</span>{member.rollNumber && <span className="block text-xs text-slate-500">{member.rollNumber}</span>}</span><span><input aria-label={`${member.fullName} score`} type="number" inputMode="decimal" min="0" max="10" step="0.01" value={value} onChange={event => updateMemberScore(member.studentId, event.target.value)} className="w-full appearance-none rounded-lg border border-slate-200 bg-white px-2.5 py-2 text-right font-bold outline-none [appearance:textfield] focus:border-primary focus:ring-2 focus:ring-primary/20 [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none" />{value && !isScoreValid(value) && <span className="mt-1 block text-[10px] font-medium text-red-600">0 to 10 only</span>}</span></label>;
              })}
            </div>
          </section>
          <div className="mt-6 flex justify-end gap-2"><button type="button" onClick={onClose} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-50">Cancel</button><button type="button" onClick={() => void save()} disabled={!isValid || saving} className="inline-flex min-w-28 items-center justify-center rounded-xl bg-primary px-4 py-2 text-sm font-bold text-white hover:bg-primary-600 disabled:cursor-not-allowed disabled:opacity-50">{saving ? <Loader2 className="h-4 w-4 animate-spin" /> : 'Save scores'}</button></div>
        </div>
      </div>
    </div>
  );
}

export default function EvaluationGrading() {
  const { user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const [teams, setTeams] = useState<WorkspaceOption[]>([]);
  const [records, setRecords] = useState<EvaluationGradingRecord[]>([]);
  const [assessmentsByTeam, setAssessmentsByTeam] = useState<Map<string, CourseAssessmentEvaluation[]>>(new Map());
  const [loadingTeams, setLoadingTeams] = useState(true);
  const [loadingRecords, setLoadingRecords] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [partialFailureCount, setPartialFailureCount] = useState(0);
  const [reloadKey, setReloadKey] = useState(0);
  const [feedbackRecord, setFeedbackRecord] = useState<EvaluationGradingRecord | null>(null);
  const [editingRecord, setEditingRecord] = useState<EvaluationGradingRecord | null>(null);
  const [editingAssessment, setEditingAssessment] = useState<{ team: EvaluationTeam; assessment: CourseAssessmentEvaluation } | null>(null);
  const [publishingEvaluationId, setPublishingEvaluationId] = useState<string | null>(null);
  const [bulkPublicationSubmitting, setBulkPublicationSubmitting] = useState(false);
  const [publicationConfirmation, setPublicationConfirmation] = useState<PublicationConfirmation | null>(null);
  const recordsRequestId = useRef(0);

  const appliedSearch = searchParams.get('search') || '';
  const [search, setSearch] = useState(appliedSearch);
  const [previousSearch, setPreviousSearch] = useState(appliedSearch);
  if (previousSearch !== appliedSearch) {
    setPreviousSearch(appliedSearch);
    setSearch(appliedSearch);
  }

  const role = String(user?.role || 'STUDENT').toUpperCase();
  const canEdit = role === 'LECTURER';
  const canViewCourseTotal = role !== 'MENTOR';
  const currentUserId = user?.id || '';
  const canViewRankings = releaseFeatureFlags.rankings && canAccessEvaluationRankings(role);
  const requestedTab = searchParams.get('tab') || 'results';
  const activeTab = requestedTab === 'rankings' && canViewRankings ? 'rankings' : 'results';
  const activeSemester = useMemo(() => resolveActiveEvaluationSemester(teams), [teams]);
  const isDefaultSemester = !searchParams.has('semester') && !searchParams.has('year');
  const semester = isDefaultSemester ? activeSemester?.semester || 'none' : searchParams.get('semester') || 'all';
  const year = isDefaultSemester ? activeSemester?.year || 'none' : searchParams.get('year') || 'all';
  const classId = searchParams.get('classId') || '';
  const teamId = searchParams.get('teamId') || '';
  const checkpoint = searchParams.get('checkpoint') || '';
  const status = searchParams.get('status') || '';

  const updateFilter = useCallback((key: string, value: string) => {
    const next = new URLSearchParams(searchParams);
    if (isDefaultSemester && key !== 'semester' && key !== 'year') {
      next.set('semester', semester);
      next.set('year', year);
    }
    if (value) next.set(key, value);
    else next.delete(key);
    if (key === 'classId') next.delete('teamId');
    setSearchParams(next, { replace: true });
  }, [isDefaultSemester, searchParams, semester, setSearchParams, year]);

  const updateSemester = (nextSemester: string) => {
    const next = new URLSearchParams(searchParams);
    next.set('semester', nextSemester);
    next.set('year', year === 'none' ? 'all' : year);
    next.delete('classId');
    next.delete('teamId');
    setSearchParams(next, { replace: true });
  };

  const updateYear = (nextYear: string) => {
    const next = new URLSearchParams(searchParams);
    next.set('semester', semester === 'none' ? 'all' : semester);
    next.set('year', nextYear);
    next.delete('classId');
    next.delete('teamId');
    setSearchParams(next, { replace: true });
  };

  const loadTeams = useCallback(async () => {
    try {
      setLoadingTeams(true);
      setErrorMessage('');
      const response = await workspaceApi.getAccessibleTeams();
      if (!response.success) throw new Error(response.message || 'Unable to load accessible teams.');
      setTeams(Array.isArray(response.data) ? response.data : []);
    } catch (error: unknown) {
      setTeams([]);
      setErrorMessage(parseApiError(error, 'Unable to load teams for evaluation and grading.').message);
    } finally {
      setLoadingTeams(false);
    }
  }, []);

  const loadEvaluationRecords = useCallback(async (scopedTeams: WorkspaceOption[]) => {
    const requestId = ++recordsRequestId.current;
    if (semester === 'none' || year === 'none') {
      setRecords([]);
      setAssessmentsByTeam(new Map());
      setPartialFailureCount(0);
      setLoadingRecords(false);
      return;
    }

    try {
      setLoadingRecords(true);
      setErrorMessage('');
      const eligibleTeams = scopedTeams.filter(team => team.hasWorkspace);
      const rosterClassIds = (role === 'ADMIN' || role === 'LECTURER')
        ? [...new Set(eligibleTeams.map(team => team.classId))]
        : [];
      const rosterPromise = mapWithConcurrency(rosterClassIds, 3, async classId => ({
        classId,
        students: await loadClassRoster(classId),
      }));
      const overviewPromise = mapWithConcurrency<WorkspaceOption, TeamCheckpointOverview>(eligibleTeams, 4, async team => {
        const [checkpointResponse, workspaceResponse, assessmentResponse] = await Promise.all([
          checkpointApi.getCheckpointData(team.teamId) as Promise<ApiEnvelope<WorkspaceCheckpointOverviewResponse>>,
          workspaceApi.getTeamWorkspace(team.teamId) as Promise<ApiEnvelope<EvaluationWorkspaceDetail>>,
          evaluationApi.getCourseAssessments(team.teamId) as Promise<ApiEnvelope<CourseAssessmentEvaluationList>>,
        ]);
        if (!checkpointResponse.success) throw new Error(checkpointResponse.message || `Unable to load checkpoints for ${team.teamName}.`);
        if (!workspaceResponse.success) throw new Error(workspaceResponse.message || `Unable to load members for ${team.teamName}.`);
        if (!assessmentResponse.success) throw new Error(assessmentResponse.message || `Unable to load course assessments for ${team.teamName}.`);
        return {
          team: {
            ...team,
            teamCode: workspaceResponse.data?.team?.teamCode || '',
            projectName: workspaceResponse.data?.project?.projectName?.trim()
              || workspaceResponse.data?.proposal?.projectName?.trim()
              || '',
            projectDescription: workspaceResponse.data?.project?.description?.trim()
              || workspaceResponse.data?.proposal?.projectDescription?.trim()
              || '',
            members: Array.isArray(workspaceResponse.data?.members) ? workspaceResponse.data.members : [],
          },
          checkpoints: (checkpointResponse.data?.checkpoints || []).map(item => ({
            number: item.number,
            title: item.title,
            shortDescription: item.shortDescription,
            courseWeight: Number(item.courseWeight || 0),
          })),
          assessments: Array.isArray(assessmentResponse.data?.assessments) ? assessmentResponse.data.assessments : [],
        };
      });
      const [rosterResults, overviewResults] = await Promise.all([rosterPromise, overviewPromise]);
      const semesterGroupsByTeam = new Map<string, Set<string>>();
      rosterResults.forEach(result => {
        if (result.status !== 'fulfilled') return;
        result.value.students.forEach(student => {
          const teamKey = String(student.teamId || '').toLowerCase();
          const groupName = student.semesterGroupName?.trim();
          if (!teamKey || !groupName) return;
          const groupNames = semesterGroupsByTeam.get(teamKey) || new Set<string>();
          groupNames.add(groupName);
          semesterGroupsByTeam.set(teamKey, groupNames);
        });
      });
      const overviews = overviewResults
        .filter((result): result is PromiseFulfilledResult<TeamCheckpointOverview> => result.status === 'fulfilled')
        .map(result => ({
          ...result.value,
          team: {
            ...result.value.team,
            semesterGroupName: [...(semesterGroupsByTeam.get(result.value.team.teamId.toLowerCase()) || [])].join(', '),
          },
        }));
      const jobs: SummaryJob[] = overviews.flatMap(item =>
        item.checkpoints.map(checkpointItem => ({ team: item.team, checkpoint: checkpointItem })),
      );
      const summaryResults = await mapWithConcurrency(jobs, 6, async job => {
        const response = await evaluationApi.getCheckpointSummary(job.team.teamId, job.checkpoint.number) as ApiEnvelope<CheckpointEvaluationSummary>;
        if (!response.success) throw new Error(response.message || `Unable to load ${job.checkpoint.title}.`);
        return { job, summary: response.data };
      });

      const nextRecords: EvaluationGradingRecord[] = [];
      summaryResults.forEach(result => {
        if (result.status !== 'fulfilled') return;
        const { job, summary } = result.value;
        const normalizedEvaluations = (Array.isArray(summary?.evaluations) ? summary.evaluations : []).map(evaluation => ({
          ...evaluation,
          status: normalizeStatus(evaluation.status),
          rubricScores: Array.isArray(evaluation.rubricScores) ? evaluation.rubricScores : [],
          memberScores: Array.isArray(evaluation.memberScores) ? evaluation.memberScores : [],
        }));
        const evaluation = selectLatestOfficialEvaluation(normalizedEvaluations);
        nextRecords.push({
          key: `${job.team.teamId}-${job.checkpoint.number}-${evaluation?._id || 'not-graded'}`,
          team: job.team,
          checkpoint: summary?.checkpoint || job.checkpoint,
          evaluation,
          status: evaluation?.status || 'NOT_GRADED',
        });
      });

      if (requestId !== recordsRequestId.current) return;
      setRecords(nextRecords);
      setAssessmentsByTeam(new Map(overviews.map(item => [item.team.teamId, item.assessments])));
      setPartialFailureCount(
        rosterResults.filter(result => result.status === 'rejected').length +
        overviewResults.filter(result => result.status === 'rejected').length +
        summaryResults.filter(result => result.status === 'rejected').length,
      );
    } catch (error: unknown) {
      if (requestId !== recordsRequestId.current) return;
      setErrorMessage(parseApiError(error, 'Unable to load evaluation and grading data.').message);
    } finally {
      if (requestId === recordsRequestId.current) setLoadingRecords(false);
    }
  }, [role, semester, year]);

  const scopedTeams = useMemo(
    () => semester === 'none' || year === 'none' ? [] : filterTeamsBySemester(teams, semester, year),
    [semester, teams, year],
  );
  useEffect(() => {
    if (loadingTeams || activeTab !== 'results') return;
    void loadEvaluationRecords(scopedTeams);
  }, [activeTab, loadEvaluationRecords, loadingTeams, scopedTeams]);
  useEffect(() => { void loadTeams(); }, [loadTeams, reloadKey]);

  const classes = useMemo(() => {
    const unique = new Map<string, WorkspaceOption>();
    scopedTeams.forEach(team => unique.set(team.classId, team));
    return [...unique.values()].sort((left, right) => left.classCode.localeCompare(right.classCode, undefined, { numeric: true }));
  }, [scopedTeams]);
  const teamOptions = useMemo(
    () => scopedTeams.filter(team => !classId || team.classId === classId)
      .sort((left, right) => left.teamName.localeCompare(right.teamName, undefined, { numeric: true })),
    [classId, scopedTeams],
  );
  const checkpointOptions = useMemo(() => {
    const unique = new Map<number, string>();
    records.forEach(record => unique.set(record.checkpoint.number, record.checkpoint.title));
    return [...unique.entries()].sort((left, right) => left[0] - right[0]);
  }, [records]);
  const semesterOptions = useMemo(() => [...new Set(teams.map(team => parseWorkspaceSemester(team.semester)?.semester).filter((value): value is string => Boolean(value)))].sort(), [teams]);
  const yearOptions = useMemo(() => [...new Set(teams.map(team => parseWorkspaceSemester(team.semester)?.year).filter((value): value is string => Boolean(value)))].sort().reverse(), [teams]);

  const activeFilters = useMemo(() => ({ search: appliedSearch, classId, teamId, checkpoint, status }), [appliedSearch, checkpoint, classId, status, teamId]);
  const deferredFilters = useDeferredValue(activeFilters);
  const isFilterPending = deferredFilters !== activeFilters;
  const filteredRecords = useMemo(() => filterEvaluationRecords(records, deferredFilters), [deferredFilters, records]);
  const classGroups = useMemo(() => groupEvaluationRecords(filteredRecords, assessmentsByTeam), [assessmentsByTeam, filteredRecords]);
  const bulkPublicationTargets = useMemo(() => {
    const publishIds = new Set<string>();
    const publishTeams = new Set<string>();
    const unpublishIds = new Set<string>();
    const unpublishTeams = new Set<string>();
    const addTarget = (evaluationId: string | null | undefined, evaluationStatus: EvaluationGradingStatus, targetTeamId: string) => {
      if (!evaluationId) return;
      if (evaluationStatus === 'SUBMITTED') {
        publishIds.add(evaluationId);
        publishTeams.add(targetTeamId);
      } else if (evaluationStatus === 'PUBLISHED') {
        unpublishIds.add(evaluationId);
        unpublishTeams.add(targetTeamId);
      }
    };

    classGroups.forEach(classGroup => classGroup.teams.forEach(teamGroup => {
      if (teamGroup.team.accessMode === 'READ_ONLY') return;
      teamGroup.assessments.forEach(assessment => {
        if (assessment.evaluatorId === currentUserId)
          addTarget(assessment.evaluationId, assessment.status, teamGroup.team.teamId);
      });
      teamGroup.recordsByCheckpoint.forEach(record => {
        if (record.evaluation?.lecturerId?._id === currentUserId)
          addTarget(record.evaluation._id, record.status, teamGroup.team.teamId);
      });
    }));

    return {
      publish: { evaluationIds: [...publishIds], teamCount: publishTeams.size },
      unpublish: { evaluationIds: [...unpublishIds], teamCount: unpublishTeams.size },
    };
  }, [classGroups, currentUserId]);
  const evaluatedRecords = filteredRecords.filter(record => record.evaluation);
  const completedCourseScores = classGroups.flatMap(group => group.teams.map(teamGroup => {
    const studentId = role === 'STUDENT'
      ? teamGroup.team.members?.find(member => member.userId === currentUserId)?.studentId
      : undefined;
    if (role === 'STUDENT' && !studentId) return { score: 0, complete: false };
    return weightedCourseScore(group.checkpoints, teamGroup.recordsByCheckpoint, teamGroup.assessments, studentId);
  })).filter(item => item.complete);
  const averageScore = completedCourseScores.length === 0 ? 0 : completedCourseScores.reduce((total, item) => total + item.score, 0) / completedCourseScores.length;
  const feedbackCount = evaluatedRecords.reduce((total, record) => total + (record.evaluation?.overallFeedback ? 1 : 0) + (record.evaluation?.rubricScores.filter(score => score.comment).length || 0), 0);
  const retry = () => setReloadKey(value => value + 1);
  const confirmPublicationChange = async () => {
    if (!publicationConfirmation || publicationConfirmation.evaluationIds.length === 0) return;
    const { action, evaluationIds, scope } = publicationConfirmation;
    const evaluationId = evaluationIds[0];
    try {
      if (scope === 'filtered') setBulkPublicationSubmitting(true);
      else setPublishingEvaluationId(evaluationId);
      const response = scope === 'filtered'
        ? await evaluationApi.updatePublicationBatch({
          action: action === 'publish' ? 'PUBLISH' : 'UNPUBLISH',
          evaluationIds,
        })
        : action === 'publish'
          ? await evaluationApi.publishEvaluation(evaluationId)
          : await evaluationApi.unpublishEvaluation(evaluationId);
      if (!response.success) throw new Error(response.message || (action === 'publish'
        ? 'Unable to publish evaluation.'
        : 'Unable to hide published scores.'));
      if (scope === 'filtered') {
        const changedCount = Number(response.data?.changedCount ?? evaluationIds.length);
        const unchangedCount = Number(response.data?.unchangedCount ?? 0);
        toast.success(`${action === 'publish' ? 'Published' : 'Hidden'} ${changedCount} evaluation${changedCount === 1 ? '' : 's'}${unchangedCount ? `; ${unchangedCount} already in the target state` : ''}.`);
      } else {
        toast.success(action === 'publish' ? 'Scores published successfully' : 'Published scores are now hidden');
      }
      setPublicationConfirmation(null);
      retry();
    } catch (error: unknown) {
      toast.error(parseApiError(error, action === 'publish'
        ? 'Unable to publish evaluation.'
        : 'Unable to hide published scores.').message);
    } finally {
      setPublishingEvaluationId(null);
      setBulkPublicationSubmitting(false);
    }
  };
  const summaryCards = [
    { label: 'Teams in view', value: new Set(filteredRecords.map(record => record.team.teamId)).size, icon: Users, color: 'text-blue-600 bg-blue-50 border-blue-100' },
    { label: 'Evaluated', value: evaluatedRecords.length, icon: CheckCircle2, color: 'text-emerald-600 bg-emerald-50 border-emerald-100' },
    ...(canViewCourseTotal ? [{ label: 'Average course score', value: completedCourseScores.length ? averageScore.toFixed(2) : '—', suffix: completedCourseScores.length ? ' / 10' : '', icon: Award, color: 'text-orange-600 bg-orange-50 border-orange-100' }] : []),
    { label: 'Written feedback', value: feedbackCount, icon: MessageSquareText, color: 'text-violet-600 bg-violet-50 border-violet-100' },
  ];
  const resetFilters = () => { setSearch(''); setSearchParams(new URLSearchParams(), { replace: true }); };
  const selectTab = (tab: 'results' | 'rankings') => {
    const next = new URLSearchParams(searchParams);
    if (tab === 'rankings') next.set('tab', 'rankings');
    else next.delete('tab');
    setSearchParams(next, { replace: true });
  };
  const publicationSubmitting = Boolean(publishingEvaluationId) || bulkPublicationSubmitting;

  if (requestedTab === 'rankings' && !canViewRankings) return <Navigate to="/403" replace />;
  if (loadingTeams) return <div className="flex min-h-[55vh] flex-col items-center justify-center"><Loader2 className="h-9 w-9 animate-spin text-primary" /><p className="mt-3 text-sm font-medium text-slate-500">Loading evaluation access…</p></div>;
  if (errorMessage && teams.length === 0) return <ErrorState title="Unable to load Evaluation & Grading" message={errorMessage} onRetry={retry} />;

  return (
    <div className="mx-auto max-w-7xl space-y-6">
      <header className="overflow-hidden rounded-2xl border border-orange-100 bg-gradient-to-r from-orange-50 via-white to-blue-50/60 p-5 shadow-sm sm:p-6">
        <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
          <div className="flex items-start gap-4"><div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-primary to-orange-500 text-white shadow-md shadow-orange-200/60"><ClipboardCheck className="h-6 w-6" /></div><div><div className="flex flex-wrap items-center gap-2"><h1 className="text-2xl font-black tracking-tight text-slate-900">Evaluation &amp; Grading</h1><span className="rounded-full border border-primary-100 bg-primary-50 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-primary">{role}</span></div><p className="mt-1 max-w-2xl text-sm text-slate-500">{roleDescription[role] || roleDescription.STUDENT}</p></div></div>
          {activeTab === 'results' && <button type="button" onClick={retry} disabled={loadingRecords} className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-600 shadow-sm transition-colors hover:border-primary/30 hover:text-primary disabled:cursor-not-allowed disabled:opacity-60"><RefreshCw className={`h-4 w-4 ${loadingRecords ? 'animate-spin' : ''}`} /> Refresh</button>}
        </div>
      </header>

      <nav className="flex w-fit items-center gap-1 rounded-xl border border-slate-200 bg-white p-1 shadow-sm" aria-label="Evaluation and grading views">
        <button type="button" onClick={() => selectTab('results')} aria-current={activeTab === 'results' ? 'page' : undefined} className={`inline-flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-bold transition-colors ${activeTab === 'results' ? 'bg-primary text-white shadow-sm' : 'text-slate-500 hover:bg-slate-50 hover:text-slate-800'}`}><ClipboardCheck className="h-4 w-4" /> Results</button>
        {canViewRankings && <button type="button" onClick={() => selectTab('rankings')} aria-current={activeTab === 'rankings' ? 'page' : undefined} className={`inline-flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-bold transition-colors ${activeTab === 'rankings' ? 'bg-primary text-white shadow-sm' : 'text-slate-500 hover:bg-slate-50 hover:text-slate-800'}`}><Trophy className="h-4 w-4" /> Rankings</button>}
      </nav>

      {activeTab === 'rankings' ? <Rankings /> : <>
      <section className="grid grid-cols-2 gap-3 lg:grid-cols-4" aria-label="Evaluation summary">
        {summaryCards.map(item => { const Icon = item.icon; return <div key={item.label} className="rounded-2xl border border-slate-200/70 bg-white p-4 shadow-sm sm:p-5"><div className={`mb-3 flex h-9 w-9 items-center justify-center rounded-xl border ${item.color}`}><Icon className="h-4 w-4" /></div><p className="text-[11px] font-bold uppercase tracking-wider text-slate-400">{item.label}</p><p className="mt-1 text-2xl font-black text-slate-900">{item.value}<span className="text-xs font-semibold text-slate-400">{'suffix' in item ? item.suffix : ''}</span></p></div>; })}
      </section>

      <section className="rounded-2xl border border-slate-200/70 bg-white p-4 shadow-sm" aria-label="Evaluation filters">
        <form onSubmit={event => { event.preventDefault(); updateFilter('search', search.trim()); }} className="flex flex-wrap items-center gap-3">
          <div className="relative min-w-[220px] flex-1"><Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" /><input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Search project name, description, or member…" aria-label="Search evaluations" className="w-full rounded-xl border border-slate-200 py-2 pl-9 pr-4 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20" /></div>
          <select value={semester} onChange={event => updateSemester(event.target.value)} aria-label="Filter by semester" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[148px]"><option value="all">All semesters</option>{semester === 'none' && <option value="none" disabled>No active semester</option>}{semesterOptions.map(value => <option key={value} value={value}>{value}</option>)}</select>
          <select value={year} onChange={event => updateYear(event.target.value)} aria-label="Filter by year" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[124px]"><option value="all">All years</option>{year === 'none' && <option value="none" disabled>No active year</option>}{yearOptions.map(value => <option key={value} value={value}>{value}</option>)}</select>
          <select value={classId} onChange={event => updateFilter('classId', event.target.value)} aria-label="Filter by class" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[160px]"><option value="">All classes</option>{classes.map(team => <option key={team.classId} value={team.classId}>{team.classCode}</option>)}</select>
          <select value={teamId} onChange={event => updateFilter('teamId', event.target.value)} aria-label="Filter by team" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[180px]"><option value="">All teams</option>{teamOptions.map(team => <option key={team.teamId} value={team.teamId}>{team.teamName}</option>)}</select>
          <select value={checkpoint} onChange={event => updateFilter('checkpoint', event.target.value)} aria-label="Filter by checkpoint" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[210px]"><option value="">All checkpoints</option>{checkpointOptions.map(([number, title]) => <option key={number} value={number}>CP {number} · {title}</option>)}</select>
          <select value={status} onChange={event => updateFilter('status', event.target.value)} aria-label="Filter by status" className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 sm:w-[150px]"><option value="">All statuses</option>{(['SUBMITTED', 'PUBLISHED', 'NOT_GRADED'] as EvaluationGradingStatus[]).map(value => <option key={value} value={value}>{evaluationStatusLabel(value)}</option>)}</select>
          <button type="submit" className="inline-flex items-center gap-2 rounded-xl bg-secondary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-secondary-700"><Filter className="h-4 w-4" /> Search</button>
          <button type="button" onClick={resetFilters} className="px-2 text-sm font-medium text-slate-400 hover:text-slate-700">Reset</button>
        </form>
      </section>

      {canEdit && classGroups.length > 0 && (
        <section className="flex flex-col justify-between gap-3 rounded-2xl border border-slate-200/70 bg-white px-4 py-3 shadow-sm sm:flex-row sm:items-center" aria-label="Bulk publication actions">
          <div>
            <p className="text-sm font-bold text-slate-800">Bulk publication</p>
            <p className="mt-0.5 text-xs text-slate-500">Applies only to editable evaluations visible under the current filters.</p>
            {(bulkPublicationTargets.publish.evaluationIds.length > MAX_BULK_PUBLICATION_COUNT || bulkPublicationTargets.unpublish.evaluationIds.length > MAX_BULK_PUBLICATION_COUNT) && (
              <p className="mt-1 text-xs font-medium text-amber-700">Refine the filters to process no more than {MAX_BULK_PUBLICATION_COUNT} evaluations at once.</p>
            )}
          </div>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              disabled={bulkPublicationTargets.publish.evaluationIds.length === 0 || bulkPublicationTargets.publish.evaluationIds.length > MAX_BULK_PUBLICATION_COUNT || bulkPublicationSubmitting || loadingRecords || isFilterPending}
              onClick={() => setPublicationConfirmation({ action: 'publish', scope: 'filtered', ...bulkPublicationTargets.publish })}
              className="inline-flex items-center gap-1.5 rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs font-bold text-emerald-700 transition-colors hover:bg-emerald-100 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {bulkPublicationSubmitting && publicationConfirmation?.action === 'publish' ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Send className="h-3.5 w-3.5" />}
              Publish all ready ({bulkPublicationTargets.publish.evaluationIds.length})
            </button>
            <button
              type="button"
              disabled={bulkPublicationTargets.unpublish.evaluationIds.length === 0 || bulkPublicationTargets.unpublish.evaluationIds.length > MAX_BULK_PUBLICATION_COUNT || bulkPublicationSubmitting || loadingRecords || isFilterPending}
              onClick={() => setPublicationConfirmation({ action: 'unpublish', scope: 'filtered', ...bulkPublicationTargets.unpublish })}
              className="inline-flex items-center gap-1.5 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-bold text-amber-700 transition-colors hover:bg-amber-100 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {bulkPublicationSubmitting && publicationConfirmation?.action === 'unpublish' ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <EyeOff className="h-3.5 w-3.5" />}
              Hide all published ({bulkPublicationTargets.unpublish.evaluationIds.length})
            </button>
          </div>
        </section>
      )}

      {partialFailureCount > 0 && <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800" role="status">Some evaluation data could not be loaded ({partialFailureCount} request{partialFailureCount === 1 ? '' : 's'}). Refresh to try again.</div>}
      {errorMessage && teams.length > 0 && <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">{errorMessage}</div>}

      {loadingRecords && records.length === 0 ? (
        <div className="flex min-h-64 flex-col items-center justify-center rounded-2xl border border-slate-200/70 bg-white"><Loader2 className="h-8 w-8 animate-spin text-primary" /><p className="mt-3 text-sm font-medium text-slate-500">Loading scores and feedback…</p></div>
      ) : classGroups.length === 0 ? (
        <div className="rounded-2xl border border-slate-200/70 bg-white shadow-sm"><EmptyState icon={BookOpenCheck} title={semester === 'none' || year === 'none' ? 'No active semester' : records.length === 0 ? 'No evaluation data yet' : 'No matching evaluations'} description={semester === 'none' || year === 'none' ? 'There is no active semester available for your current team scope. Select another semester and year to continue.' : records.length === 0 ? 'No workspace checkpoints are available for the selected semester, or grading has not been configured yet.' : 'Try a different search term or filter.'} /></div>
      ) : (
        <section className="relative min-h-64 max-w-full space-y-6 overflow-hidden" aria-label="Evaluation results" aria-busy={loadingRecords || isFilterPending}>
          <div className="flex min-h-7 items-center justify-end gap-3"><div className="flex items-center gap-2 text-sm font-medium text-slate-500">{(loadingRecords || isFilterPending) && <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />}<span>{loadingRecords ? 'Updating…' : `${classGroups.length} class${classGroups.length === 1 ? '' : 'es'}`}</span></div></div>
          <div className={`space-y-6 transition-opacity duration-150 ${loadingRecords || isFilterPending ? 'opacity-60' : 'opacity-100'}`}>
            {classGroups.map(classGroup => (
              <section key={classGroup.classId} className="min-w-0 max-w-full" aria-labelledby={`evaluation-class-${classGroup.classId}`}>
                <div className="mb-3 flex items-center gap-3"><div><h3 id={`evaluation-class-${classGroup.classId}`} className="text-base font-black text-slate-900">{classGroup.classCode}</h3><p className="text-xs font-semibold text-slate-500">{classGroup.semester} · {classGroup.courseCode}</p></div><span className="h-px flex-1 bg-slate-200" /><span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-bold text-slate-500">{classGroup.teams.length} team{classGroup.teams.length === 1 ? '' : 's'}</span></div>
                <div className="max-w-full overflow-x-auto overscroll-x-contain rounded-2xl border border-slate-200 bg-white pb-2 shadow-sm [scrollbar-gutter:stable]" tabIndex={0} aria-label={`Scrollable evaluation table for ${classGroup.classCode}`}>
                  <table className="w-max min-w-full border-separate border-spacing-0 text-left" style={{ minWidth: `${320 + classGroup.assessments.length * 260 + classGroup.checkpoints.length * 280 + (canViewCourseTotal ? 180 : 0)}px` }}>
                    <thead className="text-xs uppercase tracking-wider text-slate-500">
                      <tr className="bg-slate-50">
                        <th scope="col" className="sticky left-0 z-30 w-[320px] min-w-[320px] border-b border-r border-slate-200 bg-slate-50 px-5 py-3 font-bold">Team</th>
                        {classGroup.assessments.map(item => <th key={item.assessmentId} scope="col" className="w-[260px] min-w-[260px] border-b border-r border-slate-200 bg-blue-50/80 px-5 py-3 text-center font-black text-blue-700"><span className="mx-auto block max-w-[230px] whitespace-normal break-words leading-4" title={item.name}>{item.name}</span><span className="mt-1 block text-[10px] font-semibold normal-case tracking-normal text-slate-500">Other assessment · {Number(item.weight).toFixed(1)}%</span></th>)}
                        {classGroup.checkpoints.map(item => <th key={item.number} scope="col" className="w-[280px] min-w-[280px] border-b border-r border-slate-200 bg-orange-50/70 px-5 py-3 text-center font-black text-primary"><span className="block">Checkpoint {item.number}</span><span className="mx-auto mt-1 block max-w-[250px] whitespace-normal break-words text-center text-[10px] font-semibold normal-case leading-4 tracking-normal text-slate-500" title={item.title}>{item.title} · {Number(item.courseWeight || 0).toFixed(1)}%</span></th>)}
                        {canViewCourseTotal && <th scope="col" className="w-[180px] min-w-[180px] border-b border-slate-200 bg-emerald-50 px-4 py-3 text-center font-black text-emerald-700">Course total<span className="mt-0.5 block text-[10px] font-semibold normal-case tracking-normal text-slate-500">Weighted · / 10</span></th>}
                      </tr>
                    </thead>
                    {classGroup.teams.map(teamGroup => {
                      const allMembers = teamGroup.team.members || [];
                      const visibleMembers = role === 'MENTOR'
                        ? []
                        : role === 'STUDENT'
                          ? allMembers.filter(member => member.userId === currentUserId)
                          : allMembers;
                      const members = visibleMembers.length || role === 'MENTOR' || role === 'STUDENT'
                        ? visibleMembers
                        : [{ studentId: `empty-${teamGroup.team.teamId}`, fullName: 'No active members', rollNumber: '' }];
                      return (
                        <tbody key={teamGroup.team.teamId} className="group/team">
                          <tr className="bg-slate-50/80">
                            <th scope="rowgroup" className="sticky left-0 z-20 align-top border-b border-r border-t border-slate-200 bg-slate-50 px-5 py-4 text-left">
                              <p className="whitespace-normal break-words text-sm font-black leading-5 text-slate-900">{teamGroup.team.projectName || 'No project name'}</p>
                              <p className="mt-1.5 whitespace-normal break-words text-xs font-medium leading-5 text-slate-500">{teamGroup.team.projectDescription || 'No project description'}</p>
                            </th>
                            {teamGroup.assessments.map(assessment => {
                              const hasScore = assessment.score !== null && assessment.score !== undefined;
                              const isSubmitted = assessment.status === 'SUBMITTED';
                              return <td key={assessment.assessmentId} className="border-b border-r border-t border-slate-200 bg-blue-50/30 px-3 py-3 text-center align-middle"><div className="flex min-h-24 flex-col items-center justify-center">{hasScore ? <><span className="text-[9px] font-bold uppercase tracking-wider text-slate-400">Team score</span><p className="mt-0.5 text-xl font-black text-blue-700">{Number(assessment.score).toFixed(2)}</p></> : <><span className="text-xl font-black text-slate-300">—</span><span className="mt-1 text-[10px] font-bold uppercase tracking-wide text-slate-400">{isSubmitted ? 'Score pending publication' : 'Not graded'}</span></>}<span className={`mt-1 rounded-full border px-2 py-0.5 text-[9px] font-bold uppercase tracking-wide ${statusStyle[assessment.status]}`}>{isSubmitted && canEdit && assessment.evaluatorId === currentUserId ? 'Ready to publish' : evaluationStatusLabel(assessment.status)}</span>{canEdit && teamGroup.team.accessMode !== 'READ_ONLY' && assessment.evaluatorId === currentUserId && assessment.evaluationId && isSubmitted && <button type="button" disabled={bulkPublicationSubmitting || publishingEvaluationId === assessment.evaluationId} onClick={() => setPublicationConfirmation({ evaluationIds: [assessment.evaluationId!], teamCount: 1, scope: 'single', action: 'publish' })} className="mt-1.5 inline-flex items-center gap-1 text-[10px] font-bold text-emerald-700 hover:underline disabled:opacity-50">{publishingEvaluationId === assessment.evaluationId ? <Loader2 className="h-3 w-3 animate-spin" /> : <Send className="h-3 w-3" />} Publish</button>}{canEdit && teamGroup.team.accessMode !== 'READ_ONLY' && assessment.evaluatorId === currentUserId && assessment.evaluationId && assessment.status === 'PUBLISHED' && <button type="button" disabled={bulkPublicationSubmitting || publishingEvaluationId === assessment.evaluationId} onClick={() => setPublicationConfirmation({ evaluationIds: [assessment.evaluationId!], teamCount: 1, scope: 'single', action: 'unpublish' })} className="mt-1.5 inline-flex items-center gap-1 text-[10px] font-bold text-amber-700 hover:underline disabled:opacity-50">{publishingEvaluationId === assessment.evaluationId ? <Loader2 className="h-3 w-3 animate-spin" /> : <EyeOff className="h-3 w-3" />} Hide scores</button>}{canEdit && teamGroup.team.accessMode !== 'READ_ONLY' && <button type="button" onClick={() => setEditingAssessment({ team: teamGroup.team, assessment })} className="mt-1 inline-flex items-center gap-1 text-[10px] font-bold text-blue-700 hover:underline"><Edit3 className="h-3 w-3" /> {assessment.evaluationId ? 'Edit grading' : 'Start grading'}</button>}</div></td>;
                            })}
                            {classGroup.checkpoints.map(item => {
                              const record = teamGroup.recordsByCheckpoint.get(item.number);
                              return (
                                <td key={item.number} className="border-b border-r border-t border-slate-200 bg-orange-50/30 px-3 py-3 text-center align-middle">
                                  {record?.evaluation ? (
                                    <div className="flex min-h-24 flex-col items-center justify-center">
                                      {record.evaluation.checkpointTotal === null || record.evaluation.checkpointTotal === undefined ? <><span className="text-xl font-black text-slate-300">—</span><span className="mt-1 text-[10px] font-bold uppercase tracking-wide text-slate-400">Score pending publication</span></> : <><span className="text-[9px] font-bold uppercase tracking-wider text-slate-400">Team score</span><p className="mt-0.5 text-xl font-black text-primary">{Number(record.evaluation.checkpointTotal).toFixed(2)}</p></>}
                                      <span className={`mt-1 rounded-full border px-2 py-0.5 text-[9px] font-bold uppercase tracking-wide ${statusStyle[record.status]}`}>{record.status === 'SUBMITTED' && canEdit && record.evaluation.lecturerId?._id === currentUserId ? 'Ready to publish' : evaluationStatusLabel(record.status)}</span>
                                      <button type="button" onClick={() => setFeedbackRecord(record)} className="mt-1.5 text-[11px] font-bold text-secondary hover:underline">View feedback</button>
                                      {canEdit && teamGroup.team.accessMode !== 'READ_ONLY' && record.evaluation.lecturerId?._id === currentUserId && record.status === 'SUBMITTED' && <button type="button" disabled={bulkPublicationSubmitting || publishingEvaluationId === record.evaluation._id} onClick={() => setPublicationConfirmation({ evaluationIds: [record.evaluation!._id], teamCount: 1, scope: 'single', action: 'publish' })} className="mt-1 inline-flex items-center gap-1 text-[10px] font-bold text-emerald-700 hover:underline disabled:opacity-50">{publishingEvaluationId === record.evaluation._id ? <Loader2 className="h-3 w-3 animate-spin" /> : <Send className="h-3 w-3" />} Publish</button>}
                                      {canEdit && teamGroup.team.accessMode !== 'READ_ONLY' && record.evaluation.lecturerId?._id === currentUserId && record.status === 'PUBLISHED' && <button type="button" disabled={bulkPublicationSubmitting || publishingEvaluationId === record.evaluation._id} onClick={() => setPublicationConfirmation({ evaluationIds: [record.evaluation!._id], teamCount: 1, scope: 'single', action: 'unpublish' })} className="mt-1 inline-flex items-center gap-1 text-[10px] font-bold text-amber-700 hover:underline disabled:opacity-50">{publishingEvaluationId === record.evaluation._id ? <Loader2 className="h-3 w-3 animate-spin" /> : <EyeOff className="h-3 w-3" />} Hide scores</button>}
                                      {canEdit && teamGroup.team.accessMode !== 'READ_ONLY' && <button type="button" onClick={() => setEditingRecord(record)} className="mt-1 inline-flex items-center gap-1 text-[10px] font-bold text-primary hover:underline"><Edit3 className="h-3 w-3" /> Edit grading</button>}
                                    </div>
                                  ) : (
                                    <div className="flex min-h-24 flex-col items-center justify-center">
                                      <span className="text-xl font-black text-slate-300">—</span>
                                      <span className="mt-1 text-[10px] font-bold uppercase tracking-wide text-slate-400">Not graded</span>
                                      {canEdit && teamGroup.team.accessMode !== 'READ_ONLY' && record && <button type="button" onClick={() => setEditingRecord(record)} className="mt-1.5 inline-flex items-center gap-1 text-[10px] font-bold text-primary hover:underline"><Edit3 className="h-3 w-3" /> Start grading</button>}
                                    </div>
                                  )}
                                </td>
                              );
                            })}
                            {canViewCourseTotal && (() => { const total = weightedCourseScore(classGroup.checkpoints, teamGroup.recordsByCheckpoint, teamGroup.assessments); const showTotal = role !== 'STUDENT' && (total.complete || role === 'ADMIN' || role === 'LECTURER'); return <td className="border-b border-t border-slate-200 bg-emerald-50/40 px-3 py-3 text-center align-middle"><p className={`text-xl font-black tabular-nums ${total.complete ? 'text-emerald-700' : 'text-slate-500'}`}>{showTotal ? total.score.toFixed(2) : '—'}</p><span className={`mt-1 block text-[9px] font-bold uppercase tracking-wide ${role === 'STUDENT' ? 'text-slate-400' : total.complete ? 'text-emerald-600' : 'text-amber-600'}`}>{role === 'STUDENT' ? 'See my score below' : total.complete ? 'Complete' : 'Pending publication'}</span></td>; })()}
                          </tr>
                          {members.map(member => (
                            <tr key={`${teamGroup.team.teamId}-${member.studentId}`} className="hover:bg-orange-50/20">
                              <th scope="row" className="sticky left-0 z-10 border-b border-r border-slate-200 bg-white px-5 py-2.5 text-left hover:bg-orange-50/20">
                                <div className="flex items-center gap-2.5 pl-3">
                                  <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary-50 text-[10px] font-black text-primary">{initials(member.fullName)}</span>
                                  <span className="min-w-0">
                                    <span className="block max-w-[185px] truncate text-sm font-semibold text-slate-800" title={member.fullName}>{member.fullName}</span>
                                    {member.rollNumber && <span className="block text-[10px] font-medium text-slate-400">{member.rollNumber}</span>}
                                  </span>
                                </div>
                              </th>
                              {teamGroup.assessments.map(assessment => {
                                const result = courseAssessmentMemberScore(assessment, member.studentId);
                                return <td key={assessment.assessmentId} className="border-b border-r border-slate-200 bg-blue-50/10 px-3 py-2.5 text-center" title={result.isOverridden ? 'Member override score' : 'Member score'}><span className={`text-base font-bold tabular-nums ${result.score === null ? 'text-slate-300' : result.isOverridden ? 'text-amber-700' : 'text-slate-700'}`}>{result.score === null ? '—' : Number(result.score).toFixed(2)}</span>{result.isOverridden && <span className="sr-only"> (override)</span>}</td>;
                              })}
                              {classGroup.checkpoints.map(item => {
                                const memberResult = memberScore(teamGroup.recordsByCheckpoint.get(item.number), member.studentId);
                                return (
                                  <td key={item.number} className="border-b border-r border-slate-200 px-3 py-2.5 text-center" title={memberResult.isOverridden ? 'Member override score' : 'Member score'}>
                                    <span className={`text-base font-bold tabular-nums ${memberResult.score === null ? 'text-slate-300' : memberResult.isOverridden ? 'text-amber-700' : 'text-slate-700'}`}>{memberResult.score === null ? '—' : Number(memberResult.score).toFixed(2)}</span>
                                    {memberResult.isOverridden && <span className="sr-only"> (override)</span>}
                                  </td>
                                );
                              })}
                              {canViewCourseTotal && (() => { const total = weightedCourseScore(classGroup.checkpoints, teamGroup.recordsByCheckpoint, teamGroup.assessments, member.studentId); const showTotal = total.complete || role === 'ADMIN' || role === 'LECTURER'; return <td className="border-b border-slate-200 bg-emerald-50/20 px-3 py-2.5 text-center"><span className={`text-base font-black tabular-nums ${total.complete ? 'text-emerald-700' : 'text-slate-500'}`}>{showTotal ? total.score.toFixed(2) : '—'}</span></td>; })()}
                            </tr>
                          ))}
                        </tbody>
                      );
                    })}
                  </table>
                </div>
              </section>
            ))}
          </div>
        </section>
      )}

      {feedbackRecord && <FeedbackDialog record={feedbackRecord} onClose={() => setFeedbackRecord(null)} isStudent={role === 'STUDENT'} />}
      {editingAssessment && canEdit && <CourseAssessmentDialog team={editingAssessment.team} assessment={editingAssessment.assessment} onClose={() => setEditingAssessment(null)} onSaved={() => { setEditingAssessment(null); retry(); }} />}
      <ConfirmDialog
        isOpen={Boolean(publicationConfirmation)}
        onClose={() => { if (!publicationSubmitting) setPublicationConfirmation(null); }}
        onConfirm={confirmPublicationChange}
        isSubmitting={publicationSubmitting}
        title={publicationConfirmation?.scope === 'filtered'
          ? publicationConfirmation.action === 'unpublish' ? 'Hide all published scores?' : 'Publish all ready scores?'
          : publicationConfirmation?.action === 'unpublish' ? 'Hide published scores?' : 'Publish scores?'}
        description={publicationConfirmation?.scope === 'filtered'
          ? publicationConfirmation.action === 'unpublish'
            ? `Hide ${publicationConfirmation.evaluationIds.length} published evaluation${publicationConfirmation.evaluationIds.length === 1 ? '' : 's'} across ${publicationConfirmation.teamCount} team${publicationConfirmation.teamCount === 1 ? '' : 's'} in the current filters? Students and mentors will no longer see these scores until they are published again.`
            : `Publish ${publicationConfirmation.evaluationIds.length} ready evaluation${publicationConfirmation.evaluationIds.length === 1 ? '' : 's'} across ${publicationConfirmation.teamCount} team${publicationConfirmation.teamCount === 1 ? '' : 's'} in the current filters? Authorized students and mentors will see the applicable scores immediately.`
          : publicationConfirmation?.action === 'unpublish'
            ? 'Students and mentors will no longer see these scores. The evaluation will return to Ready to publish and can be published again later.'
            : 'Authorized students and mentors will be able to see these scores immediately. Do you want to continue?'}
        confirmText={publicationConfirmation?.scope === 'filtered'
          ? publicationConfirmation.action === 'unpublish' ? 'Hide all scores' : 'Publish all scores'
          : publicationConfirmation?.action === 'unpublish' ? 'Hide scores' : 'Publish scores'}
        confirmVariant={publicationConfirmation?.action === 'unpublish' ? 'danger' : 'primary'}
      />
      {editingRecord && canEdit && <div className="fixed inset-0 z-[70] flex items-center justify-center bg-slate-950/50 p-3 backdrop-blur-sm sm:p-6" role="dialog" aria-modal="true" aria-labelledby="grading-dialog-title"><div className="flex max-h-[94vh] w-full max-w-4xl flex-col overflow-hidden rounded-2xl border border-slate-200 bg-slate-50 shadow-2xl"><div className="flex items-center justify-between gap-4 border-b border-slate-200 bg-white px-5 py-4"><div className="min-w-0"><p className="text-[10px] font-bold uppercase tracking-wider text-primary">{editingRecord.team.classCode} · {editingRecord.team.teamName}</p><h2 id="grading-dialog-title" className="truncate text-lg font-black text-slate-900">Checkpoint {editingRecord.checkpoint.number}: {editingRecord.checkpoint.title}</h2></div><button type="button" onClick={() => { setEditingRecord(null); retry(); }} className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700" aria-label="Close grading dialog"><X className="h-5 w-5" /></button></div><div className="overflow-y-auto"><EvaluationPanel teamId={editingRecord.team.teamId} proposalId={undefined} pitchDeckId={undefined} checkpointNumber={editingRecord.checkpoint.number} embedded isReadOnly={editingRecord.team.accessMode === 'READ_ONLY'} /></div></div></div>}
      </>}
    </div>
  );
}
