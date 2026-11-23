import { useState } from 'react';
import { Crown, History, RotateCcw } from 'lucide-react';
import toast from 'react-hot-toast';
import { teamFormationApi } from '../../api/teamFormationApi';
import type { TeamFormation, TeamFormationInvitation, TeamInvitationStatus } from '../../types/teamFormation';
import { parseApiError } from '../../utils/apiError';
import { canReinvite, currentInvitations, historyInvitations, remainingInviteSlots } from '../../utils/teamFormation';
import InvitationCountdown from './InvitationCountdown';
import { useTranslation } from 'react-i18next';

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
  const { t } = useTranslation('class');
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
      toast.success(t('teamFormationMemberList.invitationSentTo', { fullName: member.fullName }));
      await onChanged();
    } catch (error) {
      toast.error(parseApiError(error, 'Could not send the invitation. Please refresh and try again.').message);
    } finally {
      setReinvitingId(null);
    }
  };

  return (
    <ul className="divide-y divide-slate-100 rounded-xl border border-slate-100" aria-label={t('teamFormationMemberList.teamFormationMembers')}>
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
                    <span className="rounded-md bg-primary-100 px-1.5 py-0.5 text-overline font-bold uppercase text-primary-700">{t('teamFormationMemberList.you')}</span>
                  )}
                  {member.isCreator && <span className="text-xs text-slate-500">{t('teamFormationMemberList.creator')}</span>}
                  {member.isProposedLeader && (
                    <span className="inline-flex items-center gap-1 rounded-md bg-amber-50 px-1.5 py-0.5 text-overline font-bold uppercase text-amber-700">
                      <Crown className="h-3 w-3" aria-hidden="true" /> {isPending ? t('teamFormationMemberList.invitedAsTeamLeader') : t('studentTeamGeneratePanel.teamLeader')}
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
                  {!isPending && member.status === 'Pending' ? t('teamFormationMemberList.closed') : member.status}
                </span>
                {isCreator && isPending && canReinvite(member.status) && (
                  <button
                    type="button"
                    disabled={Boolean(reinvitingId) || slotsLeft === 0}
                    onClick={() => void reinvite(member)}
                    title={slotsLeft === 0 ? t('teamFormationMemberList.theTeamAlreadyHas6Members') : undefined}
                    className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-2.5 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <RotateCcw className="h-3 w-3" aria-hidden="true" />
                    {reinvitingId === member.studentId ? t('teamFormationMemberList.sending') : t('teamFormationMemberList.reInvite')}
                  </button>
                )}
              </div>
            </div>
            {history.length > 0 && (
              <details className="text-xs text-slate-500">
                <summary className="inline-flex cursor-pointer items-center gap-1 select-none">
                  <History className="h-3 w-3" aria-hidden="true" /> {t('teamFormationMemberList.invitationHistory')}{history.length})
                </summary>
                <ul className="mt-1 space-y-0.5 pl-4">
                  {history.map(record => (
                    <li key={record.id}>
                      {t('teamFormationMemberList.invited', { status: record.status, v2: formatDateTime(record.createdAtUtc), v3: record.respondedAtUtc ? ` · ${t('teamFormationMemberList.respondedAt', { date: formatDateTime(record.respondedAtUtc) })}` : '' })}
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
