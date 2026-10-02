import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type RefObject,
} from 'react';
import { createPortal } from 'react-dom';
import {
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  Download,
  FileText,
  Loader2,
  Maximize2,
  Minimize2,
  MoveHorizontal,
  RefreshCw,
  Scan,
  X,
  ZoomIn,
  ZoomOut,
} from 'lucide-react';
import {
  GlobalWorkerOptions,
  getDocument,
  type PDFDocumentLoadingTask,
  type PDFDocumentProxy,
  type RenderTask,
} from 'pdfjs-dist';
import pdfWorkerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';
import toast from 'react-hot-toast';
import { checkpointApi } from '../../../api/checkpointApi';
import {
  calculatePdfPreviewScale,
  getCheckpointPreviewErrorMessage,
  getPdfPreviewKeyboardAction,
  getPdfPreviewPage,
  getPdfPreviewTitle,
  stepPdfPreviewZoom,
  type PdfPreviewFitMode,
} from '../../../utils/checkpointFilePreview';
import Button from '../../ui/Button';

GlobalWorkerOptions.workerSrc = pdfWorkerUrl;

interface PreviewFile {
  _id: string;
  originalName: string;
  canDirectDownload?: boolean;
}

interface CheckpointFilePreviewModalProps {
  teamId: string;
  checkpointNumber: number;
  file: PreviewFile | null;
  onClose: () => void;
}

interface PdfPageCanvasProps {
  document: PDFDocumentProxy;
  pageNumber: number;
  scale: number;
  fallbackSize: { width: number; height: number };
  viewerRef: RefObject<HTMLDivElement | null>;
  registerPage: (pageNumber: number, element: HTMLElement | null) => void;
}

const viewerControlClass = 'inline-flex h-9 shrink-0 items-center justify-center gap-1.5 rounded-lg px-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/20 disabled:pointer-events-none disabled:opacity-40';

function PdfPageCanvas({
  document,
  pageNumber,
  scale,
  fallbackSize,
  viewerRef,
  registerPage,
}: PdfPageCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const pageContainerRef = useRef<HTMLElement>(null);
  const [status, setStatus] = useState<'loading' | 'ready' | 'error'>('loading');
  const [shouldRender, setShouldRender] = useState(pageNumber <= 2);
  const [pageSize, setPageSize] = useState(() => ({
    width: fallbackSize.width * scale,
    height: fallbackSize.height * scale,
  }));

  const setPageContainer = useCallback((element: HTMLElement | null) => {
    pageContainerRef.current = element;
    registerPage(pageNumber, element);
  }, [pageNumber, registerPage]);

  useEffect(() => {
    if (shouldRender) return undefined;
    const pageContainer = pageContainerRef.current;
    if (!pageContainer || typeof IntersectionObserver === 'undefined') {
      setShouldRender(true);
      return undefined;
    }

    const observer = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) {
        setShouldRender(true);
        observer.disconnect();
      }
    }, {
      root: viewerRef.current,
      rootMargin: '900px 0px',
    });
    observer.observe(pageContainer);
    return () => observer.disconnect();
  }, [shouldRender, viewerRef]);

  useEffect(() => {
    if (!shouldRender) return undefined;
    let cancelled = false;
    let renderTask: RenderTask | undefined;

    const renderPage = async () => {
      setStatus('loading');
      try {
        const page = await document.getPage(pageNumber);
        if (cancelled) return;

        const viewport = page.getViewport({ scale });
        setPageSize({ width: viewport.width, height: viewport.height });
        const canvas = canvasRef.current;
        if (!canvas) return;

        const outputScale = Math.min(window.devicePixelRatio || 1, 2);
        canvas.width = Math.floor(viewport.width * outputScale);
        canvas.height = Math.floor(viewport.height * outputScale);
        canvas.style.width = `${Math.floor(viewport.width)}px`;
        canvas.style.height = `${Math.floor(viewport.height)}px`;

        renderTask = page.render({
          canvas,
          viewport,
          transform: outputScale === 1
            ? undefined
            : [outputScale, 0, 0, outputScale, 0, 0],
        });
        await renderTask.promise;
        if (!cancelled) setStatus('ready');
      } catch {
        if (!cancelled) setStatus('error');
      }
    };

    void renderPage();
    return () => {
      cancelled = true;
      renderTask?.cancel();
    };
  }, [document, pageNumber, scale, shouldRender]);

  return (
    <article
      ref={setPageContainer}
      data-page-number={pageNumber}
      aria-label={`Page ${pageNumber}`}
      aria-busy={status === 'loading'}
      className="relative shrink-0 overflow-hidden bg-white shadow-[0_10px_28px_rgba(15,23,42,0.16)] ring-1 ring-slate-900/10"
      style={{ width: pageSize.width, height: pageSize.height }}
    >
      <canvas
        ref={canvasRef}
        role="img"
        aria-label={`Rendered document page ${pageNumber}`}
        className={`block bg-white transition-opacity duration-150 ${status === 'ready' ? 'opacity-100' : 'opacity-0'}`}
      />

      {status === 'loading' && (
        <div className="absolute inset-0 flex items-center justify-center bg-white" aria-hidden="true">
          <Loader2 className="h-6 w-6 animate-spin text-primary" />
        </div>
      )}

      {status === 'error' && (
        <div className="absolute inset-0 flex flex-col items-center justify-center bg-slate-50 px-6 text-center">
          <AlertCircle className="h-7 w-7 text-red-500" />
          <p className="mt-2 text-sm font-semibold text-slate-700">Page {pageNumber} could not be rendered.</p>
        </div>
      )}
    </article>
  );
}

