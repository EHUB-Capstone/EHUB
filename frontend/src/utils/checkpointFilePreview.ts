export const PDF_PREVIEW_MIN_SCALE = 0.25;
export const PDF_PREVIEW_MAX_SCALE = 3;
export const PDF_PREVIEW_ZOOM_STEP = 0.1;

export type PdfPreviewFitMode = 'width' | 'page' | 'custom';
export type PdfPreviewKeyboardAction =
  | 'previous-page'
  | 'next-page'
  | 'zoom-in'
  | 'zoom-out'
  | 'escape';

interface PdfPageSize {
  width: number;
  height: number;
}

interface PdfViewerSize {
  width: number;
  height: number;
}

const clamp = (value: number, minimum: number, maximum: number) => (
  Math.min(maximum, Math.max(minimum, value))
);

export function calculatePdfPreviewScale(
  mode: Exclude<PdfPreviewFitMode, 'custom'>,
  page: PdfPageSize,
  viewer: PdfViewerSize,
  horizontalGutter = 64,
  verticalGutter = 48,
): number {
  if (page.width <= 0 || page.height <= 0 || viewer.width <= 0 || viewer.height <= 0) {
    return 1;
  }

  const widthScale = Math.max(viewer.width - horizontalGutter, 1) / page.width;
  const heightScale = Math.max(viewer.height - verticalGutter, 1) / page.height;
  const fittedScale = mode === 'page' ? Math.min(widthScale, heightScale) : widthScale;

  return clamp(fittedScale, PDF_PREVIEW_MIN_SCALE, PDF_PREVIEW_MAX_SCALE);
}

export function stepPdfPreviewZoom(scale: number, direction: 1 | -1): number {
  const nextScale = scale + direction * PDF_PREVIEW_ZOOM_STEP;
  return Math.round(clamp(nextScale, PDF_PREVIEW_MIN_SCALE, PDF_PREVIEW_MAX_SCALE) * 100) / 100;
}

export function getPdfPreviewPage(currentPage: number, totalPages: number, offset: 1 | -1): number {
  if (totalPages <= 0) return 1;
  return clamp(currentPage + offset, 1, totalPages);
}

export function getPdfPreviewKeyboardAction(key: string): PdfPreviewKeyboardAction | null {
  if (key === 'ArrowLeft') return 'previous-page';
  if (key === 'ArrowRight') return 'next-page';
  if (key === '+' || key === '=') return 'zoom-in';
  if (key === '-' || key === '_') return 'zoom-out';
  if (key === 'Escape') return 'escape';
  return null;
}

export function getPdfPreviewTitle(fileName: string): string {
  return `Preview · ${fileName}`;
}

export async function getCheckpointPreviewErrorMessage(error: unknown): Promise<string> {
  const response = (error as {
    code?: string;
    response?: { status?: number; data?: Blob | { message?: string } };
  }).response;
  const data = response?.data;
  if (data instanceof Blob) {
    try {
      const parsed = JSON.parse(await data.text()) as { message?: string };
      if (parsed.message) return parsed.message;
    } catch {
      // The response was not a JSON API error.
    }
  } else if (data?.message) {
    return data.message;
  }

  if ((error as { code?: string }).code === 'ECONNABORTED') {
    return 'Preview preparation timed out. You can still download the original file.';
  }
  if (response?.status === 403) return 'You do not have permission to preview this file.';
  if (response?.status === 404) return 'The submitted file could not be found.';
  if (response?.status === 415) return 'This file format cannot be previewed.';
  return 'The preview could not be prepared. You can still download the original file.';
}
