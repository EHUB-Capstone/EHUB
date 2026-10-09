import type { TeamFormation, TeamFormationInvitation } from '../types/teamFormation.ts';
import { MAX_FORMATION_MEMBERS, MIN_FORMATION_MEMBERS } from '../utils/teamFormation.ts';
import { getMockState } from './mockHelpers.ts';

const GROUP_ONE = new Set(['BBA_HM', 'BBA_FIN', 'BBA_IB', 'BBA_MC', 'BBA_MKT', 'BEN', 'BBA_TM']);
const GROUP_TWO = new Set(['BIT_AI', 'BIT_GD', 'BIT_IA', 'BIT_SE']);

/** Pending invitations past their deadline become Expired, mirroring the backend expiry job. */
export function sweepExpiredInvitations(nowMs = Date.now()): void {
  for (const formation of getMockState().formations) {
    if (formation.status !== 'Pending') continue;
    for (const invitation of formation.invitations) {
      if (invitation.status === 'Pending' && invitation.expiresAtUtc && Date.parse(invitation.expiresAtUtc) <= nowMs)
        invitation.status = 'Expired';
    }
  }
}

export function latestRecord(formation: TeamFormation, studentId: string): TeamFormationInvitation | undefined {
  const records = formation.invitations.filter((invitation) => invitation.studentId === studentId);
  return records.reduce<TeamFormationInvitation | undefined>((latest, record) =>
    !latest || Date.parse(record.createdAtUtc) >= Date.parse(latest.createdAtUtc) ? record : latest, undefined);
}

/** Pending and accepted records of an open formation hold the student's reservation. */
export function activeRecord(formation: TeamFormation, studentId: string): TeamFormationInvitation | undefined {
  if (formation.status !== 'Pending') return undefined;
  const record = latestRecord(formation, studentId);
  return record && (record.status === 'Pending' || record.status === 'Accepted') ? record : undefined;
}

export function activeFormationFor(classId: string, studentId: string, excludedFormationId = ''): TeamFormation | undefined {
  return getMockState().formations.find((formation) =>
    formation.classId === classId && formation.id !== excludedFormationId && Boolean(activeRecord(formation, studentId)));
}

export function activeRecords(formation: TeamFormation): TeamFormationInvitation[] {
  const studentIds = [...new Set(formation.invitations.map((invitation) => invitation.studentId))];
  return studentIds.map((studentId) => activeRecord(formation, studentId)).filter(Boolean) as TeamFormationInvitation[];
}

/** DTO the backend would return for this student: computed counts, blockers and per-student `isCurrent`. */
export function presentFormation(formation: TeamFormation, myStudentId: string): TeamFormation {
  const roster = getMockState().rosters[formation.classId] || [];
  const active = activeRecords(formation);
  const accepted = active.filter((invitation) => invitation.status === 'Accepted');
  const majors = accepted.map((invitation) =>
    roster.find((student) => student.studentId === invitation.studentId)?.majorCode?.toUpperCase() || '');
  const blockers: string[] = [];
  if (formation.status === 'Pending') {
    if (accepted.length < MIN_FORMATION_MEMBERS) blockers.push(`At least ${MIN_FORMATION_MEMBERS} accepted members are required.`);
    else if (accepted.length > MAX_FORMATION_MEMBERS) blockers.push(`At most ${MAX_FORMATION_MEMBERS} members are allowed.`);
    else if (!majors.some((major) => GROUP_ONE.has(major)) || !majors.some((major) => GROUP_TWO.has(major)))
      blockers.push('Team must include at least one BBA and one BIT student.');
  }
  const latestIds = new Set(
    [...new Set(formation.invitations.map((invitation) => invitation.studentId))]
      .map((studentId) => latestRecord(formation, studentId)?.id),
  );
  return {
    ...formation,
    myStudentId,
    serverTimeUtc: new Date().toISOString(),
    acceptedCount: accepted.length,
    pendingCount: active.length - accepted.length,
    activeCount: active.length,
    canFinalize: formation.status === 'Pending' && blockers.length === 0,
    finalizeBlockers: blockers,
    requiresLeaderSelection: formation.status === 'Pending'
      && !accepted.some((invitation) => invitation.studentId === formation.proposedLeaderStudentId),
    invitations: formation.invitations.map((invitation) => ({ ...invitation, isCurrent: latestIds.has(invitation.id) })),
  };
}
