import type { ClassDto, ClassRosterStudent, ClassStatus } from '../types/classes.ts';

export interface MockUser {
  id: string;
  _id: string;
  name: string;
  email: string;
  avatar: string | null;
  role: 'ADMIN' | 'LECTURER' | 'MENTOR' | 'STUDENT';
  status: 'PENDING' | 'APPROVED' | 'REJECTED' | 'BLOCKED' | 'INACTIVE';
  studentId: string | null;
  programGroup: string | null;
  major: string | null;
  phone: string | null;
  createdAt: string;
  lastSeen: string | null;
}

export interface MockSubject {
  _id: string;
  subjectCode: string;
  subjectName: string;
  status: 'active' | 'disabled';
}

export interface MockSemester {
  id: string;
  semester: 'SP' | 'SU' | 'FA';
  year: number;
  status: 'Planned' | 'Active' | 'Completed' | 'Archived';
  startDate: string | null;
  endDate: string | null;
  completedAtUtc: string | null;
  completionReason: string | null;
  rowVersion: string;
}

export interface MockRoadmapItem {
  _id: string;
  title: string;
  description: string | null;
  taskType: string;
  courseCode: string;
  weekNumber: number;
  priority: string;
  estimatedHours: number | null;
  tags: string[];
}

export interface MockRubricCriterion {
  _id: string;
  name: string;
  description: string | null;
  maxScore: number;
  weight: number;
  displayOrder: number;
}

export interface MockRubric {
  _id: string;
  name: string;
  description: string | null;
  status: string;
  totalWeight: number;
  checkpointNumber: number | null;
  criteria: MockRubricCriterion[];
}

export interface MockCheckpoint {
  number: number;
  title: string;
  shortDescription: string | null;
  requirements: string[];
  rubrics: Array<Record<string, unknown>>;
}

export interface MockCurriculum {
  roadmapItems: MockRoadmapItem[];
  rubrics: MockRubric[];
  checkpoints: MockCheckpoint[];
}

export interface MockClass extends ClassDto {
  previousStatus: Exclude<ClassStatus, 'Archived'>;
}

export interface MockRosterStudent extends ClassRosterStudent {
  userId: string | null;
}

export interface MockTeamMember {
  studentId: string;
  rollNumber: string;
  fullName: string;
  email: string | null;
  majorCode: string;
  roleInTeam: 'LEADER' | 'MEMBER';
  joinedAtUtc: string;
}

export interface MockMentor {
  mentorProfileId: string;
  userId: string;
  fullName: string;
  email: string;
  organization: string | null;
}

export interface MockMentorAssignment {
  assignmentId: string;
  teamId: string;
  teamName: string;
  classId: string;
  mentor: MockMentor;
  status: 'Active' | 'Ended';
  assignedAtUtc: string;
  endedAtUtc: string | null;
  note: string | null;
}

export interface MockTeam {
  id: string;
  classId: string;
  teamCode: string;
  teamName: string;
  description: string | null;
  status: string;
  leaderId: string | null;
  members: MockTeamMember[];
  currentMentorAssignment: MockMentorAssignment | null;
  rowVersion: string;
}

export interface MockProposalMember {
  studentId: string;
  rollNumber: string;
  fullName: string;
  majorCode: string;
  isLeader: boolean;
}

export interface MockProposalHistory {
  id: string;
  fromStatus: string | null;
  toStatus: string;
  action: string;
  comment: string | null;
  performedByUserId: string;
  occurredAtUtc: string;
}

export interface MockProposal {
  id: string;
  classId: string;
  teamName: string;
  description: string | null;
  projectName: string | null;
  status: string;
  latestReviewComment: string | null;
  approvedTeamId: string | null;
  members: MockProposalMember[];
  rowVersion: string;
  history: MockProposalHistory[];
}

export interface MockDirectionReview {
  id: string;
  fromStatus: string;
  toStatus: string;
  comment: string;
  reviewedByUserId: string;
  occurredAtUtc: string;
}

export interface MockProjectDirection {
  id: string;
  teamId: string;
  title: string;
  summary: string;
  status: string;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  rowVersion: string;
  reviews: MockDirectionReview[];
}

export interface MockAuditEntry {
  id: string;
  action: string;
  performedByUserId: string;
  performedByName: string;
  occurredAtUtc: string;
  detailsJson: string | null;
}

