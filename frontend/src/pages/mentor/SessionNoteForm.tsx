// @ts-nocheck
import React, { useState, useEffect } from 'react';
import { addSessionNote } from '../../api/mentoringApi';
import { useTranslation } from 'react-i18next';

const SessionNoteForm = ({ sessionId, initialNotes = '', onSuccess, onCancel }) => {
  const { t } = useTranslation('mentoring');
  const [notes, setNotes] = useState(initialNotes);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    setNotes(initialNotes);
    setError('');
  }, [initialNotes, sessionId]);

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!notes.trim()) {
      setError(t('sessionNoteForm.notesContentIsRequired'));
      return;
    }

    setLoading(true);
    setError('');

    try {
      await addSessionNote(sessionId, notes);
      onSuccess?.();
    } catch (err) {
      setError(err.response?.data?.message || t('sessionNoteForm.failedToSaveNotes'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
          {error}
        </div>
      )}

      <div>
        <label className="block text-sm font-medium mb-2">{t('pastSessionList.sessionNotes')}</label>
        <textarea
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          rows="6"
          className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring focus:ring-blue-200"
          placeholder={t('sessionNoteForm.documentDiscussionPointsFeedbackDecisionsAnd')}
        />
      </div>

      <div className="flex gap-3 justify-end">
        {onCancel && (
          <button
            type="button"
            onClick={onCancel}
            className="px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50"
            disabled={loading}
          >
            {t('common:actions.cancel')}
          </button>
        )}
        <button
          type="submit"
          className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50"
          disabled={loading}
        >
          {loading ? t('scheduleSessionModal.saving') : t('sessionNoteForm.saveNotes')}
        </button>
      </div>
    </form>
  );
};

export default SessionNoteForm;