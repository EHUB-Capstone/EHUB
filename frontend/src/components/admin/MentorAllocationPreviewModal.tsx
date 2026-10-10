import { useMemo, useState } from 'react';
import { AlertTriangle, Pencil, RefreshCw, Undo2 } from 'lucide-react';
import type {
  MentorAllocationConflict,
  MentorAllocationEdit,
  MentorAllocationPreview,
  MentorType,
} from '../../types/mentorAdmin';
import {
  buildTeamGrid,
  getMentorCandidates,
  getSubjectColumns,
  skipReasonLabel,
  summarizePreview,
  type SlotState,
  type SlotView,
  type TeamGridRow,
} from '../../utils/mentorAllocationPreview';
import { buildChangeReason, isChangeReasonValid } from '../../utils/mentorChangeReasons';
import Button from '../ui/Button';
import Modal from '../ui/Modal';
import MentorChangeReasonField from './MentorChangeReasonField';

type PreviewTab = 'teams' | 'mentors' | 'attention';
type TeamFilter = 'all' | 'attention' | 'changed';

interface EditorTarget {
  teamId: string;
  mentorType: MentorType;
  presetMentorId?: string;
}

interface MentorAllocationPreviewModalProps {
  isOpen: boolean;
  preview: MentorAllocationPreview | null;
  edits: MentorAllocationEdit[];
  isRefreshing: boolean;
  isCommitting: boolean;
  isStale: boolean;
  onClose: () => void;
  onApplyEdit: (edit: MentorAllocationEdit) => void;
  onRemoveEdit: (teamId: string, mentorType: MentorType) => void;
  onRegenerate: () => void;
  onConfirm: () => void;
}

const slotLabel: Record<MentorType, string> = { Enterprise: 'Enterprise mentor', Academic: 'Lecturer mentor' };

const stateStyle: Record<SlotState, { label: string; className: string }> = {
  existing: { label: 'Current', className: 'bg-slate-100 text-slate-600' },
  retained: { label: 'Kept', className: 'bg-primary-50 text-primary' },
  allocated: { label: 'New', className: 'bg-green-50 text-green-700' },
  manual: { label: 'Manual', className: 'bg-amber-50 text-amber-700' },
  unfilled: { label: 'Missing', className: 'bg-red-50 text-red-700' },
};

