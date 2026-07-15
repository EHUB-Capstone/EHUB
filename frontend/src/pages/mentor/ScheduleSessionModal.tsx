// @ts-nocheck
import React, { useState, useEffect } from 'react';
import { createSession, updateSession } from '../../api/mentoringApi';

const ScheduleSessionModal = ({ isOpen, onClose, teamId, session = null, onSuccess }) => {
  const [formData, setFormData] = useState({
    title: '',
    description: '',
    meetingDate: '',
    startTime: '',
    endTime: '',
    location: '',
    meetingLink: ''
  });

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (session) {
      setFormData({
        title: session.title || '',
        description: session.description || '',
        meetingDate: session.meetingDate ? session.meetingDate.split('T')[0] : '',
        startTime: session.startTime || '',
        endTime: session.endTime || '',
        location: session.location || '',
        meetingLink: session.meetingLink || ''
      });
    } else {
      setFormData({
        title: '',
        description: '',
        meetingDate: '',
        startTime: '',
        endTime: '',
        location: '',
        meetingLink: ''
      });
    }
    setError('');
  }, [session, isOpen]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: value
    }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    if (!formData.title || formData.title.trim() === '') {
      setError('Session title is required');
      setLoading(false);
      return;
    }

    if (!formData.meetingDate) {
      setError('Meeting date is required');
      setLoading(false);
      return;
    }

    if (formData.startTime && formData.endTime && formData.startTime >= formData.endTime) {
      setError('Start time must be before end time');
      setLoading(false);
      return;
    }

    try {
      if (session && session._id) {
        await updateSession(session._id, formData);
      } else {
        await createSession({
          ...formData,
          teamId
        });
      }

      onSuccess?.();
      onClose();
    } catch (err) {
      const errorMsg = err.response?.data?.message || err.message || 'Failed to save session';
      console.error('Session error:', err);
      setError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-lg p-6 max-w-md w-full mx-4">
        <h2 className="text-xl font-bold mb-4">
          {session ? 'Edit Session' : 'Schedule Mentoring Session'}
        </h2>

        {error && (
          <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-4">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium mb-1">Title *</label>
            <input
              type="text"
              name="title"
              value={formData.title}
              onChange={handleChange}
              required
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring focus:ring-blue-200"
              placeholder="e.g., Sprint Review"
            />
          </div>

          <div>
            <label className="block text-sm font-medium mb-1">Description</label>
            <textarea
              name="description"
              value={formData.description}
              onChange={handleChange}
              rows="3"
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring focus:ring-blue-200"
              placeholder="Session details..."
            />
          </div>

          <div>
            <label className="block text-sm font-medium mb-1">Date *</label>
            <input
              type="date"
              name="meetingDate"
              value={formData.meetingDate}
              onChange={handleChange}
              required
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring focus:ring-blue-200"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium mb-1">Start Time</label>
              <input
                type="time"
                name="startTime"
                value={formData.startTime}
                onChange={handleChange}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring focus:ring-blue-200"
              />
            </div>
            <div>
              <label className="block text-sm font-medium mb-1">End Time</label>
              <input
                type="time"
                name="endTime"
                value={formData.endTime}
                onChange={handleChange}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring focus:ring-blue-200"
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium mb-1">Location</label>
            <input
              type="text"
              name="location"
              value={formData.location}
              onChange={handleChange}
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring focus:ring-blue-200"
              placeholder="e.g., Room 302 or Online"
            />
          </div>

          <div>
            <label className="block text-sm font-medium mb-1">Meeting Link</label>
            <input
              type="url"
              name="meetingLink"
              value={formData.meetingLink}
              onChange={handleChange}
              className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring focus:ring-blue-200"
              placeholder="https://meet.google.com/..."
            />
          </div>

          <div className="flex gap-3 justify-end">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50"
              disabled={loading}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50"
              disabled={loading}
            >
              {loading ? 'Saving...' : session ? 'Update' : 'Schedule'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default ScheduleSessionModal;