// @ts-nocheck
import { useCallback, useEffect, useState } from 'react';
import { getSessionsByTeam, createSession, updateSession, deleteSession, cancelSession } from '../../api/mentoringApi';
import { useAuth } from '../../hooks/useAuth';
import toast from 'react-hot-toast';
import { useTranslation } from 'react-i18next';
import { formatDate as fmtDate } from '../../i18n/format';

export default function MentoringPanel({ teamId, isReadOnly = false }) {
  const { t } = useTranslation('workspace');
  const { user } = useAuth();
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [selectedSession, setSelectedSession] = useState(null);
  
  // Form State
  const [formData, setFormData] = useState({
    title: '', description: '', meetingDate: '', startTime: '', endTime: '', location: '', meetingLink: '', status: 'SCHEDULED', notes: ''
  });

  const isStudent = user?.role === 'STUDENT' || user?.role === 'USER';

  const fetchSessions = useCallback(async () => {
    try {
      setLoading(true);
      const res = await getSessionsByTeam(teamId);
      if (res.success) setSessions(res.data.sessions || []);
    } catch {
      toast.error(t('mentoringPanel.failedToLoadMentoringSessions'));
    } finally {
      setLoading(false);
    }
  }, [teamId]);

  useEffect(() => {
    // Fetching sessions is the side effect owned by this panel.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchSessions();
  }, [fetchSessions]);

  const openModal = (session = null) => {
    if (session) {
      setSelectedSession(session);
      setFormData({
        title: session.title || '',
        description: session.description || '',
        meetingDate: session.meetingDate ? new Date(session.meetingDate).toISOString().split('T')[0] : '',
        startTime: session.startTime || '',
        endTime: session.endTime || '',
        location: session.location || '',
        meetingLink: session.meetingLink || '',
        status: session.status || 'SCHEDULED',
        notes: session.notes || ''
      });
    } else {
      setSelectedSession(null);
      setFormData({
        title: '', description: '', meetingDate: '', startTime: '', endTime: '', location: '', meetingLink: '', status: 'SCHEDULED', notes: ''
      });
    }
    setIsModalOpen(true);
  };

  const closeModal = () => {
    setIsModalOpen(false);
    setSelectedSession(null);
  };

  const handleChange = (e) => setFormData({ ...formData, [e.target.name]: e.target.value });

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (selectedSession) {
        await updateSession(selectedSession._id, formData);
        toast.success(t('mentoringPanel.sessionUpdated'));
      } else {
        await createSession({ ...formData, teamId });
        toast.success(t('mentoringPanel.sessionCreated'));
      }
      closeModal();
      fetchSessions();
    } catch (err) {
      toast.error(err.response?.data?.error || t('mentoringPanel.failedToSaveSession'));
    }
  };

  const handleDelete = async (id) => {
    const isAdmin = user?.role === 'ADMIN';
    const confirmMessage = isAdmin
      ? 'Are you sure you want to delete this session?'
      : 'Are you sure you want to cancel this session?';

    if (!window.confirm(confirmMessage)) return;
    try {
      if (isAdmin) {
        await deleteSession(id);
        toast.success(t('mentoringPanel.sessionDeleted'));
      } else {
        await cancelSession(id);
        toast.success(t('mentoringPanel.sessionCancelled'));
      }
      fetchSessions();
    } catch (err) {
      toast.error(err.response?.data?.message || err.response?.data?.error || t('mentoringPanel.failedToUpdateSession'));
    }
  };

  if (loading) return <div className="p-4 text-center text-gray-500">{t('mentoringPanel.loadingSessions')}</div>;

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center mb-6">
        <div>
          <h2 className="text-lg font-medium text-gray-900">{t('mentoringPanel.mentoringSessions')}</h2>
          <p className="text-sm text-gray-500">{t('mentoringPanel.viewAndManageScheduledMentoringMeetings')}</p>
        </div>
        {!isStudent && !isReadOnly && (
          <button
            onClick={() => openModal()}
            className="px-4 py-2 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
          >
            {t('mentoringPanel.scheduleSession')}
          </button>
        )}
      </div>

      {sessions.length === 0 ? (
        <div className="text-center p-8 bg-gray-50 rounded-lg border border-dashed border-gray-300">
          <p className="text-gray-500">{t('mentoringPanel.noMentoringSessionsScheduled')}</p>
        </div>
      ) : (
        <div className="space-y-4">
          {sessions.map(s => (
            <div key={s._id} className="bg-white p-5 rounded-lg border border-gray-200 shadow-sm flex flex-col md:flex-row md:items-start justify-between">
              <div className="space-y-2">
                <div className="flex items-center space-x-3">
                  <h3 className="text-lg font-bold text-gray-900">{s.title}</h3>
                  <span className={`px-2 py-0.5 text-xs font-semibold rounded-full ${
                    s.status === 'COMPLETED' ? 'bg-green-100 text-green-800' :
                    s.status === 'CANCELLED' ? 'bg-red-100 text-red-800' : 'bg-blue-100 text-blue-800'
                  }`}>
                    {s.status}
                  </span>
                </div>
                <p className="text-sm text-gray-600">{s.description}</p>
                <div className="text-sm text-gray-500 grid grid-cols-1 sm:grid-cols-2 gap-2 mt-2">
                  <div className="flex items-center">
                    <svg className="w-4 h-4 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" /></svg>
                    {s.meetingDate ? fmtDate(s.meetingDate, { year: 'numeric', month: 'short', day: 'numeric' }) : 'TBD'}
                  </div>
                  <div className="flex items-center">
                    <svg className="w-4 h-4 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
                    {s.startTime || 'TBD'} {s.endTime ? `- ${s.endTime}` : ''}
                  </div>
                  <div className="flex items-center">
                    <svg className="w-4 h-4 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" /></svg>
                    {s.location || t('mentoringPanel.nA')}
                  </div>
                  {s.meetingLink && (
                    <div className="flex items-center text-blue-600">
                      <svg className="w-4 h-4 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" /></svg>
                      <a href={s.meetingLink} target="_blank" rel="noreferrer" className="hover:underline text-sm">{t('mentoringPanel.joinLink')}</a>
                    </div>
                  )}
                </div>
                {s.notes && (
                  <div className="mt-3 p-3 bg-gray-50 rounded text-sm text-gray-700 whitespace-pre-wrap">
                    <span className="font-semibold block mb-1">{t('mentoringPanel.sessionNotes')}</span>
                    {s.notes}
                  </div>
                )}
                <div className="mt-2 text-xs text-gray-400">
                  {t('mentoringPanel.by', { name: s.lecturerId?.name })}
                </div>
              </div>
              
              {!isStudent && !isReadOnly && (
                <div className="mt-4 md:mt-0 flex space-x-2">
                  <button onClick={() => openModal(s)} className="text-blue-600 hover:text-blue-900 text-sm font-medium">{t('common:actions.edit')}</button>
                  <button onClick={() => handleDelete(s._id)} className="text-red-600 hover:text-red-900 text-sm font-medium">{user?.role === 'ADMIN' ? t('common:actions.delete') : t('common:actions.cancel')}</button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {/* Modal */}
      {isModalOpen && (
        <div className="fixed z-50 inset-0 overflow-y-auto">
          <div className="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
            <div className="fixed inset-0 transition-opacity" aria-hidden="true" onClick={closeModal}>
              <div className="absolute inset-0 bg-gray-500 opacity-75"></div>
            </div>
            <div className="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-2xl sm:w-full">
              <form onSubmit={handleSubmit}>
                <div className="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
                  <h3 className="text-lg leading-6 font-medium text-gray-900 mb-4">
                    {selectedSession ? t('mentoringPanel.editMentoringSession') : t('mentoringPanel.scheduleMentoringSession')}
                  </h3>
                  <div className="grid grid-cols-1 gap-y-4 gap-x-4 sm:grid-cols-2">
                    <div className="sm:col-span-2">
                      <label className="block text-sm font-medium text-gray-700">{t('mentoringPanel.title')} <span className="text-red-500">*</span></label>
                      <input type="text" name="title" required value={formData.title} onChange={handleChange} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm" />
                    </div>
                    <div className="sm:col-span-2">
                      <label className="block text-sm font-medium text-gray-700">{t('taskModal.description')}</label>
                      <textarea name="description" rows="2" value={formData.description} onChange={handleChange} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm" />
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700">{t('mentoringPanel.date')} <span className="text-red-500">*</span></label>
                      <input type="date" name="meetingDate" required value={formData.meetingDate} onChange={handleChange} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm" />
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700">{t('taskTableView.status')}</label>
                      <select name="status" value={formData.status} onChange={handleChange} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm">
                        <option value="SCHEDULED">{t('mentoringPanel.scheduled')}</option>
                        <option value="COMPLETED">{t('boardSummary.completed')}</option>
                        <option value="CANCELLED">{t('mentoringPanel.cancelled')}</option>
                      </select>
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700">{t('mentoringPanel.startTime')}</label>
                      <input type="time" name="startTime" value={formData.startTime} onChange={handleChange} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm" />
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700">{t('mentoringPanel.endTime')}</label>
                      <input type="time" name="endTime" value={formData.endTime} onChange={handleChange} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm" />
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700">{t('mentoringPanel.locationRoom')}</label>
                      <input type="text" name="location" value={formData.location} onChange={handleChange} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm" />
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700">{t('mentoringPanel.meetingLinkUrl')}</label>
                      <input type="url" name="meetingLink" value={formData.meetingLink} onChange={handleChange} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm" />
                    </div>
                    <div className="sm:col-span-2">
                      <label className="block text-sm font-medium text-gray-700">{t('mentoringPanel.sessionNotes2')}</label>
                      <textarea name="notes" rows="4" value={formData.notes} onChange={handleChange} placeholder={t('mentoringPanel.writePrivateOrPublicSessionNotes')} className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm" />
                    </div>
                  </div>
                </div>
                <div className="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
                  <button type="submit" className="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-blue-600 text-base font-medium text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 sm:ml-3 sm:w-auto sm:text-sm">
                    {selectedSession ? t('taskModal.saveChanges') : t('mentoringPanel.schedule')}
                  </button>
                  <button type="button" onClick={closeModal} className="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm">
                    {t('common:actions.cancel')}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
