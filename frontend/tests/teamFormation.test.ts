import assert from 'node:assert/strict';
import test from 'node:test';
import type { TeamFormation, TeamFormationInvitation } from '../src/types/teamFormation.ts';
import {
  canReinvite,
  clockSkewMs,
  countdownTone,
  currentInvitations,
  effectiveInvitationStatus,
  formatCountdown,
  historyInvitations,
  isOpenFormationForMe,
  remainingInviteSlots,
  remainingMs,
} from '../src/utils/teamFormation.ts';

const HOUR = 60 * 60 * 1000;

function invitation(overrides: Partial<TeamFormationInvitation>): TeamFormationInvitation {
  return {
    id: 'i', studentId: 's', fullName: 'Student', rollNumber: 'SE1', status: 'Pending',
    isCreator: false, isProposedLeader: false, isCurrent: true,
    createdAtUtc: '2026-01-01T00:00:00.000Z', expiresAtUtc: null, respondedAtUtc: null,
    ...overrides,
  };
}

function formation(overrides: Partial<TeamFormation>): TeamFormation {
  return {
    id: 'f', classId: 'c', classCode: 'C1', teamName: 'Team', creatorStudentId: 'creator', myStudentId: 'me',
    proposedLeaderStudentId: 'creator', status: 'Pending', completedTeamId: null,
    createdAtUtc: '2026-01-01T00:00:00.000Z', serverTimeUtc: '2026-01-01T00:00:00.000Z',
    acceptedCount: 1, pendingCount: 0, activeCount: 1, canFinalize: false, finalizeBlockers: [],
    requiresLeaderSelection: false, invitations: [],
    ...overrides,
  };
}

test('countdown formats hours, minutes and seconds and never goes negative', () => {
  assert.equal(formatCountdown(24 * HOUR), '24:00:00');
  assert.equal(formatCountdown(HOUR + 2 * 60 * 1000 + 3000), '01:02:03');
  assert.equal(formatCountdown(999), '00:00:01');
  assert.equal(formatCountdown(-5000), '00:00:00');
});

test('countdown uses the server clock, so a wrong device clock does not change the time left', () => {
  const server = Date.parse('2026-01-01T12:00:00.000Z');
  const deviceNow = server - 3 * HOUR;
  const skew = clockSkewMs('2026-01-01T12:00:00.000Z', deviceNow);
  assert.equal(skew, 3 * HOUR);
  const expiresAt = '2026-01-02T12:00:00.000Z';
  assert.equal(remainingMs(expiresAt, deviceNow, skew), 24 * HOUR);
  assert.equal(remainingMs(expiresAt, deviceNow + HOUR, skew), 23 * HOUR);
  assert.equal(remainingMs(expiresAt, deviceNow + 30 * HOUR, skew), 0);
  assert.equal(remainingMs(null, deviceNow), null);
  assert.equal(clockSkewMs(undefined, deviceNow), 0);
});

test('countdown tone warns under one hour and under fifteen minutes', () => {
  assert.equal(countdownTone(2 * HOUR), 'normal');
  assert.equal(countdownTone(59 * 60 * 1000), 'warning');
  assert.equal(countdownTone(10 * 60 * 1000), 'danger');
  assert.equal(countdownTone(0), 'expired');
});

test('overdue pending invitations read as expired, but only on pending formations', () => {
  const now = Date.parse('2026-01-02T00:00:00.000Z');
  const overdue = { status: 'Pending' as const, expiresAtUtc: '2026-01-01T23:59:00.000Z' };
  const waiting = { status: 'Pending' as const, expiresAtUtc: '2026-01-02T01:00:00.000Z' };
  assert.equal(effectiveInvitationStatus(overdue, 'Pending', now), 'Expired');
  assert.equal(effectiveInvitationStatus(waiting, 'Pending', now), 'Pending');
  assert.equal(effectiveInvitationStatus(overdue, 'Completed', now), 'Pending');
  assert.equal(effectiveInvitationStatus({ status: 'Accepted', expiresAtUtc: null }, 'Pending', now), 'Accepted');
});

test('only declined, expired and left invitations can be re-invited', () => {
  assert.equal(canReinvite('Declined'), true);
  assert.equal(canReinvite('Expired'), true);
  assert.equal(canReinvite('Left'), true);
  assert.equal(canReinvite('Accepted'), false);
  assert.equal(canReinvite('Pending'), false);
});

test('invite slots count pending and accepted members against the limit of six', () => {
  assert.equal(remainingInviteSlots({ activeCount: 2 }), 4);
  assert.equal(remainingInviteSlots({ activeCount: 6 }), 0);
  assert.equal(remainingInviteSlots({ activeCount: 9 }), 0);
});

test('history keeps older records of a student while the list shows only the current one', () => {
  const data = formation({
    invitations: [
      invitation({ id: 'old', studentId: 'a', status: 'Declined', isCurrent: false }),
      invitation({ id: 'new', studentId: 'a', status: 'Pending', isCurrent: true }),
      invitation({ id: 'b1', studentId: 'b', status: 'Accepted', isCurrent: true }),
    ],
  });
  assert.deepEqual(currentInvitations(data).map((item) => item.id), ['new', 'b1']);
  assert.deepEqual(historyInvitations(data, 'a').map((item) => item.id), ['old']);
  assert.deepEqual(historyInvitations(data, 'b'), []);
});

test('students who declined, expired or left are no longer part of the open formation', () => {
  const withOwn = (status: TeamFormationInvitation['status']) => formation({
    invitations: [invitation({ studentId: 'me', status })],
  });
  assert.equal(isOpenFormationForMe(withOwn('Pending')), true);
  assert.equal(isOpenFormationForMe(withOwn('Accepted')), true);
  assert.equal(isOpenFormationForMe(withOwn('Declined')), false);
  assert.equal(isOpenFormationForMe(withOwn('Expired')), false);
  assert.equal(isOpenFormationForMe(withOwn('Left')), false);
  assert.equal(isOpenFormationForMe({ ...withOwn('Accepted'), status: 'Cancelled' }), false);
  assert.equal(isOpenFormationForMe(formation({ myStudentId: 'creator', invitations: [] })), true);
});
