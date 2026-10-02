import { useCallback, useEffect, useState } from 'react';
import { mentorSupportApi } from '../../api/mentorSupportApi';
import { teamApi } from '../../api/teamApi';
import { useAuth } from '../../hooks/useAuth';
import type { MentoringFeedback, MentoringSession, SaveMentoringSession } from '../../types/mentoring';
import { parseApiError } from '../../utils/apiError';
import { unwrapApiData } from '../../utils/classMappers';

interface TeamOption { id: string; teamName: string; teamCode?: string }
const emptyForm: SaveMentoringSession = { teamId: '', title: '', description: '', startAt: '', endAt: '', location: '', meetingUrl: '' };

export default function MentoringSessionsPage() {
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
      setTeams(list.filter((item: Record<string, unknown>) => Boolean(item.currentMentorAssignment) && item.status === 'Active')
        .map((item: Record<string, unknown>) => ({ id: String(item.id ?? ''),
        teamName: String(item.teamName ?? 'Team'), teamCode: String(item.teamCode ?? '') })));
    } catch (cause) { setError(parseApiError(cause, 'Could not load mentoring sessions').message); }
    finally { setLoading(false); }
  }, [teamId]);
  useEffect(() => { void Promise.resolve().then(load); }, [load]);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (saving) return;
    if (!form.teamId || !form.title.trim() || !form.startAt || !form.endAt || new Date(form.endAt) <= new Date(form.startAt) ||
      new Date(form.startAt) <= new Date() || (form.meetingUrl && !form.meetingUrl.startsWith('https://'))) {
      setError('Choose a team, title, and valid start/end times.'); return;
    }
    setSaving(true); setError(''); setNotice('');
    try {
      const payload = { ...form, startAt: new Date(form.startAt).toISOString(), endAt: new Date(form.endAt).toISOString() };
      if (editingId) await mentorSupportApi.updateSession(editingId, payload);
      else await mentorSupportApi.createSession(payload);
      setForm(emptyForm); setEditingId(null); setNotice(editingId ? 'Session updated.' : 'Session scheduled.');
      await load();
    } catch (cause) { setError(parseApiError(cause, 'Could not save session').message); }
    finally { setSaving(false); }
  };

  const changeStatus = async (session: MentoringSession, action: 'complete' | 'cancel') => {
    if (action === 'cancel' && !window.confirm(`Cancel “${session.title}”?`)) return;
    if (action === 'complete' && !notes[session.id]?.trim()) { setError('Add session notes before completing.'); return; }
    setSaving(true); setError(''); setNotice('');
    try {
      if (action === 'cancel') await mentorSupportApi.cancelSession(session.id);
      else await mentorSupportApi.completeSession(session.id, notes[session.id]);
      setNotice(action === 'cancel' ? 'Session cancelled.' : 'Session completed.'); await load();
    } catch (cause) { setError(parseApiError(cause, 'Could not update session').message); }
    finally { setSaving(false); }
  };

  const addAction = async (session: MentoringSession) => {
    const content = actionText[session.id]?.trim();
    if (!content) return;
    setSaving(true); setError('');
    try { await mentorSupportApi.addActionItem(session.id, content); setActionText(current => ({ ...current, [session.id]: '' })); await load(); }
    catch (cause) { setError(parseApiError(cause, 'Could not add action item').message); }
    finally { setSaving(false); }
  };

  const saveFeedback = async (session: MentoringSession) => {
    const comment = feedbackText[session.id]?.trim();
    if (!comment) { setError('Add a comment with your rating.'); return; }
    setSaving(true); setError(''); setNotice('');
    try { await mentorSupportApi.saveFeedback(session.id, feedbackRating[session.id] ?? 5, comment); setNotice('Feedback saved.'); }
    catch (cause) { setError(parseApiError(cause, 'Could not save feedback').message); }
    finally { setSaving(false); }
  };

  const viewFeedback = async (session: MentoringSession) => {
    try { const feedback = await mentorSupportApi.getFeedback(session.id);
      setFeedbackBySession(current => ({ ...current, [session.id]: feedback })); }
    catch (cause) { setError(parseApiError(cause, 'Could not load feedback').message); }
  };

  const edit = (session: MentoringSession) => {
    setEditingId(session.id);
    const local = (value: string) => {
      const date = new Date(value);
      return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
    };
    setForm({ teamId: session.teamId, title: session.title, description: session.description ?? '',
      startAt: local(session.startAt), endAt: local(session.endAt), location: session.location ?? '', meetingUrl: session.meetingUrl ?? '' });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  return <main className="mx-auto max-w-5xl space-y-6 p-4 sm:p-8">
    <header><h1 className="text-2xl font-bold text-slate-900">Mentoring sessions</h1>
      <p className="mt-1 text-sm text-slate-600">Schedule support, record outcomes, and track follow-up actions.</p></header>
    {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-red-700">{error}</p>}
    {notice && <p role="status" className="rounded-lg bg-green-50 p-3 text-green-800">{notice}</p>}
    {canManage && <form onSubmit={save} className="grid gap-3 rounded-xl border bg-white p-5 sm:grid-cols-2">
      <h2 className="text-lg font-semibold sm:col-span-2">{editingId ? 'Edit session' : 'Schedule session'}</h2>
      <label className="text-sm">Team<select required value={form.teamId} onChange={event => setForm({ ...form, teamId: event.target.value })} className="mt-1 block w-full rounded-lg border p-2">
        <option value="">Choose team</option>{teams.map(team => <option key={team.id} value={team.id}>{team.teamName} {team.teamCode}</option>)}</select></label>
      <label className="text-sm">Title<input required maxLength={200} value={form.title} onChange={event => setForm({ ...form, title: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <label className="text-sm">Starts<input required type="datetime-local" value={form.startAt} onChange={event => setForm({ ...form, startAt: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <label className="text-sm">Ends<input required type="datetime-local" value={form.endAt} onChange={event => setForm({ ...form, endAt: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <label className="text-sm">Location<input maxLength={300} value={form.location} onChange={event => setForm({ ...form, location: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <label className="text-sm">Meeting HTTPS URL<input type="url" value={form.meetingUrl} onChange={event => setForm({ ...form, meetingUrl: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <label className="text-sm sm:col-span-2">Description<textarea maxLength={2000} value={form.description} onChange={event => setForm({ ...form, description: event.target.value })} className="mt-1 block w-full rounded-lg border p-2" /></label>
      <div className="flex gap-2 sm:col-span-2"><button disabled={saving} className="rounded-lg bg-indigo-600 px-4 py-2 text-white disabled:opacity-50">{saving ? 'Saving…' : editingId ? 'Save changes' : 'Schedule'}</button>
        {editingId && <button type="button" onClick={() => { setEditingId(null); setForm(emptyForm); }} className="rounded-lg border px-4 py-2">Discard</button>}</div>
    </form>}
    <section className="space-y-3"><div className="flex items-center justify-between gap-3"><h2 className="text-lg font-semibold">History and upcoming</h2>
      <select aria-label="Filter by team" value={teamId} onChange={event => setTeamId(event.target.value)} className="rounded-lg border p-2 text-sm"><option value="">All accessible teams</option>{teams.map(team => <option key={team.id} value={team.id}>{team.teamName}</option>)}</select></div>
      {loading ? <p>Loading sessions…</p> : sessions.length === 0 ? <div className="rounded-xl border bg-white p-8 text-center text-slate-600">No mentoring sessions yet.</div> : sessions.map(session =>
        <article key={session.id} className="space-y-3 rounded-xl border bg-white p-5"><div className="flex flex-wrap justify-between gap-3"><div><h3 className="font-semibold">{session.title}</h3>
          <p className="text-sm text-slate-600">{teams.find(team => team.id === session.teamId)?.teamName ?? 'Team'} · {new Date(session.startAt).toLocaleString()} – {new Date(session.endAt).toLocaleString()}</p></div>
          <span className="text-sm font-medium">{session.status}</span></div>
          {session.description && <p className="text-sm">{session.description}</p>}
          {session.location && <p className="text-sm text-slate-600">Location: {session.location}</p>}
          {session.meetingUrl && <a className="text-sm text-indigo-700 underline" href={session.meetingUrl} target="_blank" rel="noopener noreferrer">Join meeting</a>}
          {session.notes && <p className="rounded-lg bg-slate-50 p-3 text-sm"><strong>Notes:</strong> {session.notes}</p>}
          {session.actionItems.length > 0 && <ul className="list-inside list-disc text-sm">{session.actionItems.map(item => <li key={item.id}>{item.content}</li>)}</ul>}
          {canManage && session.status === 'Scheduled' && <div className="space-y-2"><textarea aria-label={`Notes for ${session.title}`} maxLength={10000} placeholder="Session outcomes and feedback" value={notes[session.id] ?? ''} onChange={event => setNotes(current => ({ ...current, [session.id]: event.target.value }))} className="w-full rounded-lg border p-2" />
            <div className="flex gap-2"><button type="button" disabled={saving} onClick={() => edit(session)} className="rounded-lg border px-3 py-1 text-sm">Edit</button>
              <button type="button" disabled={saving || new Date(session.endAt) > new Date()} onClick={() => void changeStatus(session, 'complete')} className="rounded-lg bg-indigo-600 px-3 py-1 text-sm text-white disabled:opacity-50">Complete</button>
              <button type="button" disabled={saving} onClick={() => void changeStatus(session, 'cancel')} className="rounded-lg border border-red-200 px-3 py-1 text-sm text-red-700">Cancel</button></div></div>}
          {canManage && session.status === 'Completed' && <div className="flex gap-2"><input aria-label={`Follow-up action for ${session.title}`} maxLength={2000} placeholder="New follow-up action" value={actionText[session.id] ?? ''} onChange={event => setActionText(current => ({ ...current, [session.id]: event.target.value }))} className="min-w-0 flex-1 rounded-lg border p-2 text-sm" />
            <button type="button" disabled={saving} onClick={() => void addAction(session)} className="rounded-lg border px-3 text-sm">Add</button></div>}
          {!canManage && session.status === 'Completed' && <div className="space-y-2 border-t pt-3"><h4 className="text-sm font-semibold">Feedback for your mentor</h4>
            <label className="block text-sm">Rating<select value={feedbackRating[session.id] ?? 5} onChange={event => setFeedbackRating(current => ({ ...current, [session.id]: Number(event.target.value) }))} className="ml-2 rounded-lg border p-1">
              {[1, 2, 3, 4, 5].map(value => <option key={value} value={value}>{value}/5</option>)}</select></label>
            <textarea aria-label="Mentor feedback comment" maxLength={2000} value={feedbackText[session.id] ?? ''} onChange={event => setFeedbackText(current => ({ ...current, [session.id]: event.target.value }))} className="w-full rounded-lg border p-2" placeholder="What was useful? What could improve?" />
            <button type="button" disabled={saving} onClick={() => void saveFeedback(session)} className="rounded-lg bg-indigo-600 px-3 py-1 text-sm text-white disabled:opacity-50">Save feedback</button></div>}
          {canManage && session.status === 'Completed' && <div className="border-t pt-3"><button type="button" onClick={() => void viewFeedback(session)} className="text-sm text-indigo-700 underline">View student feedback</button>
            {feedbackBySession[session.id] && <div className="mt-2 space-y-2">{feedbackBySession[session.id].length === 0 ? <p className="text-sm text-slate-600">No feedback yet.</p> : feedbackBySession[session.id].map(item =>
              <p key={item.id} className="rounded-lg bg-slate-50 p-2 text-sm">{item.rating}/5 · {item.comment}</p>)}</div>}</div>}
        </article>)}
    </section>
  </main>;
}