export interface MockImportSession {
  classId: string;
  consumed: boolean;
  rows: Array<{
    rowNumber: number;
    studentCode: string;
    fullName: string;
    email: string;
    majorCode: string;
    isValid: boolean;
    status: string;
    errorMessage: string | null;
  }>;
}

export interface MockApiState {
  sequence: number;
  sessionUserId: string | null;
  authPasswords: Record<string, string>;
  currentSemester: { semester: 'SP' | 'SU' | 'FA'; year: number } | null;
  semesters: MockSemester[];
  users: MockUser[];
  subjects: MockSubject[];
  curricula: Record<string, MockCurriculum>;
  classes: MockClass[];
  rosters: Record<string, MockRosterStudent[]>;
  teams: MockTeam[];
  proposals: MockProposal[];
  directions: MockProjectDirection[];
  audits: Record<string, MockAuditEntry[]>;
  imports: Record<string, MockImportSession>;
}

const id = (value: number) => `00000000-0000-4000-8000-${String(value).padStart(12, '0')}`;
const now = new Date();
const isoAgo = (days: number, minutes = 0) =>
  new Date(now.getTime() - days * 86_400_000 - minutes * 60_000).toISOString();

const users: MockUser[] = [
  { id: id(1), _id: id(1), name: 'Nguyễn Minh Admin', email: 'admin@ehub.local', avatar: null, role: 'ADMIN', status: 'APPROVED', studentId: null, programGroup: null, major: null, phone: '0901000001', createdAt: isoAgo(180), lastSeen: isoAgo(0, 1) },
  { id: id(2), _id: id(2), name: 'Trần Thu Giang', email: 'giang.lecturer@ehub.local', avatar: null, role: 'LECTURER', status: 'APPROVED', studentId: null, programGroup: null, major: null, phone: '0901000002', createdAt: isoAgo(150), lastSeen: isoAgo(0, 3) },
  { id: id(3), _id: id(3), name: 'Lê Hoàng Nam', email: 'nam.lecturer@ehub.local', avatar: null, role: 'LECTURER', status: 'APPROVED', studentId: null, programGroup: null, major: null, phone: '0901000003', createdAt: isoAgo(130), lastSeen: isoAgo(1) },
  { id: id(4), _id: id(4), name: 'Phạm Anh Khoa', email: 'khoa.mentor@ehub.local', avatar: null, role: 'MENTOR', status: 'APPROVED', studentId: null, programGroup: null, major: null, phone: '0901000004', createdAt: isoAgo(120), lastSeen: isoAgo(0, 9) },
  { id: id(5), _id: id(5), name: 'Võ Hải Yến', email: 'yen.mentor@ehub.local', avatar: null, role: 'MENTOR', status: 'PENDING', studentId: null, programGroup: null, major: null, phone: '0901000005', createdAt: isoAgo(2), lastSeen: null },
  { id: id(6), _id: id(6), name: 'Blocked Mentor', email: 'blocked.mentor@ehub.local', avatar: null, role: 'MENTOR', status: 'BLOCKED', studentId: null, programGroup: null, major: null, phone: null, createdAt: isoAgo(40), lastSeen: isoAgo(20) },
  { id: id(7), _id: id(7), name: 'Inactive Lecturer', email: 'inactive.lecturer@ehub.local', avatar: null, role: 'LECTURER', status: 'INACTIVE', studentId: null, programGroup: null, major: null, phone: null, createdAt: isoAgo(80), lastSeen: isoAgo(30) },
  { id: id(8), _id: id(8), name: 'Rejected Mentor', email: 'rejected.mentor@ehub.local', avatar: null, role: 'MENTOR', status: 'REJECTED', studentId: null, programGroup: null, major: null, phone: null, createdAt: isoAgo(10), lastSeen: null },
  ...[
    ['Nguyễn Gia Huy', 'SE200001', 'BIT', 'BIT_SE'],
    ['Trần Minh Anh', 'IB200002', 'BBA', 'BBA_IB'],
    ['Lê Quốc Bảo', 'AI200003', 'BIT', 'BIT_AI'],
    ['Phạm Khánh Linh', 'MK200004', 'BBA', 'BBA_MKT'],
    ['Đỗ Hoàng Long', 'SE200005', 'BIT', 'BIT_SE'],
    ['Vũ Ngọc Mai', 'IB200006', 'BBA', 'BBA_IB'],
    ['Bùi Đức Anh', 'AI200007', 'BIT', 'BIT_AI'],
    ['Hoàng Thảo Vy', 'MK200008', 'BBA', 'BBA_MKT'],
    ['Đặng Nhật Minh', 'SE200009', 'BIT', 'BIT_SE'],
    ['Ngô Thanh Hà', 'IB200010', 'BBA', 'BBA_IB'],
    ['Đinh Tuấn Kiệt', 'SE200011', 'BIT', 'BIT_SE'],
    ['Mai Quỳnh Chi', 'MK200012', 'BBA', 'BBA_MKT'],
  ].map(([name, studentId, programGroup, major], index): MockUser => ({
    id: id(10 + index),
    _id: id(10 + index),
    name,
    email: `${studentId.toLowerCase()}@fpt.edu.vn`,
    avatar: null,
    role: 'STUDENT',
    status: 'APPROVED',
    studentId,
    programGroup,
    major,
    phone: null,
    createdAt: isoAgo(90 - index),
    lastSeen: index < 3 ? isoAgo(0, index + 2) : isoAgo(index),
  })),
];

