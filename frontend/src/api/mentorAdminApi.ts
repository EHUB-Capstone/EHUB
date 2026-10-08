import axiosClient from './axiosClient';
import type {
  IncompleteMentor,
  MentorAllocationCommitResult,
  MentorAllocationEdit,
  MentorAllocationPreview,
  MentorAllocationStrategy,
  MentorImportCommitResult,
  MentorImportPreview,
} from '../types/mentorAdmin';

interface ApiEnvelope<T> {
  success: boolean;
  data: T;
  message: string;
}

export const mentorAdminApi = {
  downloadTemplate: (): Promise<Blob> =>
    axiosClient.get('/admin/mentors/import-template', { responseType: 'blob' }),

  previewImport: (semesterId: string, file: File): Promise<ApiEnvelope<MentorImportPreview>> => {
    const form = new FormData();
    form.append('semesterId', semesterId);
    form.append('file', file);
    return axiosClient.post('/admin/mentors/imports/preview', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },

  /** Previews an import into the master mentor list: accounts and profiles only, no semester. Confirm with commitImport. */
  previewMasterImport: (file: File): Promise<ApiEnvelope<MentorImportPreview>> => {
    const form = new FormData();
    form.append('file', file);
    return axiosClient.post('/admin/mentors/master-imports/preview', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },

  /** Mentors kept in the master list without a login account yet. */
  getIncompleteMasterMentors: (): Promise<ApiEnvelope<IncompleteMentor[]>> =>
    axiosClient.get('/admin/mentors/incomplete'),

  commitImport: (sessionId: string): Promise<ApiEnvelope<MentorImportCommitResult>> =>
    axiosClient.post('/admin/mentors/imports/commit', { sessionId }),

  previewAllocation: (
    semesterId: string,
    seed?: number,
    strategy: MentorAllocationStrategy = 'Balanced',
    edits: MentorAllocationEdit[] = [],
  ): Promise<ApiEnvelope<MentorAllocationPreview>> =>
    axiosClient.post('/admin/mentors/allocations/preview', {
      semesterId,
      classIds: [],
      strategy,
      edits,
      ...(seed === undefined ? {} : { seed }),
    }),

  /** Downloads the semester assignment workbook: sheets EXE101, EXE201 and a mentor summary. */
  exportAssignments: (semesterId: string): Promise<Blob> =>
    axiosClient.get('/admin/mentors/assignments/export', { params: { semesterId }, responseType: 'blob' }),

  commitAllocation: (sessionId: string): Promise<ApiEnvelope<MentorAllocationCommitResult>> =>
    axiosClient.post('/admin/mentors/allocations/commit', { sessionId }),
};
