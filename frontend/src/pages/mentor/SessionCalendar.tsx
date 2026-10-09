// @ts-nocheck
import React, { useState, useEffect } from 'react';
import { getSessionsByTeam, cancelSession } from '../../api/mentoringApi';
import ScheduleSessionModal from './ScheduleSessionModal';
import SessionNoteForm from './SessionNoteForm';

const SessionCalendar = ({ teamId }) => {
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [showModal, setShowModal] = useState(false);
  const [selectedSession, setSelectedSession] = useState(null);
  const [showNoteForm, setShowNoteForm] = useState(false);
  const [currentMonth, setCurrentMonth] = useState(new Date());

  useEffect(() => {
    loadSessions();
  }, [teamId]);

  const loadSessions = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getSessionsByTeam(teamId);
      setSessions(data.data?.sessions || data.sessions || []);
    } catch (err) {
      const errorMsg = err.response?.data?.message || err.message || 'Failed to load sessions';
      console.error('Load sessions error:', err);
      setError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  const handleScheduleClick = (session = null) => {
    setSelectedSession(session);
    setShowModal(true);
  };

  const handleCancelSession = async (sessionId) => {
    if (!window.confirm('Are you sure you want to cancel this session?')) return;

    try {
      await cancelSession(sessionId);
      loadSessions();
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to cancel session');
    }
  };

  const handleNoteClick = (session) => {
    setSelectedSession(session);
    setShowNoteForm(true);
  };

  const formatDate = (dateString) => {
    const date = new Date(dateString);
    return date.toLocaleDateString('vi-VN', {
      weekday: 'short',
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  };

  const getStatusColor = (status) => {
    switch (status) {
      case 'COMPLETED':
        return 'bg-green-100 text-green-800';
      case 'CANCELLED':
        return 'bg-red-100 text-red-800';
      default:
        return 'bg-blue-100 text-blue-800';
    }
  };

  const getDaysInMonth = (date) => {
    return new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();
  };

  const getFirstDayOfMonth = (date) => {
    return new Date(date.getFullYear(), date.getMonth(), 1).getDay();
  };

  const previousMonth = () => {
    setCurrentMonth(new Date(currentMonth.getFullYear(), currentMonth.getMonth() - 1));
  };

  const nextMonth = () => {
    setCurrentMonth(new Date(currentMonth.getFullYear(), currentMonth.getMonth() + 1));
  };

  const getSessionsForDate = (day) => {
    const dateStr = new Date(currentMonth.getFullYear(), currentMonth.getMonth(), day)
      .toISOString()
      .split('T')[0];
    return sessions.filter(s => s.meetingDate?.split('T')[0] === dateStr);
  };

  const daysInMonth = getDaysInMonth(currentMonth);
  const firstDay = getFirstDayOfMonth(currentMonth);
  const monthName = currentMonth.toLocaleDateString('vi-VN', { month: 'long', year: 'numeric' });

  return (
    <div className="space-y-6">
      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
          {error}
        </div>
      )}

      <div className="flex justify-between items-center">
        <h3 className="text-lg font-bold">{monthName}</h3>
        <button
          onClick={() => handleScheduleClick()}
          className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700"
        >
          Schedule Session
        </button>
      </div>

      <div className="bg-white p-6 rounded-lg shadow">
        <div className="flex justify-between items-center mb-4">
          <button onClick={previousMonth} className="px-3 py-1 hover:bg-gray-200 rounded">
            ← Prev
          </button>
          <span className="font-semibold">{monthName}</span>
          <button onClick={nextMonth} className="px-3 py-1 hover:bg-gray-200 rounded">
            Next →
          </button>
        </div>

        <div className="grid grid-cols-7 gap-1 text-center text-sm mb-2">
          <div className="font-semibold text-gray-600">Sun</div>
          <div className="font-semibold text-gray-600">Mon</div>
          <div className="font-semibold text-gray-600">Tue</div>
          <div className="font-semibold text-gray-600">Wed</div>
          <div className="font-semibold text-gray-600">Thu</div>
          <div className="font-semibold text-gray-600">Fri</div>
          <div className="font-semibold text-gray-600">Sat</div>
        </div>

        <div className="grid grid-cols-7 gap-1">
          {Array(firstDay).fill(null).map((_, i) => (
            <div key={`empty-${i}`} className="p-2 text-gray-400"></div>
          ))}
          {Array(daysInMonth).fill(null).map((_, i) => {
            const day = i + 1;
            const daySessionCount = getSessionsForDate(day).length;
            return (
              <div
                key={day}
                className="p-2 border rounded text-center hover:bg-blue-50 cursor-pointer relative"
              >
                <div className="font-semibold text-sm">{day}</div>
                {daySessionCount > 0 && (
                  <div className="text-xs mt-1">
                    <span className="bg-blue-500 text-white rounded px-1 py-0.5 inline-block">
                      {daySessionCount}
                    </span>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>

      <div className="space-y-3">
        <h4 className="font-semibold text-lg">Upcoming Sessions</h4>
        {loading ? (
          <div className="text-center py-8 text-gray-500">Loading sessions...</div>
        ) : sessions.length === 0 ? (
          <div className="text-center py-8 text-gray-500">No sessions scheduled</div>
        ) : (
          <div className="space-y-2">
            {sessions
              .sort((a, b) => new Date(a.meetingDate) - new Date(b.meetingDate))
              .map(session => (
                <div
                  key={session._id}
                  className="p-4 border border-gray-200 rounded-lg bg-white hover:shadow-md transition"
                >
                  <div className="flex justify-between items-start">
                    <div className="flex-1">
                      <h5 className="font-semibold text-base">{session.title}</h5>
                      <div className="text-sm text-gray-600 mt-1 space-y-1">
                        <div>📅 {formatDate(session.meetingDate)}</div>
                        {session.startTime && session.endTime && (
                          <div>⏰ {session.startTime} - {session.endTime}</div>
                        )}
                        {session.location && <div>📍 {session.location}</div>}
                        {session.meetingLink && (
                          <div>
                            <a
                              href={session.meetingLink}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="text-blue-600 hover:underline"
                            >
                              Join Meeting
                            </a>
                          </div>
                        )}
                      </div>
                      {session.description && (
                        <p className="text-sm text-gray-700 mt-2">{session.description}</p>
                      )}
                    </div>
                    <div className="ml-4">
                      <span className={`inline-block px-3 py-1 rounded text-xs font-semibold ${getStatusColor(session.status)}`}>
                        {session.status}
                      </span>
                    </div>
                  </div>

                  <div className="flex gap-2 mt-3 flex-wrap">
                    {session.status === 'SCHEDULED' && (
                      <>
                        <button
                          onClick={() => handleScheduleClick(session)}
                          className="text-xs px-3 py-1 bg-blue-50 text-blue-600 rounded hover:bg-blue-100"
                        >
                          Edit
                        </button>
                        <button
                          onClick={() => handleCancelSession(session._id)}
                          className="text-xs px-3 py-1 bg-red-50 text-red-600 rounded hover:bg-red-100"
                        >
                          Cancel
                        </button>
                      </>
                    )}
                    <button
                      onClick={() => handleNoteClick(session)}
                      className="text-xs px-3 py-1 bg-green-50 text-green-600 rounded hover:bg-green-100"
                    >
                      {session.notes ? 'Edit Notes' : 'Add Notes'}
                    </button>
                  </div>
                </div>
              ))}
          </div>
        )}
      </div>

      <ScheduleSessionModal
        isOpen={showModal}
        onClose={() => {
          setShowModal(false);
          setSelectedSession(null);
        }}
        teamId={teamId}
        session={selectedSession}
        onSuccess={loadSessions}
      />

      {showNoteForm && selectedSession && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-lg shadow-lg p-6 max-w-2xl w-full">
            <h3 className="text-lg font-bold mb-4">Session Notes</h3>
            <SessionNoteForm
              sessionId={selectedSession._id}
              initialNotes={selectedSession.notes || ''}
              onSuccess={() => {
                setShowNoteForm(false);
                loadSessions();
              }}
              onCancel={() => setShowNoteForm(false)}
            />
          </div>
        </div>
      )}
    </div>
  );
};

export default SessionCalendar;