const subjects: MockSubject[] = [
  { _id: id(101), subjectCode: 'EXE101', subjectName: 'Experiential Entrepreneurship', status: 'active' },
  { _id: id(102), subjectCode: 'SSG104', subjectName: 'Startup Project Development', status: 'active' },
  { _id: id(103), subjectCode: 'BUS101', subjectName: 'Business Fundamentals', status: 'active' },
  { _id: id(104), subjectCode: 'LEGACY01', subjectName: 'Legacy Incubation Lab', status: 'disabled' },
];

const curriculumFor = (subject: MockSubject): MockCurriculum => ({
  roadmapItems: [
    { _id: id(200 + Number(subject._id.slice(-2))), title: 'Problem discovery', description: 'Interview target users and validate the problem.', taskType: 'COURSE_TEMPLATE', courseCode: subject.subjectCode, weekNumber: 1, priority: 'HIGH', estimatedHours: 6, tags: ['discovery', 'interview'] },
    { _id: id(220 + Number(subject._id.slice(-2))), title: 'Solution prototype', description: 'Build and test a focused prototype.', taskType: 'COURSE_TEMPLATE', courseCode: subject.subjectCode, weekNumber: 4, priority: 'MEDIUM', estimatedHours: 10, tags: ['prototype'] },
  ],
  rubrics: [{
    _id: id(250 + Number(subject._id.slice(-2))),
    name: 'Checkpoint 1 rubric',
    description: 'Problem validation and evidence quality.',
    status: 'ACTIVE',
    totalWeight: 100,
    checkpointNumber: 1,
    criteria: [
      { _id: id(280 + Number(subject._id.slice(-2))), name: 'Problem clarity', description: 'The problem is specific and evidence-backed.', maxScore: 10, weight: 50, displayOrder: 1 },
      { _id: id(300 + Number(subject._id.slice(-2))), name: 'Customer evidence', description: 'Insights are supported by interviews.', maxScore: 10, weight: 50, displayOrder: 2 },
    ],
  }],
  checkpoints: [{
    number: 1,
    title: 'Problem validation',
    shortDescription: 'Validate a meaningful customer problem.',
    requirements: ['Interview at least five target users', 'Submit an insight summary'],
    rubrics: [{ key: 'problem-clarity', label: 'Problem clarity', description: 'Clear problem statement', weight: 50, levels: [] }],
  }],
});

const classIds = { active: id(401), draft: id(402), archived: id(403) };

