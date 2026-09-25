export interface TeamFormationInvitation {
  studentId: string;
  fullName: string;
  rollNumber: string;
  status: 'Pending' | 'Accepted' | 'Declined';
  isCreator: boolean;
  isProposedLeader: boolean;
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
  invitations: TeamFormationInvitation[];
}

export interface CreateTeamFormationRequest {
  teamName: string;
  memberStudentIds: string[];
  leaderStudentId: string;
}
