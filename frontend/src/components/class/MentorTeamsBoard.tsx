import { Plus, RefreshCw, UserMinus } from 'lucide-react';
import type { ManagedTeam, MentorAssignment } from '../../types/teamManagement';
import type { MentorSlot, TeamSlotRow } from '../../utils/mentorSlotBoard';
import { MENTOR_KIND_STYLES } from '../../utils/mentorKindStyles';
import TemporaryMentorBadge from '../admin/TemporaryMentorBadge';

interface MentorTeamsBoardProps {
  rows: TeamSlotRow[];
  disabled: boolean;
  onAssign: (team: ManagedTeam, slot: MentorSlot) => void;
  onReplace: (team: ManagedTeam, slot: MentorSlot, assignment: MentorAssignment) => void;
  onEnd: (team: ManagedTeam, assignment: MentorAssignment) => void;
}

const SLOTS: MentorSlot[] = ['Enterprise', 'Academic'];

/** One row per team with a column for each mentor slot, so a missing mentor is as visible as a present one. */
export default function MentorTeamsBoard({ rows, disabled, onAssign, onReplace, onEnd }: MentorTeamsBoardProps) {
  return (
    <div className="overflow-x-auto rounded-xl border border-slate-200">
      <table className="w-full min-w-[640px] text-left">
        <thead>
          <tr className="border-b border-slate-100 bg-slate-50/70 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
            <th scope="col" className="px-4 py-2.5">Team</th>
            {SLOTS.map(slot => <th key={slot} scope="col" className="px-4 py-2.5">{MENTOR_KIND_STYLES[slot].label}</th>)}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {rows.map(row => (
            <tr key={row.team._id} className="align-top">
              <th scope="row" className="px-4 py-3 text-left font-normal">
                <p className="text-xs font-bold text-slate-800">{row.team.teamName || 'Unnamed team'}</p>
                <p className="mt-0.5 text-[10px] text-slate-400">{row.team.teamCode || row.team.groupName || '—'}</p>
                {row.missingCount > 0 && (
                  <span className="mt-1 inline-flex rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-[10px] font-semibold text-amber-800">
                    {row.missingCount === 2 ? 'No mentors yet' : 'Missing 1 mentor'}
                  </span>
                )}
              </th>
              {SLOTS.map(slot => {
                const assignment = slot === 'Enterprise' ? row.enterprise : row.academic;
                return (
                  <td key={slot} className="px-4 py-3">
                    {assignment ? (
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0">
                          <p className="flex items-center gap-1.5 truncate text-xs font-semibold text-slate-800" title={assignment.mentor.fullName}>
                            <span className="truncate">{assignment.mentor.isTemporary ? assignment.mentor.fullName.replace(/ \(no email yet\)$/, '') : assignment.mentor.fullName}</span>
                            {assignment.mentor.isTemporary && <TemporaryMentorBadge />}
                          </p>
                          <p className="truncate text-[10px] text-slate-400" title={assignment.mentor.email}>{assignment.mentor.isTemporary ? 'No account yet: add their email to activate' : assignment.mentor.email}</p>
                        </div>
                        <div className="flex shrink-0 gap-1">
                          <button
                            type="button"
                            disabled={disabled}
                            onClick={() => onReplace(row.team, slot, assignment)}
                            className="inline-flex h-7 items-center gap-1 rounded-lg border border-slate-200 bg-white px-2 text-[11px] font-semibold text-slate-600 hover:bg-slate-50 disabled:opacity-50"
                            aria-label={`Replace ${assignment.mentor.fullName} on ${row.team.teamName}`}
                          >
                            <RefreshCw className="h-3 w-3" /> Replace
                          </button>
                          <button
                            type="button"
                            disabled={disabled}
                            onClick={() => onEnd(row.team, assignment)}
                            className="inline-flex h-7 items-center gap-1 rounded-lg border border-red-100 bg-white px-2 text-[11px] font-semibold text-red-600 hover:border-red-200 hover:bg-red-50 disabled:opacity-50"
                            aria-label={`End ${assignment.mentor.fullName} on ${row.team.teamName}`}
                          >
                            <UserMinus className="h-3 w-3" /> End
                          </button>
                        </div>
                      </div>
                    ) : (
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-xs text-slate-400">Not assigned</span>
                        <button
                          type="button"
                          disabled={disabled}
                          onClick={() => onAssign(row.team, slot)}
                          className="inline-flex h-7 items-center gap-1 rounded-lg border border-primary-100 bg-primary-50 px-2 text-[11px] font-semibold text-primary hover:bg-primary-100 disabled:opacity-50"
                          aria-label={`Assign ${MENTOR_KIND_STYLES[slot].label.toLowerCase()} to ${row.team.teamName}`}
                        >
                          <Plus className="h-3 w-3" /> Assign
                        </button>
                      </div>
                    )}
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
