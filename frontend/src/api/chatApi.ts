// @ts-nocheck
// src/api/chatApi.js — Central Group Chat API
import axiosClient from './axiosClient';

export const chatApi = {
  getChannels: () => axiosClient.get('/chat/groups'),
  getMessages: (chatGroupId) => axiosClient.get(`/chat/groups/${chatGroupId}/messages`),
  getMembers: (chatGroupId) => axiosClient.get(`/chat/groups/${chatGroupId}/members`),
  updateNickname: (chatGroupId, nickname) =>
    axiosClient.patch(`/chat/groups/${chatGroupId}/nickname`, { nickname }),
  uploadFile: (formData) => axiosClient.post('/chat/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' }
  }),
};

