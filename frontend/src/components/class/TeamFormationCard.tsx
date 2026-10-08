import type { TeamFormation } from '../../types/teamFormation';
import TeamFormationControls from './TeamFormationControls';
import TeamFormationMemberList from './TeamFormationMemberList';

interface Props {
  formation: TeamFormation;
  onChanged: () => void | Promise<void>;
  acceptDisabledReason?: string;
}

export default function TeamFormationCard({ formation, onChanged, acceptDisabledReason }: Props) {
  const pending = formation.status === 'Pending';
  const creator = formation.invitations.find(item => item.isCreator);

  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm" aria-label={`Team formation ${formation.teamName}`}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">{formation.classCode} · Team formation</p>
          <h3 className="mt-1 text-lg font-bold text-slate-900">{formation.teamName}</h3>
          {creator && <p className="mt-0.5 text-xs text-slate-500">Created by {creator.fullName}</p>}
        </div>
        <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${pending ? 'bg-amber-100 text-amber-800' : formation.status === 'Completed' ? 'bg-green-100 text-green-800' : 'bg-slate-100 text-slate-700'}`}>
          {formation.status}
        </span>
      </div>
      <p className="mt-2 text-sm text-slate-600">
        {pending ? `Accepted: ${formation.acceptedCount} / 6. The creator finalizes the team once 4–6 members, including both major groups, have accepted.` :
          formation.status === 'Completed' ? 'The creator finalized the team. It is now active.' :
            'This formation was cancelled. No team was created.'}
      </p>
      <div className="mt-3">
        <TeamFormationMemberList formation={formation} onChanged={onChanged} />
      </div>
      <div className="mt-4">
        <TeamFormationControls formation={formation} onChanged={onChanged} acceptDisabledReason={acceptDisabledReason} />
      </div>
    </section>
  );
}
