import test from 'node:test';
import assert from 'node:assert/strict';
import {
  PDF_PREVIEW_MAX_SCALE,
  PDF_PREVIEW_MIN_SCALE,
  calculatePdfPreviewScale,
  getCheckpointPreviewErrorMessage,
  getPdfPreviewKeyboardAction,
  getPdfPreviewPage,
  getPdfPreviewTitle,
  stepPdfPreviewZoom,
} from '../src/utils/checkpointFilePreview.ts';

test('PDF preview fit modes use the available viewer space', () => {
  const page = { width: 600, height: 800 };
  const viewer = { width: 964, height: 848 };

  assert.equal(calculatePdfPreviewScale('width', page, viewer), 1.5);
  assert.equal(calculatePdfPreviewScale('page', page, viewer), 1);
  assert.ok(
    calculatePdfPreviewScale('width', page, { width: 380, height: 700 })
      < calculatePdfPreviewScale('width', page, viewer),
  );
});

test('PDF preview zoom and page navigation stay within their bounds', () => {
  assert.equal(stepPdfPreviewZoom(PDF_PREVIEW_MIN_SCALE, -1), PDF_PREVIEW_MIN_SCALE);
  assert.equal(stepPdfPreviewZoom(PDF_PREVIEW_MAX_SCALE, 1), PDF_PREVIEW_MAX_SCALE);
  assert.equal(stepPdfPreviewZoom(1, 1), 1.1);
  assert.equal(getPdfPreviewPage(1, 6, -1), 1);
  assert.equal(getPdfPreviewPage(1, 6, 1), 2);
  assert.equal(getPdfPreviewPage(6, 6, 1), 6);
});

test('PDF preview keyboard shortcuts map to the intended minimal controls', () => {
  assert.equal(getPdfPreviewKeyboardAction('ArrowLeft'), 'previous-page');
  assert.equal(getPdfPreviewKeyboardAction('ArrowRight'), 'next-page');
  assert.equal(getPdfPreviewKeyboardAction('+'), 'zoom-in');
  assert.equal(getPdfPreviewKeyboardAction('='), 'zoom-in');
  assert.equal(getPdfPreviewKeyboardAction('-'), 'zoom-out');
  assert.equal(getPdfPreviewKeyboardAction('Escape'), 'escape');
  assert.equal(getPdfPreviewKeyboardAction('p'), null);
});

test('PDF representation keeps the original DOCX and PPTX filename in the title', () => {
  assert.equal(getPdfPreviewTitle('weekly-report.docx'), 'Preview · weekly-report.docx');
  assert.equal(getPdfPreviewTitle('pitch-deck.pptx'), 'Preview · pitch-deck.pptx');
});

test('preview errors retain API messages and provide safe fallbacks', async () => {
  const apiError = {
    response: {
      status: 503,
      data: new Blob([JSON.stringify({ message: 'Preview service is temporarily unavailable.' })], {
        type: 'application/json',
      }),
    },
  };

  assert.equal(
    await getCheckpointPreviewErrorMessage(apiError),
    'Preview service is temporarily unavailable.',
  );
  assert.equal(
    await getCheckpointPreviewErrorMessage({ response: { status: 403 } }),
    'You do not have permission to preview this file.',
  );
  assert.equal(
    await getCheckpointPreviewErrorMessage({ response: { status: 404 } }),
    'The submitted file could not be found.',
  );
  assert.equal(
    await getCheckpointPreviewErrorMessage({ response: { status: 415 } }),
    'This file format cannot be previewed.',
  );
  assert.match(
    await getCheckpointPreviewErrorMessage({ code: 'ECONNABORTED' }),
    /timed out/i,
  );
});
