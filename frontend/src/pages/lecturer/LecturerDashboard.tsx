import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { motion } from 'framer-motion';
import { useNavigate } from 'react-router-dom';
import type { LucideIcon } from 'lucide-react';
import {
  AlertTriangle,
  ArrowRight,
  BadgeCheck,
  BarChart3,
  ClipboardCheck,
  Clock3,
  FolderKanban,
  GraduationCap,
  RefreshCw,
  RotateCcw,
  Rocket,
  Sparkles,
  Star,
  Users,
} from 'lucide-react';
import {
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
} from 'recharts';
import { dashboardApi } from '../../api/dashboardApi';
import EmptyState from '../../components/ui/EmptyState';
import ErrorState from '../../components/ui/ErrorState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import Button from '../../components/ui/Button';
import type {
  AcademicOverviewFilterOptions,
  AcademicOverviewFilters,
  AcademicOverviewResponse,
} from '../../types/dashboard';
import {
  academicOverviewMetricEntries,
  percentage,
  resolveAcademicOverviewState,
  ACADEMIC_OVERVIEW_ERROR_FALLBACK,
  resolveOverviewErrorMessage,
  semesterScopeNotice,
} from '../../utils/academicDashboard';

interface MetricPresentation {
  icon: LucideIcon;
  description: string;
  iconClass: string;
  accentClass: string;
}

const metricPresentation: Record<string, MetricPresentation> = {
  classes: {
    icon: GraduationCap,
    description: 'Assigned academic scope',
    iconClass: 'bg-blue-50 text-blue-600',
    accentClass: 'from-blue-500 to-cyan-400',
  },
  teams: {
    icon: Users,
    description: 'Active teams',
    iconClass: 'bg-cyan-50 text-cyan-600',
    accentClass: 'from-cyan-500 to-teal-400',
  },
  projects: {
    icon: FolderKanban,
    description: 'Non-archived projects',
    iconClass: 'bg-indigo-50 text-indigo-600',
    accentClass: 'from-indigo-500 to-violet-400',
  },
  submissions: {
    icon: ClipboardCheck,
    description: 'Unique team–checkpoints',
    iconClass: 'bg-emerald-50 text-emerald-600',
    accentClass: 'from-emerald-500 to-green-400',
  },
  evaluations: {
    icon: BadgeCheck,
    description: 'Finalized evaluations',
    iconClass: 'bg-violet-50 text-violet-600',
    accentClass: 'from-violet-500 to-fuchsia-400',
  },
  'potential-projects': {
    icon: Star,
    description: 'High-potential projects',
    iconClass: 'bg-amber-50 text-amber-600',
    accentClass: 'from-amber-500 to-orange-400',
  },
};

const number = (value: number): string => value.toLocaleString();

