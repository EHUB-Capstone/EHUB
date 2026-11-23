// Why AI summaries are slow, stuck or failing, for administrators: model server, the pause that protects the queue, the queue itself
// and why files failed. Counts and error codes only. Renders nothing while AI summaries are off on the server, so production shows no
// trace of it, and asks for nothing until the server has said they are on.
import { useCallback, useEffect, useState } from 'react';
import axios from 'axios';
import { Activity, CheckCircle2, Loader2, RefreshCw, TriangleAlert } from 'lucide-react';
import Badge from '../ui/Badge';
import Button from '../ui/Button';
import Card from '../ui/Card';
import { checkpointSummaryApi } from '../../api/checkpointSummaryApi';
import { useAiSummaryEnabled } from '../../hooks/useAiSummaryEnabled';
import { parseApiError } from '../../utils/apiError';
import {
  ageText, circuitView, diagnosticsHealth, failureLabel, hasFewVerdicts, helpfulShare, waitingFiles,
} from '../../utils/summaryDiagnostics';
import type { SummaryDiagnostics } from '../../types/checkpointSummary';
import { useTranslation } from 'react-i18next';
import { formatDateTime as fmtDateTime, formatNumber as fmtNumber } from '../../i18n/format';

const time = (value: string | null) => (value ? fmtDateTime(value) : '—');

const Stat = ({ label, value, hint }: { label: string; value: number; hint?: string }) => (
  <div className="rounded-xl border border-slate-200 px-3 py-2">
    <dt className="text-caption text-slate-500">{label}</dt>
    <dd className="text-subheading text-slate-900">{fmtNumber(value)}</dd>
    {hint && <p className="text-caption text-slate-500">{hint}</p>}
  </div>
);

