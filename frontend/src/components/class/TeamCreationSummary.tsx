import {
  CheckCircle2,
  Crown,
  UserPlus,
  Users,
  XCircle,
  AlertTriangle,
} from 'lucide-react';
import Button from '../ui/Button';
import { TEAM_MAJOR_GROUPS } from '../../constants/majors';
import {
  TEAM_MEMBER_LIMIT,
  validateTeamSelection,
  type TeamSelectionValidation,
} from '../../utils/teamManagement';
import type { TeamStudent } from '../../types/teamManagement';

interface TeamCreationSummaryProps {
  totalStudents: number;
  unassignedStudents: number;
  selectedStudents: TeamStudent[];
  selectedLeaderId: string;
  onLeaderChange: (studentId: string) => void;
  onCreateTeam: () => void;
}

const groupInfo = {
  GROUP_1: TEAM_MAJOR_GROUPS.find((group) => group.key === 'GROUP_1'),
  GROUP_2: TEAM_MAJOR_GROUPS.find((group) => group.key === 'GROUP_2'),
};

const pluralizeStudent = (count: number): string => `${count} ${count === 1 ? 'student' : 'students'}`;

const formatPossibleTeams = (count: number): string => {
  if (count <= 0) return '';
  if (count < 4) return 'Redistribute';
  if (count >= 4 && count <= 6) return `1×${count}`;
  if (count === 7) return '1×4 · redistribute 3';

  const candidates: Array<{ fours: number; fives: number; sixes: number; teamCount: number }> = [];
  for (let sixes = 0; sixes <= Math.floor(count / 6); sixes += 1) {
    for (let fives = 0; fives <= Math.floor(count / 5); fives += 1) {
      const remainder = count - sixes * 6 - fives * 5;
      if (remainder < 0 || remainder % 4 !== 0) continue;
      const fours = remainder / 4;
      candidates.push({
        fours,
        fives,
        sixes,
        teamCount: fours + fives + sixes,
      });
    }
  }

  candidates.sort((a, b) =>
    a.teamCount - b.teamCount ||
    b.sixes - a.sixes ||
    b.fives - a.fives,
  );

  const best = candidates[0];
  if (!best) return 'Redistribute';

  const parts = [
    best.sixes ? `${best.sixes}×6` : '',
    best.fives ? `${best.fives}×5` : '',
    best.fours ? `${best.fours}×4` : '',
  ].filter(Boolean);

  return parts.join(' · ');
};

const constraintClasses = {
  valid: {
    card: 'border-green-200 bg-green-50/80',
    icon: 'text-green-600',
    title: 'text-green-700',
    detail: 'text-green-800/75',
  },
  invalid: {
    card: 'border-red-200 bg-red-50/70',
    icon: 'text-red-500',
    title: 'text-red-700',
    detail: 'text-red-900/65',
  },
};

function ConstraintCard({
  valid,
  label,
  detail,
}: {
  valid: boolean;
  label: string;
  detail?: string;
}) {
  const Icon = valid ? CheckCircle2 : XCircle;
  const state = valid ? constraintClasses.valid : constraintClasses.invalid;

  return (
    <div className={`min-h-[74px] rounded-xl border px-3 py-3 ${state.card}`}>
      <div className="flex items-start gap-2">
        <Icon className={`mt-0.5 h-4 w-4 shrink-0 ${state.icon}`} />
        <div className="min-w-0">
          <p className={`text-sm font-bold leading-5 ${state.title}`}>{label}</p>
          {detail && (
            <p className={`mt-1 break-words text-xs font-medium leading-5 ${state.detail}`}>
              {detail}
            </p>
          )}
        </div>
      </div>
    </div>
  );
}

function getGroupDisplayName(groupKey: 'GROUP_1' | 'GROUP_2'): string {
  return groupInfo[groupKey]?.label || groupKey.replace('_', ' ');
}

function formatGroupCodes(
  validation: TeamSelectionValidation,
  groupKey: 'GROUP_1' | 'GROUP_2',
): string {
  const selectedCodes = validation.groupMajorCodes[groupKey];
  if (selectedCodes.length > 0) return selectedCodes.join(', ');

  const codes = groupInfo[groupKey]?.majors.map((major) => major.code) || [];
  return codes.join(', ');
}

