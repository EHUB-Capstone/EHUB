import { useMemo, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import { Check, Loader2, Search, X } from 'lucide-react';
import { teamApi } from '../../api/teamApi';
import { useDialogA11y } from '../../hooks/useDialogA11y';
import type { ManagedTeam, MentorAssignment } from '../../types/teamManagement';
import { parseApiError } from '../../utils/apiError';
import { isReplaceReasonValid } from '../../utils/mentorAllocationPreview';
import { slotCandidates, type MentorOption, type MentorSlot } from '../../utils/mentorSlotBoard';
import { matchesSearchQuery } from '../../utils/searchText';
import MentorKindTag from '../admin/MentorKindTag';
import { MENTOR_KIND_STYLES } from '../../utils/mentorKindStyles';

interface MentorSlotDialogProps {
  team: ManagedTeam;
  slot: MentorSlot;
  /** The mentor who holds the slot now. When set, the dialog replaces them and asks for a reason. */
  current: MentorAssignment | null;
  mentors: MentorOption[];
  onClose: () => void;
  /** Called after a successful save; the parent refreshes its data. */
  onSaved: () => Promise<void> | void;
}

/** Picks the mentor for one slot of one team: assigns it when empty, replaces the current mentor in one save otherwise. */
export default function MentorSlotDialog({ team, slot, current, mentors, onClose, onSaved }: MentorSlotDialogProps) {
  const ref = useRef<HTMLDivElement>(null);
  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState('');
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const replacing = current !== null;
  useDialogA11y(ref, { onClose, busy: saving });

  const candidates = useMemo(
    () => slotCandidates(mentors, slot, current?.mentor.mentorProfileId)
      .filter(option => matchesSearchQuery(search, [option.name, option.email, option.contractType ?? '']))
      .sort((left, right) => left.activeTeamCount - right.activeTeamCount || left.name.localeCompare(right.name)),
    [mentors, slot, current, search],
  );
  const canSave = selectedId !== '' && (!replacing || isReplaceReasonValid(reason)) && !saving;
  const label = MENTOR_KIND_STYLES[slot].label;

  const save = async () => {
    if (!canSave) return;
    setSaving(true);
    setError('');
    try {
      if (current) await teamApi.replaceMentor(team._id, current.assignmentId, selectedId, reason.trim());
      else await teamApi.assignMentor(team._id, selectedId);
      toast.success(replacing ? 'Mentor replaced' : 'Mentor assigned');
      await onSaved();
      onClose();
    } catch (requestError: unknown) {
      // The dialog stays open with the choice kept, so the admin can read the reason and try again.
      setError(parseApiError(requestError, replacing ? 'Failed to replace the mentor' : 'Failed to assign the mentor').message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-end justify-center p-0 sm:items-center sm:p-4" role="dialog" aria-modal="true" aria-labelledby="mentor-slot-title">
      <div className="absolute inset-0 bg-black/40" onClick={saving ? undefined : onClose} />
      <div ref={ref} tabIndex={-1} className="relative flex max-h-[calc(100dvh-2rem)] w-full max-w-lg flex-col overflow-hidden rounded-t-2xl bg-white shadow-float outline-none sm:rounded-2xl">
        <div className="flex items-start justify-between gap-3 border-b border-slate-100 px-5 py-4">
          <div className="min-w-0">
            <h3 id="mentor-slot-title" className="text-base font-bold text-slate-900">{replacing ? 'Replace' : 'Assign'} {label.toLowerCase()}</h3>
            <p className="mt-0.5 truncate text-xs text-slate-500">{team.teamName || team.teamCode}{current ? ` · now ${current.mentor.fullName}` : ''}</p>
          </div>
          <button type="button" onClick={onClose} disabled={saving} className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 disabled:opacity-50" aria-label="Close"><X className="h-4 w-4" /></button>
        </div>

        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-5">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              type="search"
              aria-label={`Search ${label.toLowerCase()}s`}
              placeholder="Search by name, email or contract..."
              value={search}
              onChange={event => setSearch(event.target.value)}
              className="w-full rounded-xl border border-slate-200 py-2 pl-9 pr-3 text-xs outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
          </div>
          <div className="max-h-60 space-y-1.5 overflow-y-auto rounded-xl border border-slate-100 bg-slate-50/50 p-2" role="radiogroup" aria-label={`${label} candidates`}>
            {candidates.length === 0 ? (
              <p className="px-3 py-8 text-center text-xs text-slate-400">
                {search ? 'No matching mentors.' : `No other ${label.toLowerCase()} is active in this semester. Add one first in Subject Management > Lecturers & Mentors > Add mentors.`}
              </p>
            ) : candidates.map(option => {
              const checked = selectedId === option._id;
              return (
                <button
                  key={option._id}
                  type="button"
                  role="radio"
                  aria-checked={checked}
                  disabled={saving}
                  onClick={() => setSelectedId(option._id)}
                  className={`flex w-full items-center justify-between gap-3 rounded-xl border p-2.5 text-left transition disabled:opacity-60 ${checked ? 'border-primary/30 bg-primary-50/50' : 'border-slate-200 bg-white hover:border-slate-300'}`}
                >
                  <div className="min-w-0">
                    <p className="truncate text-xs font-semibold text-slate-800">{option.name}</p>
                    <p className="truncate text-[10px] text-slate-400">{option.email}{option.contractType ? ` · ${option.contractType}` : ''} · {option.activeTeamCount} team{option.activeTeamCount === 1 ? '' : 's'} this semester</p>
                  </div>
                  <span className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-full border ${checked ? 'border-primary bg-primary' : 'border-slate-300 bg-white'}`}>
                    {checked && <Check className="h-3 w-3 text-white" />}
                  </span>
                </button>
              );
            })}
          </div>

          {replacing && (
            <div>
              <label htmlFor="replace-reason" className="mb-1 block text-xs font-semibold text-slate-700">Reason for replacing {current.mentor.fullName} *</label>
              <textarea
                id="replace-reason"
                rows={2}
                value={reason}
                disabled={saving}
                onChange={event => setReason(event.target.value)}
                placeholder="At least 3 characters"
                className="w-full rounded-xl border border-slate-200 px-3 py-2 text-xs outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
              />
              <p className="mt-1 text-[11px] text-slate-400">The current assignment is ended and kept in the history, and the new mentor starts in the same save.</p>
            </div>
          )}

          {error && <p role="alert" className="rounded-xl bg-red-50 px-3 py-2 text-xs text-red-700">{error}</p>}
        </div>

        <div className="flex items-center justify-between gap-3 border-t border-slate-100 px-5 py-3">
          <MentorKindTag type={slot} />
          <div className="flex gap-2">
            <button type="button" onClick={onClose} disabled={saving} className="rounded-xl border border-slate-200 px-4 py-2 text-xs font-semibold text-slate-500 hover:bg-slate-50 disabled:opacity-50">Cancel</button>
            <button type="button" onClick={() => void save()} disabled={!canSave} className="flex min-w-28 items-center justify-center gap-1.5 rounded-xl bg-primary px-4 py-2 text-xs font-semibold text-white shadow-sm hover:bg-primary-700 disabled:opacity-50">
              {saving ? <><Loader2 className="h-3.5 w-3.5 animate-spin" /> Saving...</> : replacing ? 'Replace mentor' : 'Assign mentor'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
