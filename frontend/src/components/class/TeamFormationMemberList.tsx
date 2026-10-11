import { useState } from 'react';
import { Crown, History, RotateCcw } from 'lucide-react';
import toast from 'react-hot-toast';
import { teamFormationApi } from '../../api/teamFormationApi';
import type { TeamFormation, TeamFormationInvitation, TeamInvitationStatus } from '../../types/teamFormation';
import { parseApiError } from '../../utils/apiError';
import { canReinvite, currentInvitations, historyInvitations, remainingInviteSlots } from '../../utils/teamFormation';
import InvitationCountdown from './InvitationCountdown';

interface Props {
  formation: TeamFormation;
  onChanged: () => void | Promise<void>;
}

const STATUS_CLASSES: Record<TeamInvitationStatus, string> = {
  Pending: 'bg-amber-50 text-amber-800',
  Accepted: 'bg-green-50 text-green-700',
  Declined: 'bg-red-50 text-red-700',
  Expired: 'bg-slate-100 text-slate-600',
  Left: 'bg-slate-100 text-slate-600',
};

const formatDateTime = (value: string | null) => (value ? new Date(value).toLocaleString() : '');

export default function TeamFormationMemberList({ formation, onChanged }: Props) {
  const [reinvitingId, setReinvitingId] = useState<string | null>(null);
  const isCreator = formation.creatorStudentId === formation.myStudentId;
  const isPending = formation.status === 'Pending';
  const slotsLeft = remainingInviteSlots(formation);
  const members = currentInvitations(formation);

  const reinvite = async (member: TeamFormationInvitation) => {
    if (reinvitingId) return;
    setReinvitingId(member.studentId);
    try {
      await teamFormationApi.invite(formation.id, [member.studentId]);
      toast.success(`Invitation sent to ${member.fullName}.`);
      await onChanged();
    } catch (error) {
      toast.error(parseApiError(error, 'Could not send the invitation. Please refresh and try again.').message);
    } finally {
      setReinvitingId(null);
    }
  };

  return (
    <ul className="divide-y divide-slate-100 rounded-xl border border-slate-100" aria-label="Team formation members">
      {members.map(member => {
        const history = historyInvitations(formation, member.studentId);
        const showCountdown = isPending && member.status === 'Pending';
        return (
          <li key={member.id} className="space-y-2 px-3 py-2.5 text-sm">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-1.5">
                  <span className="font-medium text-slate-800">{member.fullName}</span>
                  {member.studentId === formation.myStudentId && (
                    <span className="rounded-md bg-primary-100 px-1.5 py-0.5 text-[10px] font-bold uppercase text-primary">You</span>
                  )}
                  {member.isCreator && <span className="text-xs text-slate-500">· Creator</span>}
                  {member.isProposedLeader && (
                    <span className="inline-flex items-center gap-1 rounded-md bg-amber-50 px-1.5 py-0.5 text-[10px] font-bold uppercase text-amber-700">
                      <Crown className="h-3 w-3" aria-hidden="true" /> {isPending ? 'Invited as Team Leader' : 'Team Leader'}
                    </span>
                  )}
                </div>
                {member.rollNumber && <p className="mt-0.5 font-mono text-xs text-slate-400">{member.rollNumber}</p>}
              </div>
              <div className="flex flex-wrap items-center gap-2">
                {showCountdown && (
                  <InvitationCountdown
                    expiresAtUtc={member.expiresAtUtc}
                    serverTimeUtc={formation.serverTimeUtc}
                    onExpire={() => void onChanged()}
                  />
                )}
                <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${STATUS_CLASSES[member.status]}`}>
                  {!isPending && member.status === 'Pending' ? 'Closed' : member.status}
                </span>
                {isCreator && isPending && canReinvite(member.status) && (
                  <button
                    type="button"
                    disabled={Boolean(reinvitingId) || slotsLeft === 0}
                    onClick={() => void reinvite(member)}
                    title={slotsLeft === 0 ? 'The team already has 6 members or pending invitations.' : undefined}
                    className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <RotateCcw className="h-3 w-3" aria-hidden="true" />
                    {reinvitingId === member.studentId ? 'Sending…' : 'Re-invite'}
                  </button>
                )}
              </div>
            </div>
            {history.length > 0 && (
              <details className="text-xs text-slate-500">
                <summary className="inline-flex cursor-pointer items-center gap-1 select-none">
                  <History className="h-3 w-3" aria-hidden="true" /> Invitation history ({history.length})
                </summary>
                <ul className="mt-1 space-y-0.5 pl-4">
                  {history.map(record => (
                    <li key={record.id}>
                      {record.status} · invited {formatDateTime(record.createdAtUtc)}
                      {record.respondedAtUtc ? ` · responded ${formatDateTime(record.respondedAtUtc)}` : ''}
                    </li>
                  ))}
                </ul>
              </details>
            )}
          </li>
        );
      })}
    </ul>
  );
}
