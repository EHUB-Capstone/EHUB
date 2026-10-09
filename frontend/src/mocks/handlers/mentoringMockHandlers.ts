import type MockAdapter from 'axios-mock-adapter';
import type { MentorProfile, MentorProfileDraft, MentorRecommendation, MentoringFeedback, MentoringSession } from '../../types/mentoring.ts';
import { validateMentorMetadata } from '../../utils/mentorProfiles.ts';
import { allocateId, asString, failure, getMockState, ok, parseBody, requestParams, routeId } from '../mockHelpers.ts';

const profiles = new Map<string, MentorProfile>();
const sessions: MentoringSession[] = [];
const feedback = new Map<string, MentoringFeedback[]>();
const feedbackOwners = new Map<string, string>();
let profileState = getMockState();

function currentUser() {
  const state = getMockState();
  if (profileState !== state) { profiles.clear(); profileState = state; }
  return state.users.find(user => user.id === state.sessionUserId);
}

function mentorProfile(userId: string): MentorProfile | undefined {
  const user = getMockState().users.find(item => item.id === userId && item.role === 'MENTOR');
  if (!user) return undefined;
  return profiles.get(userId) ?? { id: userId, userId, fullName: user.name, email: user.email, mentorType: 'Business',
    startupDomains: [], technologySkills: [], tags: [], experiences: [],
    expertise: [], bio: null, experience: null, organization: null, linkedInUrl: null, portfolioUrl: null,
    cvFileName: null, portfolioFileName: null, maxTeams: null, status: 'Active', activeTeamCount: 0,
    totalAssignments: 0, totalSessions: 0, averageFeedbackRating: null };
}

function accessibleTeam(teamId: string) {
  const user = currentUser();
  const state = getMockState();
  const team = state.teams.find(item => item.id === teamId);
  if (!team || !user) return false;
  if (user.role === 'ADMIN') return true;
  if (user.role === 'LECTURER') return state.classes.some(item => item.id === team.classId && item.primaryLecturerId === user.id);
  if (user.role === 'MENTOR') return team.currentMentorAssignment?.mentor.userId === user.id;
  return (state.rosters[team.classId] ?? []).some(student => student.userId === user.id &&
    team.members.some(member => member.studentId === student.studentId));
}

