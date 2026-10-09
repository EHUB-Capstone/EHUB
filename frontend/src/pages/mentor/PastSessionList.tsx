// @ts-nocheck
import React, { useState, useEffect } from 'react';
import { getPastSessions } from '../../api/mentoringApi';
import SessionNoteForm from './SessionNoteForm';

const PastSessionList = () => {
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [expandedSession, setExpandedSession] = useState(null);
  const [showNoteForm, setShowNoteForm] = useState(false);
  const [selectedSession, setSelectedSession] = useState(null);
  const [filterStatus, setFilterStatus] = useState('all');

  useEffect(() => {
    loadPastSessions();
  }, []);

  const loadPastSessions = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getPastSessions();
      setSessions(data.data?.sessions || data.sessions || []);
    } catch (err) {
      const errorMsg = err.response?.data?.message || err.message || 'Failed to load past sessions';
      console.error('Load past sessions error:', err);
      setError(errorMsg);
    } finally {
      setLoading(false);
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

  const formatDateTime = (dateString, timeString) => {
    const date = formatDate(dateString);
    if (timeString) {
      return `${date} - ${timeString}`;
    }
    return date;
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

  const getLecturerName = (session) => {
    if (typeof session.lecturerId === 'string') return 'Unknown';
    return session.lecturerId?.name || 'Unknown';
  };

  const getTeamName = (session) => {
    if (typeof session.teamId === 'string') return session.teamId;
    return session.teamId?.teamName || 'Unknown';
  };

  const filteredSessions = filterStatus === 'all'
    ? sessions
    : sessions.filter(s => s.status === filterStatus);

  return (
    <div className="space-y-6">
      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
          {error}
        </div>
      )}

      <div className="flex justify-between items-center">
        <h3 className="text-lg font-bold">Past Mentoring Sessions</h3>
        <div className="flex gap-2">
          <button
            onClick={() => setFilterStatus('all')}
            className={`px-3 py-1 rounded text-sm ${
              filterStatus === 'all'
                ? 'bg-blue-600 text-white'
                : 'bg-gray-200 text-gray-800 hover:bg-gray-300'
            }`}
          >
            All
          </button>
          <button
            onClick={() => setFilterStatus('COMPLETED')}
            className={`px-3 py-1 rounded text-sm ${
              filterStatus === 'COMPLETED'
                ? 'bg-green-600 text-white'
                : 'bg-gray-200 text-gray-800 hover:bg-gray-300'
            }`}
          >
            Completed
          </button>
          <button
            onClick={() => setFilterStatus('CANCELLED')}
            className={`px-3 py-1 rounded text-sm ${
              filterStatus === 'CANCELLED'
                ? 'bg-red-600 text-white'
                : 'bg-gray-200 text-gray-800 hover:bg-gray-300'
            }`}
          >
            Cancelled
          </button>
        </div>
      </div>

      {loading ? (
        <div className="text-center py-12 text-gray-500">
          <div className="inline-block animate-spin">⚙️</div> Loading sessions...
        </div>
      ) : filteredSessions.length === 0 ? (
        <div className="text-center py-12 text-gray-500">
          No past sessions found
        </div>
      ) : (
        <div className="space-y-3">
          {filteredSessions
            .sort((a, b) => new Date(b.meetingDate) - new Date(a.meetingDate))
            .map(session => (
              <div
                key={session._id}
                className="border border-gray-200 rounded-lg bg-white hover:shadow-md transition"
              >
                <div
                  onClick={() => setExpandedSession(expandedSession === session._id ? null : session._id)}
                  className="p-4 cursor-pointer"
                >
                  <div className="flex justify-between items-start">
                    <div className="flex-1">
                      <div className="flex items-center gap-3">
                        <h5 className="font-semibold text-base">{session.title}</h5>
                        <span className={`px-3 py-1 rounded text-xs font-semibold ${getStatusColor(session.status)}`}>
                          {session.status}
                        </span>
                      </div>
                      <div className="text-sm text-gray-600 mt-2 space-y-1">
                        <div>Team: <span className="font-medium">{getTeamName(session)}</span></div>
                        <div>Mentor: <span className="font-medium">{getLecturerName(session)}</span></div>
                        <div>Date: <span className="font-medium">{formatDateTime(session.meetingDate, session.startTime)}</span></div>
                      </div>
                    </div>
                    <div className="ml-4 text-2xl">
                      {expandedSession === session._id ? '▼' : '▶'}
                    </div>
                  </div>
                </div>

                {expandedSession === session._id && (
                  <div className="border-t border-gray-200 p-4 bg-gray-50 space-y-4">
                    {session.description && (
                      <div>
                        <h6 className="font-semibold text-sm mb-2">Description</h6>
                        <p className="text-sm text-gray-700">{session.description}</p>
                      </div>
                    )}

                    {session.location && (
                      <div>
                        <h6 className="font-semibold text-sm mb-2">Location</h6>
                        <p className="text-sm text-gray-700">{session.location}</p>
                      </div>
                    )}

                    {session.meetingLink && (
                      <div>
                        <h6 className="font-semibold text-sm mb-2">Meeting Link</h6>
                        <a
                          href={session.meetingLink}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="text-blue-600 hover:underline text-sm"
                        >
                          {session.meetingLink}
                        </a>
                      </div>
                    )}

                    {session.notes && (
                      <div>
                        <h6 className="font-semibold text-sm mb-2">Notes</h6>
                        <p className="text-sm text-gray-700 whitespace-pre-wrap">{session.notes}</p>
                      </div>
                    )}

                    {session.actionItems && session.actionItems.length > 0 && (
                      <div>
                        <h6 className="font-semibold text-sm mb-2">Action Items</h6>
                        <ul className="space-y-2">
                          {session.actionItems.map((item, idx) => (
                            <li key={idx} className="text-sm flex items-start gap-2 text-gray-700">
                              <input
                                type="checkbox"
                                checked={item.completed || item.done}
                                disabled
                                className="mt-0.5"
                              />
                              <span className={item.completed || item.done ? 'line-through text-gray-500' : ''}>
                                {item.content || item.item}
                              </span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}

                    <div className="flex gap-2 pt-2">
                      <button
                        onClick={() => handleNoteClick(session)}
                        className="text-xs px-3 py-1 bg-blue-50 text-blue-600 rounded hover:bg-blue-100"
                      >
                        {session.notes ? 'Edit Notes' : 'Add Notes'}
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ))}
        </div>
      )}

      {showNoteForm && selectedSession && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-lg shadow-lg p-6 max-w-2xl w-full">
            <h3 className="text-lg font-bold mb-4">Session Notes</h3>
            <SessionNoteForm
              sessionId={selectedSession._id}
              initialNotes={selectedSession.notes || ''}
              onSuccess={() => {
                setShowNoteForm(false);
                loadPastSessions();
              }}
              onCancel={() => setShowNoteForm(false)}
            />
          </div>
        </div>
      )}
    </div>
  );
};

export default PastSessionList;