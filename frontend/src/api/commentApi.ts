// @ts-nocheck
// frontend/src/api/commentApi.js — COMMENT APIs
import axios from 'axios';

const getBaseUrl = () => {
  const envUrl = import.meta.env.VITE_API_URL;
  if (!envUrl) return 'http://localhost:5000/api';
  return envUrl.endsWith('/api') ? envUrl : `${envUrl}/api`;
};
const API_URL = getBaseUrl();

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  return {
    headers: {
      Authorization: `Bearer ${token}`
    }
  };
};

export const commentApi = {
  // Create a new comment
  createComment: async (commentData) => {
    const response = await axios.post(
      `${API_URL}/comments`,
      commentData,
      getAuthHeaders()
    );
    return response.data;
  },

  // Get single comment with replies
  getComment: async (commentId) => {
    const response = await axios.get(
      `${API_URL}/comments/${commentId}`,
      getAuthHeaders()
    );
    return response.data;
  },

  // Update comment text
  updateComment: async (commentId, text) => {
    const response = await axios.put(
      `${API_URL}/comments/${commentId}`,
      { text },
      getAuthHeaders()
    );
    return response.data;
  },

  // Delete comment
  deleteComment: async (commentId) => {
    const response = await axios.delete(
      `${API_URL}/comments/${commentId}`,
      getAuthHeaders()
    );
    return response.data;
  },

  // Proposal comments
  getProposalComments: async (proposalId, section = null, resolved = null) => {
    let url = `${API_URL}/comments/proposal/${proposalId}`;
    const params = [];
    if (section) params.push(`section=${encodeURIComponent(section)}`);
    if (resolved !== null) params.push(`resolved=${resolved}`);
    if (params.length > 0) url += '?' + params.join('&');

    const response = await axios.get(url, getAuthHeaders());
    return response.data;
  },

  // Get comment summary for proposal
  getProposalCommentSummary: async (proposalId) => {
    const response = await axios.get(
      `${API_URL}/comments/proposal/${proposalId}/summary`,
      getAuthHeaders()
    );
    return response.data;
  },

  // Evaluation comments
  getEvaluationComments: async (evaluationId, section = null, resolved = null) => {
    let url = `${API_URL}/comments/evaluation/${evaluationId}`;
    const params = [];
    if (section) params.push(`section=${encodeURIComponent(section)}`);
    if (resolved !== null) params.push(`resolved=${resolved}`);
    if (params.length > 0) url += '?' + params.join('&');

    const response = await axios.get(url, getAuthHeaders());
    return response.data;
  },

  // Threaded replies
  addReply: async (commentId, text) => {
    const response = await axios.post(
      `${API_URL}/comments/${commentId}/replies`,
      { text },
      getAuthHeaders()
    );
    return response.data;
  },

  // Update reply
  updateReply: async (commentId, replyId, text) => {
    const response = await axios.put(
      `${API_URL}/comments/${commentId}/replies/${replyId}`,
      { text },
      getAuthHeaders()
    );
    return response.data;
  },

  // Delete reply
  deleteReply: async (commentId, replyId) => {
    const response = await axios.delete(
      `${API_URL}/comments/${commentId}/replies/${replyId}`,
      getAuthHeaders()
    );
    return response.data;
  },

  // Resolve/Unresolve comment
  resolveComment: async (commentId, resolved) => {
    const response = await axios.patch(
      `${API_URL}/comments/${commentId}/resolve`,
      { resolved },
      getAuthHeaders()
    );
    return response.data;
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
