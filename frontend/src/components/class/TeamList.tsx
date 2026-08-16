import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ChevronDown,
  ChevronRight,
  Crown,
  ExternalLink,
  FolderKanban,
  Lightbulb,
  Pencil,
  Plus,
  Rocket,
  Trash2,
  UserRoundCheck,
  Users,
} from 'lucide-react';
import Button from '../ui/Button';
import type { ManagedTeam, TeamProject, TeamStudent } from '../../types/teamManagement';
import { entityId, getTeamMembers, getTeamProject } from '../../utils/teamManagement';

interface TeamListProps {
  teams: ManagedTeam[];
  classStudents?: TeamStudent[];
  canDelete?: boolean;
  canManageInfo?: boolean;
  currentStudentId?: string;
  onCreate?: () => void;
  onAssign?: () => void;
  onEdit?: (team: ManagedTeam) => void;
  onDelete?: (team: ManagedTeam) => void;
  onReview?: (team: ManagedTeam) => void;
  onRevise?: (team: ManagedTeam) => void;
  onProjectDirection?: (team: ManagedTeam) => void;
  onCancelProposal?: (team: ManagedTeam) => void;
  onRefresh?: () => void | Promise<void>;
}

const statusStyles: Record<string, string> = {
  ACTIVE: 'bg-green-100 text-green-700',
  APPROVED: 'bg-green-100 text-green-700',
  PENDING: 'bg-amber-100 text-amber-700',
  NEEDS_REVISION: 'bg-orange-100 text-orange-700',
  NEEDSREVISION: 'bg-orange-100 text-orange-700',
  DRAFT: 'bg-slate-100 text-slate-600',
  CANCELLED: 'bg-slate-100 text-slate-600',
  REJECTED: 'bg-red-100 text-red-700',
  ARCHIVED: 'bg-slate-100 text-slate-600',
};

const projectStatusStyles: Record<string, string> = {
  DRAFT: 'bg-slate-100 text-slate-600',
  IN_PROGRESS: 'bg-blue-100 text-blue-700',
  VALIDATED: 'bg-green-100 text-green-700',
  COMPLETED: 'bg-purple-100 text-purple-700',
};

const readableStatus = (status?: string | null) => (status || 'ACTIVE')
  .replaceAll('_', ' ')
  .toLowerCase()
  .replace(/^./, (character) => character.toUpperCase());

export default function TeamList({
  teams,
  classStudents = [],
  canDelete = true,
  canManageInfo = true,
  currentStudentId,
  onCreate,
  onAssign,
  onEdit,
  onDelete,
  onReview,
  onRevise,
  onProjectDirection,
  onCancelProposal,
}: TeamListProps) {
  const safeTeams = Array.isArray(teams) ? teams : [];

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 rounded-2xl border border-slate-200/60 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary-50 text-primary"><Users className="h-5 w-5" /></div>
          <div>
            <h2 className="font-bold text-slate-900">Team management</h2>
            <p className="text-sm text-slate-500">{safeTeams.length} team{safeTeams.length === 1 ? '' : 's'} in this class</p>
          </div>
        </div>
        {canManageInfo && (onAssign || onCreate) && (
          <div className="flex flex-col gap-2 sm:flex-row">
            {onAssign && <Button variant="outline" icon={UserRoundCheck} onClick={onAssign}>Assign students</Button>}
            {onCreate && <Button variant="gradient" icon={Plus} onClick={onCreate}>Create team</Button>}
          </div>
        )}
      </div>

      {safeTeams.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-12 text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-white text-slate-400 shadow-xs"><Users className="h-6 w-6" /></div>
          <h3 className="mt-4 font-semibold text-slate-700">No teams yet</h3>
          <p className="mt-1 text-sm text-slate-500">Create the first team and assign students from this class.</p>
          {canManageInfo && onCreate && <Button className="mt-4" variant="outline" icon={Plus} onClick={onCreate}>Create first team</Button>}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
          {safeTeams.map((team) => (
            <TeamCard
              key={team._id}
              team={team}
              students={classStudents}
              canDelete={canDelete}
              canManageInfo={canManageInfo}
              currentStudentId={currentStudentId}
              onEdit={onEdit}
              onDelete={onDelete}
              onReview={onReview}
              onRevise={onRevise}
              onProjectDirection={onProjectDirection}
              onCancelProposal={onCancelProposal}
            />
          ))}
        </div>
      )}
    </div>
  );
}

interface TeamCardProps {
  team: ManagedTeam;
  students: TeamStudent[];
  canDelete: boolean;
  canManageInfo: boolean;
  currentStudentId?: string;
  onEdit?: (team: ManagedTeam) => void;
  onDelete?: (team: ManagedTeam) => void;
  onReview?: (team: ManagedTeam) => void;
  onRevise?: (team: ManagedTeam) => void;
  onProjectDirection?: (team: ManagedTeam) => void;
  onCancelProposal?: (team: ManagedTeam) => void;
}

