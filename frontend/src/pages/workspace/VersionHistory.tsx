import { useCallback, useEffect, useState } from 'react';
import { Calendar, Eye, History, Loader2, RotateCcw, User } from 'lucide-react';
import toast from 'react-hot-toast';
import { workspaceApi } from '../../api/workspaceApi';
import Modal from '../../components/ui/Modal';
import type { ProjectProposal, ProjectProposalContent, ProjectProposalVersion, ProjectProposalVersionSummary } from '../../types/projectProposal';
import { parseApiError } from '../../utils/apiError';
import { unwrapApiData } from '../../utils/classMappers';
import { projectProposalFields } from '../../utils/projectProposal';

type Props = {
  proposal: ProjectProposal;
  canRestore: boolean;
  onProposalChanged: (proposal: ProjectProposal) => void;
};

type DiffPart = { value: string; type: 'equal' | 'added' | 'removed' };

function DiffText({ oldText = '', newText = '' }: { oldText?: string; newText?: string }) {
  if (oldText === newText) return <span className="whitespace-pre-wrap">{newText || '—'}</span>;
  const oldWords = oldText.split(/(\s+)/);
  const newWords = newText.split(/(\s+)/);
  if (oldWords.length * newWords.length > 40_000) {
    return <span className="space-y-2"><del className="block whitespace-pre-wrap rounded bg-red-50 p-2 text-red-800">{oldText || '—'}</del><ins className="block whitespace-pre-wrap rounded bg-emerald-50 p-2 text-emerald-900 no-underline">{newText || '—'}</ins></span>;
  }
  const table = Array.from({ length: oldWords.length + 1 }, () => Array<number>(newWords.length + 1).fill(0));
  for (let i = 1; i <= oldWords.length; i += 1) {
    for (let j = 1; j <= newWords.length; j += 1) {
      table[i][j] = oldWords[i - 1] === newWords[j - 1]
        ? table[i - 1][j - 1] + 1
        : Math.max(table[i - 1][j], table[i][j - 1]);
    }
  }
  const result: DiffPart[] = [];
  let i = oldWords.length;
  let j = newWords.length;
  while (i > 0 || j > 0) {
    if (i > 0 && j > 0 && oldWords[i - 1] === newWords[j - 1]) {
      result.push({ value: oldWords[i - 1], type: 'equal' }); i -= 1; j -= 1;
    } else if (j > 0 && (i === 0 || table[i][j - 1] >= table[i - 1][j])) {
      result.push({ value: newWords[j - 1], type: 'added' }); j -= 1;
    } else {
      result.push({ value: oldWords[i - 1], type: 'removed' }); i -= 1;
    }
  }
  return (
    <span className="whitespace-pre-wrap">
      {result.reverse().map((part, index) => part.type === 'added'
        ? <ins key={index} className="rounded bg-emerald-100 px-0.5 text-emerald-900 no-underline">{part.value}</ins>
        : part.type === 'removed'
          ? <del key={index} className="rounded bg-red-100 px-0.5 text-red-800">{part.value}</del>
          : <span key={index}>{part.value}</span>)}
    </span>
  );
}

