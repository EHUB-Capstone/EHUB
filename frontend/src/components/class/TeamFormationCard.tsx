import { useState } from 'react';
import toast from 'react-hot-toast';
import { teamFormationApi } from '../../api/teamFormationApi';
import type { TeamFormation } from '../../types/teamFormation';
import { parseApiError } from '../../utils/apiError';
import ConfirmDialog from '../ui/ConfirmDialog';

interface Props {
  formation: TeamFormation;
  onChanged: () => void | Promise<void>;
}

export default function TeamFormationCard({ formation, onChanged }: Props) {
  const [working, setWorking] = useState(false);
  const [cancelConfirmOpen, setCancelConfirmOpen] = useState(false);
  const ownInvitation = formation.invitations.find(item => item.studentId === formation.myStudentId);
  const isCreator = formation.creatorStudentId === formation.myStudentId;
  const pending = formation.status === 'Pending';

  const act = async (action: 'accept' | 'decline' | 'cancel') => {
    if (working) return;
    if (action === 'decline' && !window.confirm('Declining will cancel this formation for everyone. Continue?')) return;
    setWorking(true);
    try {
      await teamFormationApi[action](formation.id);
      if (action === 'cancel') setCancelConfirmOpen(false);
      toast.success(action === 'accept' ? 'Invitation accepted.' : 'Formation cancelled.');
      await onChanged();
    } catch (error) {
      toast.error(parseApiError(error, 'Could not update the formation. Please refresh and try again.').message);
    } finally {
      setWorking(false);
    }
  };

  return (
    <>
    <section className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm" aria-label={`Team formation ${formation.teamName}`}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">{formation.classCode} · Team formation</p>
          <h3 className="mt-1 text-lg font-bold text-slate-900">{formation.teamName}</h3>
        </div>
        <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${pending ? 'bg-amber-100 text-amber-800' : formation.status === 'Completed' ? 'bg-green-100 text-green-800' : 'bg-slate-100 text-slate-700'}`}>
          {formation.status}
        </span>
      </div>
      <p className="mt-2 text-sm text-slate-600">
        {pending ? 'Waiting for all members to accept before the team is created.' :
          formation.status === 'Completed' ? 'All members accepted. The team is active.' :
            'This formation was cancelled. No team was created.'}
      </p>
      <ul className="mt-3 divide-y divide-slate-100 rounded-xl border border-slate-100">
        {formation.invitations.map(member => (
          <li key={member.studentId} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
            <span className="font-medium text-slate-800">
              {member.fullName} {member.isCreator && <span className="text-xs text-slate-500">· Creator</span>}
              {member.isProposedLeader && <span className="text-xs text-amber-700"> · Proposed Leader</span>}
            </span>
            <span className="text-xs font-semibold text-slate-600">
              {formation.status === 'Cancelled' && member.status !== 'Declined' ? 'Cancelled' : member.status}
            </span>
          </li>
        ))}
      </ul>
      {pending && (ownInvitation?.status === 'Pending' || isCreator) && (
        <div className="mt-4 flex flex-wrap gap-2">
          {ownInvitation?.status === 'Pending' && <>
            <button type="button" disabled={working} onClick={() => void act('accept')}
              className="rounded-lg bg-green-600 px-3 py-2 text-sm font-semibold text-white hover:bg-green-700 disabled:opacity-50">Accept</button>
            <button type="button" disabled={working} onClick={() => void act('decline')}
              className="rounded-lg border border-red-200 px-3 py-2 text-sm font-semibold text-red-700 hover:bg-red-50 disabled:opacity-50">Decline</button>
          </>}
          {isCreator && <button type="button" disabled={working} onClick={() => setCancelConfirmOpen(true)}
            className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50">Cancel formation</button>}
        </div>
      )}
    </section>
    <ConfirmDialog
      isOpen={cancelConfirmOpen}
      onClose={() => { if (!working) setCancelConfirmOpen(false); }}
      onConfirm={() => act('cancel')}
      isSubmitting={working}
      title="Cancel team formation?"
      description="Cancel this formation and release all invitations?"
      confirmText="Cancel formation"
      cancelText="Keep formation"
    />
    </>
  );
}
