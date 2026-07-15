// @ts-nocheck
import { useState, useEffect } from 'react';
import toast from 'react-hot-toast';
import { mentoringApi } from '../../api/mentoringApi';
import { dashboardApi } from '../../api/dashboardApi';
import { teamApi } from '../../api/teamApi';
import { classApi } from '../../api/classApi';
import { useAuth } from '../../hooks/useAuth';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import EmptyState from '../../components/ui/EmptyState';
import Button from '../../components/ui/Button';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import Modal from '../../components/ui/Modal';
import { formatSlotTime, SLOT_OPTIONS, TEACHING_DAYS } from '../../constants/classSchedule';
import { Calendar, Plus, Video, Clock, Trash2, Edit, ChevronLeft, ChevronRight, CheckCircle } from 'lucide-react';

const MONTHS = ['January','February','March','April','May','June','July','August','September','October','November','December'];
const DAYS = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];

const MentoringSessions = () => {
  const { user } = useAuth();
  const toId = (value) => {
    if (!value) return '';
    if (typeof value === 'string') return value;
    if (typeof value === 'object') {
      if (value._id) return toId(value._id);
      if (value.id) return toId(value.id);
      if (typeof value.toString === 'function') return value.toString();
    }
    return String(value);
  };
  const getTodayDateInput = () => {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  };
  const minMeetingDate = getTodayDateInput();

  const [sessions, setSessions] = useState([]);
  const [teamId, setTeamId] = useState(null);
  const [teams, setTeams] = useState([]);
  const [myClasses, setMyClasses] = useState([]);
  const [activeView, setActiveView] = useState('timetable'); // default to timetable so they see schedules immediately
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [editingSession, setEditingSession] = useState(null);
  const [calMonth, setCalMonth] = useState(new Date());
  const [form, setForm] = useState({ title: '', meetingDate: '', notes: '', actionItems: [''], classId: '', teamId: '' });
  const [classTeams, setClassTeams] = useState([]);

  const getClassLabel = (cls) => cls?.classCode || cls?.className || cls?.name || 'Unnamed class';
  const selectedClassTeams = form.classId
    ? classTeams
    : [];

  const loadTeamsForClass = async (classId) => {
    if (!classId) {
      setClassTeams([]);
      return;
    }

    try {
      const res = await classApi.getTeams(classId);
      const teamsData = res.data?.teams || res.teams || res.data || [];
      const selectedClass = myClasses.find(cls => toId(cls._id) === toId(classId));
      const isClassMentor = selectedClass && selectedClass.mentorIds && selectedClass.mentorIds.some(m => toId(m) === toId(user?._id));
      const visibleTeams = user?.role === 'MENTOR'
        ? teamsData.filter(team => toId(team?.mentorId) === toId(user?._id) || isClassMentor)
        : teamsData;
      const seen = new Set();
      setClassTeams((Array.isArray(visibleTeams) ? visibleTeams : []).filter(team => {
        const id = toId(team?._id);
        if (!id || seen.has(id)) return false;
        seen.add(id);
        return true;
      }));
    } catch (err) {
      console.error('Failed to load class teams:', err);
      setClassTeams([]);
      toast.error('Failed to load teams for the selected class');
    }
  };

  const fetchData = async () => {
    try {
      setLoading(true);
      let list = [];
      let filteredTeams = [];
      let accessibleClasses = [];
      
      if (user?.role === 'STUDENT') {
        const res = await dashboardApi.getStudent();
        const d = res.data || res;
        if (d.team?._id) {
          setTeamId(d.team._id);
          const mRes = await mentoringApi.getSessionsByTeam(d.team._id);
          list = mRes.data?.sessions || mRes.sessions || mRes.data || [];
        }

        try {
          const classesRes = await classApi.getMyClasses();
          accessibleClasses = classesRes.data?.classes || classesRes.classes || classesRes.data || [];
        } catch (classesErr) {
          console.error('Failed to load student classes:', classesErr);
        }
      } else {

        const [sessionsRes, classesRes, teamsRes] = await Promise.allSettled([
          mentoringApi.getAllSessions(),
          classApi.getAll(),
          teamApi.getAll()
        ]);

        if (sessionsRes.status === 'fulfilled') {
          const res = sessionsRes.value;
          list = res.data?.sessions || res.sessions || res.data || [];
        }

        const classesData = classesRes.status === 'fulfilled'
          ? (classesRes.value.data?.classes || classesRes.value.classes || classesRes.value.data || [])
          : [];
        const teamsData = teamsRes.status === 'fulfilled'
          ? (teamsRes.value.data?.teams || teamsRes.value.teams || teamsRes.value.data || [])
          : [];

        if (user?.role === 'LECTURER' || user?.role === 'LECTURE') {
          accessibleClasses = classesData.filter(c => toId(c.lectureId) === toId(user?._id));
          const lecturerClassIds = new Set(accessibleClasses.map(c => toId(c._id)));
          filteredTeams = teamsData.filter(t => lecturerClassIds.has(toId(t.classId)) || toId(t.lectureId) === toId(user?._id));
        } else if (user?.role === 'MENTOR') {
          const classAssignedClassIds = new Set(
            classesData
              .filter(c => c.mentorIds && c.mentorIds.some(m => toId(m) === toId(user?._id)))
              .map(c => toId(c._id))
          );
          const teamAssignedClassIds = new Set(
            teamsData
              .filter(t => toId(t.mentorId) === toId(user?._id))
              .map(t => toId(t.classId))
          );
          const allAccessibleClassIds = new Set([...classAssignedClassIds, ...teamAssignedClassIds]);

          accessibleClasses = classesData.filter(c => allAccessibleClassIds.has(toId(c._id)));
          const mentorClassIds = new Set(accessibleClasses.map(c => toId(c._id)));

          filteredTeams = teamsData.filter(t =>
            mentorClassIds.has(toId(t.classId)) ||
            toId(t.mentorId) === toId(user?._id)
          );

          if (filteredTeams.length === 0 && mentorClassIds.size > 0) {
            const classTeamResults = await Promise.allSettled(
              [...mentorClassIds].map(classId => classApi.getTeams(classId))
            );

            const fallbackTeams = classTeamResults
              .filter(result => result.status === 'fulfilled')
              .flatMap(result => {
                const payload = result.value;
                return payload.data?.teams || payload.teams || payload.data || [];
              });

            const seen = new Set();
            filteredTeams = fallbackTeams.filter(team => {
              const id = toId(team._id);
              if (!id || seen.has(id)) return false;
              seen.add(id);
              return true;
            });
          }
        } else {
          accessibleClasses = classesData;
          filteredTeams = teamsData;
        }

        setTeams(filteredTeams);
        if (user?.role === 'MENTOR' && filteredTeams.length === 0) {
          toast('You are assigned to class(es), but no teams are available yet. Please generate/assign teams first.', {
            icon: 'ℹ️'
          });
        }
      }

      setSessions(Array.isArray(list) ? list : []);
      setMyClasses(Array.isArray(accessibleClasses) ? accessibleClasses : []);
    } catch {
      toast.error('Failed to load sessions');
    } finally {
      setLoading(false);
    }
  };

  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { void fetchData(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const openAdd = () => {
    setEditingSession(null);
    const defaultClassId = myClasses.length === 1 ? toId(myClasses[0]._id) : '';
    setForm({ 
      title: '', 
      meetingDate: '', 
      startTime: '',
      endTime: '',
      location: '',
      meetingLink: '',
      notes: '', 
      actionItems: [''], 
      classId: defaultClassId,
      teamId: '' 
    });
    void loadTeamsForClass(defaultClassId);
    setIsModalOpen(true);
  };

  const handleClassChange = (classId) => {
    void loadTeamsForClass(classId);
    setForm(prev => ({
      ...prev,
      classId,
      teamId: ''
    }));
  };

  const handleTeamChange = (teamIdValue) => {
    setForm(prev => ({
      ...prev,
      teamId: teamIdValue,
    }));
  };

  const openEdit = (s) => {
    const sessionTeamId = s.teamId?._id || s.teamId || '';
    const sessionClassId = s.classId?._id || s.classId || s.teamId?.classId?._id || s.teamId?.classId || '';
    setEditingSession(s);
    setForm({
      title: s.title,
      meetingDate: s.meetingDate ? new Date(s.meetingDate).toISOString().slice(0, 16) : '',
      startTime: s.startTime || '',
      endTime: s.endTime || '',
      location: s.location || '',
      meetingLink: s.meetingLink || '',
      notes: s.notes || '',
      actionItems: s.actionItems?.map(a => a.content || a.item || a) || [''],
      classId: sessionClassId,
      teamId: sessionTeamId,
    });
    void loadTeamsForClass(sessionClassId);
    setIsModalOpen(true);
  };

  
  const handleDelete = (session) => {
    const isAdmin = user?.role === 'ADMIN';
    setDeleteTarget({
      id: session._id,
      title: session.title,
      action: isAdmin ? 'delete' : 'cancel'
    });
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    try {
      if (deleteTarget.action === 'delete') {
        await mentoringApi.deleteSession(deleteTarget.id);
        setSessions(sessions.filter(s => s._id !== deleteTarget.id));
        toast.success('Session deleted!');
      } else {
        await mentoringApi.cancelSession(deleteTarget.id);
        setSessions(sessions.map(s => s._id === deleteTarget.id ? { ...s, status: 'CANCELLED' } : s));
        toast.success('Session cancelled!');
      }
      setDeleteTarget(null);
    } catch (err) {
      toast.error(err.response?.data?.message || err.response?.data?.error || 'Failed to update session');
    } finally {
      setIsDeleting(false);
    }
  };

  const handleSubmit = async () => {
    if (!form.title || form.title.trim() === '') { 
      toast.error('Session title is required'); 
      return; 
    }
    if (!form.meetingDate) { 
      toast.error('Meeting date is required'); 
      return; 
    }

    const selectedDate = form.meetingDate.split('T')[0];
    if (selectedDate < minMeetingDate) {
      toast.error('Past dates are not allowed');
      return;
    }
    
    // Validate time range if both start and end time provided
    if (form.startTime && form.endTime && form.startTime >= form.endTime) {
      toast.error('Start time must be before end time');
      return;
    }
    
    if (user?.role !== 'STUDENT' && !form.classId) {
      toast.error('Please select a class');
      return;
    }

    if (user?.role !== 'STUDENT' && !form.teamId) { 
      toast.error('Please select a team'); 
      return; 
    }
    
    // Check if user has teams
    if (user?.role !== 'STUDENT' && teams.length === 0) {
      toast.error('You do not have permission to create sessions for any team');
      return;
    }
    
    setIsSubmitting(true);
    try {
      const payload = {
        title: form.title.trim(),
        meetingDate: form.meetingDate,
        startTime: form.startTime ? form.startTime.trim() : '',
        endTime: form.endTime ? form.endTime.trim() : '',
        location: form.location ? form.location.trim() : '',
        meetingLink: form.meetingLink ? form.meetingLink.trim() : '',
        notes: form.notes ? form.notes.trim() : '',
        actionItems: form.actionItems
          .filter(a => a.trim())
          .map(a => ({ content: a.trim(), completed: false })),
        teamId: user?.role === 'STUDENT' ? teamId : form.teamId,
      };
      
      if (editingSession) {
        await mentoringApi.updateSession(editingSession._id, payload);
        toast.success('Session updated!');
      } else {
        await mentoringApi.createSession(payload);
        toast.success('Session scheduled!');
      }
      setIsModalOpen(false);
      await fetchData();
    } catch (err) {
      console.error('Submit error:', err);
      const errorMsg = err.response?.data?.message || err.message || 'Failed to save session';
      toast.error(errorMsg);
    } finally {
      setIsSubmitting(false);
    }
  };

  // Calendar helpers
  const monthStart = new Date(calMonth.getFullYear(), calMonth.getMonth(), 1);
  const daysInMonth = new Date(calMonth.getFullYear(), calMonth.getMonth() + 1, 0).getDate();
  const startWeekday = monthStart.getDay();

  const sessionsOnDay = (day) => {
    return sessions.filter(s => {
      const d = new Date(s.meetingDate);
      return d.getFullYear() === calMonth.getFullYear() && d.getMonth() === calMonth.getMonth() && d.getDate() === day;
    });
  };

  const isUpcoming = (s) => {
    if (!s.meetingDate) return false;
    try {
      const datePart = new Date(s.meetingDate).toISOString().split('T')[0];
      const timePart = s.endTime || s.startTime || '23:59';
      const sessionDateTime = new Date(`${datePart}T${timePart}`);
      return sessionDateTime >= new Date();
    } catch {
      return new Date(s.meetingDate) >= new Date();
    }
  };

  const upcomingSessions = sessions.filter(isUpcoming).sort((a, b) => new Date(a.meetingDate) - new Date(b.meetingDate));
  const pastSessions = sessions.filter(s => !isUpcoming(s)).sort((a, b) => new Date(b.meetingDate) - new Date(a.meetingDate));

  const renderTimetable = () => {
    const slots = SLOT_OPTIONS.map(option => option.val);

    const getClassForSlot = (day, slot) => {
      return myClasses.find(c => {
        return c.schedule && 
               c.schedule.dayOfWeek && 
               c.schedule.dayOfWeek.toUpperCase() === day.toUpperCase() && 
               parseInt(c.schedule.slot, 10) === slot;
      });
    };

    return (
      <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6 overflow-x-auto">
        <div className="flex justify-between items-center mb-6">
          <div>
            <h3 className="text-lg font-bold text-slate-800 flex items-center gap-2">
              <Clock className="w-5 h-5 text-primary" />
              {user?.role === 'LECTURER' || user?.role === 'LECTURE' ? 'Teaching Timetable' : 'Class Timetable'}
            </h3>
            <p className="text-xs text-slate-400 mt-1">Weekly schedule of all your classes and slots</p>
          </div>
        </div>
        <table className="w-full min-w-[700px] border-collapse">
          <thead>
            <tr className="bg-slate-50/70 border-b border-slate-200">
              <th className="py-3 px-4 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider w-[150px]">Time / Slot</th>
              {TEACHING_DAYS.map(d => (
                <th key={d} className="py-3 px-4 text-center text-xs font-semibold text-slate-500 uppercase tracking-wider">
                  {d}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {slots.map(slot => (
              <tr key={slot} className="hover:bg-slate-50/10 transition-colors">
                <td className="py-4 px-4 align-top border-r border-slate-100/60">
                  <div className="font-semibold text-slate-800 text-sm">Slot {slot}</div>
                  <div className="text-xs text-slate-400 mt-1">{formatSlotTime(slot)}</div>
                </td>
                {TEACHING_DAYS.map(day => {
                  const cls = getClassForSlot(day, slot);
                  return (
                    <td key={day} className="py-3 px-3 align-middle border-r border-slate-100 last:border-r-0 text-center">
                      {cls ? (
                        <div className="p-3 rounded-xl bg-gradient-to-br from-primary-50/60 to-primary-100/30 border border-primary-200/60 shadow-xs inline-block w-full">
                          <div className="font-bold text-primary text-sm truncate">{cls.classCode}</div>
                          <div className="text-[11px] font-semibold text-slate-500 mt-1">{cls.subjectCode}</div>
                          <div className="inline-flex items-center gap-1 mt-2 text-[10px] bg-white px-2.5 py-0.5 rounded-full border border-primary-200 text-primary font-medium">
                            Room: {cls.schedule.room || 'TBD'}
                          </div>
                        </div>
                      ) : (
                        <span className="text-slate-300 text-xs italic">—</span>
                      )}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  };

  if (loading) return <LoadingSkeleton />;

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-end">
        <div>
          <h1 className="text-3xl font-bold text-slate-900">Schedules & Timetable</h1>
          <p className="text-slate-500 mt-1">Manage your weekly learning classes and mentoring sessions</p>
        </div>
        {activeView === 'mentoring' && (
          <Button 
            variant="gradient" 
            size="sm" 
            icon={Plus} 
            onClick={openAdd}
            disabled={user?.role !== 'STUDENT' && (!teams || teams.length === 0)}
            title={user?.role !== 'STUDENT' && (!teams || teams.length === 0) ? 'No teams available. You don\'t have permission to create sessions.' : 'Schedule a new mentoring session'}
          >
            Schedule Session
          </Button>
        )}
      </div>

      {/* Tabs */}
      <div className="flex border-b border-slate-200/80 gap-6">
        <button
          onClick={() => setActiveView('timetable')}
          className={`pb-3 text-sm font-semibold border-b-2 transition-all flex items-center gap-2 relative ${
            activeView === 'timetable'
              ? 'border-primary text-primary font-bold'
              : 'border-transparent text-slate-400 hover:text-slate-600'
          }`}
        >
          <Clock className="w-4 h-4" />
          Weekly Timetable
        </button>
        <button
          onClick={() => setActiveView('mentoring')}
          className={`pb-3 text-sm font-semibold border-b-2 transition-all flex items-center gap-2 relative ${
            activeView === 'mentoring'
              ? 'border-primary text-primary font-bold'
              : 'border-transparent text-slate-400 hover:text-slate-600'
          }`}
        >
          <Calendar className="w-4 h-4" />
          Mentoring Sessions
        </button>
      </div>

      {activeView === 'mentoring' ? (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Calendar */}
        <div className="lg:col-span-8 bg-white rounded-2xl border border-slate-200/60 shadow-sm p-6">
          <div className="flex justify-between items-center mb-6">
            <h3 className="text-lg font-semibold text-slate-900">{MONTHS[calMonth.getMonth()]} {calMonth.getFullYear()}</h3>
            <div className="flex bg-slate-100 rounded-lg p-0.5 gap-0.5">
              <button className="p-1.5 rounded-md hover:bg-white transition-all" onClick={() => setCalMonth(d => new Date(d.getFullYear(), d.getMonth() - 1))}>
                <ChevronLeft className="w-4 h-4 text-slate-500" />
              </button>
              <button className="p-1.5 rounded-md hover:bg-white transition-all" onClick={() => setCalMonth(d => new Date(d.getFullYear(), d.getMonth() + 1))}>
                <ChevronRight className="w-4 h-4 text-slate-500" />
              </button>
            </div>
          </div>

          <div className="border border-slate-200 rounded-xl overflow-hidden">
            <div className="grid grid-cols-7 bg-slate-50 border-b border-slate-200">
              {DAYS.map(d => <div key={d} className="py-2.5 text-center text-xs text-slate-400 uppercase font-semibold">{d}</div>)}
            </div>
            <div className="grid grid-cols-7">
              {[...Array(startWeekday)].map((_, i) => <div key={`empty-${i}`} className="min-h-[72px] bg-slate-50/50 border-r border-b border-slate-100" />)}
              {[...Array(daysInMonth)].map((_, i) => {
                const day = i + 1;
                const today = new Date();
                const isToday = today.getDate() === day && today.getMonth() === calMonth.getMonth() && today.getFullYear() === calMonth.getFullYear();
                const daySessions = sessionsOnDay(day);
                return (
                  <div key={day} className={`min-h-[72px] border-r border-b border-slate-100 p-1.5 ${isToday ? 'bg-primary-50/30' : ''}`}>
                    <span className={`inline-flex w-6 h-6 items-center justify-center text-xs font-medium rounded-full ${isToday ? 'bg-primary text-white' : 'text-slate-700'}`}>{day}</span>
                    <div className="mt-0.5 space-y-0.5">
                      {daySessions.slice(0, 2).map(s => (
                        <div key={s._id} className="bg-primary text-white text-[9px] px-1.5 py-0.5 rounded truncate font-medium">{s.title}</div>
                      ))}
                      {daySessions.length > 2 && <div className="text-[9px] text-slate-400">+{daySessions.length - 2} more</div>}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        {/* Sidebar */}
        <div className="lg:col-span-4 space-y-5">
          {/* Upcoming */}
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-5">
            <div className="flex justify-between items-center mb-4">
              <h3 className="font-semibold text-slate-900">Upcoming</h3>
              <span className="text-xs text-slate-400">{upcomingSessions.length} sessions</span>
            </div>
            {upcomingSessions.length === 0 ? (
              <EmptyState icon={Calendar} title="No upcoming sessions" size="sm" />
            ) : (
              <div className="space-y-3">
                {upcomingSessions.slice(0, 3).map(s => (
                  <div key={s._id} className="p-3 rounded-xl bg-primary-50/40 border border-primary-100 group relative">
                    <div className="absolute left-0 top-0 bottom-0 w-1 bg-primary rounded-l-xl" />
                    <div className="flex justify-between items-start pl-2">
                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <h4 className="text-sm font-semibold text-slate-900 truncate">{s.title}</h4>
                          {s.status === 'CANCELLED' && (
                            <span className="text-xs text-red-600 bg-red-50 px-2 py-0.5 rounded-full font-medium">Cancelled</span>
                          )}
                        </div>
                        <p className="text-xs text-slate-500 mt-0.5 flex items-center gap-1">
                          <Clock className="w-3 h-3" />
                          {new Date(s.meetingDate).toLocaleDateString()} {s.startTime ? `${s.startTime}${s.endTime ? ` - ${s.endTime}` : ''}` : ''}
                        </p>
                      </div>
                      <div className="flex gap-2 shrink-0 ml-2 items-start">
                        <button onClick={() => openEdit(s)} className="p-1 rounded text-slate-400 hover:text-primary hover:bg-white transition-all" title="Edit session" aria-label="Edit session"><Edit className="w-3 h-3" /></button>
                        <button
                          onClick={() => handleDelete(s)}
                          title={user?.role === 'ADMIN' ? 'Delete session' : 'Cancel session'}
                          aria-label={user?.role === 'ADMIN' ? 'Delete session' : 'Cancel session'}
                          className="p-1 rounded text-slate-400 hover:text-red-500 hover:bg-white transition-all"
                        >
                          <Trash2 className="w-3 h-3" />
                        </button>
                      </div>
                    </div>
                    <button
                      className={`mt-2 ml-2 w-full text-xs py-1.5 rounded-lg flex items-center justify-center gap-1 transition-colors ${s.status === 'CANCELLED' ? 'bg-slate-100 text-slate-400 pointer-events-none' : 'bg-primary text-white hover:bg-primary-dark'}`}
                      onClick={() => {
                        if (s.status === 'CANCELLED') return;
                        toast.success('Opening Google Meet...', { icon: '🎥' }); setTimeout(() => window.open('https://meet.google.com/new', '_blank'), 800);
                      }}
                    >
                      <Video className="w-3 h-3" /> {s.status === 'CANCELLED' ? 'Cancelled' : 'Join Meet'}
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Past Sessions */}
          <div className="bg-white rounded-2xl border border-slate-200/60 shadow-sm p-5">
            <h3 className="font-semibold text-slate-900 mb-3">Past Sessions</h3>
            {pastSessions.length === 0 ? (
              <p className="text-sm text-slate-400">No past sessions</p>
            ) : (
              <div className="space-y-2">
                {pastSessions.slice(0, 4).map(s => (
                  <div key={s._id} className="flex items-center gap-2.5 p-2.5 rounded-lg hover:bg-slate-50 group">
                    <CheckCircle className="w-4 h-4 text-green-400 shrink-0" />
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-medium text-slate-700 truncate">{s.title}</p>
                      <p className="text-xs text-slate-400">{new Date(s.meetingDate).toLocaleDateString()}</p>
                    </div>
                    <div className="flex gap-1 opacity-0 group-hover:opacity-100">
                      <button onClick={() => openEdit(s)} className="p-1 rounded text-slate-400 hover:text-primary" title="Edit session" aria-label="Edit session"><Edit className="w-3 h-3" /></button>
                      <button onClick={() => handleDelete(s)} title={user?.role === 'ADMIN' ? 'Delete session' : 'Cancel session'} aria-label={user?.role === 'ADMIN' ? 'Delete session' : 'Cancel session'} className="p-1 rounded text-slate-400 hover:text-red-500"><Trash2 className="w-3 h-3" /></button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
      ) : (
        renderTimetable()
      )}

      <Modal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title={editingSession ? 'Edit Session' : 'Schedule Mentoring Session'}
        submitText={editingSession ? 'Save' : 'Schedule'}
        isSubmitting={isSubmitting}
        onSubmit={handleSubmit}
      >
        <div className="space-y-4 max-h-[60vh] overflow-y-auto">
          {user?.role !== 'STUDENT' && (
            <>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Select Class * {myClasses.length === 0 && <span className="text-red-500 text-xs">(No classes available)</span>}</label>
                {myClasses.length === 0 ? (
                  <div className="w-full p-3 border border-red-300 bg-red-50 rounded-lg text-sm text-red-700">
                    No classes available for your account.
                  </div>
                ) : (
                  <select
                    className="w-full border border-slate-200 rounded-lg px-3 py-2 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none text-sm bg-white"
                    value={form.classId}
                    onChange={(e) => handleClassChange(e.target.value)}
                    required
                  >
                    <option value="">-- Choose Class --</option>
                    {myClasses.map(cls => (
                      <option key={cls._id} value={cls._id}>
                        {getClassLabel(cls)}
                      </option>
                    ))}
                  </select>
                )}
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Select Team * {form.classId && selectedClassTeams.length === 0 && <span className="text-red-500 text-xs">(No teams in this class)</span>}</label>
                {!form.classId ? (
                  <div className="w-full p-3 border border-slate-200 bg-slate-50 rounded-lg text-sm text-slate-500">
                    Choose a class first to see its teams.
                  </div>
                ) : selectedClassTeams.length === 0 ? (
                  <div className="w-full p-3 border border-red-300 bg-red-50 rounded-lg text-sm text-red-700">
                    No teams available for the selected class. Contact your admin.
                  </div>
                ) : (
                  <select
                    className="w-full border border-slate-200 rounded-lg px-3 py-2 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none text-sm bg-white"
                    value={form.teamId}
                    onChange={(e) => handleTeamChange(e.target.value)}
                    required
                  >
                    <option value="">-- Choose Team --</option>
                    {selectedClassTeams.map(t => (
                      <option key={t._id} value={t._id}>
                        {t.teamName} ({t.teamCode})
                      </option>
                    ))}
                  </select>
                )}
              </div>
            </>
          )}
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Session Title *</label>
            <input 
              type="text" 
              className="w-full border border-slate-200 rounded-lg px-3 py-2 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none" 
              value={form.title} 
              onChange={(e) => setForm({ ...form, title: e.target.value })} 
              placeholder="e.g. Business Model Review" 
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Date *</label>
            <input 
              type="date" 
              className="w-full border border-slate-200 rounded-lg px-3 py-2 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none" 
              min={minMeetingDate}
              value={form.meetingDate ? form.meetingDate.split('T')[0] : ''} 
              onChange={(e) => {
                const date = e.target.value;
                const time = form.meetingDate ? form.meetingDate.split('T')[1] : '';
                setForm({ ...form, meetingDate: time ? `${date}T${time}` : date });
              }} 
              required
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Start Time</label>
              <input 
                type="time" 
                className="w-full border border-slate-200 rounded-lg px-3 py-2 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none" 
                value={form.startTime} 
                onChange={(e) => setForm({ ...form, startTime: e.target.value })} 
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">End Time</label>
              <input 
                type="time" 
                className="w-full border border-slate-200 rounded-lg px-3 py-2 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none" 
                value={form.endTime} 
                onChange={(e) => setForm({ ...form, endTime: e.target.value })} 
              />
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Location</label>
            <input 
              type="text" 
              className="w-full border border-slate-200 rounded-lg px-3 py-2 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none" 
              value={form.location} 
              onChange={(e) => setForm({ ...form, location: e.target.value })} 
              placeholder="e.g. Room 302 or Online" 
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Meeting Link</label>
            <input 
              type="url" 
              className="w-full border border-slate-200 rounded-lg px-3 py-2 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none" 
              value={form.meetingLink} 
              onChange={(e) => setForm({ ...form, meetingLink: e.target.value })} 
              placeholder="https://meet.google.com/..." 
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Notes</label>
            <textarea 
              className="w-full border border-slate-200 rounded-lg px-3 py-2 focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none resize-none" 
              rows={3} 
              value={form.notes} 
              onChange={(e) => setForm({ ...form, notes: e.target.value })} 
              placeholder="Session agenda, topics to discuss..." 
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Action Items</label>
            {form.actionItems.map((item, i) => (
              <div key={i} className="flex gap-2 mb-2">
                <input 
                  type="text" 
                  className="flex-1 border border-slate-200 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary outline-none" 
                  value={item} 
                  onChange={(e) => {
                    const arr = [...form.actionItems]; 
                    arr[i] = e.target.value; 
                    setForm({ ...form, actionItems: arr });
                  }} 
                  placeholder={`Action item ${i + 1}`} 
                />
                {i > 0 && (
                  <button 
                    type="button" 
                    onClick={() => setForm({ ...form, actionItems: form.actionItems.filter((_, j) => j !== i) })} 
                    className="text-red-400 hover:text-red-600 px-1"
                  >
                    ✕
                  </button>
                )}
              </div>
            ))}
            <button 
              type="button" 
              onClick={() => setForm({ ...form, actionItems: [...form.actionItems, ''] })} 
              className="text-sm text-primary hover:underline"
            >
              + Add action item
            </button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={confirmDelete}
        title={deleteTarget?.action === 'delete' ? 'Delete Session' : 'Cancel Session'}
        description={deleteTarget?.action === 'delete'
          ? `Are you sure you want to delete ${deleteTarget?.title || 'this item'}? This action cannot be undone.`
          : `Are you sure you want to cancel ${deleteTarget?.title || 'this session'}?`
        }
        confirmText={deleteTarget?.action === 'delete' ? 'Delete' : 'Cancel'}
        isSubmitting={isDeleting}
      />
    </div>
  );
};
export default MentoringSessions;