export function registerMentoringMockHandlers(mock: MockAdapter): void {
  const saveManagedProfile = (body: Record<string, unknown>, id?: string) => {
    const user = currentUser();
    if (!user) return failure(401, 'UNAUTHORIZED', 'Authentication required.');
    if (user.role !== 'ADMIN') return failure(403, 'CLASS_ACCESS_DENIED', 'Administrator role required.');
    const existing = id ? [...profiles.values()].find(x => x.id === id) ?? mentorProfile(id) : undefined;
    if (id && !existing) return failure(404, 'COMMON_NOT_FOUND_ERROR', 'Mentor profile was not found.');
    const name = asString(body.fullName).trim(); const email = asString(body.email).trim().toLowerCase();
    const profile = body.profile as MentorProfileDraft | undefined;
    if (!name || name.length > 100 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length > 320 || !profile ||
      !Array.isArray(profile.expertise) || !Array.isArray(profile.startupDomains) || !Array.isArray(profile.technologySkills) ||
      !Array.isArray(profile.tags) || !Array.isArray(profile.experiences) || !profile.bio?.trim())
      return failure(400, 'COMMON_VALIDATION_ERROR', 'Name, valid email, background and metadata are required.');
    const error = validateMentorMetadata(profile);
    if (error) return failure(400, 'COMMON_VALIDATION_ERROR', error);
    if (!id && (asString(body.temporaryPassword).length < 6 || asString(body.temporaryPassword).length > 100))
      return failure(400, 'COMMON_VALIDATION_ERROR', 'A temporary password of 6–100 characters is required.');
    const state = getMockState();
    if (state.users.some(x => x.email.toLowerCase() === email && x.id !== existing?.userId))
      return failure(400, 'COMMON_VALIDATION_ERROR', 'Email already exists.');
    const userId = existing?.userId ?? allocateId();
    const account = state.users.find(x => x.id === userId);
    if (account) {
      if (account.email !== email && state.authPasswords[account.email]) {
        state.authPasswords[email] = state.authPasswords[account.email]; delete state.authPasswords[account.email];
      }
      account.name = name; account.email = email;
    }
    else {
      state.users.push({ id: userId, _id: userId, name, email, role: 'MENTOR', status: 'APPROVED', avatar: null,
        studentId: null, programGroup: null, major: null, phone: null, createdAt: new Date().toISOString(), lastSeen: null });
      state.authPasswords[email] = asString(body.temporaryPassword);
    }
    const updated: MentorProfile = { ...mentorProfile(userId)!, ...profile, fullName: name, email, userId };
    profiles.set(userId, updated);
    return ok(updated);
  };
  mock.onPost('/mentoring/profiles').reply(config => saveManagedProfile(parseBody(config)));
  mock.onPut(/^\/mentoring\/profiles\/[^/]+$/).reply(config => saveManagedProfile(parseBody(config), routeId(config, /^\/mentoring\/profiles\/([^/]+)$/)));
  mock.onGet('/mentoring/profile').reply(() => {
    const user = currentUser();
    const profile = user && mentorProfile(user.id);
    return profile ? ok(profile) : failure(403, 'CLASS_ACCESS_DENIED', 'Mentor role required.');
  });
  mock.onPut('/mentoring/profile').reply(config => {
    const user = currentUser(); const existing = user && mentorProfile(user.id);
    if (!existing) return failure(403, 'CLASS_ACCESS_DENIED', 'Mentor role required.');
    const body = parseBody(config);
    const metadata = { ...existing, ...body } as MentorProfileDraft;
    const validation = validateMentorMetadata(metadata);
    if (validation) return failure(400, 'COMMON_VALIDATION_ERROR', validation);
    if (metadata.mentorType !== existing.mentorType) return failure(400, 'COMMON_VALIDATION_ERROR', 'Mentor type is managed by the administrator.');
    const updated = { ...existing, ...body } as MentorProfile;
    profiles.set(user!.id, updated);
    return ok(updated);
  });
  mock.onPost(/^\/mentoring\/profile\/documents\/(cv|portfolio)$/).reply(config => {
    const user = currentUser(); const existing = user && mentorProfile(user.id);
    if (!existing) return failure(403, 'CLASS_ACCESS_DENIED', 'Mentor role required.');
    const kind = routeId(config, /^\/mentoring\/profile\/documents\/(cv|portfolio)$/);
    const file = config.data instanceof FormData ? config.data.get('file') : null;
    if (!(file instanceof File)) return failure(400, 'COMMON_VALIDATION_ERROR', 'File is required.');
    const updated = kind === 'cv' ? { ...existing, cvFileName: file.name } : { ...existing, portfolioFileName: file.name };
    profiles.set(user!.id, updated);
    return ok(updated);
  });
  mock.onGet(/^\/mentoring\/profiles\/[^/]+\/documents\/(cv|portfolio)$/).reply(config => {
    const user = currentUser();
    const mentorId = routeId(config, /^\/mentoring\/profiles\/([^/]+)\/documents\/(?:cv|portfolio)$/);
    if (!user || (user.id !== mentorId && user.role !== 'ADMIN' && user.role !== 'LECTURER'))
      return failure(403, 'CLASS_ACCESS_DENIED', 'Access denied.');
    return [200, new Blob(['Mock document'])];
  });
  mock.onGet('/mentoring/directory').reply(() => {
    if (!['ADMIN', 'LECTURER'].includes(currentUser()?.role ?? '')) return failure(403, 'CLASS_ACCESS_DENIED', 'Access denied.');
    return ok(getMockState().users.filter(user => user.role === 'MENTOR' && user.status === 'APPROVED')
      .map(user => mentorProfile(user.id)).filter((item): item is MentorProfile => Boolean(item)));
  });
  mock.onGet(/^\/mentoring\/teams\/[^/]+\/recommendations$/).reply(config => {
    const teamId = routeId(config, /^\/mentoring\/teams\/([^/]+)\/recommendations$/);
    const team = getMockState().teams.find(item => item.id === teamId);
    if (!team) return failure(404, 'TEAM_NOT_FOUND', 'Team not found.');
    if (!['ADMIN', 'LECTURER'].includes(currentUser()?.role ?? '') || !accessibleTeam(teamId))
      return failure(403, 'CLASS_ACCESS_DENIED', 'Access denied.');
    const text = `${team.teamName} ${team.projectName ?? ''} ${team.projectDescription ?? ''}`.toLowerCase();
    const state = getMockState();
    const semesterId = state.classes.find(item => item.id === team.classId)?.semesterId;
    const items: MentorRecommendation[] = state.users.filter(user => user.role === 'MENTOR' && user.status === 'APPROVED' &&
      state.semesterStaffAssignments.some(assignment => assignment.semesterId === semesterId &&
        assignment.userId === user.id && assignment.role === 'MENTOR' && assignment.status === 'ACTIVE')).map(user => {
      const mentor = mentorProfile(user.id)!;
      const matches = [...mentor.expertise, ...mentor.startupDomains, ...mentor.technologySkills, ...mentor.tags,
        ...mentor.experiences.map(x => x.area)].filter(skill => text.includes(skill.toLowerCase()));
      const activeTeamCount = getMockState().teams.filter(item => item.currentMentorAssignment?.mentor.userId === user.id).length;
      const slot = 'Enterprise';
      const hasCapacity = !(team.currentMentorAssignments ?? []).some(a => a.slot === slot);
      return { mentor, fitScore: Math.round(30 * Math.min(1, matches.length / 2)),
        reasons: [...matches.map(skill => `Chuyên môn phù hợp: ${skill}`), hasCapacity ? 'Còn khả năng nhận nhóm' : 'Đã đủ số nhóm'],
        activeTeamCount, hasCapacity };
    });
    return ok(items.filter(item => item.hasCapacity).sort((a, b) => b.fitScore - a.fitScore));
  });
  mock.onGet('/mentoring/sessions').reply(config => {
    const teamId = asString(requestParams(config).teamId);
    return ok(sessions.filter(item => (!teamId || item.teamId === teamId) && accessibleTeam(item.teamId)));
  });
  mock.onPost('/mentoring/sessions').reply(config => {
    const body = parseBody(config);
    const teamId = asString(body.teamId);
    if (!accessibleTeam(teamId) || currentUser()?.role === 'STUDENT') return failure(403, 'CLASS_ACCESS_DENIED', 'Access denied.');
    const item: MentoringSession = { id: allocateId(), teamId, mentorAssignmentId: asString(body.mentorAssignmentId) || teamId, title: asString(body.title),
      description: asString(body.description), startAt: asString(body.startAt), endAt: asString(body.endAt),
      location: asString(body.location), meetingUrl: asString(body.meetingUrl), status: 'Scheduled', notes: null, actionItems: [] };
    sessions.push(item); return ok(item);
  });
  mock.onPut(/^\/mentoring\/sessions\/[^/]+$/).reply(config => {
    const item = sessions.find(value => value.id === routeId(config, /^\/mentoring\/sessions\/([^/]+)$/));
    if (!item) return failure(404, 'COMMON_NOT_FOUND_ERROR', 'Session not found.');
    if (!accessibleTeam(item.teamId) || currentUser()?.role === 'STUDENT') return failure(403, 'CLASS_ACCESS_DENIED', 'Access denied.');
    Object.assign(item, parseBody(config)); return ok(item);
  });
  mock.onPost(/^\/mentoring\/sessions\/[^/]+\/(complete|cancel)$/).reply(config => {
    const id = routeId(config, /^\/mentoring\/sessions\/([^/]+)\/(?:complete|cancel)$/);
    const item = sessions.find(value => value.id === id);
    if (!item) return failure(404, 'COMMON_NOT_FOUND_ERROR', 'Session not found.');
    if (!accessibleTeam(item.teamId) || currentUser()?.role === 'STUDENT') return failure(403, 'CLASS_ACCESS_DENIED', 'Access denied.');
    if (config.url?.endsWith('/complete')) { item.status = 'Completed'; item.notes = asString(parseBody(config).notes); }
    else item.status = 'Cancelled';
    return ok(item);
  });
  mock.onPost(/^\/mentoring\/sessions\/[^/]+\/action-items$/).reply(config => {
    const item = sessions.find(value => value.id === routeId(config, /^\/mentoring\/sessions\/([^/]+)\/action-items$/));
    if (!item) return failure(404, 'COMMON_NOT_FOUND_ERROR', 'Session not found.');
    if (!accessibleTeam(item.teamId) || currentUser()?.role === 'STUDENT') return failure(403, 'CLASS_ACCESS_DENIED', 'Access denied.');
    const action = { id: allocateId(), content: asString(parseBody(config).content), dueDate: null, completed: false };
    item.actionItems.push(action); return ok(action);
  });
  mock.onPut(/^\/mentoring\/sessions\/[^/]+\/feedback$/).reply(config => {
    const item = sessions.find(value => value.id === routeId(config, /^\/mentoring\/sessions\/([^/]+)\/feedback$/));
    if (!item) return failure(404, 'COMMON_NOT_FOUND_ERROR', 'Session not found.');
    if (!accessibleTeam(item.teamId) || currentUser()?.role !== 'STUDENT') return failure(403, 'CLASS_ACCESS_DENIED', 'Access denied.');
    const body = parseBody(config);
    const existing = (feedback.get(item.id) ?? []).find(value => feedbackOwners.get(value.id) === currentUser()?.id);
    const entry: MentoringFeedback = { id: existing?.id ?? allocateId(), rating: Number(body.rating),
      comment: asString(body.comment), createdAtUtc: existing?.createdAtUtc ?? new Date().toISOString() };
    feedbackOwners.set(entry.id, currentUser()!.id);
    feedback.set(item.id, [...(feedback.get(item.id) ?? []).filter(value => value.id !== entry.id), entry]); return ok(entry);
  });
  mock.onGet(/^\/mentoring\/sessions\/[^/]+\/feedback$/).reply(config => {
    const item = sessions.find(value => value.id === routeId(config, /^\/mentoring\/sessions\/([^/]+)\/feedback$/));
    if (!item) return failure(404, 'COMMON_NOT_FOUND_ERROR', 'Session not found.');
    if (!accessibleTeam(item.teamId) || currentUser()?.role === 'STUDENT') return failure(403, 'CLASS_ACCESS_DENIED', 'Access denied.');
    return ok(feedback.get(item.id) ?? []);
  });
}
