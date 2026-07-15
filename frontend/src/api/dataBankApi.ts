// @ts-nocheck
import axiosClient from './axiosClient';

const appendJson = (formData, key, value) => {
  formData.append(key, JSON.stringify(value ?? {}));
};

export const dataBankApi = {
  getColumns: () => axiosClient.get('/data-bank/columns'),
  getClasses: () => axiosClient.get('/data-bank/classes'),
  syncClass: (classId) => axiosClient.post(`/data-bank/classes/${classId}/sync`),
  inspectWorkbook: (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return axiosClient.post('/data-bank/workbook/inspect', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
  previewImport: ({ file, sheetName, headerRow, scope, mappings }) => {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('sheetName', sheetName || '');
    formData.append('headerRow', headerRow || 1);
    appendJson(formData, 'scope', scope);
    appendJson(formData, 'mappings', mappings || []);
    return axiosClient.post('/data-bank/imports/preview', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
  commitImport: ({ batchId, file, mappings, confirmHighConflicts }) => {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('confirmHighConflicts', confirmHighConflicts ? 'true' : 'false');
    appendJson(formData, 'mappings', mappings || []);
    return axiosClient.post(`/data-bank/imports/${batchId}/commit`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
  getDatasets: (params) => axiosClient.get('/data-bank/datasets', { params }),
  exportPreview: (payload) => axiosClient.post('/data-bank/exports/preview', payload),
  downloadExport: (payload) => axiosClient.post('/data-bank/exports/download', payload, { responseType: 'blob' }),
  addGridColumn: (payload) => axiosClient.post('/data-bank/grid/columns', payload),
  deleteGridColumn: (payload) => axiosClient.delete('/data-bank/grid/columns', { data: payload }),
  addGridRow: (payload) => axiosClient.post('/data-bank/grid/rows', payload),
  deleteGridRow: (payload) => axiosClient.delete('/data-bank/grid/rows', { data: payload }),
  editGridCell: (payload) => axiosClient.patch('/data-bank/grid/cells', payload),
  editGridCellsBulk: (payload) => axiosClient.patch('/data-bank/grid/cells/bulk', payload),
  clearGridCell: (payload) => axiosClient.delete('/data-bank/grid/cells', { data: payload }),
  getHistory: (params) => axiosClient.get('/data-bank/imports/history', { params }),
  rollback: (batchId, payload = {}) => axiosClient.post(`/data-bank/imports/${batchId}/rollback`, payload),
  getTemplates: () => axiosClient.get('/data-bank/templates'),
  saveTemplate: (payload, id) => (id ? axiosClient.put(`/data-bank/templates/${id}`, payload) : axiosClient.post('/data-bank/templates', payload)),
  deleteTemplate: (id) => axiosClient.delete(`/data-bank/templates/${id}`),
};
