import assert from 'node:assert/strict';
import test from 'node:test';
import type { AccountApprovalRequest } from '../src/types/accountApproval.ts';
import {
  applyApprovalDecision,
  filterApprovalRequests,
  getApprovalStats,
  normalizeApprovalStatus,
  registrationToApprovalRequest,
  validateRejectionReason,
} from '../src/utils/accountApproval.ts';

const requests: AccountApprovalRequest[] = [
  {
    id: 'lecturer-1',
    fullName: 'Nguyen Minh Anh',
    email: 'anh@fpt.edu.vn',
    role: 'LECTURER',
    status: 'PENDING',
    submittedAt: '2026-07-20T08:00:00.000Z',
    department: 'Software Engineering',
    expertise: 'Product Management',
  },
  {
    id: 'mentor-1',
    fullName: 'Tran Bao Chau',
    email: 'chau@startup.vn',
    role: 'MENTOR',
    status: 'APPROVED',
    submittedAt: '2026-07-19T08:00:00.000Z',
    institution: 'LaunchPad Vietnam',
    expertise: 'Growth, Fundraising',
  },
];

test('normalizes backend-style account statuses for the approval UI', () => {
  assert.equal(normalizeApprovalStatus('PendingApproval'), 'PENDING');
  assert.equal(normalizeApprovalStatus('Active'), 'APPROVED');
  assert.equal(normalizeApprovalStatus('Denied'), 'REJECTED');
});

test('maps Lecturer and Mentor registrations into typed approval requests', () => {
  const result = registrationToApprovalRequest({
    id: 'mentor-2',
    fullName: 'Le Gia Huy',
    email: 'huy@example.com',
    roles: ['MENTOR'],
    status: 'PendingApproval',
    expertise: ['FinTech', 'Business Model'],
  });

  assert.equal(result?.role, 'MENTOR');
  assert.equal(result?.status, 'PENDING');
  assert.equal(result?.expertise, 'FinTech, Business Model');
});

test('ignores student registrations in the staff approval workflow', () => {
  const result = registrationToApprovalRequest({
    id: 'student-1',
    fullName: 'Student User',
    email: 'student@example.com',
    role: 'STUDENT',
  });

  assert.equal(result, null);
});

test('approves a request locally without changing other records', () => {
  const result = applyApprovalDecision(requests, {
    requestId: 'lecturer-1',
    status: 'APPROVED',
    reviewedAt: '2026-07-22T09:00:00.000Z',
  });

  assert.equal(result[0].status, 'APPROVED');
  assert.equal(result[0].reviewedAt, '2026-07-22T09:00:00.000Z');
  assert.equal(result[1], requests[1]);
});

test('stores the rejection reason in frontend approval state', () => {
  const result = applyApprovalDecision(requests, {
    requestId: 'lecturer-1',
    status: 'REJECTED',
    reason: 'Professional information could not be verified.',
  });

  assert.equal(result[0].status, 'REJECTED');
  assert.equal(result[0].rejectionReason, 'Professional information could not be verified.');
});

test('requires a clear rejection reason', () => {
  assert.equal(validateRejectionReason('Too short'), 'Please provide a reason of at least 10 characters.');
  assert.equal(validateRejectionReason('The submitted professional information could not be verified.'), null);
});

test('filters requests by status, role and search text', () => {
  const result = filterApprovalRequests(requests, {
    status: 'PENDING',
    role: 'LECTURER',
    search: 'product',
  });

  assert.deepEqual(result.map((request) => request.id), ['lecturer-1']);
});

test('calculates approval dashboard statistics', () => {
  assert.deepEqual(getApprovalStats(requests), {
    total: 2,
    pending: 1,
    lecturers: 1,
    mentors: 1,
    approved: 1,
    rejected: 0,
  });
});
