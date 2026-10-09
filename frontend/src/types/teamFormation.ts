export type TeamInvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Expired' | 'Left';

export interface TeamFormationInvitation {
  id: string;
  studentId: string;
  fullName: string;
  rollNumber: string;
  status: TeamInvitationStatus;
  isCreator: boolean;
  isProposedLeader: boolean;
  /** True for the most recent record of this student; older records are history. */
  isCurrent: boolean;
  createdAtUtc: string;
  expiresAtUtc: string | null;
  respondedAtUtc: string | null;
}

export interface TeamFormation {
  id: string;
  classId: string;
  classCode: string;
  teamName: string;
  creatorStudentId: string;
  myStudentId: string;
  proposedLeaderStudentId: string;
  status: 'Pending' | 'Completed' | 'Cancelled';
  completedTeamId: string | null;
  createdAtUtc: string;
  /** Server clock at response time; used to correct invitation countdowns for client clock skew. */
  serverTimeUtc: string;
  acceptedCount: number;
  pendingCount: number;
  activeCount: number;
  canFinalize: boolean;
  finalizeBlockers: string[];
  /** True when the proposed leader is not an accepted member, so the creator must pick one to finalize. */
  requiresLeaderSelection: boolean;
  /** Includes history: several records per student, oldest first. */
  invitations: TeamFormationInvitation[];
}

export interface CreateTeamFormationRequest {
  teamName: string;
  /** Students to invite; the creator is added automatically and must not be listed. */
  inviteeStudentIds: string[];
  leaderStudentId: string;
}

export interface FinalizeTeamFormationRequest {
  leaderStudentId?: string;
  confirmPendingInvitations: boolean;
}
