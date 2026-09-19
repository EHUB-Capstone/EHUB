import axiosClient from './axiosClient';

export const productFeedbackApi = {
  create: (data: any) => axiosClient.post('/product-feedback', data),
  mine: () => axiosClient.get('/product-feedback/mine'),
  inbox: (params: any) => axiosClient.get('/product-feedback', { params }),
  get: (id: string) => axiosClient.get(`/product-feedback/${id}`),
  remove: (id: string) => axiosClient.delete(`/product-feedback/${id}`),
  removeAll: () => axiosClient.delete('/product-feedback'),
  upload: (id: string, file: File) => {
    const data = new FormData();
    data.append('file', file);

    return axiosClient.post(`/product-feedback/${id}/attachments`, data, {
      headers: { 'Content-Type': 'multipart/form-data' }
    });
  },
  download: (feedbackId: string, attachmentId: string) => axiosClient.get(
    `/product-feedback/${feedbackId}/attachments/${attachmentId}/download`,
    { responseType: 'blob' }
  )
};
