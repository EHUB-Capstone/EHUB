import { mentorAdminApi } from '../api/mentorAdminApi.ts';
import type { MentorAllocationJob, MentorAllocationPreview } from '../types/mentorAdmin.ts';

export class MentorAllocationJobError extends Error {}

function pause(milliseconds: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal.aborted) { reject(new DOMException('Request aborted', 'AbortError')); return; }
    const abort = () => {
      clearTimeout(timer);
      signal.removeEventListener('abort', abort);
      reject(new DOMException('Request aborted', 'AbortError'));
    };
    const timer = setTimeout(() => {
      signal.removeEventListener('abort', abort);
      resolve();
    }, milliseconds);
    signal.addEventListener('abort', abort, { once: true });
  });
}

export async function waitForMentorAllocationJob(
  initial: MentorAllocationJob,
  signal: AbortSignal,
  onProgress: (job: MentorAllocationJob) => void,
): Promise<MentorAllocationPreview> {
  let job = initial;
  let failures = 0;
  while (!signal.aborted) {
    onProgress(job);
    if (job.status === 'Ready') {
      if (job.preview) return job.preview;
      throw new MentorAllocationJobError('The AI result is unavailable. Please generate the proposal again.');
    }
    if (job.status === 'Failed') throw new MentorAllocationJobError(job.message || 'AI allocation could not finish. Please try again.');
    await pause(1000, signal);
    try {
      job = (await mentorAdminApi.getAllocationJob(job.id, signal)).data;
      failures = 0;
    } catch (error) {
      if (signal.aborted) throw error;
      const status = (error as { response?: { status?: number } }).response?.status;
      if ((status && status < 500) || ++failures >= 3) throw error;
      await pause(2000, signal);
    }
  }
  throw new DOMException('Request aborted', 'AbortError');
}