export default function VersionHistory({ proposal, canRestore, onProposalChanged }: Props) {
  const [versions, setVersions] = useState<ProjectProposalVersionSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [selected, setSelected] = useState<ProjectProposalVersion | null>(null);
  const [previous, setPrevious] = useState<ProjectProposalVersion | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [compareMode, setCompareMode] = useState(false);
  const [restoring, setRestoring] = useState(false);

  const loadVersions = useCallback(async () => {
    setLoading(true);
    setLoadError('');
    try {
      const response = await workspaceApi.getProposalVersions(proposal.id);
      setVersions(unwrapApiData<ProjectProposalVersionSummary[]>(response) || []);
    } catch (error) {
      setLoadError(parseApiError(error, 'Unable to load proposal version history.').message);
    } finally {
      setLoading(false);
    }
  }, [proposal.id]);

  useEffect(() => {
    void proposal.rowVersion;
    void loadVersions();
  }, [loadVersions, proposal.rowVersion]);

  const openVersion = async (summary: ProjectProposalVersionSummary) => {
    setDetailLoading(true);
    setCompareMode(false);
    try {
      const selectedIndex = versions.findIndex((item) => item.id === summary.id);
      const previousSummary = selectedIndex >= 0 ? versions[selectedIndex + 1] : undefined;
      const [selectedResponse, previousResponse] = await Promise.all([
        workspaceApi.getProposalVersion(proposal.id, summary.id),
        previousSummary ? workspaceApi.getProposalVersion(proposal.id, previousSummary.id) : Promise.resolve(null),
      ]);
      setSelected(unwrapApiData<ProjectProposalVersion>(selectedResponse));
      setPrevious(previousResponse ? unwrapApiData<ProjectProposalVersion>(previousResponse) : null);
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to load this proposal version.').message);
    } finally {
      setDetailLoading(false);
    }
  };

  const restore = async (version: ProjectProposalVersion | ProjectProposalVersionSummary) => {
    if (!canRestore || restoring) return;
    if (!window.confirm(`Restore version ${version.versionNumber} as a new draft version?`)) return;
    setRestoring(true);
    try {
      const response = await workspaceApi.restoreProposalVersion(proposal.id, version.id, {
        rowVersion: proposal.rowVersion,
        changeNote: `Restored version ${version.versionNumber}.`,
      });
      const updated = unwrapApiData<ProjectProposal>(response);
      if (!updated) throw new Error('The server did not return the restored proposal.');
      setSelected(null);
      onProposalChanged(updated);
      toast.success(`Version ${version.versionNumber} restored as a new draft.`);
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to restore this version. Refresh and try again.').message);
    } finally {
      setRestoring(false);
    }
  };

  return (
    <section className="rounded-2xl border border-slate-200/60 bg-white p-5 shadow-sm">
      <div className="flex items-center justify-between border-b border-slate-100 pb-3">
        <h2 className="flex items-center gap-2 font-bold text-slate-800"><History className="h-4 w-4 text-primary" /> Version history</h2>
        <span className="rounded bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-500">{versions.length} saves</span>
      </div>

      {loading ? (
        <div className="flex items-center justify-center gap-2 py-8 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" /> Loading versions…</div>
      ) : loadError ? (
        <div className="py-6 text-center"><p className="text-sm text-red-600">{loadError}</p><button type="button" onClick={() => void loadVersions()} className="mt-2 text-sm font-semibold text-primary hover:underline">Retry</button></div>
      ) : versions.length === 0 ? (
        <p className="py-6 text-center text-sm italic text-slate-400">No saved version yet.</p>
      ) : (
        <div className="mt-3 max-h-[360px] space-y-2 overflow-y-auto pr-1">
          {versions.map((version) => (
            <div key={version.id} className="flex items-center justify-between rounded-xl border border-slate-100 bg-slate-50/50 p-3">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2"><span className="text-xs font-bold text-primary">v{version.versionNumber}</span><span className="rounded bg-white px-1.5 py-0.5 text-[10px] font-semibold text-slate-500">{version.purpose === 'Submission' ? 'Submission' : 'Draft save'}</span></div>
                <p className="mt-1 truncate text-xs font-medium text-slate-700" title={version.changeNote}>{version.changeNote || 'No change note'}</p>
                <div className="mt-1 flex flex-wrap gap-3 text-[10px] text-slate-400"><span className="flex items-center gap-1"><Calendar className="h-3 w-3" />{new Date(version.createdAtUtc).toLocaleString()}</span><span className="flex items-center gap-1"><User className="h-3 w-3" />{version.changedByUserId}</span></div>
              </div>
              <div className="ml-2 flex shrink-0 gap-1">
                <button type="button" onClick={() => void openVersion(version)} disabled={detailLoading} aria-label={`View version ${version.versionNumber}`} className="rounded-lg border border-slate-200 bg-white p-2 text-slate-500 hover:text-primary disabled:opacity-50"><Eye className="h-3.5 w-3.5" /></button>
                {canRestore && <button type="button" onClick={() => void restore(version)} disabled={restoring} aria-label={`Restore version ${version.versionNumber}`} className="rounded-lg border border-slate-200 bg-white p-2 text-slate-500 hover:text-primary disabled:opacity-50"><RotateCcw className="h-3.5 w-3.5" /></button>}
              </div>
            </div>
          ))}
        </div>
      )}

      <Modal isOpen={selected !== null} onClose={() => setSelected(null)} title={selected ? `Proposal snapshot v${selected.versionNumber}` : ''} size="xl">
        {selected && (
          <div className="space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 pb-4 text-xs text-slate-500">
              <div><p>{new Date(selected.createdAtUtc).toLocaleString()}</p><p className="mt-1">{selected.changeNote || 'No change note'}</p></div>
              {previous && <button type="button" onClick={() => setCompareMode((value) => !value)} className={`rounded-xl border px-3 py-2 font-bold ${compareMode ? 'border-primary-200 bg-primary-50 text-primary' : 'border-slate-200 bg-white text-slate-600'}`}>{compareMode ? 'Hide comparison' : `Compare with v${previous.versionNumber}`}</button>}
            </div>
            <div className="max-h-[55vh] space-y-3 overflow-y-auto pr-1">
              {projectProposalFields.map((field) => {
                const currentValue = selected.snapshot[field.name as keyof ProjectProposalContent];
                const oldValue = previous?.snapshot[field.name as keyof ProjectProposalContent] || '';
                return <div key={field.name} className="rounded-xl border border-slate-100 bg-slate-50 p-3"><h3 className="text-xs font-bold uppercase tracking-wide text-slate-400">{field.label}</h3><div className="mt-1 text-sm leading-6 text-slate-700">{compareMode && previous ? <DiffText oldText={oldValue} newText={currentValue} /> : <span className="whitespace-pre-wrap">{currentValue || '—'}</span>}</div></div>;
              })}
            </div>
            <div className="flex justify-end gap-2 border-t border-slate-100 pt-4">
              <button type="button" onClick={() => setSelected(null)} className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-700">Close</button>
              {canRestore && <button type="button" onClick={() => void restore(selected)} disabled={restoring} className="inline-flex items-center gap-2 rounded-xl bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">{restoring && <Loader2 className="h-4 w-4 animate-spin" />} Restore this version</button>}
            </div>
          </div>
        )}
      </Modal>
    </section>
  );
}