const classes: MockClass[] = [
  { id: classIds.active, slug: 'fa2026-exe101-1', classCode: 'EXE101-FA26-01', classIndex: 1, courseId: id(101), subjectCode: 'EXE101', subjectName: 'Experiential Entrepreneurship', semesterId: id(501), semesterCode: 'FA2026', year: 2026, primaryLecturerId: id(2), primaryLecturerName: 'Trần Thu Giang', primaryLecturerEmail: 'giang.lecturer@ehub.local', room: 'P.301', schedules: [{ dayOfWeek: 2, slotNumber: 2, room: 'P.301' }, { dayOfWeek: 5, slotNumber: 3, room: 'P.305' }], isEnrollmentMajorLocked: false, status: 'Active', previousStatus: 'Active', studentCount: 10, teamCount: 2, mentors: [{ mentorProfileId: id(4), userId: id(4), fullName: 'Phạm Anh Khoa', email: 'khoa.mentor@ehub.local' }], createdAtUtc: isoAgo(45), rowVersion: 'rv-1' },
  { id: classIds.draft, slug: 'fa2026-ssg104-2', classCode: 'SSG104-FA26-02', classIndex: 2, courseId: id(102), subjectCode: 'SSG104', subjectName: 'Startup Project Development', semesterId: id(501), semesterCode: 'FA2026', year: 2026, primaryLecturerId: null, primaryLecturerName: null, primaryLecturerEmail: null, room: 'P.204', schedules: [], isEnrollmentMajorLocked: false, status: 'Draft', previousStatus: 'Draft', studentCount: 4, teamCount: 0, mentors: [], createdAtUtc: isoAgo(30), rowVersion: 'rv-2' },
  { id: classIds.archived, slug: 'sp2026-bus101-1', classCode: 'BUS101-SP26-01', classIndex: 1, courseId: id(103), subjectCode: 'BUS101', subjectName: 'Business Fundamentals', semesterId: id(502), semesterCode: 'SP2026', year: 2026, primaryLecturerId: id(3), primaryLecturerName: 'Lê Hoàng Nam', primaryLecturerEmail: 'nam.lecturer@ehub.local', room: 'P.102', schedules: [{ dayOfWeek: 3, slotNumber: 1, room: null }], isEnrollmentMajorLocked: false, status: 'Archived', previousStatus: 'Completed', studentCount: 3, teamCount: 0, mentors: [], createdAtUtc: isoAgo(160), completedAtUtc: isoAgo(105), completionReason: 'Spring term completed', rowVersion: 'rv-3' },
];

const rosterStudent = (user: MockUser, index: number, teamId: string | null): MockRosterStudent => ({
  studentId: user.id,
  userId: user.id,
  rollNumber: user.studentId || `MOCK${index}`,
  fullName: user.name,
  email: user.email,
  majorCode: user.major,
  profileMajorCode: user.major,
  majorVerificationStatus: index % 3 === 0 ? 'Verified' : 'Unverified',
  memberCode: `MEM-${String(index).padStart(3, '0')}`,
  enrollmentStatus: 'Active',
  teamId,
  teamName: teamId === id(601) ? 'Phoenix Founders' : teamId === id(602) ? 'GreenByte' : null,
  isTeamLeader: (teamId === id(601) && index === 1) || (teamId === id(602) && index === 5),
  joinedAtUtc: isoAgo(40 - index),
});

const studentUsers = users.filter((user) => user.role === 'STUDENT');
const activeRoster = studentUsers.slice(0, 10).map((user, index) =>
  rosterStudent(user, index + 1, index < 4 ? id(601) : index < 8 ? id(602) : null));
const draftRoster = studentUsers.slice(8, 12).map((user, index) => rosterStudent(user, index + 20, null));
const archivedRoster = studentUsers.slice(0, 3).map((user, index) => ({
  ...rosterStudent(user, index + 30, null),
  enrollmentStatus: 'Completed',
}));

const semesters: MockSemester[] = [
  {
    id: id(501), semester: 'FA', year: 2026, status: 'Active',
    startDate: '2026-09-01', endDate: '2026-12-31',
    completedAtUtc: null, completionReason: null, rowVersion: 'rv-semester-1',
  },
  {
    id: id(502), semester: 'SP', year: 2026, status: 'Completed',
    startDate: '2026-01-01', endDate: '2026-04-30',
    completedAtUtc: isoAgo(100), completionReason: 'Academic term completed', rowVersion: 'rv-semester-2',
  },
];

const memberFromRoster = (student: MockRosterStudent, leaderId: string): MockTeamMember => ({
  studentId: student.studentId,
  rollNumber: student.rollNumber,
  fullName: student.fullName,
  email: student.email,
  majorCode: student.majorCode || '',
  roleInTeam: student.studentId === leaderId ? 'LEADER' : 'MEMBER',
  joinedAtUtc: student.joinedAtUtc,
});

