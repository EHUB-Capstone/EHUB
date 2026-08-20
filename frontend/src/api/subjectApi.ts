import axiosClient from './axiosClient';
import type {
  PlanSemesterPayload,
  SemesterCode,
  SemesterLifecyclePayload,
  SubjectStatus,
  UpdateSemesterDatesPayload,
} from '../types/subjects';

interface GetSubjectsParams {
  search?: string;
  status?: SubjectStatus;
}

interface SaveSubjectPayload {
  subjectCode?: string;
  subjectName: string;
  status: SubjectStatus;
}

export const subjectApi = {
  getAll: (params: GetSubjectsParams = {}) => axiosClient.get('/subjects', { params }),
  getActive: () => axiosClient.get('/subjects/active'),
  create: (data: SaveSubjectPayload) => axiosClient.post('/subjects', data),
  update: (id: string, data: SaveSubjectPayload) => axiosClient.put(`/subjects/${id}`, data),
  delete: (id: string) => axiosClient.delete(`/subjects/${id}`),
  getCurrentSemester: () => axiosClient.get('/subjects/current-semester'),
  getSemesters: () => axiosClient.get('/subjects/semesters'),
  getClassCreationSemesterOptions: () => axiosClient.get('/subjects/semesters/class-creation-options'),
  planSemester: (data: PlanSemesterPayload) => axiosClient.post('/subjects/semesters', data),
  updateSemesterDates: (id: string, data: UpdateSemesterDatesPayload) =>
    axiosClient.put(`/subjects/semesters/${id}/dates`, data),
  updateCurrentSemester: (semester: SemesterCode, year: number) =>
    axiosClient.post('/subjects/current-semester', { semester, year }),
  getSemesterCompletionPreview: (id: string) => axiosClient.get(`/subjects/semesters/${id}/completion-preview`),
  completeSemester: (id: string, data: SemesterLifecyclePayload) =>
    axiosClient.post(`/subjects/semesters/${id}/complete`, data),
  reopenSemester: (id: string, data: SemesterLifecyclePayload) =>
    axiosClient.post(`/subjects/semesters/${id}/reopen`, data),
  getTeachingStaff: (params: { semester: SemesterCode; year: number }) =>
    axiosClient.get('/subjects/teaching-staff', { params }),
  getCurriculum: (subjectCode: string) => axiosClient.get(`/subjects/${subjectCode}/curriculum`),
  synchronizeCheckpoints: (subjectCode: string, data: unknown) => axiosClient.put(`/subjects/${subjectCode}/checkpoints`, data),
  createRoadmapItem: (subjectCode: string, data: unknown) => axiosClient.post(`/subjects/${subjectCode}/roadmap`, data),
  updateRoadmapItem: (subjectCode: string, id: string, data: unknown) => axiosClient.put(`/subjects/${subjectCode}/roadmap/${id}`, data),
  deleteRoadmapItem: (subjectCode: string, id: string) => axiosClient.delete(`/subjects/${subjectCode}/roadmap/${id}`),
  createRubric: (subjectCode: string, data: unknown) => axiosClient.post(`/subjects/${subjectCode}/rubrics`, data),
  updateRubric: (subjectCode: string, id: string, data: unknown) => axiosClient.put(`/subjects/${subjectCode}/rubrics/${id}`, data),
  deleteRubric: (subjectCode: string, id: string) => axiosClient.delete(`/subjects/${subjectCode}/rubrics/${id}`),
  createCriterion: (subjectCode: string, rubricId: string, data: unknown) => axiosClient.post(`/subjects/${subjectCode}/rubrics/${rubricId}/criteria`, data),
  updateCriterion: (subjectCode: string, rubricId: string, id: string, data: unknown) => axiosClient.put(`/subjects/${subjectCode}/rubrics/${rubricId}/criteria/${id}`, data),
  deleteCriterion: (subjectCode: string, rubricId: string, id: string) => axiosClient.delete(`/subjects/${subjectCode}/rubrics/${rubricId}/criteria/${id}`),
};
