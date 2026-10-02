import { useState } from 'react';
import { Calendar, Crown, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { teamFormationApi } from '../../api/teamFormationApi';
import type { TeamFormation } from '../../types/teamFormation';
import { parseApiError } from '../../utils/apiError';
import ConfirmDialog from '../../components/ui/ConfirmDialog';

interface Props {
  formation: TeamFormation;
  onChanged: () => Promise<void>;
}

export default function PendingTeamFormationView({ formation, onChanged }: Props) {
  const [working, setWorking] = useState(false);
  const [cancelConfirmOpen, setCancelConfirmOpen] = useState(false);
  const ownInvitation = formation.invitations.find(member => member.studentId === formation.myStudentId);
  const isCreator = formation.creatorStudentId === formation.myStudentId;
  const acceptedCount = formation.invitations.filter(member => member.status === 'Accepted').length;

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
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900">My Team</h1>
          <p className="mt-1 text-sm text-slate-500">
            Team invitation in class <span className="font-semibold text-slate-700">{formation.classCode}</span>.
          </p>
        </div>
        <span className="inline-flex items-center gap-1.5 rounded-xl border border-amber-200 bg-amber-50 px-3 py-1.5 text-xs font-semibold text-amber-800">
          <Calendar className="h-3.5 w-3.5" /> Awaiting acceptance
        </span>
      </div>

      <div className="max-w-2xl space-y-6">
        <section className="relative space-y-4 overflow-hidden rounded-2xl border border-slate-200/60 bg-gradient-to-br from-white to-slate-50/50 p-6 shadow-sm" aria-label={`Team formation ${formation.teamName}`}>
          <div className="flex items-center gap-4">
            <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-primary to-secondary shadow-sm">
              <Users className="h-6 w-6 text-white" />
            </div>
            <div className="min-w-0">
              <h2 className="break-words text-2xl font-bold leading-tight text-slate-900">{formation.teamName}</h2>
              <p className="mt-0.5 text-xs font-mono tracking-wider text-slate-400">{formation.classCode} · FORMATION PENDING</p>
            </div>
          </div>
          <p className="text-sm text-slate-600">
            {acceptedCount}/{formation.invitations.length} members accepted. The team will be created after everyone accepts.
          </p>
          {ownInvitation?.status === 'Pending' && (
            <div className="flex flex-wrap gap-2">
              <button type="button" disabled={working} onClick={() => void act('accept')}
                className="rounded-xl bg-gradient-to-r from-primary to-secondary px-4 py-2.5 text-sm font-bold text-white hover:opacity-95 disabled:cursor-not-allowed disabled:opacity-50">
                {working ? 'Updating…' : 'Accept invitation'}
              </button>
              <button type="button" disabled={working} onClick={() => void act('decline')}
                className="rounded-xl border border-red-200 px-4 py-2.5 text-sm font-semibold text-red-700 hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-50">
                Decline
              </button>
            </div>
          )}
          {ownInvitation?.status === 'Accepted' && !isCreator && (
            <p className="text-sm font-medium text-green-700">You accepted. Waiting for the other members.</p>
          )}
          {isCreator && (
            <button type="button" disabled={working} onClick={() => setCancelConfirmOpen(true)}
              className="rounded-xl border border-slate-300 px-4 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50">
              Cancel formation
            </button>
          )}
        </section>

        <section className="overflow-hidden rounded-2xl border border-slate-200/60 bg-white shadow-sm" aria-label="Proposed team members">
          <div className="border-b border-slate-100 bg-slate-50/50 p-4">
            <h3 className="flex items-center gap-2 text-sm font-bold text-slate-800">
              <Users className="h-4 w-4 text-primary" /> Team Members ({formation.invitations.length})
            </h3>
          </div>
          <div className="divide-y divide-slate-100">
            {formation.invitations.map(member => (
              <div key={member.studentId} className={`flex flex-wrap items-center justify-between gap-3 p-4 ${member.studentId === formation.myStudentId ? 'bg-primary-50/20' : ''}`}>
                <div className="flex min-w-0 items-center gap-3">
                  <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-secondary-300 to-secondary text-xs font-bold text-white">
                    {member.fullName.charAt(0).toUpperCase() || '?'}
                  </div>
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-semibold text-slate-800">{member.fullName}</span>
                      {member.isProposedLeader && <span className="inline-flex items-center gap-1 rounded-md bg-amber-50 px-1.5 py-0.5 text-[10px] font-bold uppercase text-amber-700">
                        <Crown className="h-3 w-3" /> Proposed Leader
                      </span>}
                      {member.studentId === formation.myStudentId && <span className="rounded-md bg-primary-100 px-1.5 py-0.5 text-[10px] font-bold uppercase text-primary">You</span>}
                    </div>
                    <p className="mt-0.5 text-xs font-mono text-slate-400">{member.rollNumber}</p>
                  </div>
                </div>
                <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${member.status === 'Accepted' ? 'bg-green-50 text-green-700' : 'bg-amber-50 text-amber-800'}`}>
                  {member.status}
                </span>
              </div>
            ))}
          </div>
        </section>
      </div>
    </div>
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
