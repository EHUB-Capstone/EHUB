export interface ProjectWorkspaceProfile {
  id: string;
  teamId: string;
  classId: string;
  subjectId: string;
  semesterId: string;
  projectName: string;
  description: string;
  problem: string;
  solution: string;
  targetUsers: string;
  keywords: string[];
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface ProjectWorkspaceMember {
  studentId: string;
  userId: string | { _id?: string; id?: string } | null;
  fullName: string;
  email?: string | null;
  roleInTeam: string;
}

export interface WorkspaceProjectProposal {
  id: string;
  teamName: string;
  projectName: string;
  projectDescription: string;
  status: string;
}

export interface ProjectWorkspaceDetail {
  team: {
    id?: string;
    _id?: string;
    teamName: string;
    leaderId: string | null;
  };
  class: {
    classCode: string;
    subjectCode: string;
    subjectName: string;
    semesterCode: string;
  };
  members: ProjectWorkspaceMember[];
  proposal: WorkspaceProjectProposal | null;
  project: ProjectWorkspaceProfile | null;
  activities: Array<{
    id: string;
    action: string;
    summary: string;
    actorName: string;
    occurredAtUtc: string;
  }>;
}
