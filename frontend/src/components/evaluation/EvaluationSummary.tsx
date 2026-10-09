// @ts-nocheck
// frontend/src/components/evaluation/EvaluationSummary.jsx
import React, { useState, useEffect } from 'react';
import RubricScoringForm from './RubricScoringForm';
import CommentThread from './CommentThread';
import PerformanceLevelBadge from './PerformanceLevelBadge';

/**
 * EvaluationSummary Component
 * 
 * Displays comprehensive evaluation summary with:
 * - Rubric scores table with weighted calculations
 * - Overall feedback section
 * - Comments/feedback threads
 * - Evaluation history
 */
const EvaluationSummary = ({ 
  evaluation, 
  checkpoint,
  isLecturer = false,
  isMentor = false,
  currentUserId,
  currentUserRole,
  onUpdate,
  onSubmit
}) => {
  const [editMode, setEditMode] = useState(false);
  const shouldHideSensitiveScores = isMentor || (currentUserRole || '').toUpperCase() === 'STUDENT' || (currentUserRole || '').toUpperCase() === 'USER';
  const [formData, setFormData] = useState({
    overallFeedback: evaluation?.overallFeedback || '',
  });

  const [rubricScores, setRubricScores] = useState(evaluation?.rubricScores || []);
  const [totalScore, setTotalScore] = useState(evaluation?.checkpointTotal || 0);

  const canEdit = isLecturer;
  const canSubmit = isLecturer && evaluation?.status === 'DRAFT';

  const handleSaveChanges = async () => {
    await onUpdate?.({
      ...formData,
      rubricScores: rubricScores,
      status: evaluation?.status === 'SUBMITTED' || evaluation?.status === 'PUBLISHED'
        ? evaluation.status
        : 'DRAFT'
    });
    setEditMode(false);
  };

  const handleSubmit = async () => {
    if (window.confirm('Are you sure you want to submit this evaluation?')) {
      await onSubmit?.({
        ...formData,
        rubricScores: rubricScores,
        status: 'SUBMITTED'
      });
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="bg-white rounded-lg shadow-sm p-6 border border-gray-200">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="text-2xl font-bold text-gray-800">Evaluation Summary</h2>
            <p className="text-sm text-gray-600 mt-1">
              {checkpoint?.title || `Checkpoint ${evaluation?.checkpointNumber}`}
            </p>
          </div>
          <div className="text-right">
            {shouldHideSensitiveScores ? (
              /* Mentor: show performance badge instead of numeric total */
              evaluation?.overallLevel && evaluation.overallLevel !== 'Unscored' ? (
                <div className="flex flex-col items-end gap-1.5">
                  <PerformanceLevelBadge
                    level={evaluation.overallLevel}
                    label={evaluation.overallLabel || ''}
                    size="lg"
                  />
                </div>
              ) : null
            ) : (
              /* Lecturer / Admin / Student: full numeric score */
              <>
                <div className="text-4xl font-bold text-blue-600">
                  {totalScore.toFixed(2)}/10
                </div>
                <div className={`text-sm font-semibold mt-1 px-3 py-1 rounded-full inline-block ${
                  evaluation?.status === 'SUBMITTED' ? 'bg-green-100 text-green-800' :
                  evaluation?.status === 'DRAFT' ? 'bg-yellow-100 text-yellow-800' :
                  'bg-gray-100 text-gray-800'
                }`}>
                  {evaluation?.status || 'DRAFT'}
                </div>
              </>
            )}
          </div>
        </div>

        <div className="grid grid-cols-3 gap-4 text-sm">
          <div className="bg-blue-50 p-3 rounded">
            <div className="text-gray-600">Evaluator</div>
            <div className="font-semibold">{evaluation?.lecturerId?.name}</div>
          </div>
          <div className="bg-blue-50 p-3 rounded">
            <div className="text-gray-600">Role</div>
            <div className="font-semibold">{evaluation?.evaluatorRole}</div>
          </div>
          <div className="bg-blue-50 p-3 rounded">
            <div className="text-gray-600">Submitted</div>
            <div className="font-semibold">
              {evaluation?.submittedAt ? new Date(evaluation.submittedAt).toLocaleDateString() : 'Not submitted'}
            </div>
          </div>
        </div>

        {/* Action Buttons */}
        {canEdit && (
          <div className="flex gap-2 mt-4">
            {!editMode ? (
              <>
                <button
                  onClick={() => setEditMode(true)}
                  className="btn btn-primary btn-sm"
                >
                  Edit Evaluation
                </button>
                {canSubmit && (
                  <button
                    onClick={handleSubmit}
                    className="btn btn-success btn-sm"
                  >
                    Submit Official Evaluation
                  </button>
                )}
              </>
            ) : (
              <>
                <button
                  onClick={handleSaveChanges}
                  className="btn btn-primary btn-sm"
                >
                  Save Changes
                </button>
                <button
                  onClick={() => {
                    setEditMode(false);
                    setFormData({
                      overallFeedback: evaluation?.overallFeedback || '',
                    });
                  }}
                  className="btn btn-ghost btn-sm"
                >
                  Cancel
                </button>
              </>
            )}
          </div>
        )}
      </div>

      {/* Rubric Scoring Section */}
      <div className="bg-white rounded-lg shadow-sm p-6 border border-gray-200">
        <h3 className="text-xl font-bold text-gray-800 mb-4">Rubric Scores</h3>
        <RubricScoringForm
          checkpointNumber={evaluation?.checkpointNumber}
          initialScores={evaluation?.rubricScores || []}
          onScoresChange={setRubricScores}
          onTotalChange={setTotalScore}
          readOnly={!editMode || !canEdit}
          hideSensitiveScores={shouldHideSensitiveScores}
        />
      </div>

      {/* Feedback Section */}
      <div className="bg-white rounded-lg shadow-sm p-6 border border-gray-200">
        <h3 className="text-xl font-bold text-gray-800 mb-4">Detailed Feedback</h3>
        
        <div className="space-y-4">
          {/* Overall Feedback */}
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-2">
              Overall Feedback
            </label>
            {editMode && canEdit ? (
              <textarea
                value={formData.overallFeedback}
                onChange={(e) => setFormData({...formData, overallFeedback: e.target.value})}
                placeholder="Provide overall feedback and observations..."
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                rows="5"
              />
            ) : (
              <div className="bg-gray-50 p-3 rounded-lg text-sm text-gray-700">
                {formData.overallFeedback || 'No overall feedback provided.'}
              </div>
            )}
          </div>

        </div>
      </div>

      {/* Statistics — hidden for Mentor/Student */}
      {!shouldHideSensitiveScores && (
        <div className="grid grid-cols-2 gap-4">
          <div className="bg-gradient-to-br from-blue-50 to-blue-100 rounded-lg p-4 border border-blue-200">
            <div className="text-sm text-blue-700 font-semibold">Average Score</div>
            <div className="text-2xl font-bold text-blue-900 mt-1">{totalScore.toFixed(2)}</div>
          </div>
          <div className="bg-gradient-to-br from-purple-50 to-purple-100 rounded-lg p-4 border border-purple-200">
            <div className="text-sm text-purple-700 font-semibold">Criteria Evaluated</div>
            <div className="text-2xl font-bold text-purple-900 mt-1">{rubricScores.length}</div>
          </div>
        </div>
      )}
    </div>
  );
};

export default EvaluationSummary;
