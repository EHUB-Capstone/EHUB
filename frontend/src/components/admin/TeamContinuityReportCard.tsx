import { useCallback, useEffect, useState } from 'react';
import { History, Loader2, RefreshCw } from 'lucide-react';
import { subjectApi } from '../../api/subjectApi';
import { teamLineageApi } from '../../api/teamLineageApi';
import type { SemesterDto } from '../../types/subjects';
import type { TeamContinuityReport } from '../../types/teamLineage';
import { parseApiError } from '../../utils/apiError';

const SEMESTER_ORDER: Record<string, number> = { SP: 1, SU: 2, FA: 3 };

function sortNewestFirst(semesters: SemesterDto[]): SemesterDto[] {
  return [...semesters].sort((left, right) =>
    right.year - left.year || (SEMESTER_ORDER[right.semester] ?? 0) - (SEMESTER_ORDER[left.semester] ?? 0));
}

export default function TeamContinuityReportCard() {
  const [semesters, setSemesters] = useState<SemesterDto[]>([]);
  const [semesterId, setSemesterId] = useState('');
  const [report, setReport] = useState<TeamContinuityReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadSemesters = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await subjectApi.getSemesters();
      const loaded = sortNewestFirst((response?.data?.semesters ?? []) as SemesterDto[]);
      setSemesters(loaded);
      setSemesterId((current) => current || loaded[0]?.id || '');
      if (loaded.length === 0) setLoading(false);
    } catch (caught: unknown) {
      setError(parseApiError(caught, 'Unable to load semesters.').message);
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadSemesters();
  }, [loadSemesters]);

  const loadReport = useCallback(async (signal?: AbortSignal) => {
    if (!semesterId) return;
    setLoading(true);
    setError(null);
    try {
      const response = await teamLineageApi.getContinuityReport(semesterId, { signal });
      setReport(response.data ?? null);
    } catch (caught: unknown) {
      if ((caught as { code?: string })?.code === 'ERR_CANCELED') return;
      setError(parseApiError(caught, 'Unable to load the team continuity report.').message);
    } finally {
      setLoading(false);
    }
  }, [semesterId]);

  useEffect(() => {
    const controller = new AbortController();
    void loadReport(controller.signal);
    return () => controller.abort();
  }, [loadReport]);

  return (
    <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm" aria-labelledby="team-continuity-heading">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 p-5">
        <div>
          <h2 id="team-continuity-heading" className="flex items-center gap-2 text-lg font-bold text-slate-900">
            <History className="h-5 w-5 text-primary" aria-hidden="true" />Team Continuity
          </h2>
          <p className="text-sm text-slate-500">Teams and projects continued from the previous semester</p>
        </div>
        <label className="flex items-center gap-2 text-sm text-slate-600">
          Semester
          <select
            value={semesterId}
            onChange={(event) => setSemesterId(event.target.value)}
            disabled={semesters.length === 0}
            className="rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-sm"
          >
            {semesters.map((semester) => (
              <option key={semester.id} value={semester.id}>{semester.semester} {semester.year}</option>
            ))}
          </select>
        </label>
      </div>

      <div className="p-5">
        {loading && (
          <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading report…</p>
        )}
        {!loading && error && (
          <div className="text-sm text-red-700" role="alert">
            <p>{error}</p>
            <button type="button" onClick={() => void (semesters.length ? loadReport() : loadSemesters())} className="mt-2 flex items-center gap-1 font-semibold underline">
              <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" /> Retry
            </button>
          </div>
        )}
        {!loading && !error && semesters.length === 0 && <p className="text-sm text-slate-500">No semesters yet.</p>}
        {!loading && !error && report && (
          <div className="space-y-4">
            <dl className="grid grid-cols-3 gap-3 text-center">
              {[
                ['Teams continued', report.continuedTeamCount],
                ['Projects continued', report.continuedProjectCount],
                ['Dissolved', report.dissolvedCount],
              ].map(([label, value]) => (
                <div key={label as string} className="rounded-xl bg-slate-50 p-3">
                  <dd className="text-2xl font-bold text-slate-900">{value}</dd>
                  <dt className="mt-0.5 text-xs text-slate-500">{label}</dt>
                </div>
              ))}
            </dl>
            {report.classes.length === 0 ? (
              <p className="text-sm text-slate-500">No team was continued into {report.semesterCode}.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[420px] text-sm">
                  <thead className="text-left text-xs uppercase text-slate-400">
                    <tr><th className="py-2 pr-4">Class</th><th className="py-2 pr-4">Continued</th><th className="py-2">Dissolved</th></tr>
                  </thead>
                  <tbody>
                    {report.classes.map((item) => (
                      <tr key={item.classId} className="border-t border-slate-100">
                        <td className="py-2 pr-4 font-semibold text-slate-800">{item.classCode}</td>
                        <td className="py-2 pr-4">{item.continuedTeamCount}</td>
                        <td className="py-2">{item.dissolvedCount}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </div>
    </section>
  );
}
