// @ts-nocheck
// frontend/src/components/evaluation/EvaluationHistoryPage.jsx
import React, { useState, useEffect } from 'react';
import moment from 'moment';
import PerformanceLevelBadge from './PerformanceLevelBadge';

/**
 * EvaluationHistoryPage Component
 * 
 * Displays comprehensive evaluation history including:
 * - Timeline of all evaluation actions (CREATE, UPDATE, SUBMIT)
 * - Version comparisons (diff between versions)
 * - Who changed what and when
 * - Score changes over time
 * - Audit trail for compliance
 */
const EvaluationHistoryPage = ({ 
  history = [],
  evaluation,
  teamId,
  currentUserRole = '',
  loading = false,
  error = null
}) => {
  const isMentor = (currentUserRole || '').toUpperCase() === 'MENTOR' || (currentUserRole || '').toUpperCase() === 'STUDENT';
  const [selectedVersion, setSelectedVersion] = useState(null);
  const [compareMode, setCompareMode] = useState(false);
  const [compareWithVersion, setCompareWithVersion] = useState(null);
  const [expandedItem, setExpandedItem] = useState(null);

  const actionColors = {
    CREATED: 'bg-blue-100 text-blue-800 border-blue-300',
    UPDATED: 'bg-yellow-100 text-yellow-800 border-yellow-300',
    SUBMITTED: 'bg-green-100 text-green-800 border-green-300',
    PUBLISHED: 'bg-purple-100 text-purple-800 border-purple-300',
  };

  const actionIcons = {
    CREATED: '✨',
    UPDATED: '✏️',
    SUBMITTED: '📤',
    PUBLISHED: '📢',
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="text-center">
          <div className="loading loading-lg text-primary mb-4"></div>
          <p>Loading evaluation history...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="alert alert-error">
        <span>{error}</span>
      </div>
    );
  }

  if (history.length === 0) {
    return (
      <div className="bg-gray-50 rounded-lg p-12 text-center border border-gray-200">
        <div className="text-4xl mb-4">📋</div>
        <p className="text-gray-600 font-semibold">No evaluation history available</p>
      </div>
    );
  }

  return (
    <div className="max-w-6xl mx-auto p-6">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900 mb-2">Evaluation History</h1>
        <p className="text-gray-600">
          {history.length} {history.length === 1 ? 'event' : 'events'} recorded
        </p>
      </div>

      <div className="grid grid-cols-4 gap-4 mb-8">
        <div className="bg-blue-50 rounded-lg p-4 border border-blue-200">
          <div className="text-sm text-blue-600 font-semibold">Total Changes</div>
          <div className="text-2xl font-bold text-blue-900">{history.length}</div>
        </div>
        <div className="bg-green-50 rounded-lg p-4 border border-green-200">
          <div className="text-sm text-green-600 font-semibold">Submissions</div>
          <div className="text-2xl font-bold text-green-900">
            {history.filter(h => h.action === 'SUBMITTED').length}
          </div>
        </div>
        <div className="bg-yellow-50 rounded-lg p-4 border border-yellow-200">
          <div className="text-sm text-yellow-600 font-semibold">Updates</div>
          <div className="text-2xl font-bold text-yellow-900">
            {history.filter(h => h.action === 'UPDATED').length}
          </div>
        </div>
        <div className="bg-purple-50 rounded-lg p-4 border border-purple-200">
          <div className="text-sm text-purple-600 font-semibold">Last Modified</div>
          <div className="text-sm font-bold text-purple-900">
            {moment(history[0]?.createdAt).fromNow()}
          </div>
        </div>
      </div>

      {/* Timeline */}
      <div className="space-y-4">
        {history.map((item, index) => (
          <div
            key={item._id}
            className={`rounded-lg border-l-4 p-4 cursor-pointer transition-all ${actionColors[item.action] || actionColors.UPDATED}`}
            onClick={() => setExpandedItem(expandedItem === item._id ? null : item._id)}
          >
            {/* Timeline Item Header */}
            <div className="flex items-start justify-between">
              <div className="flex items-center gap-4 flex-1">
                <div className="text-2xl">{actionIcons[item.action]}</div>
                <div className="flex-1">
                  <div className="font-bold text-gray-900">
                    {item.action === 'CREATED' && 'Evaluation Created'}
                    {item.action === 'UPDATED' && 'Evaluation Updated'}
                    {item.action === 'SUBMITTED' && 'Evaluation Submitted'}
                    {item.action === 'PUBLISHED' && 'Evaluation Published'}
                  </div>
                  <div className="text-sm text-gray-600 mt-1">
                    by <span className="font-semibold">{item.changedBy?.name}</span>
                    {' '} • {moment(item.createdAt).format('MMMM D, YYYY h:mm A')}
                  </div>
                  {item.note && (
                    <div className="text-sm text-gray-700 mt-2 italic">
                      "{item.note}"
                    </div>
                  )}
                </div>
              </div>
              <div className="text-gray-500 font-bold text-xl">
                {expandedItem === item._id ? '▼' : '▶'}
              </div>
            </div>

            {/* Expandable Details */}
            {expandedItem === item._id && item.snapshot && (
              <div className="mt-4 pt-4 border-t border-gray-300 space-y-4">
                {/* Rubric Scores Changes */}
                {item.snapshot.rubricScores && (
                  <div>
                    <h4 className="font-bold text-gray-900 mb-3">Scores</h4>
                    <div className="grid grid-cols-2 gap-3">
                      {item.snapshot.rubricScores.map((score) => (
                        <div key={score.criterionKey} className="bg-white bg-opacity-50 p-3 rounded">
                          <div className="text-sm font-semibold text-gray-800">
                            {score.criterionName}
                          </div>
                          <div className="flex items-center justify-between mt-1">
                            <span className="text-sm text-gray-600">
                              Weight: {score.weight}%
                            </span>
                            {/* Mentor sees level badge; others see numeric score */}
                            {isMentor ? (
                              <PerformanceLevelBadge
                                level={score.level || 'Unscored'}
                                label={score.label || ''}
                                size="sm"
                                showLabel={false}
                              />
                            ) : (
                              <span className="text-sm font-bold text-blue-700">
                                {score.score}/10
                              </span>
                            )}
                          </div>
                          {!isMentor && (
                            <div className="text-xs text-gray-600 mt-1">
                              Weighted: {score.weightedScore}
                            </div>
                          )}
                          {score.comment && (
                            <div className="text-xs text-gray-700 mt-2 bg-white bg-opacity-75 p-2 rounded">
                              "{score.comment}"
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Overall Feedback */}
                {item.snapshot.overallFeedback && (
                  <div>
                    <h4 className="font-bold text-gray-900 mb-2">Overall Feedback</h4>
                    <div className="bg-white bg-opacity-50 p-3 rounded text-sm text-gray-800 whitespace-pre-wrap">
                      {item.snapshot.overallFeedback}
                    </div>
                  </div>
                )}


                {/* Change Summary */}
                <div className="bg-white bg-opacity-75 p-3 rounded border border-gray-300">
                  <div className={`grid gap-3 text-sm ${isMentor ? 'grid-cols-2' : 'grid-cols-3'}`}>
                    {/* Checkpoint Total — hidden for Mentor */}
                    {!isMentor && (
                      <div>
                        <div className="text-gray-600">Checkpoint Total</div>
                        <div className="font-bold text-lg text-blue-700">
                          {item.snapshot.checkpointTotal?.toFixed(2) || 'N/A'}
                        </div>
                      </div>
                    )}
                    {isMentor && (
                      <div>
                        <div className="text-gray-600">Overall Performance</div>
                        <div className="mt-1">
                          <PerformanceLevelBadge
                            level={item.snapshot.overallLevel || 'Unscored'}
                            label={item.snapshot.overallLabel || ''}
                            size="sm"
                          />
                        </div>
                      </div>
                    )}
                    <div>
                      <div className="text-gray-600">Status</div>
                      <div className="font-bold">
                        <span className={`px-2 py-1 rounded text-xs font-semibold ${
                          item.snapshot.status === 'SUBMITTED' ? 'bg-green-200 text-green-800' :
                          item.snapshot.status === 'DRAFT' ? 'bg-yellow-200 text-yellow-800' :
                          'bg-gray-200 text-gray-800'
                        }`}>
                          {item.snapshot.status}
                        </span>
                      </div>
                    </div>
                    <div>
                      <div className="text-gray-600">Modified By</div>
                      <div className="font-bold text-gray-900">
                        {item.changedBy?.name}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        ))}
      </div>

      {/* Timeline Footer */}
      <div className="mt-8 p-4 bg-gray-50 rounded-lg border border-gray-200 text-center">
        <p className="text-sm text-gray-600">
          First evaluation created on{' '}
          <span className="font-semibold">
            {moment(history[history.length - 1]?.createdAt).format('MMMM D, YYYY')}
          </span>
        </p>
      </div>
    </div>
  );
};

export default EvaluationHistoryPage;
