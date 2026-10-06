export interface StudentPreviousScores {
  studentId: string;
  semesterCode: string | null;
  components: Array<{ assessmentId: string; checkpointNumber: number | null; name: string; weight: number; score: number }>;
}