const mentor: MockMentor = { mentorProfileId: id(4), userId: id(4), fullName: 'Phạm Anh Khoa', email: 'khoa.mentor@ehub.local', organization: 'E-HUB Ventures' };
const teams: MockTeam[] = [
  { id: id(601), classId: classIds.active, teamCode: 'EXE-T01', teamName: 'Phoenix Founders', description: 'Marketplace for trusted student services.', status: 'Active', leaderId: activeRoster[0].studentId, members: activeRoster.slice(0, 4).map((student) => memberFromRoster(student, activeRoster[0].studentId)), currentMentorAssignment: { assignmentId: id(701), teamId: id(601), teamName: 'Phoenix Founders', classId: classIds.active, mentor, status: 'Active', assignedAtUtc: isoAgo(20), endedAtUtc: null, note: 'Focus on customer validation.' }, rowVersion: 'rv-10' },
  { id: id(602), classId: classIds.active, teamCode: 'EXE-T02', teamName: 'GreenByte', description: 'Smart energy insights for small offices.', status: 'Active', leaderId: activeRoster[4].studentId, members: activeRoster.slice(4, 8).map((student) => memberFromRoster(student, activeRoster[4].studentId)), currentMentorAssignment: null, rowVersion: 'rv-11' },
];

const initialMockState: MockApiState = {
    sequence: 1_000,
    sessionUserId: null,
    authPasswords: {},
    currentSemester: { semester: 'FA', year: 2026 },
    semesters,
    users,
    subjects,
    curricula: Object.fromEntries(subjects.map((subject) => [subject.subjectCode, curriculumFor(subject)])),
    classes,
    rosters: { [classIds.active]: activeRoster, [classIds.draft]: draftRoster, [classIds.archived]: archivedRoster },
    teams,
    proposals: [{
      id: id(801), classId: classIds.active, teamName: 'Nova Crew', description: 'Draft proposal from unassigned students.', projectName: 'Campus Loop', status: 'Pending', latestReviewComment: null, approvedTeamId: null,
      members: activeRoster.slice(8, 10).map((student, index) => ({ studentId: student.studentId, rollNumber: student.rollNumber, fullName: student.fullName, majorCode: student.majorCode || '', isLeader: index === 0 })),
      rowVersion: 'rv-20', history: [{ id: id(802), fromStatus: 'Draft', toStatus: 'Pending', action: 'SUBMITTED', comment: null, performedByUserId: activeRoster[8].userId || activeRoster[8].studentId, occurredAtUtc: isoAgo(1) }],
    }],
    directions: [{ id: id(901), teamId: id(601), title: 'Student Services Marketplace', summary: 'Validate trust, fulfillment time, and willingness to pay before building the full marketplace.', status: 'Submitted', submittedAtUtc: isoAgo(2), reviewedAtUtc: null, rowVersion: 'rv-30', reviews: [] }],
    audits: {
      [classIds.active]: [{ id: id(951), action: 'CLASS_CREATED', performedByUserId: id(1), performedByName: 'Nguyễn Minh Admin', occurredAtUtc: isoAgo(45), detailsJson: JSON.stringify({ status: 'Active' }) }],
      [classIds.draft]: [{ id: id(952), action: 'CLASS_CREATED', performedByUserId: id(2), performedByName: 'Trần Thu Giang', occurredAtUtc: isoAgo(30), detailsJson: JSON.stringify({ status: 'Draft' }) }],
      [classIds.archived]: [{ id: id(953), action: 'CLASS_ARCHIVED', performedByUserId: id(1), performedByName: 'Nguyễn Minh Admin', occurredAtUtc: isoAgo(5), detailsJson: JSON.stringify({ reason: 'Semester completed' }) }],
    },
    imports: {},
};

export function createInitialMockState(): MockApiState {
  // Never expose the module-level fixtures directly: handlers deliberately mutate
  // state, while reset must always start from an untouched snapshot.
  return structuredClone(initialMockState);
}

export function nextMockId(state: MockApiState): string {
  state.sequence += 1;
  return id(state.sequence);
}

export function nextRowVersion(state: MockApiState): string {
  state.sequence += 1;
  return `rv-${state.sequence}`;
}