function TeamCard({
  team,
  students,
  canDelete,
  canManageInfo,
  currentStudentId,
  onEdit,
  onDelete,
  onReview,
  onRevise,
  onProjectDirection,
  onCancelProposal,
}: TeamCardProps) {
  const navigate = useNavigate();
  const [expanded, setExpanded] = useState(false);
  const members = getTeamMembers(team, students);
  const project = getTeamProject(team);
  const leaderId = entityId(team.leaderId);
  const status = String(team.status || 'ACTIVE').toUpperCase();
  const teamInitial = team.teamName.trim().charAt(0).toUpperCase() || 'T';
  const isPending = status === 'PENDING';
  const needsRevision = status === 'NEEDSREVISION' || status === 'NEEDS_REVISION';
  const currentStudentIsMember = Boolean(currentStudentId && members.some(member => member._id === currentStudentId));
  const canOpenTeamWorkspace = canManageInfo || currentStudentIsMember;

  return (
    <article className={`overflow-hidden rounded-2xl border bg-white shadow-sm transition-shadow hover:shadow-card ${isPending ? 'border-amber-200' : 'border-slate-200/70'}`}>
      <div className="p-4">
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 items-center gap-3">
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-primary text-base font-bold text-white">{teamInitial}</div>
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <h3 className="truncate font-bold text-slate-900">{team.teamName || 'Unnamed team'}</h3>
                <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold uppercase ${statusStyles[status] || statusStyles.ACTIVE}`}>{readableStatus(status)}</span>
              </div>
              <p className="mt-0.5 font-mono text-xs text-slate-400">{team.teamCode || 'No team code'}</p>
            </div>
          </div>

          <div className="flex shrink-0 items-center gap-1">
            {isPending && onReview && <button type="button" onClick={() => onReview(team)} className="rounded-lg bg-amber-50 px-2.5 py-1.5 text-xs font-semibold text-amber-700 hover:bg-amber-100">Review</button>}
            {needsRevision && onRevise && <button type="button" onClick={() => onRevise(team)} className="rounded-lg bg-orange-50 px-2.5 py-1.5 text-xs font-semibold text-orange-700 hover:bg-orange-100">Revise</button>}
            {team.isProposal && ['PENDING', 'NEEDSREVISION', 'NEEDS_REVISION', 'DRAFT'].includes(status) && onCancelProposal && <button type="button" onClick={() => onCancelProposal(team)} className="rounded-lg bg-red-50 px-2.5 py-1.5 text-xs font-semibold text-red-600 hover:bg-red-100">Cancel</button>}
            {!team.isProposal && canManageInfo && onEdit && <button type="button" onClick={() => onEdit(team)} className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-primary-50 hover:text-primary" aria-label={`Edit ${team.teamName}`} title="Edit team"><Pencil className="h-4 w-4" /></button>}
            {!team.isProposal && canDelete && onDelete && <button type="button" onClick={() => onDelete(team)} className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-300 hover:bg-red-50 hover:text-red-500" aria-label={`Delete ${team.teamName}`} title="Delete team"><Trash2 className="h-4 w-4" /></button>}
          </div>
        </div>

        <div className="mt-4 grid grid-cols-2 gap-2">
          <div className="rounded-xl bg-slate-50 p-3">
            <p className="flex items-center gap-1.5 text-xs font-medium text-slate-500"><Users className="h-3.5 w-3.5" /> Members</p>
            <p className="mt-1 text-lg font-bold text-slate-900">{members.length}</p>
          </div>
          <div className={`rounded-xl p-3 ${project ? 'bg-secondary-50' : 'bg-slate-50'}`}>
            <p className={`flex items-center gap-1.5 text-xs font-medium ${project ? 'text-secondary' : 'text-slate-500'}`}><Rocket className="h-3.5 w-3.5" /> Project</p>
            <p className={`mt-1 truncate text-sm font-bold ${project ? 'text-secondary-dark' : 'text-slate-400'}`}>{project?.name || 'Not linked'}</p>
          </div>
        </div>

        <p className="mt-3 text-xs font-medium text-slate-500">Chat: {team.hasChatGroup ? 'Chat' : 'No Chat'}</p>

        <div className="mt-4 flex items-center justify-between gap-3">
          <div className="flex -space-x-2">
            {members.slice(0, 5).map((member) => (
              <span key={member._id} title={member.fullName} className={`flex h-8 w-8 items-center justify-center rounded-full border-2 border-white text-[10px] font-bold text-white ${member._id === leaderId ? 'bg-amber-500' : 'bg-secondary'}`}>{member.fullName.charAt(0).toUpperCase()}</span>
            ))}
            {members.length > 5 && <span className="flex h-8 w-8 items-center justify-center rounded-full border-2 border-white bg-slate-200 text-[10px] font-bold text-slate-600">+{members.length - 5}</span>}
          </div>
          <button type="button" onClick={() => setExpanded((value) => !value)} className="flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-100" aria-expanded={expanded}>
            {expanded ? 'Hide details' : 'View details'}
            {expanded ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
          </button>
        </div>
      </div>

      {expanded && (
        <div className="border-t border-slate-100 bg-slate-50/50 p-4">
          {team.rejectReason && (status === 'REJECTED' || needsRevision) && (
            <div className="mb-4 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800"><strong>Review note:</strong> {team.rejectReason}</div>
          )}

          <div className="space-y-4">
            <div>
              <h4 className="mb-2 flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-slate-500"><Users className="h-3.5 w-3.5" /> Members</h4>
              {members.length === 0 ? (
                <p className="rounded-xl border border-dashed border-slate-200 bg-white py-5 text-center text-sm text-slate-400">No members assigned</p>
              ) : (
                <div className="grid gap-2 sm:grid-cols-2">
                  {members.map((member) => (
                    <div key={member._id} className="flex items-center gap-2.5 rounded-xl border border-slate-100 bg-white p-2.5">
                      <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-bold text-white ${member._id === leaderId ? 'bg-amber-500' : 'bg-secondary'}`}>{member.fullName.charAt(0).toUpperCase()}</span>
                      <span className="min-w-0 flex-1"><span className="block truncate text-sm font-semibold text-slate-800">{member.fullName}</span><span className="block truncate text-xs text-slate-400">{member.rollNumber || member.email || 'Student'}</span></span>
                      {member._id === leaderId && <Crown className="h-4 w-4 shrink-0 text-amber-500" />}
                      {member._id === currentStudentId && <span className="rounded-full bg-primary-50 px-1.5 py-0.5 text-[10px] font-semibold text-primary">You</span>}
                    </div>
                  ))}
                </div>
              )}
            </div>

            <ProjectDetail project={project} />

            {team.description && (
              <div className="rounded-xl border border-slate-200 bg-white p-3">
                <h4 className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-slate-500"><Lightbulb className="h-3.5 w-3.5" /> Team description</h4>
                <p className="mt-2 text-sm leading-6 text-slate-600">{team.description}</p>
              </div>
            )}

            {!team.isProposal && canOpenTeamWorkspace && onProjectDirection && <button type="button" onClick={() => onProjectDirection(team)} className="flex w-full items-center justify-center gap-2 rounded-xl border border-secondary-200 bg-white px-4 py-2.5 text-sm font-semibold text-secondary hover:bg-secondary-50"><Lightbulb className="h-4 w-4" /> Project direction</button>}
            {!team.isProposal && canOpenTeamWorkspace && <button type="button" onClick={() => navigate(`/workspace/teams/${team._id}`)} className="flex w-full items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 hover:border-primary hover:text-primary">Open team workspace <ExternalLink className="h-4 w-4" /></button>}
          </div>
        </div>
      )}
    </article>
  );
}

