export const CHECKPOINT_UPLOAD_ALLOWED_EXTENSIONS = ['.pdf', '.docx', '.pptx'] as const;
export const CHECKPOINT_UPLOAD_MAX_FILE_SIZE_MB = 100;
export const CHECKPOINT_UPLOAD_MAX_FILE_SIZE = CHECKPOINT_UPLOAD_MAX_FILE_SIZE_MB * 1024 * 1024;
/** Timeout for the small JSON calls (initiate / complete). The file itself uses {@link checkpointPutTimeoutMs}. */
export const CHECKPOINT_UPLOAD_TIMEOUT_MS = 90_000;

// Allow for a slow student connection (~1 Mbit/s) before giving up on a single PUT.
const SLOWEST_EXPECTED_BYTES_PER_SECOND = 128 * 1024;
const MIN_PUT_TIMEOUT_MS = 120_000;

export function checkpointPutTimeoutMs(fileSize: number): number {
  return Math.max(MIN_PUT_TIMEOUT_MS, Math.ceil(fileSize / SLOWEST_EXPECTED_BYTES_PER_SECOND) * 1000);
}

export function isCheckpointFilePreviewable(fileName: string): boolean {
  const normalizedName = fileName.trim().toLowerCase();
  return CHECKPOINT_UPLOAD_ALLOWED_EXTENSIONS.some(extension => normalizedName.endsWith(extension));
}

export function formatFileSize(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) return '0 B';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}

type UploadFileCandidate = Pick<File, 'name' | 'size'>;

export function validateCheckpointUploadFile(file: UploadFileCandidate): string | null {
  if (file.size <= 0) {
    return `"${file.name}" is empty.`;
  }

  if (file.size > CHECKPOINT_UPLOAD_MAX_FILE_SIZE) {
    return `"${file.name}" exceeds the ${CHECKPOINT_UPLOAD_MAX_FILE_SIZE_MB} MB limit (${formatFileSize(file.size)}).`;
  }

  const extension = file.name.includes('.')
    ? file.name.slice(file.name.lastIndexOf('.')).toLowerCase()
    : '';
  if (!CHECKPOINT_UPLOAD_ALLOWED_EXTENSIONS.includes(
    extension as (typeof CHECKPOINT_UPLOAD_ALLOWED_EXTENSIONS)[number],
  )) {
    return `"${file.name}" — unsupported format. Use ${CHECKPOINT_UPLOAD_ALLOWED_EXTENSIONS.join(', ')}.`;
  }

  return null;
}

export function getUploadProgressPercent(loaded: number, total: number | undefined, fallbackTotal: number): number {
  const progressTotal = total || fallbackTotal;
  if (!progressTotal || progressTotal <= 0) return 0;
  return Math.max(0, Math.min(100, Math.round((loaded / progressTotal) * 100)));
}

interface ResumableUploadState {
  session?: { urlExpiresAt: string; sessionExpiresAt: string };
  putDone: boolean;
}

/**
 * A retry may reuse its upload session only while it is still valid: the presigned URL for a
 * new PUT, or the session itself when the bytes are already stored and only complete is missing.
 */
export function needsNewUploadSession(item: ResumableUploadState, nowMs: number): boolean {
  if (!item.session) return true;
  const deadline = Date.parse(item.putDone ? item.session.sessionExpiresAt : item.session.urlExpiresAt);
  return Number.isNaN(deadline) || deadline <= nowMs;
}

export type CheckpointUploadFailureStage = 'request' | 'storage';

export function checkpointUploadFailureMessage(
  error: unknown,
  fileName: string,
  stage: CheckpointUploadFailureStage = 'request',
): string {
  const uploadError = error as {
    code?: string;
    message?: string;
    response?: {
      status?: number;
      data?: { message?: string; error?: string; title?: string } | string;
    };
  };

  if (uploadError.code === 'ECONNABORTED') {
    return `"${fileName}" took too long to upload. Check your connection and try again.`;
  }

  if (stage === 'storage') {
    // Object storage answers in XML (or not at all when CORS/network fails), never with the API envelope.
    if (uploadError.response?.status === 403) {
      return `The upload link for "${fileName}" expired or was rejected. Retry to get a new link.`;
    }
    if (!uploadError.response) {
      return `Could not send "${fileName}" to file storage. Check your connection and try again.`;
    }
    return `File storage could not accept "${fileName}" (HTTP ${uploadError.response.status ?? 'error'}). Please try again.`;
  }

  if (uploadError.response?.status === 413) {
    return `"${fileName}" exceeds the ${CHECKPOINT_UPLOAD_MAX_FILE_SIZE_MB} MB limit.`;
  }

  const responseData = uploadError.response?.data;
  if (typeof responseData === 'object' && responseData) {
    const apiMessage = responseData.message || responseData.error || responseData.title;
    if (apiMessage) return apiMessage;
  }

  if (uploadError.code === 'ERR_NETWORK' || !uploadError.response) {
    return `Could not reach the upload service for "${fileName}". Check that the backend is running and try again.`;
  }

  if ((uploadError.response.status ?? 0) >= 500) {
    return `The server could not process "${fileName}". Please try again or contact support if the problem continues.`;
  }

  return `Could not upload "${fileName}" (HTTP ${uploadError.response.status ?? 'error'}). Please try again.`;
}