export default function MentorAllocationPreviewModal({
  isOpen,
  preview,
  edits,
  isRefreshing,
  isCommitting,
  isStale,
  onClose,
  onApplyEdit,
  onRemoveEdit,
  onRegenerate,
  onConfirm,
}: MentorAllocationPreviewModalProps) {
  const [tab, setTab] = useState<PreviewTab>('teams');
  const [filter, setFilter] = useState<TeamFilter>('all');
  const [search, setSearch] = useState('');
  const [editor, setEditor] = useState<EditorTarget | null>(null);

  const grid = useMemo(() => (preview ? buildTeamGrid(preview) : []), [preview]);
  const summary = useMemo(() => (preview ? summarizePreview(preview) : null), [preview]);
  const visibleRows = useMemo(() => {
    const term = search.trim().toLowerCase();
    return grid.filter(row => {
      const needsAttention = row.enterprise.state === 'unfilled' || row.academic.state === 'unfilled';
      const changed = [row.enterprise, row.academic].some(slot => slot.state !== 'existing');
      if (filter === 'attention' && !needsAttention) return false;
      if (filter === 'changed' && !changed) return false;
      if (!term) return true;
      return [row.teamCode, row.teamName, row.classCode, row.enterprise.mentorName, row.academic.mentorName]
        .some(value => value?.toLowerCase().includes(term));
    });
  }, [grid, filter, search]);

  if (!preview || !summary) return null;

  const busy = isRefreshing || isCommitting;
  const conflicts = preview.conflicts ?? [];
  const skipped = preview.skipped ?? [];
  const unfilled = preview.unfilled ?? [];
  const attentionCount = conflicts.length + skipped.length + unfilled.length;

  const openEditor = (target: EditorTarget) => {
    setTab('teams');
    setFilter('all');
    setSearch('');
    setEditor(target);
  };

  const applyEdit = (edit: MentorAllocationEdit) => {
    setEditor(null);
    onApplyEdit(edit);
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={busy ? () => undefined : onClose}
      title="Mentor assignment preview"
      size="2xl"
      submitText="Confirm and save"
      onSubmit={onConfirm}
      isSubmitting={isCommitting}
      submitDisabled={!preview.canCommit || isStale || isRefreshing}
    >
      <div className="space-y-4">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-xs leading-5 text-slate-500">
            Nothing is saved until you confirm. Existing mentors are never replaced unless you choose to replace them.
            {' '}Strategy: <strong className="text-slate-700">{preview.strategy ?? 'Balanced'}</strong> · seed {preview.seed}.
          </p>
          <Button size="sm" variant="outline" icon={RefreshCw} onClick={onRegenerate} disabled={busy}>
            Regenerate{edits.length > 0 ? ' (clears edits)' : ''}
          </Button>
        </div>

        {isStale && (
          <div role="alert" className="flex items-start gap-2 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>The data changed after this preview was generated. Regenerate the preview before saving.</span>
          </div>
        )}
        {preview.warnings.length > 0 && (
          <ul className="space-y-1 rounded-xl bg-amber-50 px-3 py-2 text-sm text-amber-800">
            {preview.warnings.map(message => <li key={message}>{message}</li>)}
          </ul>
        )}

        <div className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-4 lg:grid-cols-7">
          <Metric label="Teams" value={summary.teams} />
          <Metric label="Kept" value={summary.retained} />
          <Metric label="New" value={summary.allocated} />
          <Metric label="Manual" value={summary.manual} />
          <Metric label="Replaced" value={summary.replacements} warning={summary.replacements > 0} />
          <Metric label="Missing enterprise" value={summary.unfilledEnterprise} danger={summary.unfilledEnterprise > 0} />
          <Metric label="Missing lecturer" value={summary.unfilledAcademic} danger={summary.unfilledAcademic > 0} />
        </div>

        <div role="tablist" aria-label="Preview views" className="flex flex-wrap gap-1 border-b border-slate-200">
          <TabButton active={tab === 'teams'} onClick={() => setTab('teams')}>By team ({grid.length})</TabButton>
          <TabButton active={tab === 'mentors'} onClick={() => setTab('mentors')}>By mentor ({preview.mentorLoads?.length ?? 0})</TabButton>
          <TabButton active={tab === 'attention'} onClick={() => setTab('attention')}>Needs attention ({attentionCount})</TabButton>
        </div>

        <div className={isRefreshing ? 'pointer-events-none opacity-60' : ''} aria-busy={isRefreshing}>
          {tab === 'teams' && (
            <section aria-label="Assignments by team" className="space-y-3">
              <div className="flex flex-col gap-2 sm:flex-row">
                <input
                  value={search}
                  onChange={event => setSearch(event.target.value)}
                  placeholder="Search team, class or mentor..."
                  aria-label="Search teams"
                  className="min-w-0 flex-1 rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary"
                />
                <select
                  value={filter}
                  onChange={event => setFilter(event.target.value as TeamFilter)}
                  aria-label="Filter teams"
                  className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-primary"
                >
                  <option value="all">All teams</option>
                  <option value="changed">Changed by this preview</option>
                  <option value="attention">Missing a mentor</option>
                </select>
              </div>
              {visibleRows.length === 0 ? (
                <p className="rounded-xl bg-slate-50 px-3 py-6 text-center text-sm text-slate-500">No team matches the current filter.</p>
              ) : (
                <div className="max-h-[48vh] overflow-auto rounded-xl border border-slate-200 bg-white">
                  <table className="w-full min-w-[820px] text-left text-xs">
                    <caption className="sr-only">Mentors of each team after this preview</caption>
                    <thead className="sticky top-0 z-10 bg-slate-50 text-slate-500">
                      <tr>
                        <th scope="col" className="px-3 py-2">Team</th>
                        <th scope="col" className="px-3 py-2">{slotLabel.Enterprise}</th>
                        <th scope="col" className="px-3 py-2">{slotLabel.Academic}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {visibleRows.map(row => (
                        <TeamRow
                          key={row.teamId}
                          row={row}
                          preview={preview}
                          edits={edits}
                          editor={editor}
                          disabled={busy}
                          onOpenEditor={openEditor}
                          onCloseEditor={() => setEditor(null)}
                          onApplyEdit={applyEdit}
                          onRemoveEdit={onRemoveEdit}
                        />
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          )}

          {tab === 'mentors' && <MentorLoadTable preview={preview} />}

          {tab === 'attention' && (
            <AttentionPanel
              conflicts={conflicts}
              skipped={skipped}
              unfilled={unfilled}
              onReplace={(conflict) => conflict.teamId && openEditor({
                teamId: conflict.teamId,
                mentorType: conflict.mentorType,
                presetMentorId: conflict.proposedMentorProfileId ?? undefined,
              })}
            />
          )}
        </div>
      </div>
    </Modal>
  );
}

function TeamRow({
  row, preview, edits, editor, disabled, onOpenEditor, onCloseEditor, onApplyEdit, onRemoveEdit,
}: {
  row: TeamGridRow;
  preview: MentorAllocationPreview;
  edits: MentorAllocationEdit[];
  editor: EditorTarget | null;
  disabled: boolean;
  onOpenEditor: (target: EditorTarget) => void;
  onCloseEditor: () => void;
  onApplyEdit: (edit: MentorAllocationEdit) => void;
  onRemoveEdit: (teamId: string, mentorType: MentorType) => void;
}) {
  return (
    <tr className="border-t border-slate-100 align-top">
      <td className="px-3 py-2">
        <span className="block font-semibold text-slate-800">{row.teamCode}</span>
        <span className="block text-slate-500">{row.teamName}</span>
        <span className="text-[11px] text-slate-400">{row.classCode}{row.subjectCode ? ` · ${row.subjectCode}` : ''}</span>
      </td>
      {(['Enterprise', 'Academic'] as const).map(type => {
        const slot = type === 'Enterprise' ? row.enterprise : row.academic;
        const editing = editor?.teamId === row.teamId && editor.mentorType === type;
        const hasEdit = edits.some(edit => edit.teamId === row.teamId && edit.mentorType === type);
        return (
          <td key={type} className="px-3 py-2">
            <SlotCell slot={slot} />
            {!editing && (
              <div className="mt-1.5 flex flex-wrap gap-1">
                <Button size="xs" variant="ghost" icon={Pencil} disabled={disabled} onClick={() => onOpenEditor({ teamId: row.teamId, mentorType: type })} aria-label={`Change ${slotLabel[type].toLowerCase()} of ${row.teamCode}`}>
                  Change
                </Button>
                {hasEdit && (
                  <Button size="xs" variant="ghost" icon={Undo2} disabled={disabled} onClick={() => onRemoveEdit(row.teamId, type)} aria-label={`Undo edit of ${row.teamCode} ${slotLabel[type].toLowerCase()}`}>
                    Undo
                  </Button>
                )}
              </div>
            )}
            {editing && (
              <SlotEditor
                teamCode={row.teamCode}
                type={type}
                slot={slot}
                candidates={getMentorCandidates(preview, type)}
                presetMentorId={editor.presetMentorId}
                onCancel={onCloseEditor}
                onApply={onApplyEdit}
                teamId={row.teamId}
              />
            )}
          </td>
        );
      })}
    </tr>
  );
}

function SlotCell({ slot }: { slot: SlotView }) {
  const style = stateStyle[slot.state];
  return (
    <div>
      <div className="flex flex-wrap items-center gap-1.5">
        <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${style.className}`}>{style.label}</span>
        {slot.mentorName
          ? <span className="font-medium text-slate-800">{slot.mentorName}</span>
          : <span className="text-slate-400">No mentor</span>}
      </div>
      {slot.replacesCurrent && slot.currentMentorName && (
        <p className="mt-1 text-[11px] text-amber-700">Replaces <span className="line-through">{slot.currentMentorName}</span> (assignment ends on save)</p>
      )}
    </div>
  );
}

function SlotEditor({
  teamId, teamCode, type, slot, candidates, presetMentorId, onCancel, onApply,
}: {
  teamId: string;
  teamCode: string;
  type: MentorType;
  slot: SlotView;
  candidates: ReturnType<typeof getMentorCandidates>;
  presetMentorId?: string;
  onCancel: () => void;
  onApply: (edit: MentorAllocationEdit) => void;
}) {
  const occupied = slot.state === 'existing' || slot.replacesCurrent;
  const [mentorId, setMentorId] = useState(presetMentorId ?? '');
  const [reasonCode, setReasonCode] = useState('');
  const [reasonNote, setReasonNote] = useState('');
  const sameAsCurrent = occupied && mentorId !== '' && mentorId === (slot.currentMentorProfileId ?? slot.mentorProfileId);
  const needsReason = occupied && mentorId !== '' && !sameAsCurrent;
  const reasonOk = !needsReason || isChangeReasonValid(reasonCode, reasonNote);
  const canApply = !sameAsCurrent && reasonOk && (mentorId !== '' || !occupied);
  const fieldId = `${teamId}-${type}`;

  const apply = () => {
    if (!canApply) return;
    onApply({
      teamId,
      mentorType: type,
      mentorProfileId: mentorId === '' ? null : mentorId,
      ...(needsReason ? { replace: true, reason: buildChangeReason(reasonCode, reasonNote) } : {}),
    });
  };

  return (
    <div className="mt-2 space-y-2 rounded-lg border border-slate-200 bg-slate-50 p-2.5">
      <label htmlFor={`mentor-${fieldId}`} className="block text-[11px] font-semibold text-slate-600">
        {slotLabel[type]} for {teamCode}
      </label>
      <select
        id={`mentor-${fieldId}`}
        value={mentorId}
        onChange={event => setMentorId(event.target.value)}
        className="w-full rounded-lg border border-slate-200 bg-white px-2 py-1.5 text-xs text-slate-700 outline-none focus:border-primary"
      >
        <option value="">{occupied ? 'Choose a mentor...' : 'Leave empty'}</option>
        {candidates.map(candidate => (
          <option key={candidate.mentorProfileId} value={candidate.mentorProfileId}>
            {candidate.mentorName} · {candidate.totalAfter} team{candidate.totalAfter === 1 ? '' : 's'}
          </option>
        ))}
      </select>
      {candidates.length === 0 && <p className="text-[11px] text-amber-700">No active {slotLabel[type].toLowerCase()} in this semester.</p>}
      {needsReason && (
        <MentorChangeReasonField
          idPrefix={`reason-${fieldId}`}
          mentorName={slot.currentMentorName ?? slot.mentorName}
          code={reasonCode}
          note={reasonNote}
          onCodeChange={setReasonCode}
          onNoteChange={setReasonNote}
        />
      )}
      <div className="flex justify-end gap-1.5">
        <Button size="xs" variant="ghost" onClick={onCancel}>Cancel</Button>
        <Button size="xs" disabled={!canApply} onClick={apply}>{needsReason ? 'Replace' : 'Apply'}</Button>
      </div>
    </div>
  );
}

function MentorLoadTable({ preview }: { preview: MentorAllocationPreview }) {
  const subjects = getSubjectColumns(preview);
  const mentors = preview.mentorLoads ?? [];
  if (mentors.length === 0) {
    return <p className="rounded-xl bg-slate-50 px-3 py-6 text-center text-sm text-slate-500">No mentor is active in this semester.</p>;
  }
  return (
    <section aria-label="Load by mentor" className="max-h-[52vh] overflow-auto rounded-xl border border-slate-200 bg-white">
      <table className="w-full min-w-[720px] text-left text-xs">
        <caption className="sr-only">Teams carried by every active mentor, before and after this preview</caption>
        <thead className="sticky top-0 z-10 bg-slate-50 text-slate-500">
          <tr>
            <th scope="col" className="px-3 py-2">Mentor</th>
            <th scope="col" className="px-3 py-2">Type</th>
            {subjects.map(code => <th key={code} scope="col" className="px-3 py-2">{code}</th>)}
            <th scope="col" className="px-3 py-2">Total</th>
            <th scope="col" className="px-3 py-2">Status</th>
          </tr>
        </thead>
        <tbody>
          {mentors.map(mentor => {
            const unassigned = mentor.totalAfter === 0;
            return (
              <tr key={mentor.mentorProfileId} className="border-t border-slate-100">
                <td className="px-3 py-2"><span className="block font-semibold text-slate-800">{mentor.mentorName}</span><span className="text-slate-400">{mentor.mentorEmail}</span></td>
                <td className="px-3 py-2">{slotLabel[mentor.mentorType]}{mentor.contractType ? <span className="block text-slate-400">{mentor.contractType}</span> : null}</td>
                {subjects.map(code => {
                  const entry = mentor.subjects.find(item => item.subjectCode === code);
                  return <td key={code} className="px-3 py-2">{entry ? formatChange(entry.before, entry.added, entry.removed ?? 0) : '0'}</td>;
                })}
                <td className="px-3 py-2 font-semibold text-slate-800">{mentor.totalBefore === mentor.totalAfter ? mentor.totalAfter : `${mentor.totalBefore} → ${mentor.totalAfter}`}</td>
                <td className="px-3 py-2">{unassigned ? <span className="rounded-full bg-amber-50 px-2 py-0.5 text-[10px] font-semibold text-amber-700">Unassigned</span> : <span className="text-slate-500">Assigned</span>}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </section>
  );
}

function formatChange(before: number, added: number, removed: number) {
  if (!added && !removed) return String(before);
  return `${before} → ${before + added - removed}`;
}

function AttentionPanel({
  conflicts, skipped, unfilled, onReplace,
}: {
  conflicts: MentorAllocationConflict[];
  skipped: NonNullable<MentorAllocationPreview['skipped']>;
  unfilled: NonNullable<MentorAllocationPreview['unfilled']>;
  onReplace: (conflict: MentorAllocationConflict) => void;
}) {
  if (conflicts.length + skipped.length + unfilled.length === 0) {
    return <p className="rounded-xl bg-green-50 px-3 py-6 text-center text-sm text-green-700">Every slot has a mentor and nothing needs a decision.</p>;
  }
  return (
    <div className="max-h-[52vh] space-y-4 overflow-auto">
      {conflicts.length > 0 && (
        <section aria-label="Conflicts" className="space-y-2">
          <h3 className="text-sm font-semibold text-slate-800">Conflicts ({conflicts.length})</h3>
          <ul className="space-y-2">
            {conflicts.map((conflict, index) => (
              <li key={`${conflict.teamId}-${conflict.mentorType}-${index}`} className="flex flex-col gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 sm:flex-row sm:items-center sm:justify-between">
                <span><strong>{conflict.teamCode || 'Team'}</strong> · {slotLabel[conflict.mentorType]}: {conflict.message}</span>
                {conflict.kind === 'SlotOccupied' && conflict.teamId && (
                  <Button size="xs" variant="outline" onClick={() => onReplace(conflict)}>Replace…</Button>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}

      {skipped.length > 0 && (
        <section aria-label="Previous mentors not carried over" className="space-y-2">
          <h3 className="text-sm font-semibold text-slate-800">Previous mentors not carried over ({skipped.length})</h3>
          <ul className="space-y-1.5">
            {skipped.map((item, index) => (
              <li key={`${item.sourceTeamCode}-${item.mentorType}-${index}`} className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-500 opacity-70">
                <span className="font-semibold">{item.mentorName || 'Unknown mentor'}</span> · {slotLabel[item.mentorType]} · {item.teamCode || item.sourceTeamCode}
                <span className="ml-2 rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-semibold text-slate-600">{skipReasonLabel[item.reason]}</span>
                <span className="mt-0.5 block text-[11px]">{item.message}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {unfilled.length > 0 && (
        <section aria-label="Slots without a mentor" className="space-y-2">
          <h3 className="text-sm font-semibold text-slate-800">Slots still without a mentor ({unfilled.length})</h3>
          <ul className="grid gap-1.5 sm:grid-cols-2">
            {unfilled.slice(0, 60).map(item => (
              <li key={`${item.teamId}-${item.mentorType}`} className="rounded-lg bg-red-50 px-3 py-1.5 text-xs text-red-700">
                <strong>{item.teamCode}</strong> · {item.classCode} · {slotLabel[item.mentorType]}
              </li>
            ))}
          </ul>
          {unfilled.length > 60 && <p className="text-xs text-slate-500">and {unfilled.length - 60} more. Use the filter on the team view to see them all.</p>}
        </section>
      )}
    </div>
  );
}

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={`-mb-px border-b-2 px-3 py-2 text-sm font-semibold transition-colors ${active ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-800'}`}
    >
      {children}
    </button>
  );
}

function Metric({ label, value, danger = false, warning = false }: { label: string; value: number; danger?: boolean; warning?: boolean }) {
  return (
    <div className={`rounded-lg px-3 py-2 ${danger ? 'bg-red-50 text-red-700' : warning ? 'bg-amber-50 text-amber-700' : 'bg-slate-50 text-slate-700'}`}>
      <span className="block text-[10px] uppercase text-slate-400">{label}</span>
      <strong className="text-base">{value}</strong>
    </div>
  );
}
