import { useState } from 'react';
import { Lock } from 'lucide-react';
import toast from 'react-hot-toast';
import { teamFormationApi } from '../../api/teamFormationApi';
import type { TeamFormation } from '../../types/teamFormation';
import { parseApiError } from '../../utils/apiError';
import { currentInvitations } from '../../utils/teamFormation';
import ConfirmDialog from '../ui/ConfirmDialog';
import InvitationCountdown from './InvitationCountdown';
import TeamFormationFinalizePanel from './TeamFormationFinalizePanel';

interface Props {
  formation: TeamFormation;
  onChanged: () => void | Promise<void>;
  /** Shown instead of enabling Accept, e.g. when the student must choose a major first. */
  acceptDisabledReason?: string;
}

type Action = 'accept' | 'decline' | 'leave' | 'cancel';

const SUCCESS_MESSAGES: Record<Action, string> = {
  accept: 'Invitation accepted.',
  decline: 'Invitation declined.',
  leave: 'You left the team formation.',
  cancel: 'Team formation cancelled.',
};

/** Actions available to the signed-in student on a pending formation, by role in it. */
export default function TeamFormationControls({ formation, onChanged, acceptDisabledReason }: Props) {
  const [working, setWorking] = useState(false);
  const [confirming, setConfirming] = useState<'decline' | 'leave' | 'cancel' | null>(null);
  const own = currentInvitations(formation).find(item => item.studentId === formation.myStudentId);
  const isCreator = formation.creatorStudentId === formation.myStudentId;
  if (formation.status !== 'Pending' || !own) return null;

  const act = async (action: Action) => {
    if (working) return;
    setWorking(true);
    try {
      await teamFormationApi[action](formation.id);
      setConfirming(null);
      toast.success(SUCCESS_MESSAGES[action]);
      await onChanged();
    } catch (error) {
      toast.error(parseApiError(error, 'Could not update the formation. Please refresh and try again.').message);
    } finally {
      setWorking(false);
    }
  };

  const buttonBase = 'rounded-lg px-3 py-2 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50';
  const dialog = {
    decline: {
      title: 'Decline team invitation?',
      description: 'You will not join this team. The other members can still continue without you.',
      confirmText: 'Decline invitation',
      cancelText: 'Keep invitation',
    },
    leave: {
      title: 'Leave this team formation?',
      description: 'Your place will be released and the creator can invite someone else. You can be invited again later.',
      confirmText: 'Leave formation',
      cancelText: 'Stay',
    },
    cancel: {
      title: 'Cancel team formation?',
      description: 'Cancel this formation and release every invitation? All invited students will be notified.',
      confirmText: 'Cancel formation',
      cancelText: 'Keep formation',
    },
  } as const;

  return (
    <div className="space-y-3">
      {own.status === 'Pending' && (
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2 text-sm text-slate-600">
            <span>{own.isProposedLeader ? 'You are invited as Team Leader. Time left to respond:' : 'Time left to respond:'}</span>
            <InvitationCountdown
              expiresAtUtc={own.expiresAtUtc}
              serverTimeUtc={formation.serverTimeUtc}
              onExpire={() => void onChanged()}
            />
          </div>
          {acceptDisabledReason && (
            <div className="flex items-start gap-2 rounded-lg border border-orange-200 bg-orange-50 px-3 py-2 text-xs text-orange-800">
              <Lock className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
              <span>{acceptDisabledReason}</span>
            </div>
          )}
          <div className="flex flex-wrap gap-2">
            <button type="button" disabled={working || Boolean(acceptDisabledReason)} onClick={() => void act('accept')}
              className={`${buttonBase} bg-green-600 text-white hover:bg-green-700`}>
              {working ? 'Updating…' : 'Accept invitation'}
            </button>
            <button type="button" disabled={working} onClick={() => setConfirming('decline')}
              className={`${buttonBase} border border-red-200 text-red-700 hover:bg-red-50`}>
              Decline
            </button>
          </div>
        </div>
      )}

      {own.status === 'Accepted' && !isCreator && (
        <div className="flex flex-wrap items-center gap-3">
          <p className="text-sm font-medium text-green-700">You accepted. Waiting for the creator to finalize the team.</p>
          <button type="button" disabled={working} onClick={() => setConfirming('leave')}
            className={`${buttonBase} border border-slate-300 text-slate-700 hover:bg-slate-50`}>
            Leave formation
          </button>
        </div>
      )}

      {isCreator && <TeamFormationFinalizePanel formation={formation} onChanged={onChanged} />}

      {isCreator && (
        <button type="button" disabled={working} onClick={() => setConfirming('cancel')}
          className={`${buttonBase} border border-slate-300 text-slate-700 hover:bg-slate-50`}>
          Cancel formation
        </button>
      )}

      {confirming && (
        <ConfirmDialog
          isOpen
          onClose={() => { if (!working) setConfirming(null); }}
          onConfirm={() => act(confirming)}
          isSubmitting={working}
          {...dialog[confirming]}
        />
      )}
    </div>
  );
}
