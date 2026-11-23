import { useCallback, useEffect, useState } from 'react';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import type { MentorAssignment } from '../../types/teamManagement';
import { normalizeManagedTeam } from '../../utils/teamManagement';
import { teamApi } from '../../api/teamApi';
import { useAuth } from '../../hooks/useAuth';
import type { MentoringFeedback, MentoringSession, SaveMentoringSession } from '../../types/mentoring';
import { parseApiError } from '../../utils/apiError';
import { unwrapApiData } from '../../utils/classMappers';
import Badge from '../../components/ui/Badge';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import { useTranslation } from 'react-i18next';

interface TeamOption { id: string; teamName: string; teamCode?: string; assignments: MentorAssignment[] }
const emptyForm: SaveMentoringSession = { teamId: '', title: '', description: '', startAt: '', endAt: '', location: '', meetingUrl: '' };

export default function MentoringSessionsPage() {
  const { t } = useTranslation('mentoring');
  const { user } = useAuth();
  const canManage = ['ADMIN', 'LECTURER', 'MENTOR'].includes(user?.role ?? '');
  const [sessions, setSessions] = useState<MentoringSession[]>([]);
  const [teams, setTeams] = useState<TeamOption[]>([]);
  const [teamId, setTeamId] = useState('');
  const [form, setForm] = useState<SaveMentoringSession>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [actionText, setActionText] = useState<Record<string, string>>({});
  const [feedbackText, setFeedbackText] = useState<Record<string, string>>({});
  const [feedbackRating, setFeedbackRating] = useState<Record<string, number>>({});
  const [feedbackBySession, setFeedbackBySession] = useState<Record<string, MentoringFeedback[]>>({});

  const load = useCallback(async () => {
    setLoading(true); setError('');
    try {
      const [sessionList, teamResponse] = await Promise.all([mentorSupportApi.getSessions(teamId || undefined), teamApi.getAll({})]);
      setSessions(sessionList);
      const raw = unwrapApiData<unknown>(teamResponse);
      const list = Array.isArray(raw) ? raw : [];
      setTeams(list.map(normalizeManagedTeam).filter(item => (item.currentMentorAssignments?.length || 0) > 0 && item.status === 'Active')
        .map(item => ({ id: item._id, teamName: item.teamName, teamCode: item.teamCode ?? '',
          assignments: (item.currentMentorAssignments ?? []).filter(a => user?.role !== 'MENTOR' || a.mentor.userId === user.id) })));
    } catch (cause) { setError(parseApiError(cause, t('mentoringSessionsPage.couldNotLoadMentoringSessions')).message); }
    finally { setLoading(false); }
  }, [teamId, user?.role, user?.id]);
  useEffect(() => { void Promise.resolve().then(load); }, [load]);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (saving) return;
    if (!form.teamId || !form.title.trim() || !form.startAt || !form.endAt || new Date(form.endAt) <= new Date(form.startAt) ||
      new Date(form.startAt) <= new Date() || (form.meetingUrl && !form.meetingUrl.startsWith('https://'))) {
      setError(t('mentoringSessionsPage.chooseATeamTitleAndValid')); return;
    }
    setSaving(true); setError(''); setNotice('');
    try {
      const payload = { ...form, startAt: new Date(form.startAt).toISOString(), endAt: new Date(form.endAt).toISOString() };
      if (editingId) await mentorSupportApi.updateSession(editingId, payload);
      else await mentorSupportApi.createSession(payload);
      setForm(emptyForm); setEditingId(null); setNotice(editingId ? t('mentoringSessionsPage.sessionUpdated') : t('mentoringSessionsPage.sessionScheduled'));
      await load();
    } catch (cause) { setError(parseApiError(cause, t('mentoringSessionsPage.couldNotSaveSession')).message); }
    finally { setSaving(false); }
  };

  const changeStatus = async (session: MentoringSession, action: 'complete' | 'cancel') => {
    if (action === 'cancel' && !window.confirm(t('mentoringSessionsPage.cancel', { title: session.title }))) return;
    if (action === 'complete' && !notes[session.id]?.trim()) { setError(t('mentoringSessionsPage.addSessionNotesBeforeCompleting')); return; }
    setSaving(true); setError(''); setNotice('');
    try {
      if (action === 'cancel') await mentorSupportApi.cancelSession(session.id);
      else await mentorSupportApi.completeSession(session.id, notes[session.id]);
      setNotice(action === 'cancel' ? t('mentoringSessionsPage.sessionCancelled') : t('mentoringSessionsPage.sessionCompleted')); await load();
    } catch (cause) { setError(parseApiError(cause, t('mentoringSessionsPage.couldNotUpdateSession')).message); }
    finally { setSaving(false); }
  };

  const addAction = async (session: MentoringSession) => {
    const content = actionText[session.id]?.trim();
    if (!content) return;
    setSaving(true); setError('');
    try { await mentorSupportApi.addActionItem(session.id, content); setActionText(current => ({ ...current, [session.id]: '' })); await load(); }
    catch (cause) { setError(parseApiError(cause, t('mentoringSessionsPage.couldNotAddActionItem')).message); }
    finally { setSaving(false); }
  };

  const saveFeedback = async (session: MentoringSession) => {
    const comment = feedbackText[session.id]?.trim();
    if (!comment) { setError(t('mentoringSessionsPage.addACommentWithYourRating')); return; }
    setSaving(true); setError(''); setNotice('');
    try { await mentorSupportApi.saveFeedback(session.id, feedbackRating[session.id] ?? 5, comment); setNotice(t('mentoringSessionsPage.feedbackSaved')); }
    catch (cause) { setError(parseApiError(cause, t('mentoringSessionsPage.couldNotSaveFeedback')).message); }
    finally { setSaving(false); }
  };

  const viewFeedback = async (session: MentoringSession) => {
    try { const feedback = await mentorSupportApi.getFeedback(session.id);
      setFeedbackBySession(current => ({ ...current, [session.id]: feedback })); }
    catch (cause) { setError(parseApiError(cause, t('mentoringSessionsPage.couldNotLoadFeedback')).message); }
  };

  const edit = (session: MentoringSession) => {
    setEditingId(session.id);
    const local = (value: string) => {
      const date = new Date(value);
      return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
    };
    setForm({ mentorAssignmentId: session.mentorAssignmentId, teamId: session.teamId, title: session.title, description: session.description ?? '',
      startAt: local(session.startAt), endAt: local(session.endAt), location: session.location ?? '', meetingUrl: session.meetingUrl ?? '' });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  return <main className="mx-auto max-w-5xl space-y-6 p-4 text-body text-slate-700 sm:p-8">
    <header><h1 className="text-display text-secondary">{t('mentoringSessionsPage.mentoringSessions')}</h1>
      <p className="mt-1 text-body text-slate-600">{t('mentoringSessionsPage.scheduleSupportRecordOutcomesAndTrack')}</p></header>
    {error && <p role="alert" className="rounded-lg bg-danger-50 p-3 text-body text-danger-dark">{error}</p>}
    {notice && <p role="status" className="rounded-lg bg-success-50 p-3 text-body text-success-dark">{notice}</p>}
    {canManage && <form onSubmit={save} className="grid gap-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-card sm:grid-cols-2">
      <h2 className="text-heading text-secondary sm:col-span-2">{editingId ? t('mentoringSessions.editSession') : t('mentoringSessionsPage.scheduleSession')}</h2>
      <label className="text-body">{t('mentoringSessionsPage.team')}<select required value={form.teamId} onChange={event => setForm({ ...form, teamId: event.target.value, mentorAssignmentId: undefined })} className="mt-1 block w-full rounded-lg border p-2">
        <option value="">{t('mentoringSessionsPage.chooseTeam')}</option>{teams.map(team => <option key={team.id} value={team.id}>{team.teamName} {team.teamCode}</option>)}</select></label>
      <label className="text-body">{t('mentoringSessionsPage.mentor')}<select required disabled={Boolean(editingId)} value={form.mentorAssignmentId ?? ''} onChange={event => setForm({ ...form, mentorAssignmentId: event.target.value })} className="mt-1 block w-full rounded-lg border p-2">
        <option value="">{t('mentoringSessionsPage.chooseMentor')}</option>{teams.find(team => team.id === form.teamId)?.assignments.map(a => <option key={a.assignmentId} value={a.assignmentId}>{a.mentor.fullName} · {a.slot}</option>)}</select></label>
      <label className="text-body">{t('mentoringSessionsPage.title')}<input required maxLength={200} value={form.title} onChange={event => setForm({ ...form, title: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <label className="text-body">{t('mentoringSessionsPage.starts')}<input required type="datetime-local" value={form.startAt} onChange={event => setForm({ ...form, startAt: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <label className="text-body">{t('mentoringSessionsPage.ends')}<input required type="datetime-local" value={form.endAt} onChange={event => setForm({ ...form, endAt: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <label className="text-body">{t('mentoringSessions.location')}<input maxLength={300} value={form.location} onChange={event => setForm({ ...form, location: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <label className="text-body">{t('mentoringSessionsPage.meetingHttpsUrl')}<input type="url" value={form.meetingUrl} onChange={event => setForm({ ...form, meetingUrl: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <label className="text-body sm:col-span-2">{t('mentoringSessionsPage.description')}<textarea maxLength={2000} value={form.description} onChange={event => setForm({ ...form, description: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <div className="flex gap-2 sm:col-span-2"><Button type="submit" disabled={saving}>{saving ? t('mentoringSessionsPage.saving') : editingId ? t('mentoringSessionsPage.saveChanges') : t('mentoringSessions.schedule')}</Button>
        {editingId && <Button variant="outline" onClick={() => { setEditingId(null); setForm(emptyForm); }}>{t('mentoringSessionsPage.discard')}</Button>}</div>
    </form>}
    <section className="space-y-3"><div className="flex items-center justify-between gap-3"><h2 className="text-heading text-secondary">{t('mentoringSessionsPage.historyAndUpcoming')}</h2>
      <select aria-label={t('mentoringSessionsPage.filterByTeam')} value={teamId} onChange={event => setTeamId(event.target.value)} className="rounded-lg border p-2 text-body"><option value="">{t('mentoringSessionsPage.allAccessibleTeams')}</option>{teams.map(team => <option key={team.id} value={team.id}>{team.teamName}</option>)}</select></div>
      {loading ? <p>{t('mentoringSessionsPage.loadingSessions')}</p> : sessions.length === 0 ? <div className="rounded-2xl border border-slate-200 bg-white p-8 text-center text-slate-600 shadow-card">{t('mentoringSessionsPage.noMentoringSessionsYet')}</div> : sessions.map(session =>
        <article key={session.id} className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-card"><div className="flex flex-wrap justify-between gap-3"><div><h3 className="text-subheading text-secondary">{session.title}</h3>
          <p className="text-body text-slate-600">{teams.find(team => team.id === session.teamId)?.teamName ?? t('mentoringSessionsPage.team')} · {new Date(session.startAt).toLocaleString()} – {new Date(session.endAt).toLocaleString()}</p></div>
          <Badge variant={session.status === 'Scheduled' ? 'info' : session.status === 'Completed' ? 'success' : session.status === 'Cancelled' ? 'neutral' : 'danger'}>{session.status}</Badge></div>
          {session.description && <p className="text-body">{session.description}</p>}
          {session.location && <p className="text-body text-slate-600">{t('mentoringSessionsPage.location', { location: session.location })}</p>}
          {session.meetingUrl && <a className="text-body text-primary-700 underline" href={session.meetingUrl} target="_blank" rel="noopener noreferrer">{t('mentoringSessionsPage.joinMeeting')}</a>}
          {session.notes && <Card tone="inset" padding="p-3" className="text-body"><p><strong>{t('mentoringSessionsPage.notes')}</strong> {session.notes}</p></Card>}
          {session.actionItems.length > 0 && <ul className="list-inside list-disc text-body">{session.actionItems.map(item => <li key={item.id}>{item.content}</li>)}</ul>}
          {canManage && session.status === 'Scheduled' && <div className="space-y-2"><textarea aria-label={t('mentoringSessionsPage.notesFor', { title: session.title })} maxLength={10000} placeholder={t('mentoringSessionsPage.sessionOutcomesAndFeedback')} value={notes[session.id] ?? ''} onChange={event => setNotes(current => ({ ...current, [session.id]: event.target.value }))} className="w-full rounded-lg border p-2" />
            <div className="flex gap-2"><Button variant="outline" size="sm" disabled={saving} onClick={() => edit(session)}>{t('common:actions.edit')}</Button>
              <Button variant="outline" size="sm" disabled={saving || new Date(session.endAt) > new Date()} onClick={() => void changeStatus(session, 'complete')}>{t('mentoringSessionsPage.complete')}</Button>
              <Button variant="danger-outline" size="sm" disabled={saving} onClick={() => void changeStatus(session, 'cancel')}>{t('common:actions.cancel')}</Button></div></div>}
          {canManage && session.status === 'Completed' && <div className="flex gap-2"><input aria-label={t('mentoringSessionsPage.followUpActionFor', { title: session.title })} maxLength={2000} placeholder={t('mentoringSessionsPage.newFollowUpAction')} value={actionText[session.id] ?? ''} onChange={event => setActionText(current => ({ ...current, [session.id]: event.target.value }))} className="min-w-0 flex-1 rounded-lg border p-2 text-body" />
            <Button variant="outline" size="sm" disabled={saving} onClick={() => void addAction(session)}>{t('common:actions.add')}</Button></div>}
          {!canManage && session.status === 'Completed' && <div className="space-y-2 border-t pt-3"><h4 className="text-subheading text-secondary">{t('mentoringSessionsPage.feedbackForYourMentor')}</h4>
            <label className="block text-body">{t('mentoringSessionsPage.rating')}<select value={feedbackRating[session.id] ?? 5} onChange={event => setFeedbackRating(current => ({ ...current, [session.id]: Number(event.target.value) }))} className="ml-2 rounded-lg border p-1">
              {[1, 2, 3, 4, 5].map(value => <option key={value} value={value}>{value}/5</option>)}</select></label>
            <textarea aria-label={t('mentoringSessionsPage.mentorFeedbackComment')} maxLength={2000} value={feedbackText[session.id] ?? ''} onChange={event => setFeedbackText(current => ({ ...current, [session.id]: event.target.value }))} className="w-full rounded-lg border p-2" placeholder={t('mentoringSessionsPage.whatWasUsefulWhatCouldImprove')} />
            <Button variant="outline" size="sm" disabled={saving} onClick={() => void saveFeedback(session)}>{t('mentoringSessionsPage.saveFeedback')}</Button></div>}
          {canManage && session.status === 'Completed' && <div className="border-t pt-3"><button type="button" onClick={() => void viewFeedback(session)} className="text-body text-primary-700 underline">{t('mentoringSessionsPage.viewStudentFeedback')}</button>
            {feedbackBySession[session.id] && <div className="mt-2 space-y-2">{feedbackBySession[session.id].length === 0 ? <p className="text-body text-slate-600">{t('mentoringSessionsPage.noFeedbackYet')}</p> : feedbackBySession[session.id].map(item =>
              <Card key={item.id} tone="inset" padding="p-2" className="text-body"><p>{item.rating}/5 · {item.comment}</p></Card>)}</div>}</div>}
        </article>)}
    </section>
  </main>;
}
