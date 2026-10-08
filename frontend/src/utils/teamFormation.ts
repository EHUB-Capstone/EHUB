import type { TeamFormation, TeamFormationInvitation, TeamInvitationStatus } from '../types/teamFormation.ts';

export const MAX_FORMATION_MEMBERS = 6;
export const MIN_FORMATION_MEMBERS = 4;
export const INVITATION_LIFETIME_MS = 24 * 60 * 60 * 1000;

/** Difference between the server clock and this device, so countdowns do not depend on the device clock. */
export function clockSkewMs(serverTimeUtc: string | undefined, clientNowMs: number): number {
  const server = serverTimeUtc ? Date.parse(serverTimeUtc) : Number.NaN;
  return Number.isNaN(server) ? 0 : server - clientNowMs;
}

/** Milliseconds left before the invitation expires; null when it has no deadline. */
export function remainingMs(expiresAtUtc: string | null | undefined, nowMs: number, skewMs = 0): number | null {
  if (!expiresAtUtc) return null;
  const expires = Date.parse(expiresAtUtc);
  if (Number.isNaN(expires)) return null;
  return Math.max(0, expires - (nowMs + skewMs));
}

/** hh:mm:ss, hours are not capped so 24h reads 24:00:00. */
export function formatCountdown(ms: number): string {
  const totalSeconds = Math.max(0, Math.ceil(ms / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return [hours, minutes, seconds].map((part) => String(part).padStart(2, '0')).join(':');
}

export type CountdownTone = 'normal' | 'warning' | 'danger' | 'expired';

export function countdownTone(ms: number): CountdownTone {
  if (ms <= 0) return 'expired';
  if (ms < 15 * 60 * 1000) return 'danger';
  if (ms < 60 * 60 * 1000) return 'warning';
  return 'normal';
}

/** Pending records past their deadline count as expired even before the server job flips them. */
export function effectiveInvitationStatus(
  invitation: Pick<TeamFormationInvitation, 'status' | 'expiresAtUtc'>,
  formationStatus: TeamFormation['status'],
  nowMs: number,
  skewMs = 0,
): TeamInvitationStatus {
  if (formationStatus === 'Pending' && invitation.status === 'Pending' && remainingMs(invitation.expiresAtUtc, nowMs, skewMs) === 0)
    return 'Expired';
  return invitation.status;
}

/** Latest record per student; the rest is history. */
export function currentInvitations(formation: Pick<TeamFormation, 'invitations'>): TeamFormationInvitation[] {
  return formation.invitations.filter((invitation) => invitation.isCurrent);
}

export function historyInvitations(formation: Pick<TeamFormation, 'invitations'>, studentId: string): TeamFormationInvitation[] {
  return formation.invitations.filter((invitation) => invitation.studentId === studentId && !invitation.isCurrent);
}

export function pendingCurrentInvitations(formation: TeamFormation, nowMs: number, skewMs = 0): TeamFormationInvitation[] {
  return currentInvitations(formation).filter((invitation) =>
    effectiveInvitationStatus(invitation, formation.status, nowMs, skewMs) === 'Pending');
}

/**
 * A pending formation the signed-in student still takes part in: they created it, or their latest
 * invitation is waiting or accepted. Declined, expired and left students are out of it.
 */
export function isOpenFormationForMe(formation: TeamFormation): boolean {
  if (formation.status !== 'Pending') return false;
  if (formation.creatorStudentId === formation.myStudentId) return true;
  const own = currentInvitations(formation).find((invitation) => invitation.studentId === formation.myStudentId);
  return own?.status === 'Pending' || own?.status === 'Accepted';
}

/** Slots still free for new invitations: pending and accepted members both count. */
export function remainingInviteSlots(formation: Pick<TeamFormation, 'activeCount'>): number {
  return Math.max(0, MAX_FORMATION_MEMBERS - formation.activeCount);
}

/** Re-invite is available for records that ended without joining. Accepted and pending are never re-invited. */
export function canReinvite(status: TeamInvitationStatus): boolean {
  return status === 'Declined' || status === 'Expired' || status === 'Left';
}
