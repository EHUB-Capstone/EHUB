export type MentorProfileStatus = 'Active' | 'Inactive' | 'Unavailable';

export interface MentorProfile {
  id: string;
  userId: string;
  fullName: string;
  email: string;
  phone?: string | null;
  avatarUrl?: string | null;
  /** Enterprise = enterprise mentor, Academic = lecturer mentor. Fixed once the mentor is created. */
  mentorType: 'Enterprise' | 'Academic';
  status: MentorProfileStatus;
  expertise: string[];
  bio?: string | null;
  availabilityNote?: string | null;
  organization?: string | null;
  department?: string | null;
  jobTitle?: string | null;
  contractType?: string | null;
  educationLevel?: string | null;
  currentAddress?: string | null;
  linkedInUrl?: string | null;
  fptEmail?: string | null;
  /** yyyy-MM-dd */
  dateOfBirth?: string | null;
  activeTeamCount: number;
  rowVersion: string;
}

export interface UpdateMentorProfilePayload {
  rowVersion: string;
  status: MentorProfileStatus;
  expertise: string[];
  bio: string | null;
  availabilityNote: string | null;
  organization: string | null;
  department: string | null;
  jobTitle: string | null;
  contractType: string | null;
  educationLevel: string | null;
  currentAddress: string | null;
  linkedInUrl: string | null;
  fptEmail: string | null;
  dateOfBirth: string | null;
}
