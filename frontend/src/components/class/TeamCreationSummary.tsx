import {
  AlertTriangle,
  CheckCircle2,
  Crown,
  UserPlus,
  XCircle,
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
  selectedStudents: TeamStudent[];
  selectedLeaderId: string;
  onLeaderChange: (studentId: string) => void;
  onCreateTeam: () => void;
}

const groupInfo = {
  GROUP_1: TEAM_MAJOR_GROUPS.find((group) => group.key === 'GROUP_1'),
  GROUP_2: TEAM_MAJOR_GROUPS.find((group) => group.key === 'GROUP_2'),
};

interface RequirementItemProps {
  valid: boolean;
  label: string;
  detail: string;
  tooltip?: string;
  className?: string;
}

function RequirementItem({
  valid,
  label,
  detail,
  tooltip,
  className = '',
}: RequirementItemProps) {
  const Icon = valid ? CheckCircle2 : XCircle;

  return (
    <div className={`flex min-w-0 items-center gap-2.5 px-3 py-2 ${className}`}>
      <Icon
        aria-hidden="true"
        className={`h-4 w-4 shrink-0 ${valid ? 'text-emerald-600' : 'text-red-500'}`}
      />
      <div className="min-w-0 flex-1">
        <p className="truncate text-xs font-bold leading-4 text-slate-800">{label}</p>
        <p
          className="truncate text-[11px] font-medium leading-4 text-slate-500"
          title={tooltip}
        >
          {detail}
        </p>
      </div>
      <span className="sr-only">{valid ? 'Requirement satisfied' : 'Requirement not satisfied'}</span>
    </div>
  );
}

function getGroupDisplayName(groupKey: 'GROUP_1' | 'GROUP_2'): string {
  return groupInfo[groupKey]?.label || groupKey.replace('_', ' ');
}

function getEligibleGroupCodes(groupKey: 'GROUP_1' | 'GROUP_2'): string[] {
  return groupInfo[groupKey]?.majors.map((major) => major.code) || [];
}

function formatGroupDetail(
  validation: TeamSelectionValidation,
  groupKey: 'GROUP_1' | 'GROUP_2',
): string {
  const selectedCodes = validation.groupMajorCodes[groupKey];
  if (selectedCodes.length > 0) return selectedCodes.join(', ');

  const eligibleCount = getEligibleGroupCodes(groupKey).length;
  return `At least 1 required · ${eligibleCount} eligible majors`;
}

function formatLeaderName(student: TeamStudent): string {
  return student.rollNumber
    ? `${student.fullName} · ${student.rollNumber}`
    : student.fullName;
}

function formatMissingMajorWarning(count: number): string {
  return `${count} selected ${count === 1 ? 'student has' : 'students have'} no declared major`;
}

export default function TeamCreationSummary({
  selectedStudents,
  selectedLeaderId,
  onLeaderChange,
  onCreateTeam,
}: TeamCreationSummaryProps) {
  const validation = validateTeamSelection(selectedStudents, selectedLeaderId);
  const groupOneCodes = getEligibleGroupCodes('GROUP_1');
  const groupTwoCodes = getEligibleGroupCodes('GROUP_2');

  return (
    <section
      className="overflow-hidden rounded-xl border border-slate-200/90 bg-white shadow-xs"
      aria-label="Team formation"
    >
      <div className="grid lg:grid-cols-[minmax(0,1fr)_280px]">
        <div className="min-w-0 p-3">
          <div className="mb-2 flex items-center justify-between gap-3">
            <h2 className="text-xs font-bold uppercase tracking-wide text-slate-500">
              Team requirements
            </h2>
            <span className="text-[11px] font-medium text-slate-400">
              {validation.memberCount}/{TEAM_MEMBER_LIMIT} selected
            </span>
          </div>

          <div className="grid overflow-hidden rounded-lg border border-slate-200 bg-slate-50/60 sm:grid-cols-2">
            <RequirementItem
              valid={validation.isMemberCountValid}
              label="4–6 members"
              detail={validation.isMemberCountValid ? 'Team size is valid' : `${validation.memberCount} selected`}
              className="border-b border-slate-200 sm:border-r"
            />
            <RequirementItem
              valid={validation.hasGroupOne}
              label={getGroupDisplayName('GROUP_1')}
              detail={formatGroupDetail(validation, 'GROUP_1')}
              tooltip={`Eligible majors: ${groupOneCodes.join(', ')}`}
              className="border-b border-slate-200"
            />
            <RequirementItem
              valid={validation.hasGroupTwo}
              label={getGroupDisplayName('GROUP_2')}
              detail={formatGroupDetail(validation, 'GROUP_2')}
              tooltip={`Eligible majors: ${groupTwoCodes.join(', ')}`}
              className="border-b border-slate-200 sm:border-b-0 sm:border-r"
            />
            <RequirementItem
              valid={validation.isTeamLeaderValid}
              label="Team Leader"
              detail={validation.leaderStudent ? formatLeaderName(validation.leaderStudent) : 'Select from chosen members'}
            />
          </div>

          {validation.missingMajorStudents.length > 0 && (
            <p className="mt-2 flex items-center gap-1.5 text-[11px] font-semibold text-amber-700">
              <AlertTriangle aria-hidden="true" className="h-3.5 w-3.5 shrink-0 text-amber-500" />
              {formatMissingMajorWarning(validation.missingMajorStudents.length)}
            </p>
          )}
        </div>

        <div className="flex min-w-0 flex-col justify-center gap-2 border-t border-slate-200 bg-slate-50/35 p-3 lg:border-l lg:border-t-0">
          <label
            htmlFor="team-summary-leader"
            className="flex items-center gap-1.5 text-xs font-bold text-slate-700"
          >
            <Crown aria-hidden="true" className="h-3.5 w-3.5 text-amber-500" />
            Team Leader
            <span className="text-red-500" aria-hidden="true">*</span>
          </label>
          <select
            id="team-summary-leader"
            value={validation.isTeamLeaderValid ? selectedLeaderId : ''}
            onChange={(event) => onLeaderChange(event.target.value)}
            disabled={selectedStudents.length === 0}
            aria-invalid={!validation.isTeamLeaderValid}
            className="h-9 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/15 disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-400"
          >
            <option value="">Select leader</option>
            {selectedStudents.map((student) => (
              <option key={student._id} value={student._id}>
                {student.rollNumber ? `${student.fullName} · ${student.rollNumber}` : student.fullName}
              </option>
            ))}
          </select>

          <Button
            variant="primary"
            size="md"
            icon={validation.canCreateTeam ? UserPlus : undefined}
            disabled={!validation.canCreateTeam}
            onClick={onCreateTeam}
            className="h-9 w-full rounded-lg"
          >
            Create Team
          </Button>
        </div>
      </div>
    </section>
  );
}