function MetricCard({
  title,
  value,
  presentation,
  delay,
}: {
  title: string;
  value: number;
  presentation: MetricPresentation;
  delay: number;
}) {
  const Icon = presentation.icon;
  return (
    <motion.article
      initial={{ opacity: 0, y: 16 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.35, delay }}
      className="group relative overflow-hidden rounded-2xl border border-slate-200/70 bg-white p-5 shadow-card transition-all duration-300 hover:-translate-y-0.5 hover:shadow-elevated"
    >
      <div className={`absolute inset-x-0 top-0 h-1 bg-gradient-to-r ${presentation.accentClass}`} />
      <div className="mb-5 flex items-start justify-between gap-3">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.12em] text-slate-400">{title}</p>
          <p className="mt-1 text-xs text-slate-500">{presentation.description}</p>
        </div>
        <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl transition-transform group-hover:scale-110 ${presentation.iconClass}`}>
          <Icon className="h-5 w-5" />
        </span>
      </div>
      <p className="text-3xl font-bold tracking-tight text-slate-900">{number(value)}</p>
    </motion.article>
  );
}

const LecturerDashboard = () => {
  const navigate = useNavigate();
  const [overview, setOverview] = useState<AcademicOverviewResponse | null>(null);
  const [filters, setFilters] = useState<AcademicOverviewFilters>({});
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [updating, setUpdating] = useState(false);
  const [error, setError] = useState(false);
  const [errorMessage, setErrorMessage] = useState(ACADEMIC_OVERVIEW_ERROR_FALLBACK);
  const hasOverviewRef = useRef(false);

  const loadOverview = useCallback(async (
    selectedFilters: AcademicOverviewFilters,
    signal?: AbortSignal,
    isRefresh = false,
  ) => {
    if (isRefresh) setRefreshing(true);
    else if (hasOverviewRef.current) setUpdating(true);
    else setLoading(true);
    setError(false);

    try {
      const response = await dashboardApi.getAcademicOverview({ signal, filters: selectedFilters });
      if (!response.success || !response.data) throw new Error(response.message);
      hasOverviewRef.current = true;
      setOverview(response.data);
    } catch (loadError) {
      if (!signal?.aborted) {
        setErrorMessage(resolveOverviewErrorMessage(loadError));
        setError(true);
      }
    } finally {
      if (!signal?.aborted) {
        setLoading(false);
        setRefreshing(false);
        setUpdating(false);
      }
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void loadOverview(filters, controller.signal);
    return () => controller.abort();
  }, [filters, loadOverview]);

  const portfolioData = useMemo(() => {
    if (!overview) return [];
    const potential = overview.metrics.totalPotentialProjects;
    return [
      { name: 'Potential', value: potential, color: '#f59e0b' },
      { name: 'Other projects', value: Math.max(0, overview.metrics.totalProjects - potential), color: '#dbeafe' },
    ];
  }, [overview]);

  const viewState = resolveAcademicOverviewState({
    loading,
    hasError: error,
    hasAssignedClasses: overview?.hasAssignedClasses,
    hasMatchingClasses: overview?.hasMatchingClasses,
  });

  if (viewState === 'loading') {
    return (
      <div className="space-y-6">
        <div className="rounded-3xl bg-slate-100 p-7"><LoadingSkeleton lines={2} /></div>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 6 }).map((_, index) => (
            <LoadingSkeleton key={index} variant="card" />
          ))}
        </div>
        <div className="grid gap-5 lg:grid-cols-3">
          <LoadingSkeleton variant="card" className="lg:col-span-2 h-72" />
          <LoadingSkeleton variant="card" className="h-72" />
        </div>
      </div>
    );
  }

  if (viewState === 'error') {
    return (
      <ErrorState
        title="Academic overview unavailable"
        message={errorMessage}
        onRetry={() => void loadOverview(filters)}
      />
    );
  }

  const semesterLabel = overview?.scope.semesterName || overview?.scope.semesterCode || 'Active semester';

  if (!overview) return null;

  const handleSemesterChange = (semesterId: string) => {
    setFilters({ semesterId });
  };
  const handleSubjectChange = (courseId: string) => {
    setFilters((current) => ({
      semesterId: current.semesterId ?? overview.scope.semesterId,
      ...(courseId ? { courseId } : {}),
    }));
  };
  const handleClassChange = (classId: string) => {
    setFilters((current) => ({
      semesterId: current.semesterId ?? overview.scope.semesterId,
      ...(current.courseId ? { courseId: current.courseId } : {}),
      ...(classId ? { classId } : {}),
    }));
  };
  const resetFilters = () => setFilters({});

  if (viewState === 'unassigned' || viewState === 'filtered-empty') {
    const filteredEmpty = viewState === 'filtered-empty';
    return (
      <div className="space-y-6">
        <DashboardHero
          semesterLabel={semesterLabel}
          notice={semesterScopeNotice(overview.scope)}
          refreshing={refreshing}
          onRefresh={() => void loadOverview(filters, undefined, true)}
        />
        <DashboardFilters
          options={overview.filterOptions}
          selectedSemesterId={filters.semesterId ?? overview.scope.semesterId}
          selectedCourseId={filters.courseId ?? overview.scope.courseId ?? ''}
          selectedClassId={filters.classId ?? overview.scope.classId ?? ''}
          busy={updating || refreshing}
          canReset={Object.keys(filters).length > 0}
          onSemesterChange={handleSemesterChange}
          onSubjectChange={handleSubjectChange}
          onClassChange={handleClassChange}
          onReset={resetFilters}
        />
        <div className="rounded-2xl border border-slate-200/60 bg-white shadow-sm">
          <EmptyState
            icon={GraduationCap}
            title={filteredEmpty ? 'No classes match these filters' : 'No classes assigned'}
            description={filteredEmpty
              ? 'Try another semester, subject, or class to view academic overview data.'
              : 'You do not have any non-archived classes assigned yet.'}
            action={filteredEmpty
              ? { label: 'Reset Filters', onClick: resetFilters }
              : { label: 'View My Classes', onClick: () => navigate('/lecturer/classes') }}
          />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 pb-6">
      <DashboardHero
        semesterLabel={semesterLabel}
        notice={semesterScopeNotice(overview.scope)}
        refreshing={refreshing}
        onRefresh={() => void loadOverview(filters, undefined, true)}
      />

      <DashboardFilters
        options={overview.filterOptions}
        selectedSemesterId={filters.semesterId ?? overview.scope.semesterId}
        selectedCourseId={filters.courseId ?? overview.scope.courseId ?? ''}
        selectedClassId={filters.classId ?? overview.scope.classId ?? ''}
        busy={updating || refreshing}
        canReset={Object.keys(filters).length > 0}
        onSemesterChange={handleSemesterChange}
        onSubjectChange={handleSubjectChange}
        onClassChange={handleClassChange}
        onReset={resetFilters}
      />

      <div className={`space-y-6 ${updating ? 'pointer-events-none opacity-50 transition-opacity' : 'transition-opacity'}`} aria-busy={updating}>
      <section aria-label="Academic overview metrics" className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {academicOverviewMetricEntries(overview.metrics).map((metric, index) => (
          <MetricCard
            key={metric.key}
            title={metric.title}
            value={metric.value}
            presentation={metricPresentation[metric.key]}
            delay={index * 0.04}
          />
        ))}
      </section>

      <section className="grid gap-5 lg:grid-cols-3">
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm lg:col-span-2">
          <div className="mb-5">
            <h2 className="flex items-center gap-2 text-lg font-bold text-slate-900">
              <BarChart3 className="h-5 w-5 text-primary" /> Checkpoint Progress
            </h2>
            <p className="mt-1 text-sm text-slate-500">Progress for the nearest scheduled checkpoint</p>
          </div>
          {overview.checkpointProgress.length === 0 ? (
            <EmptyState icon={ClipboardCheck} title="No checkpoint deadline configured" description="Checkpoint progress will appear after a deadline is set for a checkpoint." size="sm" />
          ) : (
            <div className="space-y-4">
              {overview.checkpointProgress.map((checkpoint) => (
                <div key={checkpoint.checkpointId} className="rounded-xl border border-slate-100 bg-slate-50/60 p-4">
                  <div className="mb-3 flex flex-wrap items-start justify-between gap-2">
                    <div>
                      <p className="font-semibold text-slate-900">{checkpoint.courseCode} · CP{checkpoint.checkpointNumber}</p>
                      <p className="text-sm text-slate-500">{checkpoint.title}</p>
                    </div>
                    {checkpoint.missedDeadlineTeams > 0 && (
                      <span className="rounded-full bg-red-50 px-2.5 py-1 text-xs font-bold text-red-600">
                        {checkpoint.missedDeadlineTeams} {checkpoint.missedDeadlineTeams === 1 ? 'team' : 'teams'} missed deadline
                      </span>
                    )}
                  </div>
                  <ProgressRow label="Submitted" value={checkpoint.submittedTeams} total={checkpoint.expectedTeams} color="bg-emerald-500" />
                  <ProgressRow label="Evaluated" value={checkpoint.evaluatedProjects} total={checkpoint.expectedTeams} color="bg-violet-500" />
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="space-y-5">
          <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
            <div className="border-b border-slate-100 bg-gradient-to-r from-amber-50 to-red-50 p-5">
              <h2 className="flex items-center gap-2 text-lg font-bold text-slate-900">
                <AlertTriangle className="h-5 w-5 text-amber-500" /> Attention Required
              </h2>
              <p className="mt-1 text-sm text-slate-500">Items that may need lecturer action</p>
            </div>
            <div className="divide-y divide-slate-100 p-5">
              <AttentionItem
                icon={Clock3}
                label="Missed deadlines"
                value={overview.attention.missedDeadlines}
                tone="red"
              />
              <AttentionItem
                icon={BadgeCheck}
                label="Awaiting evaluation"
                value={overview.attention.pendingEvaluations}
                tone="amber"
              />
            </div>
          </div>
          <div className="relative overflow-hidden rounded-2xl border border-amber-100 bg-gradient-to-br from-white via-amber-50/40 to-orange-50 p-5 shadow-sm">
            <div className="absolute -right-8 -top-8 h-28 w-28 rounded-full bg-amber-200/30 blur-2xl" />
            <h2 className="flex items-center gap-2 text-lg font-bold text-slate-900">
              <Sparkles className="h-5 w-5 text-amber-500" /> Project Portfolio
            </h2>
            <p className="mt-1 text-sm text-slate-500">High-potential projects in your scope</p>
            {overview.metrics.totalProjects > 0 ? (
              <div className="relative mt-3">
                <ResponsiveContainer width="100%" height={190}>
                  <PieChart>
                    <Pie data={portfolioData} dataKey="value" innerRadius={55} outerRadius={78} paddingAngle={3} stroke="none">
                      {portfolioData.map((item) => <Cell key={item.name} fill={item.color} />)}
                    </Pie>
                    <Tooltip />
                  </PieChart>
                </ResponsiveContainer>
                <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center pt-1">
                  <span className="text-3xl font-bold text-slate-900">{number(overview.metrics.totalPotentialProjects)}</span>
                  <span className="text-xs font-medium text-slate-500">Potential</span>
                </div>
              </div>
            ) : (
              <EmptyState icon={Rocket} title="No projects yet" description="Projects will appear once teams create them." size="sm" />
            )}
            {overview.metrics.totalProjects > 0 && (
              <div className="flex items-center justify-center gap-4 text-xs text-slate-600">
                {portfolioData.map((item) => (
                  <span key={item.name} className="flex items-center gap-1.5">
                    <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: item.color }} />
                    {item.name}: {item.value}
                  </span>
                ))}
              </div>
            )}
          </div>
        </div>
      </section>

      <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-100 p-5">
          <h2 className="flex items-center gap-2 text-lg font-bold text-slate-900">
            <GraduationCap className="h-5 w-5 text-primary" /> Class Breakdown
          </h2>
          <p className="mt-1 text-sm text-slate-500">Academic activity by assigned class</p>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-400">
              <tr>
                {['Class', 'Teams', 'Projects', 'Submissions', 'Evaluations', 'Potential'].map((heading) => (
                  <th key={heading} className="px-5 py-3 font-semibold">{heading}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {overview.classes.map((item) => (
                <tr key={item.classId} className="border-t border-slate-100 transition-colors hover:bg-slate-50/70">
                  <td className="px-5 py-3.5 font-semibold text-slate-900">{item.classCode}</td>
                  <td className="px-5 py-3.5 text-slate-600">{number(item.teams)}</td>
                  <td className="px-5 py-3.5 text-slate-600">{number(item.projects)}</td>
                  <td className="px-5 py-3.5 text-emerald-700">{number(item.submissions)}</td>
                  <td className="px-5 py-3.5 text-violet-700">{number(item.evaluations)}</td>
                  <td className="px-5 py-3.5">
                    <span className="inline-flex items-center gap-1 rounded-lg bg-amber-50 px-2 py-1 font-semibold text-amber-700">
                      <Star className="h-3.5 w-3.5" /> {number(item.potentialProjects)}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 p-5">
          <div>
            <h2 className="flex items-center gap-2 text-lg font-bold text-slate-900">
              <Star className="h-5 w-5 text-amber-500" /> Top Performing Teams
            </h2>
            <p className="mt-1 text-sm text-slate-500">Top five teams by current course total</p>
          </div>
          <Button variant="ghost-primary" size="sm" iconRight={ArrowRight} onClick={() => navigate('/evaluation-grading?tab=rankings')}>
            View Rankings
          </Button>
        </div>
        {overview.topTeams.length === 0 ? (
          <EmptyState icon={Star} title="No evaluated teams yet" description="Team rankings will appear after evaluations are finalized." size="sm" />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[700px] text-sm">
              <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-400">
                <tr>
                  {['Rank', 'Team', 'Class', 'Project', 'Progress', 'Course Total'].map((heading) => (
                    <th key={heading} className="px-5 py-3 font-semibold">{heading}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {overview.topTeams.map((team, index) => (
                  <tr key={team.teamId} className="cursor-pointer border-t border-slate-100 transition-colors hover:bg-primary-50/30" onClick={() => navigate(`/workspace/teams/${team.teamId}`)}>
                    <td className="px-5 py-3.5">
                      <span className={`inline-flex h-7 w-7 items-center justify-center rounded-lg font-bold ${index === 0 ? 'bg-amber-100 text-amber-700' : index === 1 ? 'bg-slate-200 text-slate-700' : index === 2 ? 'bg-orange-100 text-orange-700' : 'text-slate-500'}`}>
                        {index + 1}
                      </span>
                    </td>
                    <td className="px-5 py-3.5 font-semibold text-slate-900">{team.teamName}</td>
                    <td className="px-5 py-3.5 text-slate-600">{team.classCode}</td>
                    <td className="px-5 py-3.5 text-slate-600">{team.projectName || '—'}</td>
                    <td className="px-5 py-3.5 text-slate-600">{team.completedComponents}/{team.totalComponents}</td>
                    <td className="px-5 py-3.5">
                      <span className="rounded-lg bg-primary-50 px-2.5 py-1 font-bold text-primary">{team.courseTotal.toFixed(2)}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      </div>
    </div>
  );
};

function DashboardFilters({
  options,
  selectedSemesterId,
  selectedCourseId,
  selectedClassId,
  busy,
  canReset,
  onSemesterChange,
  onSubjectChange,
  onClassChange,
  onReset,
}: {
  options: AcademicOverviewFilterOptions;
  selectedSemesterId: string;
  selectedCourseId: string;
  selectedClassId: string;
  busy: boolean;
  canReset: boolean;
  onSemesterChange: (semesterId: string) => void;
  onSubjectChange: (courseId: string) => void;
  onClassChange: (classId: string) => void;
  onReset: () => void;
}) {
  const selectClassName = 'w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm text-slate-700 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400';

  return (
    <section aria-label="Dashboard filters" className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="grid gap-4 md:grid-cols-3 lg:grid-cols-[1fr_1fr_1fr_auto] lg:items-end">
        <label className="space-y-1.5 text-sm font-semibold text-slate-700">
          <span>Semester</span>
          <select
            value={selectedSemesterId}
            onChange={(event) => onSemesterChange(event.target.value)}
            disabled={busy || options.semesters.length === 0}
            className={selectClassName}
          >
            {options.semesters.map((semester) => (
              <option key={semester.id} value={semester.id}>
                {semester.name || semester.code}{semester.isActive ? ' (Active)' : ''}
              </option>
            ))}
          </select>
        </label>
        <label className="space-y-1.5 text-sm font-semibold text-slate-700">
          <span>Subject</span>
          <select
            value={selectedCourseId}
            onChange={(event) => onSubjectChange(event.target.value)}
            disabled={busy || options.subjects.length === 0}
            className={selectClassName}
          >
            <option value="">All Subjects</option>
            {options.subjects.map((subject) => (
              <option key={subject.id} value={subject.id}>{subject.code} · {subject.name}</option>
            ))}
          </select>
        </label>
        <label className="space-y-1.5 text-sm font-semibold text-slate-700">
          <span>Class</span>
          <select
            value={selectedClassId}
            onChange={(event) => onClassChange(event.target.value)}
            disabled={busy || options.classes.length === 0}
            className={selectClassName}
          >
            <option value="">All Classes</option>
            {options.classes.map((item) => (
              <option key={item.id} value={item.id}>{item.code}</option>
            ))}
          </select>
        </label>
        <Button
          variant="outline"
          icon={RotateCcw}
          disabled={!canReset || busy}
          onClick={onReset}
          className="w-full border-slate-200 text-slate-700 lg:w-auto"
        >
          Reset
        </Button>
      </div>
      {busy && <p className="mt-3 text-xs font-medium text-primary" role="status">Updating dashboard…</p>}
    </section>
  );
}

function DashboardHero({
  semesterLabel,
  notice,
  refreshing,
  onRefresh,
}: {
  semesterLabel: string;
  notice?: string | null;
  refreshing: boolean;
  onRefresh: () => void;
}) {
  return (
    <header
      className="overflow-hidden rounded-2xl border border-orange-100 bg-gradient-to-r from-orange-50 via-white to-amber-50/70 p-5 shadow-sm sm:min-h-[132px] sm:p-6"
    >
      <div className="flex min-h-[84px] flex-col justify-between gap-5 sm:flex-row sm:items-center">
        <div className="flex items-start gap-4">
          <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-primary to-orange-500 text-white shadow-md shadow-orange-200/60">
            <GraduationCap className="h-6 w-6" />
          </span>
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-black tracking-tight text-slate-900">Academic Overview</h1>
              <span className="rounded-full border border-primary-100 bg-primary-50 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wider text-primary">
                {semesterLabel}
              </span>
            </div>
            <p className="mt-1 max-w-2xl text-sm leading-relaxed text-slate-500">Track academic delivery, checkpoint progress, and promising projects across your assigned classes.</p>
            {notice && <p role="status" className="mt-2 inline-block rounded-lg bg-amber-50 px-2.5 py-1 text-xs font-medium text-amber-800">{notice}</p>}
          </div>
        </div>
        <Button
          variant="outline"
          icon={RefreshCw}
          isLoading={refreshing}
          onClick={onRefresh}
          className="shrink-0 border-slate-200 bg-white text-slate-700 shadow-sm hover:border-slate-300 hover:bg-slate-50"
        >
          Refresh
        </Button>
      </div>
    </header>
  );
}

function ProgressRow({ label, value, total, color }: { label: string; value: number; total: number; color: string }) {
  const progress = percentage(value, total);
  return (
    <div className="mt-3">
      <div className="mb-1.5 flex items-center justify-between text-xs">
        <span className="font-medium text-slate-600">{label}</span>
        <span className="font-semibold text-slate-700">{value}/{total} · {progress}%</span>
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-slate-200">
        <div className={`h-full rounded-full transition-all duration-500 ${color}`} style={{ width: `${progress}%` }} />
      </div>
    </div>
  );
}

function AttentionItem({
  icon: Icon,
  label,
  value,
  tone,
}: {
  icon: LucideIcon;
  label: string;
  value: number;
  tone: 'red' | 'amber';
}) {
  const style = tone === 'red'
    ? 'bg-red-50 text-red-600'
    : 'bg-amber-50 text-amber-600';
  return (
    <div className="flex items-center gap-3 py-4 first:pt-0 last:pb-0">
      <span className={`flex h-10 w-10 items-center justify-center rounded-xl ${style}`}><Icon className="h-5 w-5" /></span>
      <span className="flex-1 text-sm font-medium text-slate-600">{label}</span>
      <span className="text-2xl font-bold text-slate-900">{number(value)}</span>
    </div>
  );
}

export default LecturerDashboard;
