import axiosClient from './axiosClient';
import type {
  IncompleteMentor,
  IncompleteMentorList,
  MentorAllocationCommitResult,
  MentorAllocationEdit,
  MentorAllocationPreview,
  MentorAllocationStrategy,
  MentorImportCommitResult,
  MentorImportPreview,
  MentorSemesterClassList,
  UpdateIncompleteMentorPayload,
} from '../types/mentorAdmin';

interface ApiEnvelope<T> {
  success: boolean;
  data: T;
  message: string;
}

export const mentorAdminApi = {
  downloadTemplate: (): Promise<Blob> =>
    axiosClient.get('/admin/mentors/import-template', { responseType: 'blob' }),

  /** Previews an import into the master mentor list: accounts and profiles only. Confirm with commitImport. */
  previewMasterImport: (file: File): Promise<ApiEnvelope<MentorImportPreview>> => {
    const form = new FormData();
    form.append('file', file);
    return axiosClient.post('/admin/mentors/master-imports/preview', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },

  /** Mentors saved without a login account yet, paged. mentorType is 'Enterprise' or 'Academic'. */
  getIncompleteMasterMentors: (
    params: { page?: number; limit?: number; search?: string; mentorType?: string } = {},
  ): Promise<ApiEnvelope<IncompleteMentorList>> =>
    axiosClient.get('/admin/mentors/incomplete', { params }),

  updateIncompleteMentor: (id: string, payload: UpdateIncompleteMentorPayload): Promise<ApiEnvelope<IncompleteMentor>> =>
    axiosClient.put(`/admin/mentors/incomplete/${id}`, payload),

  deleteIncompleteMentor: (id: string): Promise<ApiEnvelope<null>> =>
    axiosClient.delete(`/admin/mentors/incomplete/${id}`),

  commitImport: (sessionId: string): Promise<ApiEnvelope<MentorImportCommitResult>> =>
    axiosClient.post('/admin/mentors/imports/commit', { sessionId }),

  /** Active classes of a semester with their open mentor slots. */
  getSemesterClasses: (semesterId: string): Promise<ApiEnvelope<MentorSemesterClassList>> =>
    axiosClient.get(`/admin/mentors/semesters/${semesterId}/classes`),

  previewAllocation: (
    semesterId: string,
    seed?: number,
    strategy: MentorAllocationStrategy = 'Balanced',
    edits: MentorAllocationEdit[] = [],
    includeTemporaryMentors = true,
  ): Promise<ApiEnvelope<MentorAllocationPreview>> =>
    axiosClient.post('/admin/mentors/allocations/preview', {
      semesterId,
      classIds: [],
      strategy,
      edits,
      includeTemporaryMentors,
      ...(seed === undefined ? {} : { seed }),
    }),

  /** Downloads the semester assignment workbook: sheets EXE101, EXE201 and a mentor summary. */
  exportAssignments: (semesterId: string): Promise<Blob> =>
    axiosClient.get('/admin/mentors/assignments/export', { params: { semesterId }, responseType: 'blob' }),

  commitAllocation: (sessionId: string): Promise<ApiEnvelope<MentorAllocationCommitResult>> =>
    axiosClient.post('/admin/mentors/allocations/commit', { sessionId }),
};
