import { useState } from 'react';
import { Crown } from 'lucide-react';
import toast from 'react-hot-toast';
import { teamFormationApi } from '../../api/teamFormationApi';
import type { TeamFormation } from '../../types/teamFormation';
import { parseApiError } from '../../utils/apiError';
import { MAX_FORMATION_MEMBERS, currentInvitations } from '../../utils/teamFormation';
import ConfirmDialog from '../ui/ConfirmDialog';
import InvitationCountdown from './InvitationCountdown';

interface Props {
  formation: TeamFormation;
  onChanged: () => void | Promise<void>;
}

/** Creator-only: shows readiness, lets the creator confirm the leader, and finalizes the official team. */
export default function TeamFormationFinalizePanel({ formation, onChanged }: Props) {
  const [working, setWorking] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [leaderId, setLeaderId] = useState('');
  const members = currentInvitations(formation);
  const accepted = members.filter(member => member.status === 'Accepted');
  const pending = members.filter(member => member.status === 'Pending');
  const proposedLeader = members.find(member => member.studentId === formation.proposedLeaderStudentId);
  const needsLeader = formation.requiresLeaderSelection;
  const effectiveLeaderId = needsLeader ? leaderId : formation.proposedLeaderStudentId;
  const effectiveLeader = members.find(member => member.studentId === effectiveLeaderId);
  const canSubmit = formation.canFinalize && Boolean(effectiveLeaderId) && !working;

  const finalize = async () => {
    if (working) return;
    setWorking(true);
    try {
      await teamFormationApi.finalize(formation.id, {
        leaderStudentId: needsLeader ? leaderId : undefined,
        confirmPendingInvitations: pending.length > 0,
      });
      setConfirmOpen(false);
      toast.success('Team created.');
      await onChanged();
    } catch (error) {
      toast.error(parseApiError(error, 'Could not finalize the team. Please refresh and try again.').message);
    } finally {
      setWorking(false);
    }
  };

  return (
    <section className="space-y-3 rounded-xl border border-slate-200 bg-slate-50/60 p-3" aria-label="Finalize team">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h4 className="text-sm font-bold text-slate-800">Finalize team</h4>
        <span className="text-xs font-semibold text-slate-600">
          Accepted: {accepted.length} / {MAX_FORMATION_MEMBERS}
        </span>
      </div>

      {formation.canFinalize ? (
        <p className="text-sm font-medium text-green-700">Team is ready to be finalized.</p>
      ) : (
        <div className="text-sm text-amber-800" role="status">
          <p className="font-semibold">Team cannot be finalized yet.</p>
          <ul className="mt-1 list-disc pl-5">
            {formation.finalizeBlockers.map(reason => <li key={reason}>{reason}</li>)}
          </ul>
        </div>
      )}

      {needsLeader ? (
        <div>
          <label htmlFor={`leader-${formation.id}`} className="mb-1 flex items-center gap-1 text-xs font-semibold text-slate-600">
            <Crown className="h-3.5 w-3.5 text-amber-500" aria-hidden="true" /> Choose the Team Leader <span className="text-red-500">*</span>
          </label>
          <p className="mb-1.5 text-xs text-slate-500">
            {proposedLeader ? `${proposedLeader.fullName}, the proposed leader, has not accepted. ` : ''}
            Pick one of the accepted members.
          </p>
          <select
            id={`leader-${formation.id}`}
            value={leaderId}
            onChange={event => setLeaderId(event.target.value)}
            className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
          >
            <option value="">Select leader</option>
            {accepted.map(member => <option key={member.studentId} value={member.studentId}>{member.fullName} ({member.rollNumber})</option>)}
          </select>
        </div>
      ) : (
        <p className="flex items-center gap-1.5 text-sm text-slate-700">
          <Crown className="h-4 w-4 text-amber-500" aria-hidden="true" />
          Team Leader: <strong>{proposedLeader?.fullName}</strong>
        </p>
      )}

      <button
        type="button"
        disabled={!canSubmit}
        onClick={() => setConfirmOpen(true)}
        className="rounded-xl bg-primary px-4 py-2.5 text-sm font-bold text-white hover:bg-primary-dark disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-primary"
      >
        Finalize Team
      </button>

      <ConfirmDialog
        isOpen={confirmOpen}
        onClose={() => { if (!working) setConfirmOpen(false); }}
        onConfirm={finalize}
        isSubmitting={working}
        title="Finalize this team?"
        description={pending.length > 0
          ? 'Some invitations are still waiting for an answer. If you finalize now, these students will not join the team and their invitations will be closed.'
          : `The team will be created with ${accepted.length} members. This cannot be undone.`}
        confirmText="Finalize team"
        cancelText="Keep waiting"
        confirmVariant="primary"
        details={(
          <div className="w-full space-y-3 text-left text-sm">
            <p className="text-slate-700">
              Team Leader: <strong>{effectiveLeader?.fullName ?? '—'}</strong>
            </p>
            {pending.length > 0 && (
              <ul className="divide-y divide-slate-100 rounded-xl border border-amber-200 bg-amber-50/60">
                {pending.map(member => (
                  <li key={member.id} className="flex items-center justify-between gap-2 px-3 py-2">
                    <span className="font-medium text-slate-800">{member.fullName}</span>
                    <InvitationCountdown expiresAtUtc={member.expiresAtUtc} serverTimeUtc={formation.serverTimeUtc} />
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      />
    </section>
  );
}
