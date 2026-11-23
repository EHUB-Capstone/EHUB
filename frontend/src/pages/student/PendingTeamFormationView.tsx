import { Calendar, Users } from 'lucide-react';
import type { TeamFormation } from '../../types/teamFormation';
import TeamFormationControls from '../../components/class/TeamFormationControls';
import TeamFormationMemberList from '../../components/class/TeamFormationMemberList';
import { useTranslation } from 'react-i18next';

interface Props {
  formation: TeamFormation;
  onChanged: () => Promise<void>;
}

export default function PendingTeamFormationView({ formation, onChanged }: Props) {
  const { t } = useTranslation('class');
  const creator = formation.invitations.find(member => member.isCreator);

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-heading sm:text-display text-secondary tracking-tight">{t('myTeam.myTeam')}</h1>
          <p className="mt-1 text-sm text-slate-500">
            {t('pendingTeamFormationView.teamInvitationInClass')} <span className="font-semibold text-slate-700">{formation.classCode}</span>.
          </p>
        </div>
        <span className="inline-flex items-center gap-1.5 rounded-xl border border-amber-200 bg-amber-50 px-3 py-1.5 text-xs font-semibold text-amber-800">
          <Calendar className="h-3.5 w-3.5" /> {t('pendingTeamFormationView.awaitingFinalization')}
        </span>
      </div>

      <div className="max-w-2xl space-y-6">
        <section className="relative space-y-4 overflow-hidden rounded-2xl border border-slate-200/60 p-6 shadow-sm bg-white" aria-label={t('teamFormationCard.teamFormation', { teamName: formation.teamName })}>
          <div className="flex items-center gap-4">
            <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl shadow-sm bg-primary">
              <Users className="h-6 w-6 text-white" />
            </div>
            <div className="min-w-0">
              <h2 className="break-words text-2xl font-bold leading-tight text-slate-900">{formation.teamName}</h2>
              <p className="mt-0.5 text-xs font-mono tracking-wider text-slate-400">{t('pendingTeamFormationView.formationPending', { classCode: formation.classCode })}</p>
              {creator && <p className="mt-0.5 text-xs text-slate-500">{t('teamFormationCard.createdBy', { fullName: creator.fullName })}</p>}
            </div>
          </div>
          <p className="text-sm text-slate-600">
            {t('pendingTeamFormationView.accepted6TheTeamIsCreated', { acceptedCount: formation.acceptedCount })}
          </p>
          <TeamFormationControls formation={formation} onChanged={onChanged} />
        </section>

        <section className="overflow-hidden rounded-2xl border border-slate-200/60 bg-white shadow-sm" aria-label={t('pendingTeamFormationView.proposedTeamMembers')}>
          <div className="border-b border-slate-100 bg-slate-50/50 p-4">
            <h3 className="flex items-center gap-2 text-sm font-bold text-slate-800">
              <Users className="h-4 w-4 text-primary" /> {t('pendingTeamFormationView.members')}{formation.activeCount} {t('pendingTeamFormationView.active')}
            </h3>
          </div>
          <div className="p-3">
            <TeamFormationMemberList formation={formation} onChanged={onChanged} />
          </div>
        </section>
      </div>
    </div>
  );
}
