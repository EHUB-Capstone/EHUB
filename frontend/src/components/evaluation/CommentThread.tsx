// @ts-nocheck
// frontend/src/components/evaluation/CommentThread.jsx
import React, { useState } from 'react';
import { formatDistanceToNow } from 'date-fns';
import moment from 'moment';

/**
 * CommentThread Component
 * 
 * Displays a single comment with threaded replies
 * Supports:
 * - Adding replies
 * - Editing comments/replies (by author or admin)
 * - Deleting comments/replies
 * - Resolving comments
 */
const CommentThread = ({ 
  comment, 
  onAddReply, 
  onEditComment,
  onDeleteComment,
  onResolve,
  currentUserId,
  currentUserRole,
  readOnly = false
}) => {
  const [isReplying, setIsReplying] = useState(false);
  const [replyText, setReplyText] = useState('');
  const [isEditing, setIsEditing] = useState(false);
  const [editText, setEditText] = useState(comment.text);
  const [expandReplies, setExpandReplies] = useState(true);

  const handleAddReply = async () => {
    if (!replyText.trim()) return;
    
    await onAddReply?.(comment._id, replyText);
    setReplyText('');
    setIsReplying(false);
  };

  const handleEditComment = async () => {
    if (!editText.trim()) return;
    
    await onEditComment?.(comment._id, editText);
    setIsEditing(false);
  };

  const canEdit = currentUserId === comment.authorId._id || currentUserRole === 'ADMIN';
  const canResolve = ['LECTURER', 'MENTOR', 'ADMIN'].includes(currentUserRole);
  const canReply = !readOnly;

  return (
    <div className="bg-white border-l-4 border-blue-400 p-4 mb-4 rounded-r-lg">
      {/* Comment Header */}
      <div className="flex items-start justify-between mb-2">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-full bg-gradient-to-br from-blue-400 to-blue-600 flex items-center justify-center text-white font-bold text-sm">
            {comment.authorName.charAt(0)}
          </div>
          <div>
            <div className="font-semibold text-sm">{comment.authorName}</div>
            <div className="flex items-center gap-2">
              <span className="text-xs bg-blue-100 text-blue-700 px-2 py-1 rounded">
                {comment.authorRole}
              </span>
              <span className="text-xs text-gray-500">
                {moment(comment.createdAt).fromNow()}
              </span>
              {comment.section && (
                <span className="text-xs bg-gray-100 text-gray-700 px-2 py-1 rounded">
                  {comment.section}
                </span>
              )}
            </div>
          </div>
        </div>
        
        {/* Actions Menu */}
        <div className="flex items-center gap-2">
          {comment.resolved && (
            <span className="text-xs bg-green-100 text-green-700 px-2 py-1 rounded font-semibold">
              ✓ Resolved
            </span>
          )}
          
          <div className="dropdown dropdown-end">
            <button className="btn btn-sm btn-ghost" tabIndex="0">
              ⋮
            </button>
            <ul tabIndex="0" className="dropdown-content z-[1] menu p-2 shadow bg-base-100 rounded-box w-52">
              {canEdit && (
                <>
                  <li>
                    <a onClick={() => setIsEditing(!isEditing)}>
                      {isEditing ? 'Cancel Edit' : 'Edit'}
                    </a>
                  </li>
                  <li>
                    <a onClick={() => onDeleteComment?.(comment._id)} className="text-red-600">
                      Delete
                    </a>
                  </li>
                </>
              )}
              {canResolve && (
                <li>
                  <a 
                    onClick={() => onResolve?.(comment._id, !comment.resolved)}
                    className={comment.resolved ? 'text-yellow-600' : 'text-green-600'}
                  >
                    {comment.resolved ? 'Mark Unresolved' : 'Mark Resolved'}
                  </a>
                </li>
              )}
            </ul>
          </div>
        </div>
      </div>

      {/* Comment Content */}
      <div className="mb-3 ml-13">
        {isEditing ? (
          <div className="flex gap-2 mb-2">
            <textarea
              value={editText}
              onChange={(e) => setEditText(e.target.value)}
              className="flex-1 px-3 py-2 border rounded text-sm"
              rows="3"
            />
            <div className="flex flex-col gap-1">
              <button
                onClick={handleEditComment}
                className="btn btn-sm btn-primary"
              >
                Save
              </button>
              <button
                onClick={() => setIsEditing(false)}
                className="btn btn-sm btn-ghost"
              >
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <p className="text-sm text-gray-800 whitespace-pre-wrap break-words">
            {comment.text}
          </p>
        )}
      </div>

      {/* Replies Section */}
      <div className="ml-8 mt-4 space-y-3">
        {comment.replies && comment.replies.length > 0 && (
          <div>
            <button
              onClick={() => setExpandReplies(!expandReplies)}
              className="text-xs text-blue-600 hover:text-blue-800 font-semibold mb-2"
            >
              {expandReplies ? '▼' : '▶'} {comment.replies.length} {comment.replies.length === 1 ? 'reply' : 'replies'}
            </button>

            {expandReplies && (
              <div className="space-y-3 border-l-2 border-gray-300 pl-4">
                {comment.replies.map((reply) => (
                  <div
                    key={reply._id}
                    className="bg-gray-50 p-3 rounded border border-gray-200 hover:border-gray-300"
                  >
                    <div className="flex items-start justify-between mb-2">
                      <div className="flex items-center gap-2">
                        <div className="w-8 h-8 rounded-full bg-gradient-to-br from-gray-400 to-gray-600 flex items-center justify-center text-white font-bold text-xs">
                          {reply.authorName.charAt(0)}
                        </div>
                        <div>
                          <div className="text-xs font-semibold">{reply.authorName}</div>
                          <div className="text-xs text-gray-500">
                            {moment(reply.createdAt).fromNow()}
                          </div>
                        </div>
                      </div>
                    </div>
                    <p className="text-xs text-gray-700 whitespace-pre-wrap break-words">
                      {reply.text}
                    </p>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Reply Input */}
        {canReply && (
          <div>
            {isReplying ? (
              <div className="flex gap-2">
                <textarea
                  value={replyText}
                  onChange={(e) => setReplyText(e.target.value)}
                  placeholder="Type your reply..."
                  className="flex-1 px-3 py-2 border rounded text-sm"
                  rows="2"
                />
                <div className="flex flex-col gap-1">
                  <button
                    onClick={handleAddReply}
                    disabled={!replyText.trim()}
                    className="btn btn-sm btn-primary"
                  >
                    Reply
                  </button>
                  <button
                    onClick={() => {
                      setIsReplying(false);
                      setReplyText('');
                    }}
                    className="btn btn-sm btn-ghost"
                  >
                    Cancel
                  </button>
                </div>
              </div>
            ) : (
              <button
                onClick={() => setIsReplying(true)}
                className="text-xs text-blue-600 hover:text-blue-800 font-semibold"
              >
                + Reply
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default CommentThread;