export default function CheckpointFilePreviewModal({
  teamId,
  checkpointNumber,
  file,
  onClose,
}: CheckpointFilePreviewModalProps) {
  const [pdfDocument, setPdfDocument] = useState<PDFDocumentProxy | null>(null);
  const [firstPageSize, setFirstPageSize] = useState<{ width: number; height: number } | null>(null);
  const [viewerSize, setViewerSize] = useState({ width: 0, height: 0 });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [retryKey, setRetryKey] = useState(0);
  const [downloading, setDownloading] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [fitMode, setFitMode] = useState<PdfPreviewFitMode>('width');
  const [customScale, setCustomScale] = useState(1);
  const [isFullscreen, setIsFullscreen] = useState(false);

  const viewerShellRef = useRef<HTMLDivElement>(null);
  const documentAreaRef = useRef<HTMLDivElement>(null);
  const pageElementsRef = useRef(new Map<number, HTMLElement>());
  const scrollFrameRef = useRef<number | null>(null);

  useEffect(() => {
    if (!file) return undefined;
    const controller = new AbortController();
    let loadingTask: PDFDocumentLoadingTask | undefined;

    setLoading(true);
    setError('');
    setPdfDocument(null);
    setFirstPageSize(null);
    setCurrentPage(1);
    setFitMode('width');
    pageElementsRef.current.clear();

    const loadPreview = async () => {
      try {
        const blob = await checkpointApi.previewFile(teamId, checkpointNumber, file._id, {
          signal: controller.signal,
        });
        if (controller.signal.aborted) return;

        const previewBytes = new Uint8Array(await blob.arrayBuffer());
        if (controller.signal.aborted) return;

        loadingTask = getDocument({ data: previewBytes });
        const loadedDocument = await loadingTask.promise;
        const firstPage = await loadedDocument.getPage(1);
        if (controller.signal.aborted) return;

        const viewport = firstPage.getViewport({ scale: 1 });
        setFirstPageSize({ width: viewport.width, height: viewport.height });
        setPdfDocument(loadedDocument);
      } catch (requestError: unknown) {
        if (controller.signal.aborted) return;
        setError(await getCheckpointPreviewErrorMessage(requestError));
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    };

    void loadPreview();
    return () => {
      controller.abort();
      void loadingTask?.destroy();
    };
  }, [checkpointNumber, file, retryKey, teamId]);

  useEffect(() => {
    if (!file) return undefined;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, [file]);

  useEffect(() => {
    const documentArea = documentAreaRef.current;
    if (!documentArea || !pdfDocument) return undefined;

    const measure = () => {
      setViewerSize({
        width: documentArea.clientWidth,
        height: documentArea.clientHeight,
      });
    };

    measure();
    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', measure);
      return () => window.removeEventListener('resize', measure);
    }

    const observer = new ResizeObserver(measure);
    observer.observe(documentArea);
    return () => observer.disconnect();
  }, [pdfDocument]);

  useEffect(() => {
    const updateFullscreenState = () => {
      setIsFullscreen(document.fullscreenElement === viewerShellRef.current);
    };
    document.addEventListener('fullscreenchange', updateFullscreenState);
    return () => document.removeEventListener('fullscreenchange', updateFullscreenState);
  }, []);

  const scale = useMemo(() => {
    if (fitMode === 'custom' || !firstPageSize) return customScale;
    return calculatePdfPreviewScale(
      fitMode,
      firstPageSize,
      viewerSize,
      viewerSize.width < 640 ? 24 : 64,
      viewerSize.height < 640 ? 24 : 48,
    );
  }, [customScale, firstPageSize, fitMode, viewerSize]);

  const downloadOriginal = useCallback(async () => {
    if (!file) return;
    setDownloading(true);
    try {
      await checkpointApi.downloadFile(teamId, checkpointNumber, file._id, file.originalName, {
        canDirectDownload: file.canDirectDownload,
      });
    } catch {
      toast.error('Unable to download the original file.');
    } finally {
      setDownloading(false);
    }
  }, [checkpointNumber, file, teamId]);

  const changePage = useCallback((offset: 1 | -1) => {
    if (!pdfDocument) return;
    const nextPage = getPdfPreviewPage(currentPage, pdfDocument.numPages, offset);
    const pageElement = pageElementsRef.current.get(nextPage);
    const documentArea = documentAreaRef.current;
    if (!pageElement || !documentArea) return;

    const nextTop = pageElement.getBoundingClientRect().top
      - documentArea.getBoundingClientRect().top
      + documentArea.scrollTop
      - 24;
    documentArea.scrollTo({ top: Math.max(nextTop, 0), behavior: 'smooth' });
    setCurrentPage(nextPage);
  }, [currentPage, pdfDocument]);

  const changeZoom = useCallback((direction: 1 | -1) => {
    setCustomScale(stepPdfPreviewZoom(scale, direction));
    setFitMode('custom');
  }, [scale]);

  const closePreview = useCallback(() => {
    if (document.fullscreenElement === viewerShellRef.current) {
      void document.exitFullscreen().finally(onClose);
      return;
    }
    onClose();
  }, [onClose]);

  const toggleFullscreen = useCallback(async () => {
    try {
      if (document.fullscreenElement === viewerShellRef.current) {
        await document.exitFullscreen();
      } else {
        await viewerShellRef.current?.requestFullscreen();
      }
    } catch {
      toast.error('Fullscreen view is not available in this browser.');
    }
  }, []);

  useEffect(() => {
    if (!file) return undefined;
    const handleKeyDown = (event: KeyboardEvent) => {
      const action = getPdfPreviewKeyboardAction(event.key);
      if (!action) return;

      if (action === 'escape') {
        event.preventDefault();
        if (document.fullscreenElement === viewerShellRef.current) {
          void document.exitFullscreen();
        } else {
          onClose();
        }
        return;
      }
      if (!pdfDocument) return;

      event.preventDefault();
      if (action === 'previous-page') changePage(-1);
      if (action === 'next-page') changePage(1);
      if (action === 'zoom-in') changeZoom(1);
      if (action === 'zoom-out') changeZoom(-1);
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [changePage, changeZoom, file, onClose, pdfDocument]);

  const registerPage = useCallback((pageNumber: number, element: HTMLElement | null) => {
    if (element) pageElementsRef.current.set(pageNumber, element);
    else pageElementsRef.current.delete(pageNumber);
  }, []);

  const updateCurrentPageFromScroll = useCallback(() => {
    const documentArea = documentAreaRef.current;
    if (!documentArea || pageElementsRef.current.size === 0) return;

    const areaRect = documentArea.getBoundingClientRect();
    const readingLine = areaRect.top + Math.min(areaRect.height * 0.35, 280);
    let closestPage = currentPage;
    let closestDistance = Number.POSITIVE_INFINITY;

    pageElementsRef.current.forEach((element, pageNumber) => {
      const pageRect = element.getBoundingClientRect();
      const distance = readingLine >= pageRect.top && readingLine <= pageRect.bottom
        ? 0
        : Math.min(Math.abs(pageRect.top - readingLine), Math.abs(pageRect.bottom - readingLine));
      if (distance < closestDistance) {
        closestDistance = distance;
        closestPage = pageNumber;
      }
    });

    setCurrentPage(previous => previous === closestPage ? previous : closestPage);
  }, [currentPage]);

  const handleDocumentScroll = useCallback(() => {
    if (scrollFrameRef.current !== null) cancelAnimationFrame(scrollFrameRef.current);
    scrollFrameRef.current = requestAnimationFrame(updateCurrentPageFromScroll);
  }, [updateCurrentPageFromScroll]);

  useEffect(() => () => {
    if (scrollFrameRef.current !== null) cancelAnimationFrame(scrollFrameRef.current);
  }, []);

  if (!file) return null;

  const previewTitle = getPdfPreviewTitle(file.originalName);
  const totalPages = pdfDocument?.numPages ?? 0;
  const zoomPercentage = Math.round(scale * 100);

  return createPortal(
    <div
      className="fixed inset-0 z-[120] flex items-end justify-center p-0 sm:items-center sm:p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="checkpoint-file-preview-title"
    >
      <div
        className="absolute inset-0 bg-slate-950/50 backdrop-blur-sm"
        onClick={closePreview}
        aria-hidden="true"
      />

      <div
        ref={viewerShellRef}
        className={`relative flex h-[96dvh] w-full flex-col overflow-hidden border border-slate-200/70 bg-white shadow-float sm:h-[90vh] sm:w-[90vw] sm:max-w-[1600px] sm:rounded-2xl ${isFullscreen ? 'h-screen! w-screen! max-w-none! rounded-none! border-0!' : 'rounded-t-2xl'}`}
      >
        <header className="flex min-h-16 shrink-0 items-center gap-3 border-b border-slate-200 bg-white px-3 py-3 sm:px-5">
          <div className="flex min-w-0 flex-1 items-center gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary-50 text-primary ring-1 ring-primary/10">
              <FileText className="h-5 w-5" />
            </div>
            <h2 id="checkpoint-file-preview-title" className="min-w-0 truncate text-sm font-bold text-slate-900 sm:text-base" title={file.originalName}>
              <span className="text-slate-500">Preview · </span>
              {file.originalName}
            </h2>
          </div>

          <div className="flex shrink-0 items-center gap-1.5 sm:gap-2">
            <Button
              size="sm"
              variant="outline"
              icon={Download}
              isLoading={downloading}
              onClick={() => void downloadOriginal()}
              aria-label="Download original file"
              className="px-2.5 sm:px-3"
            >
              <span className="hidden sm:inline">Download original</span>
            </Button>
            <Button
              size="sm"
              variant="ghost"
              icon={X}
              onClick={closePreview}
              aria-label="Close preview"
              className="px-2.5 sm:px-3"
            >
              <span className="hidden sm:inline">Close</span>
            </Button>
          </div>
        </header>

        {!loading && !error && pdfDocument && (
          <div className="shrink-0 overflow-x-auto border-b border-slate-200 bg-white px-2 py-2 sm:px-4" role="toolbar" aria-label="Document preview controls">
            <div className="mx-auto flex min-w-max items-center justify-center gap-1 sm:gap-2">
              <button
                type="button"
                className={viewerControlClass}
                onClick={() => changePage(-1)}
                disabled={currentPage <= 1}
                aria-label="Previous page"
                title="Previous page (Left arrow)"
              >
                <ChevronLeft className="h-4 w-4" />
              </button>
              <div className="min-w-20 rounded-lg bg-slate-100 px-3 py-2 text-center text-sm font-semibold tabular-nums text-slate-700" aria-live="polite">
                {currentPage} <span className="font-normal text-slate-400">/</span> {totalPages}
              </div>
              <button
                type="button"
                className={viewerControlClass}
                onClick={() => changePage(1)}
                disabled={currentPage >= totalPages}
                aria-label="Next page"
                title="Next page (Right arrow)"
              >
                <ChevronRight className="h-4 w-4" />
              </button>

              <span className="mx-1 h-6 w-px bg-slate-200" aria-hidden="true" />

              <button
                type="button"
                className={viewerControlClass}
                onClick={() => changeZoom(-1)}
                aria-label="Zoom out"
                title="Zoom out (-)"
              >
                <ZoomOut className="h-4 w-4" />
              </button>
              <div className="w-16 text-center text-sm font-semibold tabular-nums text-slate-700" aria-live="polite">
                {zoomPercentage}%
              </div>
              <button
                type="button"
                className={viewerControlClass}
                onClick={() => changeZoom(1)}
                aria-label="Zoom in"
                title="Zoom in (+)"
              >
                <ZoomIn className="h-4 w-4" />
              </button>

              <span className="mx-1 h-6 w-px bg-slate-200" aria-hidden="true" />

              <button
                type="button"
                className={`${viewerControlClass} ${fitMode === 'width' ? 'bg-primary-50 text-primary hover:bg-primary-100 hover:text-primary-dark' : ''}`}
                onClick={() => setFitMode('width')}
                aria-pressed={fitMode === 'width'}
                title="Fit width"
              >
                <MoveHorizontal className="h-4 w-4" />
                <span className="hidden md:inline">Fit width</span>
              </button>
              <button
                type="button"
                className={`${viewerControlClass} ${fitMode === 'page' ? 'bg-primary-50 text-primary hover:bg-primary-100 hover:text-primary-dark' : ''}`}
                onClick={() => setFitMode('page')}
                aria-pressed={fitMode === 'page'}
                title="Fit page"
              >
                <Scan className="h-4 w-4" />
                <span className="hidden md:inline">Fit page</span>
              </button>
              <button
                type="button"
                className={viewerControlClass}
                onClick={() => void toggleFullscreen()}
                aria-label={isFullscreen ? 'Exit fullscreen' : 'Enter fullscreen'}
                title={isFullscreen ? 'Exit fullscreen' : 'Fullscreen'}
              >
                {isFullscreen ? <Minimize2 className="h-4 w-4" /> : <Maximize2 className="h-4 w-4" />}
                <span className="hidden lg:inline">{isFullscreen ? 'Exit fullscreen' : 'Fullscreen'}</span>
              </button>
            </div>
          </div>
        )}

        <main className="relative min-h-0 flex-1 bg-slate-200/80">
          {loading && (
            <div className="absolute inset-0 flex flex-col items-center justify-center bg-slate-100 text-center" role="status" aria-live="polite">
              <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-white shadow-card ring-1 ring-slate-200">
                <Loader2 className="h-7 w-7 animate-spin text-primary" />
              </div>
              <p className="mt-4 text-sm font-bold text-slate-800">Loading preview...</p>
              <p className="mt-1 text-xs text-slate-500">Preparing the document viewer</p>
            </div>
          )}

          {!loading && error && (
            <div className="absolute inset-0 flex items-center justify-center overflow-y-auto bg-slate-100 p-4 sm:p-8" role="alert">
              <div className="w-full max-w-lg rounded-2xl border border-red-200 bg-white px-6 py-8 text-center shadow-card sm:px-10">
                <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-red-50 text-red-500">
                  <AlertCircle className="h-7 w-7" />
                </div>
                <p className="mt-4 font-bold text-slate-900">Preview unavailable</p>
                <p className="mt-2 text-sm leading-6 text-slate-600">{error}</p>
                <div className="mt-6 flex flex-wrap justify-center gap-2">
                  <Button variant="outline" icon={RefreshCw} onClick={() => setRetryKey(current => current + 1)}>
                    Retry
                  </Button>
                  <Button icon={Download} isLoading={downloading} onClick={() => void downloadOriginal()}>
                    Download original
                  </Button>
                </div>
              </div>
            </div>
          )}

          {!loading && !error && pdfDocument && firstPageSize && (
            <div
              ref={documentAreaRef}
              className="absolute inset-0 overflow-auto overscroll-contain bg-slate-200/80"
              onScroll={handleDocumentScroll}
              tabIndex={0}
              aria-label={`${previewTitle}. ${totalPages} pages.`}
            >
              <div className="flex min-h-full min-w-full w-max flex-col items-center gap-5 p-3 sm:gap-7 sm:p-8">
                {Array.from({ length: pdfDocument.numPages }, (_, index) => {
                  const pageNumber = index + 1;
                  return (
                    <PdfPageCanvas
                      key={pageNumber}
                      document={pdfDocument}
                      pageNumber={pageNumber}
                      scale={scale}
                      fallbackSize={firstPageSize}
                      viewerRef={documentAreaRef}
                      registerPage={registerPage}
                    />
                  );
                })}
              </div>
            </div>
          )}
        </main>
      </div>
    </div>,
    document.body,
  );
}
