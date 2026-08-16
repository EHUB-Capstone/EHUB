// @ts-nocheck
// frontend/src/api/commentApi.js — COMMENT APIs
import axiosClient from './axiosClient';

export const commentApi = {
  // Create a new comment
  createComment: async (commentData) => {
    return axiosClient.post('/comments', commentData);
  },

  // Get single comment with replies
  getComment: async (commentId) => {
    return axiosClient.get(`/comments/${commentId}`);
  },

  // Update comment text
  updateComment: async (commentId, text) => {
    return axiosClient.put(`/comments/${commentId}`, { text });
  },

  // Delete comment
  deleteComment: async (commentId) => {
    return axiosClient.delete(`/comments/${commentId}`);
  },

  // Proposal comments
  getProposalComments: async (proposalId, section = null, resolved = null) => {
    let url = `/comments/proposal/${proposalId}`;
    const params = [];
    if (section) params.push(`section=${encodeURIComponent(section)}`);
    if (resolved !== null) params.push(`resolved=${resolved}`);
    if (params.length > 0) url += '?' + params.join('&');

    return axiosClient.get(url);
  },

  // Get comment summary for proposal
  getProposalCommentSummary: async (proposalId) => {
    return axiosClient.get(`/comments/proposal/${proposalId}/summary`);
  },

  // Evaluation comments
  getEvaluationComments: async (evaluationId, section = null, resolved = null) => {
    let url = `/comments/evaluation/${evaluationId}`;
    const params = [];
    if (section) params.push(`section=${encodeURIComponent(section)}`);
    if (resolved !== null) params.push(`resolved=${resolved}`);
    if (params.length > 0) url += '?' + params.join('&');

    return axiosClient.get(url);
  },

  // Threaded replies
  addReply: async (commentId, text) => {
    return axiosClient.post(`/comments/${commentId}/replies`, { text });
  },

  // Update reply
  updateReply: async (commentId, replyId, text) => {
    return axiosClient.put(`/comments/${commentId}/replies/${replyId}`, { text });
  },

  // Delete reply
  deleteReply: async (commentId, replyId) => {
    return axiosClient.delete(`/comments/${commentId}/replies/${replyId}`);
  },

  // Resolve/Unresolve comment
  resolveComment: async (commentId, resolved) => {
    return axiosClient.patch(`/comments/${commentId}/resolve`, { resolved });
  },
};

// Export individual methods for backward compatibility
export const {
  createComment,
  getComment,
  updateComment,
  deleteComment,
  getProposalComments,
  getProposalCommentSummary,
  getEvaluationComments,
  addReply,
  updateReply,
  deleteReply,
  resolveComment,
} = commentApi;

// Legacy method name support
export const getProposalSectionComments = async (proposalId, section) => {
  return commentApi.getProposalComments(proposalId, section);
};

export const createProposalComment = async (proposalId, data) => {
  return commentApi.createComment({
    ...data,
    proposalId,
  });
};