export default function AiSummaryDiagnosticsCard() {
  const { t } = useTranslation('admin');
  const enabled = useAiSummaryEnabled();
  const [data, setData] = useState<SummaryDiagnostics | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!enabled) return undefined;
    const controller = new AbortController();
    setLoading(true);
    checkpointSummaryApi.diagnostics(controller.signal)
      .then((response) => { setData(response.data); setError(''); })
      .catch((caught) => {
        if (axios.isCancel(caught)) return;
        // The page never shows a raw server error; a request that is refused or too frequent has its own plain message.
        const code = parseApiError(caught, '').code;
        setError(code === 'WORKSPACE_ACCESS_DENIED' ? t('aiSummaryDiagnosticsCard.onlyAdministratorsCanSeeThis')
          : caught?.response?.status === 429 ? t('aiSummaryDiagnosticsCard.refreshedTooOftenPleaseWaitA')
          : t('aiSummaryDiagnosticsCard.couldNotLoadTheAiSummary'));
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [enabled, reloadKey]);

  const reload = useCallback(() => setReloadKey((value) => value + 1), []);
  if (!enabled) return null;

  const health = data ? diagnosticsHealth(data) : null;
  const circuit = data ? circuitView[data.circuit] : null;

  return (
    <Card hover={false} padding="p-5" className="space-y-4" aria-label={t('aiSummaryDiagnosticsCard.aiSummaryHealth')}>
      <div className="flex flex-wrap items-center gap-2">
        <Activity className="h-4 w-4 text-primary-700" aria-hidden="true" />
        <h2 className="text-subheading text-secondary">{t('aiSummaryDiagnosticsCard.aiSummaryHealth')}</h2>
        {health && <Badge variant={health.tone}>{health.label}</Badge>}
        <button
          type="button"
          onClick={reload}
          disabled={loading}
          aria-label={t('aiSummaryDiagnosticsCard.refreshAiSummaryHealth')}
          className="ml-auto inline-flex h-9 w-9 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 disabled:cursor-wait"
        >
          <RefreshCw className={loading ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} aria-hidden="true" />
        </button>
      </div>

      {error && !data ? (
        <div className="flex flex-wrap items-center gap-3" role="alert">
          <p className="text-body-sm text-danger-dark">{error}</p>
          <Button variant="outline" size="sm" icon={RefreshCw} onClick={reload}>{t('classManagement.retry')}</Button>
        </div>
      ) : !data ? (
        <p className="flex items-center gap-2 text-body-sm text-slate-500" role="status">
          <Loader2 className="h-4 w-4 animate-spin text-primary" aria-hidden="true" /> {t('common:states.loading')}
        </p>
      ) : (
        <>
          {error && <p className="text-body-sm text-danger-dark" role="alert">{t('aiSummaryDiagnosticsCard.showingTheLastResult', { error })}</p>}

          {health!.problems.length === 0 ? (
            <p className="flex items-center gap-2 text-body-sm text-slate-600">
              <CheckCircle2 className="h-4 w-4 text-success" aria-hidden="true" /> {t('aiSummaryDiagnosticsCard.theModelAnswersAndTheQueue')}
            </p>
          ) : (
            <ul className="space-y-1.5" aria-label={t('aiSummaryDiagnosticsCard.whatNeedsAttention')}>
              {health!.problems.map((problem) => (
                <li key={problem} className="flex items-start gap-2 text-body-sm text-slate-700">
                  <TriangleAlert className={`mt-0.5 h-4 w-4 shrink-0 ${health!.tone === 'danger' ? 'text-danger' : 'text-warning'}`} aria-hidden="true" />
                  {problem}
                </li>
              ))}
            </ul>
          )}

          <dl className="grid gap-x-6 gap-y-2 text-body-sm sm:grid-cols-2">
            <div className="flex items-center justify-between gap-3 border-b border-slate-100 pb-2">
              <dt className="text-slate-500">{t('aiSummaryDiagnosticsCard.modelServer')}</dt>
              <dd><Badge variant={data.modelReachable ? 'success' : 'danger'}>{data.modelReachable ? t('aiSummaryDiagnosticsCard.answering') : t('aiSummaryDiagnosticsCard.notAnswering')}</Badge></dd>
            </div>
            <div className="flex items-center justify-between gap-3 border-b border-slate-100 pb-2">
              <dt className="text-slate-500">{t('aiSummaryDiagnosticsCard.model')}</dt>
              <dd className="flex items-center gap-2 text-slate-900">
                {data.model}
                {/* With the server not answering, whether the model is there cannot be known. */}
                {data.modelReachable
                  ? <Badge variant={data.modelAvailable ? 'success' : 'danger'}>{data.modelAvailable ? t('aiSummaryDiagnosticsCard.ready') : t('aiSummaryDiagnosticsCard.notPulled')}</Badge>
                  : <Badge variant="neutral">{t('classManagement.unknown')}</Badge>}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-3 border-b border-slate-100 pb-2">
              <dt className="text-slate-500">{t('aiSummaryDiagnosticsCard.worker')}</dt>
              <dd className="flex items-center gap-2 text-slate-900">
                <Badge variant={circuit!.tone}>{circuit!.label}</Badge>
                {data.circuit === 'Open' && data.circuitOpenUntilUtc && <span className="text-caption text-slate-500">{t('aiSummaryDiagnosticsCard.until', { v1: time(data.circuitOpenUntilUtc) })}</span>}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-3 border-b border-slate-100 pb-2">
              <dt className="text-slate-500">{t('aiSummaryDiagnosticsCard.promptVersion')}</dt>
              <dd className="text-slate-900">{data.promptVersion}{data.autoSummarize ? t('aiSummaryDiagnosticsCard.autoAfterUpload') : t('aiSummaryDiagnosticsCard.manualOnly')}</dd>
            </div>
            <div className="flex items-center justify-between gap-3 border-b border-slate-100 pb-2">
              <dt className="text-slate-500">{t('aiSummaryDiagnosticsCard.lastModelFailure')}</dt>
              <dd className="text-slate-900">{time(data.lastModelFailureUtc)}</dd>
            </div>
            <div className="flex items-center justify-between gap-3 border-b border-slate-100 pb-2">
              <dt className="text-slate-500">{t('aiSummaryDiagnosticsCard.lastSummaryMade')}</dt>
              <dd className="text-slate-900">{time(data.lastGeneratedAtUtc)}</dd>
            </div>
          </dl>

          <div className="space-y-2">
            <h3 className="text-overline font-bold uppercase text-slate-500">{t('aiSummaryDiagnosticsCard.queue')}</h3>
            <dl className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
              <Stat label={t('aiSummaryDiagnosticsCard.waitingRequested')} value={data.pendingRequested} hint={`limit ${data.maxRequestedQueue}`} />
              <Stat label={t('aiSummaryDiagnosticsCard.waitingAutomatic')} value={data.pendingAutomatic} hint={`limit ${data.maxAutomaticQueue}`} />
              <Stat label={t('aiSummaryDiagnosticsCard.processing')} value={data.processing} />
              <Stat label={t('aiSummaryDiagnosticsCard.ready')} value={data.ready} />
              <Stat label={t('classManagement.failed')} value={data.failed} />
              <Stat label={t('aiSummaryDiagnosticsCard.cancelled')} value={data.cancelled} />
            </dl>
            <p className="text-caption text-slate-500">
              {waitingFiles(data) === 0 ? t('aiSummaryDiagnosticsCard.nothingIsWaiting') : t('aiSummaryDiagnosticsCard.theOldestOfWaitingFileS', { v1: waitingFiles(data), v2: ageText(data.oldestPendingSeconds) })}
            </p>
          </div>

          {data.failuresByCode.length > 0 && (
            <div className="space-y-2">
              <h3 className="text-overline font-bold uppercase text-slate-500">{t('aiSummaryDiagnosticsCard.whyFilesFailed')}</h3>
              <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200">
                {data.failuresByCode.map((item) => (
                  <li key={item.errorCode} className="flex items-center justify-between gap-3 px-3 py-2 text-body-sm">
                    <span className="min-w-0 text-slate-700">{failureLabel(item.errorCode)}</span>
                    <Badge variant="neutral">{item.count}</Badge>
                  </li>
                ))}
              </ul>
            </div>
          )}

          <div className="space-y-2">
            <h3 className="text-overline font-bold uppercase text-slate-500">{t('aiSummaryDiagnosticsCard.whatReviewersSaidOfTheSummaries')}</h3>
            {data.feedbackByPromptVersion.length === 0 ? (
              <p className="text-body-sm text-slate-500">{t('aiSummaryDiagnosticsCard.nobodyHasRatedASummaryYet')}</p>
            ) : (
              <div className="overflow-x-auto rounded-xl border border-slate-200">
                <table className="w-full min-w-[360px] text-body-sm">
                  <thead className="bg-slate-50 text-left text-caption text-slate-600">
                    <tr><th className="px-3 py-2">{t('aiSummaryDiagnosticsCard.promptVersion')}</th><th className="px-3 py-2">{t('aiSummaryDiagnosticsCard.useful')}</th><th className="px-3 py-2">{t('aiSummaryDiagnosticsCard.notUseful')}</th><th className="px-3 py-2">{t('aiSummaryDiagnosticsCard.shareUseful')}</th></tr>
                  </thead>
                  <tbody>
                    {data.feedbackByPromptVersion.map((row) => {
                      const share = helpfulShare(row);
                      return (
                        <tr key={row.promptVersion} className="border-t border-slate-100">
                          <td className="px-3 py-2 font-medium text-slate-900">{row.promptVersion}</td>
                          <td className="px-3 py-2">{row.helpful}</td>
                          <td className="px-3 py-2">{row.notHelpful}</td>
                          <td className="px-3 py-2">{share === null ? '—' : `${share}%`}{share !== null && hasFewVerdicts(row) && <span className="ml-2 text-caption text-slate-500">{t('aiSummaryDiagnosticsCard.tooFewRatingsToJudge')}</span>}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          <p className="text-caption text-slate-500">
            {t('aiSummaryDiagnosticsCard.theWorkerSPauseIsRemembered')}
          </p>
        </>
      )}
    </Card>
  );
}
