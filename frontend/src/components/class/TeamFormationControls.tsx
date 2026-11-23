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
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';

interface Props {
  formation: TeamFormation;
  onChanged: () => void | Promise<void>;
  /** Shown instead of enabling Accept, e.g. when the student must choose a major first. */
  acceptDisabledReason?: string;
}

type Action = 'accept' | 'decline' | 'leave' | 'cancel';

const SUCCESS_MESSAGES: Record<Action, string> = {
  get accept() { return i18n.t('class:teamFormationControls.accept'); },
  get decline() { return i18n.t('class:teamFormationControls.decline'); },
  get leave() { return i18n.t('class:teamFormationControls.leave'); },
  get cancel() { return i18n.t('class:teamFormationControls.cancel'); },
};

/** Actions available to the signed-in student on a pending formation, by role in it. */
export default function TeamFormationControls({ formation, onChanged, acceptDisabledReason }: Props) {
  const { t } = useTranslation('class');
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
      toast.error(parseApiError(error, t('teamFormationControls.couldNotUpdateTheFormationPlease')).message);
    } finally {
      setWorking(false);
    }
  };

  const buttonBase = 'rounded-lg px-3 py-2 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50';
  const dialog = {
    decline: {
      title: t('teamFormationControls.declineTeamInvitation'),
      description: t('teamFormationControls.youWillNotJoinThisTeam'),
      confirmText: t('teamFormationControls.declineInvitation'),
      cancelText: t('teamFormationControls.keepInvitation'),
    },
    leave: {
      title: t('teamFormationControls.leaveThisTeamFormation'),
      description: t('teamFormationControls.yourPlaceWillBeReleasedAnd'),
      confirmText: t('teamFormationControls.leaveFormation'),
      cancelText: t('teamFormationControls.stay'),
    },
    cancel: {
      title: t('teamFormationControls.cancelTeamFormation'),
      description: t('teamFormationControls.cancelThisFormationAndReleaseEvery'),
      confirmText: t('teamFormationControls.cancelFormation'),
      cancelText: t('teamFormationControls.keepFormation'),
    },
  } as const;

  return (
    <div className="space-y-3">
      {own.status === 'Pending' && (
        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2 text-sm text-slate-600">
            <span>{own.isProposedLeader ? t('teamFormationControls.youAreInvitedAsTeamLeader') : t('teamFormationControls.timeLeftToRespond')}</span>
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
              {working ? t('teamFormationControls.updating') : t('teamFormationControls.acceptInvitation')}
            </button>
            <button type="button" disabled={working} onClick={() => setConfirming('decline')}
              className={`${buttonBase} border border-red-200 text-red-700 hover:bg-red-50`}>
              {t('teamFormationControls.decline')}
            </button>
          </div>
        </div>
      )}

      {own.status === 'Accepted' && !isCreator && (
        <div className="flex flex-wrap items-center gap-3">
          <p className="text-sm font-medium text-green-700">{t('teamFormationControls.youAcceptedWaitingForTheCreator')}</p>
          <button type="button" disabled={working} onClick={() => setConfirming('leave')}
            className={`${buttonBase} border border-slate-300 text-slate-700 hover:bg-slate-50`}>
            {t('teamFormationControls.leaveFormation')}
          </button>
        </div>
      )}

      {isCreator && <TeamFormationFinalizePanel formation={formation} onChanged={onChanged} />}

      {isCreator && (
        <button type="button" disabled={working} onClick={() => setConfirming('cancel')}
          className={`${buttonBase} border border-slate-300 text-slate-700 hover:bg-slate-50`}>
          {t('teamFormationControls.cancelFormation')}
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