function ProjectDetail({ project }: { project: TeamProject | null }) {
  if (!project) {
    return (
      <div className="rounded-xl border border-dashed border-slate-300 bg-white p-4 text-center">
        <FolderKanban className="mx-auto h-5 w-5 text-slate-300" />
        <p className="mt-2 text-sm font-medium text-slate-500">No project linked to this team</p>
      </div>
    );
  }

  const status = String(project.status || 'DRAFT').toUpperCase();
  return (
    <div className="rounded-xl border border-secondary-100 bg-secondary-50 p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 items-center gap-2.5">
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white text-secondary shadow-xs"><Rocket className="h-4 w-4" /></span>
          <div className="min-w-0"><p className="text-[10px] font-bold uppercase tracking-wider text-secondary">Linked project</p><h4 className="truncate font-bold text-slate-900">{project.name}</h4></div>
        </div>
        <span className={`shrink-0 rounded-full px-2 py-1 text-[10px] font-bold uppercase ${projectStatusStyles[status] || projectStatusStyles.DRAFT}`}>{readableStatus(status)}</span>
      </div>
      {project.startupField && <p className="mt-3 text-xs font-semibold text-secondary">Field: {project.startupField}</p>}
      {project.description && <p className="mt-2 text-sm leading-6 text-slate-600">{project.description}</p>}
      {(project.problem || project.solution) && (
        <div className="mt-3 grid gap-2 sm:grid-cols-2">
          {project.problem && <div className="rounded-lg bg-white/80 p-2.5"><p className="text-[10px] font-bold uppercase text-slate-400">Problem</p><p className="mt-1 text-xs text-slate-600">{project.problem}</p></div>}
          {project.solution && <div className="rounded-lg bg-white/80 p-2.5"><p className="text-[10px] font-bold uppercase text-slate-400">Solution</p><p className="mt-1 text-xs text-slate-600">{project.solution}</p></div>}
        </div>
      )}
    </div>
  );
}