function formatSelectedMajors(validation: TeamSelectionValidation): string {
  if (validation.majorCount === 0) return 'No major selected';
  return `Selected majors: ${validation.majorCodes.join(', ')}`;
}

function formatLeaderName(student: TeamStudent): string {
  return student.rollNumber
    ? `${student.fullName} · ${student.rollNumber}`
    : student.fullName;
}

function formatMissingMajorWarning(count: number): string {
  return `${pluralizeStudent(count)} selected ${count === 1 ? 'has' : 'have'} no declared major.`;
}

export default function TeamCreationSummary({
  totalStudents,
  unassignedStudents,
  selectedStudents,
  selectedLeaderId,
  onLeaderChange,
  onCreateTeam,
}: TeamCreationSummaryProps) {
  const validation = validateTeamSelection(selectedStudents, selectedLeaderId);
  const possibleTeams = formatPossibleTeams(unassignedStudents);

  return (
    <section className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-xs">
      <span className="sr-only">{totalStudents} students in class</span>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.35fr)_minmax(240px,0.75fr)]">
        <div className="min-w-0">
          <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wide text-slate-500">
            <Users className="h-4 w-4 text-slate-400" />
            Selection Summary
          </div>

          <p className="mt-2 text-lg font-bold text-slate-900">
            {pluralizeStudent(validation.memberCount)} selected
          </p>
          <p className="mt-0.5 break-words text-sm font-medium text-slate-500">
            {formatSelectedMajors(validation)}
          </p>

          <div className="mt-4 space-y-1">
            <p className="text-sm font-semibold text-slate-700">
              {pluralizeStudent(unassignedStudents)} remaining
            </p>
            {possibleTeams && (
              <p className="break-words text-xs font-medium text-slate-500">
                Possible teams: {possibleTeams}
              </p>
            )}
          </div>
        </div>

        <div className="grid gap-2 sm:grid-cols-2">
          <ConstraintCard
            valid={validation.isMemberCountValid}
            label="4–6 members"
            detail={`${validation.memberCount} / ${TEAM_MEMBER_LIMIT}`}
          />
          <ConstraintCard
            valid={validation.hasGroupOne}
            label={validation.hasGroupOne ? getGroupDisplayName('GROUP_1') : `${getGroupDisplayName('GROUP_1')} required`}
            detail={formatGroupCodes(validation, 'GROUP_1')}
          />
          <ConstraintCard
            valid={validation.hasGroupTwo}
            label={validation.hasGroupTwo ? getGroupDisplayName('GROUP_2') : `${getGroupDisplayName('GROUP_2')} required`}
            detail={formatGroupCodes(validation, 'GROUP_2')}
          />
          <ConstraintCard
            valid={validation.isTeamLeaderValid}
            label="Team Leader"
            detail={validation.leaderStudent ? formatLeaderName(validation.leaderStudent) : 'Required'}
          />
        </div>

        <div className="flex min-w-0 flex-col gap-2">
          <label htmlFor="team-summary-leader" className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wide text-slate-500">
            <Crown className="h-4 w-4 text-amber-500" />
            Team Leader
          </label>
          <select
            id="team-summary-leader"
            value={validation.isTeamLeaderValid ? selectedLeaderId : ''}
            onChange={(event) => onLeaderChange(event.target.value)}
            disabled={selectedStudents.length === 0}
            className="min-h-10 rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400"
          >
            <option value="">Select leader</option>
            {selectedStudents.map((student) => (
              <option key={student._id} value={student._id}>
                {student.rollNumber ? `${student.fullName} · ${student.rollNumber}` : student.fullName}
              </option>
            ))}
          </select>

          {validation.canCreateTeam && (
            <div className="mt-1 flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-xs font-bold text-green-700">
              <CheckCircle2 className="h-3.5 w-3.5 shrink-0" />
              Team is ready to be created
            </div>
          )}

          <Button
            variant="primary"
            size="md"
            icon={validation.canCreateTeam ? UserPlus : undefined}
            disabled={!validation.canCreateTeam}
            onClick={onCreateTeam}
            className="mt-1 w-full"
          >
            Create Team
          </Button>
        </div>
      </div>

      {validation.missingMajorStudents.length > 0 && (
        <div className="mt-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2.5 text-sm font-semibold text-amber-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-500" />
          <span>{formatMissingMajorWarning(validation.missingMajorStudents.length)}</span>
        </div>
      )}
    </section>
  );
}
