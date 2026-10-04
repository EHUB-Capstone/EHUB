import assert from 'node:assert/strict';
import test from 'node:test';
import { markPreviewOpen, measurePreviewFirstPage } from '../src/utils/previewTiming.ts';

function fakePerformance() {
  const marks = new Map<string, number>();
  let clock = 0;
  const measures: Array<{ name: string; duration: number }> = [];
  return {
    advance: (ms: number) => { clock += ms; },
    measures,
    mark(name: string) { marks.set(name, clock); },
    measure(name: string, start: string, end?: string) {
      if (!marks.has(start)) throw new Error('missing start mark');
      const duration = (end ? marks.get(end)! : clock) - marks.get(start)!;
      measures.push({ name, duration });
      return { duration };
    },
    clearMarks(name?: string) { if (name) marks.delete(name); else marks.clear(); },
    clearMeasures() { measures.length = 0; },
  };
}

test('preview timing measures from open to first painted page and tags the source', () => {
  const perf = fakePerformance();
  markPreviewOpen('file-1', perf);
  perf.advance(1250);
  assert.equal(measurePreviewFirstPage('file-1', 'network', perf), 1250);
  assert.deepEqual(perf.measures, [{ name: 'preview:first-page:network', duration: 1250 }]);
});

test('preview timing never throws and ignores a first page without a matching open mark', () => {
  const perf = fakePerformance();
  assert.equal(measurePreviewFirstPage('never-opened', 'unknown', perf), null);
  assert.equal(measurePreviewFirstPage('x', 'cache', null), null);
  markPreviewOpen('x', null);
});

test('reopening the same file restarts the measurement', () => {
  const perf = fakePerformance();
  markPreviewOpen('file-1', perf);
  perf.advance(5000);
  markPreviewOpen('file-1', perf);
  perf.advance(300);
  assert.equal(measurePreviewFirstPage('file-1', 'cache', perf), 300);
});
