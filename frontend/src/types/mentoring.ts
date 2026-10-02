export interface MentorProfile {
  id: string;
  userId: string;
  fullName: string;
  mentorType: 'Business' | 'IT' | 'Unspecified';
  expertise: string[];
  bio: string | null;
  experience: string | null;
  organization: string | null;
  linkedInUrl: string | null;
  portfolioUrl: string | null;
  cvFileName: string | null;
  portfolioFileName: string | null;
  maxTeams: number | null;
  status: string;
  activeTeamCount: number;
  totalAssignments: number;
  totalSessions: number;
  averageFeedbackRating: number | null;
}

export interface MentorRecommendation {
  mentor: MentorProfile;
  fitScore: number;
  reasons: string[];
  activeTeamCount: number;
  hasCapacity: boolean;
}

export interface MentoringActionItem {
  id: string;
  content: string;
  dueDate: string | null;
  completed: boolean;
}

export interface MentoringFeedback {
  id: string;
  rating: number;
  comment: string;
  createdAtUtc: string;
}

export interface MentoringSession {
  id: string;
  teamId: string;
  mentorAssignmentId: string;
  title: string;
  description: string | null;
  startAt: string;
  endAt: string;
  location: string | null;
  meetingUrl: string | null;
  status: 'Scheduled' | 'Completed' | 'Cancelled' | 'NoShow';
  notes: string | null;
  actionItems: MentoringActionItem[];
}

export interface SaveMentoringSession {
  mentorAssignmentId?: string;
  teamId: string;
  title: string;
  description: string;
  startAt: string;
  endAt: string;
  location: string;
  meetingUrl: string;
}
