export const CHECKPOINT_UPLOAD_ALLOWED_EXTENSIONS = ['.pdf', '.docx', '.pptx'] as const;
export const CHECKPOINT_UPLOAD_MAX_FILE_SIZE = 15 * 1024 * 1024;
export const CHECKPOINT_UPLOAD_TIMEOUT_MS = 90_000;

export function isCheckpointFilePreviewable(fileName: string): boolean {
  const normalizedName = fileName.trim().toLowerCase();
  return CHECKPOINT_UPLOAD_ALLOWED_EXTENSIONS.some(extension => normalizedName.endsWith(extension));
}

type UploadFileCandidate = Pick<File, 'name' | 'size'>;

export function validateCheckpointUploadFile(file: UploadFileCandidate): string | null {
  if (file.size <= 0) {
    return `"${file.name}" is empty.`;
  }

  if (file.size > CHECKPOINT_UPLOAD_MAX_FILE_SIZE) {
    return `"${file.name}" exceeds the 15 MB limit.`;
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

export function checkpointUploadFailureMessage(error: unknown, fileName: string): string {
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

  if (uploadError.response?.status === 413) {
    return `"${fileName}" exceeds the 15 MB limit.`;
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
