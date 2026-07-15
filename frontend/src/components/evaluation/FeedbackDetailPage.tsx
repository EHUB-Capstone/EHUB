// @ts-nocheck
// frontend/src/components/evaluation/FeedbackDetailPage.jsx
import React, { useState, useEffect } from 'react';
import CommentThread from './CommentThread';

/**
 * FeedbackDetailPage Component
 * 
 * Displays detailed feedback and comments for a proposal
 * Features:
 * - Comments filtered by section (Idea, Problem Analysis, MVP, etc.)
 * - Comment statistics (total, resolved, unresolved)
 * - Add new comments
 * - Reply to comments
 * - Resolve comments
 * - Edit/delete comments
 */
const FeedbackDetailPage = ({ 
  proposal, 
  evaluation,
  comments = [],
  currentUserId,
  currentUserRole,
  onAddComment,
  onAddReply,
  onEditComment,
  onDeleteComment,
  onResolveComment,
  loading = false,
  error = null
}) => {
  const [selectedSection, setSelectedSection] = useState('OVERALL');
  const [showUnresolved, setShowUnresolved] = useState(true);
  const [newCommentText, setNewCommentText] = useState('');
  const [isAddingComment, setIsAddingComment] = useState(false);

  const sections = [
    { key: 'OVERALL', label: 'Overall', icon: '📋' },
    { key: 'IDEA', label: 'Startup Idea', icon: '💡' },
    { key: 'PROBLEM_ANALYSIS', label: 'Problem Analysis', icon: '🔍' },
    { key: 'MARKET_RESEARCH', label: 'Market Research', icon: '📊' },
    { key: 'MVP_PROTOTYPE', label: 'MVP/Prototype', icon: '🛠️' },
    { key: 'BUSINESS_MODEL', label: 'Business Model', icon: '💼' },
    { key: 'PITCH_DECK', label: 'Pitch Deck', icon: '🎯' },
  ];

  // Filter comments based on section and resolved status
  const filteredComments = comments.filter((comment) => {
    const sectionMatch = selectedSection === 'ALL' || comment.section === selectedSection;
    const resolvedMatch = showUnresolved ? !comment.resolved : comment.resolved;
    return sectionMatch && resolvedMatch;
  });

  // Calculate statistics
  const stats = {
    total: comments.length,
    unresolved: comments.filter(c => !c.resolved).length,
    resolved: comments.filter(c => c.resolved).length,
    bySection: {}
  };

  comments.forEach(comment => {
    if (!stats.bySection[comment.section]) {
      stats.bySection[comment.section] = { total: 0, unresolved: 0, resolved: 0 };
    }
    stats.bySection[comment.section].total++;
    if (comment.resolved) {
      stats.bySection[comment.section].resolved++;
    } else {
      stats.bySection[comment.section].unresolved++;
    }
  });

  const handleAddComment = async () => {
    if (!newCommentText.trim()) return;

    await onAddComment?.({
      proposalId: proposal._id,
      evaluationId: evaluation?._id,
      text: newCommentText,
      section: selectedSection,
      checkpointNumber: evaluation?.checkpointNumber,
      teamId: proposal.teamId
    });

    setNewCommentText('');
    setIsAddingComment(false);
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="text-center">
          <div className="loading loading-lg text-primary mb-4"></div>
          <p>Loading feedback data...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-6xl mx-auto p-6">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900 mb-2">Feedback & Comments</h1>
        <p className="text-gray-600">
          Team: <span className="font-semibold">{proposal?.teamId?.name}</span>
        </p>
      </div>

      {error && (
        <div className="alert alert-error mb-6">
          <span>{error}</span>
        </div>
      )}

      {/* Statistics Bar */}
      <div className="grid grid-cols-4 gap-4 mb-6">
        <div className="bg-blue-50 rounded-lg p-4 border border-blue-200">
          <div className="text-sm text-blue-600 font-semibold">Total Comments</div>
          <div className="text-2xl font-bold text-blue-900">{stats.total}</div>
        </div>
        <div className="bg-yellow-50 rounded-lg p-4 border border-yellow-200">
          <div className="text-sm text-yellow-600 font-semibold">Unresolved</div>
          <div className="text-2xl font-bold text-yellow-900">{stats.unresolved}</div>
        </div>
        <div className="bg-green-50 rounded-lg p-4 border border-green-200">
          <div className="text-sm text-green-600 font-semibold">Resolved</div>
          <div className="text-2xl font-bold text-green-900">{stats.resolved}</div>
        </div>
        <div className="bg-purple-50 rounded-lg p-4 border border-purple-200">
          <div className="text-sm text-purple-600 font-semibold">Completion</div>
          <div className="text-2xl font-bold text-purple-900">
            {stats.total > 0 ? Math.round((stats.resolved / stats.total) * 100) : 0}%
          </div>
        </div>
      </div>

      <div className="grid grid-cols-4 gap-6">
        {/* Sidebar */}
        <div className="col-span-1">
          <div className="bg-white rounded-lg shadow-sm p-4 border border-gray-200 sticky top-6">
            <h3 className="font-bold text-gray-800 mb-4">Sections</h3>
            
            <div className="space-y-2 mb-6">
              {sections.map(section => (
                <button
                  key={section.key}
                  onClick={() => setSelectedSection(section.key)}
                  className={`w-full text-left px-3 py-2 rounded transition-colors text-sm ${
                    selectedSection === section.key
                      ? 'bg-blue-100 text-blue-900 font-semibold'
                      : 'hover:bg-gray-100 text-gray-700'
                  }`}
                >
                  <span className="mr-2">{section.icon}</span>
                  {section.label}
                  {stats.bySection[section.key] && (
                    <span className="float-right bg-gray-300 text-gray-900 text-xs rounded-full px-2 py-0.5">
                      {stats.bySection[section.key].total}
                    </span>
                  )}
                </button>
              ))}
            </div>

            {/* Filter Toggle */}
            <div className="border-t pt-4">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={showUnresolved}
                  onChange={(e) => setShowUnresolved(e.target.checked)}
                  className="checkbox checkbox-sm"
                />
                <span className="text-sm font-semibold text-gray-700">
                  Show Unresolved Only
                </span>
              </label>
            </div>
          </div>
        </div>

        {/* Main Content */}
        <div className="col-span-3">
          {/* New Comment Form */}
          <div className="bg-white rounded-lg shadow-sm p-6 border border-gray-200 mb-6">
            <h3 className="font-bold text-gray-800 mb-4">Add Comment</h3>
            {isAddingComment ? (
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-semibold text-gray-700 mb-2">
                    Section
                  </label>
                  <select
                    value={selectedSection}
                    onChange={(e) => setSelectedSection(e.target.value)}
                    className="select select-bordered w-full select-sm"
                  >
                    {sections.map(section => (
                      <option key={section.key} value={section.key}>
                        {section.icon} {section.label}
                      </option>
                    ))}
                  </select>
                </div>
                <textarea
                  value={newCommentText}
                  onChange={(e) => setNewCommentText(e.target.value)}
                  placeholder="Type your comment here..."
                  className="textarea textarea-bordered w-full"
                  rows="4"
                />
                <div className="flex gap-2 justify-end">
                  <button
                    onClick={() => {
                      setIsAddingComment(false);
                      setNewCommentText('');
                    }}
                    className="btn btn-ghost btn-sm"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={handleAddComment}
                    disabled={!newCommentText.trim()}
                    className="btn btn-primary btn-sm"
                  >
                    Post Comment
                  </button>
                </div>
              </div>
            ) : (
              <button
                onClick={() => setIsAddingComment(true)}
                className="btn btn-outline btn-sm w-full"
              >
                + Add Comment to {sections.find(s => s.key === selectedSection)?.label}
              </button>
            )}
          </div>

          {/* Comments List */}
          <div className="space-y-4">
            {filteredComments.length === 0 ? (
              <div className="bg-gray-50 rounded-lg p-8 text-center border border-gray-200">
                <div className="text-4xl mb-2">📝</div>
                <p className="text-gray-600 font-semibold">
                  No comments for this section
                </p>
                <p className="text-sm text-gray-500 mt-1">
                  Comments will appear here once they are added
                </p>
              </div>
            ) : (
              filteredComments.map((comment) => (
                <CommentThread
                  key={comment._id}
                  comment={comment}
                  onAddReply={(commentId, replyText) => onAddReply?.(commentId, replyText)}
                  onEditComment={(commentId, text) => onEditComment?.(commentId, text)}
                  onDeleteComment={(commentId) => onDeleteComment?.(commentId)}
                  onResolve={(commentId, resolved) => onResolveComment?.(commentId, resolved)}
                  currentUserId={currentUserId}
                  currentUserRole={currentUserRole}
                  readOnly={false}
                />
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default FeedbackDetailPage;
