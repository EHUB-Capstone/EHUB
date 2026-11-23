// "How the team moved since the earlier checkpoints": for each thing the rubrics ask for (roadmap, persona, market size...),
// whether the files of each checkpoint mention it. Worked out from what the files already stored (no AI, no file read), so it
// loads by itself when the AI summary card is open and exists only while AI summaries are on.
import { useCallback, useEffect, useState } from 'react';
import axios from 'axios';
import { Check, Loader2, Minus, RefreshCw, TrendingUp } from 'lucide-react';
import Badge from '../../ui/Badge';
import Button from '../../ui/Button';
import Card from '../../ui/Card';
import { checkpointSummaryApi } from '../../../api/checkpointSummaryApi';
import { parseApiError } from '../../../utils/apiError';
import { hasProgress, partiallyReadCheckpoints, progressChangeView, progressStateText } from '../../../utils/checkpointSummary';
import type { CheckpointProgress, ProgressConcept, ProgressState } from '../../../types/checkpointSummary';
import { useTranslation } from 'react-i18next';

interface Props {
  teamId: string;
  checkpointNumber: number;
}

const CELL_CLASS: Record<ProgressState, string> = {
  Found: 'border-success-light bg-success-50 text-success-dark',
  NotMentioned: 'border-slate-200 bg-slate-50 text-slate-600',
  NotAsked: 'border-slate-200 bg-white text-slate-500',
  NotRead: 'border-slate-200 bg-white text-slate-500',
};

function Cell({ number, state }: { number: number; state: ProgressState }) {
  const { t } = useTranslation('checkpoint');
  return (
    <li title={progressStateText[state]} className={`flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-caption ${CELL_CLASS[state]}`}>
      {state === 'Found' && <Check className="h-3 w-3" aria-hidden="true" />}
      {state === 'NotMentioned' && <Minus className="h-3 w-3" aria-hidden="true" />}
      {t('checkpointProgressBlock.cp')}{number}
      {(state === 'NotAsked' || state === 'NotRead') && <span aria-hidden="true">· {state === 'NotAsked' ? 'n/a' : 'unread'}</span>}
      <span className="sr-only">: {progressStateText[state]}</span>
    </li>
  );
}

function Concept({ concept }: { concept: ProgressConcept }) {
  const { t } = useTranslation('checkpoint');
  const change = progressChangeView[concept.change];
  return (
    <li className="space-y-1">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-body-sm font-medium text-slate-900">{concept.label}</span>
        {change && <Badge variant={change.tone}>{change.label}</Badge>}
      </div>
      <ul className="flex flex-wrap gap-1.5" aria-label={t('checkpointProgressBlock.byCheckpoint', { label: concept.label })}>
        {concept.cells.map((cell) => <Cell key={cell.checkpointNumber} number={cell.checkpointNumber} state={cell.state} />)}
      </ul>
    </li>
  );
}

export default function CheckpointProgressBlock({ teamId, checkpointNumber }: Props) {
  const { t } = useTranslation('checkpoint');
  const [progress, setProgress] = useState<CheckpointProgress | null>(null);
  const [loading, setLoading] = useState(checkpointNumber > 1);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);

  const load = useCallback(async (signal: AbortSignal) => {
    setLoading(true);
    setError('');
    try {
      const response = await checkpointSummaryApi.progress(teamId, checkpointNumber, signal);
      setProgress(response.data);
    } catch (caught) {
      if (axios.isCancel(caught)) return;
      setProgress(null);
      setError(parseApiError(caught, 'Could not load the progress across checkpoints.').message);
    } finally {
      if (!signal.aborted) setLoading(false);
    }
  }, [teamId, checkpointNumber]);

  // A different team or checkpoint is a different picture; leaving the screen cancels the request.
  useEffect(() => {
    if (checkpointNumber <= 1) return undefined;
    const controller = new AbortController();
    setProgress(null);
    load(controller.signal);
    return () => controller.abort();
  }, [load, checkpointNumber, attempt]);

  if (checkpointNumber <= 1) return null;
  if (loading) {
    return (
      <p className="flex items-center gap-2 text-caption text-slate-500" role="status">
        <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" /> {t('checkpointProgressBlock.loadingProgressAcrossCheckpoints')}
      </p>
    );
  }
  if (error) {
    return (
      <div className="flex flex-wrap items-center gap-3" role="alert">
        <p className="text-caption text-danger-dark">{error}</p>
        <Button variant="outline" size="sm" icon={RefreshCw} onClick={() => setAttempt((value) => value + 1)}>{t('checkpointFilePreviewModal.retry')}</Button>
      </div>
    );
  }
  // Nothing to compare (the earlier checkpoints have no files, or no rubric names a thing a file could mention).
  if (!hasProgress(progress)) return null;

  const partial = partiallyReadCheckpoints(progress);
  return (
    <Card tone="inset" padding="p-4" hover={false} className="space-y-3">
      <h4 className="flex items-center gap-1.5 text-overline font-bold uppercase text-slate-500">
        <TrendingUp className="h-3.5 w-3.5" aria-hidden="true" />
        {t('checkpointProgressBlock.progressAcrossCheckpoints')}
      </h4>
      <p className="text-caption text-slate-500">
        {t('checkpointProgressBlock.whetherTheFilesOfEachCheckpoint')}
      </p>
      <ul className="space-y-3">
        {progress.concepts.map((concept) => <Concept key={concept.label} concept={concept} />)}
      </ul>
      {partial.length > 0 && (
        <p className="text-caption text-slate-500">
          {t('checkpointProgressBlock.onlyPartOfTheFilesCould', { v1: partial.join('; ') })}
        </p>
      )}
    </Card>
  );
}
