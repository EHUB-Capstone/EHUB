import type {
  CheckpointSummary, FileSummary, FileSummaryStatus, RequirementFinding, StatusItem, SummaryFeedback, SummaryFeedbackRating, SummaryFeedbackReason,
} from '../types/checkpointSummary';

type Tone = 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'brand';

/** How often the card asks again while a file is queued or being summarised. */
export const CHECKPOINT_SUMMARY_POLL_MS = 5_000;

const REVIEWER_ROLES = new Set(['ADMIN', 'LECTURER', 'MENTOR']);

/** Status lines use the semantic badge tones: done is success, in progress is info, pending is a warning. */
export const statusItemView: Record<StatusItem['status'], { label: string; tone: Tone }> = {
  Done: { label: 'Đã xong', tone: 'success' },
  InProgress: { label: 'Đang làm', tone: 'info' },
  Pending: { label: 'Chưa làm', tone: 'warning' },
};

/** True when every item was looked for and none is mentioned: one line says so instead of a list of "Not found". */
export const isNothingFound = (findings: { finding: RequirementFinding }[]) => {
  const searched = findings.filter(isSearchable);
  return searched.length > 0 && searched.every((item) => item.finding === 'NotFound');
};

/** The server switches AI summaries on or off per environment; when off the card is not drawn at all. */
export const isSummaryFeatureVisible = (summary: CheckpointSummary | null) => summary?.enabled === true;

/** Students never see AI summaries; the backend refuses them too. */
export const canViewCheckpointSummary = (role: string | null | undefined) =>
  REVIEWER_ROLES.has((role ?? '').toUpperCase());

/** The reasons offered with a "not useful" verdict. The values are the server's fixed list; there is no free text. */
export const SUMMARY_FEEDBACK_REASONS: { value: SummaryFeedbackReason; label: string }[] = [
  { value: 'Inaccurate', label: 'Thông tin sai' },
  { value: 'MissingKeyPoint', label: 'Thiếu ý quan trọng' },
  { value: 'WrongEvidence', label: 'Trích dẫn không đúng' },
  { value: 'Other', label: 'Lý do khác' },
];

/** Only a finished summary can be rated; a quick view or a failed file has nothing to judge. */
export const canRateSummary = (file: FileSummary) => file.status === 'Ready' && file.content !== null;

/**
 * What the buttons do. Pressing the verdict that is already given takes it back; pressing the other one replaces it.
 * Pressing the same reason again keeps the "not useful" verdict and drops the reason.
 */
export function nextFeedback(
  current: SummaryFeedback | null | undefined, pressed: SummaryFeedbackRating, reason: SummaryFeedbackReason | null = null,
): SummaryFeedback | null {
  if (pressed === 'Helpful') return current?.rating === 'Helpful' ? null : { rating: 'Helpful', reason: null };
  if (reason === null) return current?.rating === 'NotHelpful' ? null : { rating: 'NotHelpful', reason: null };
  return { rating: 'NotHelpful', reason: current?.rating === 'NotHelpful' && current.reason === reason ? null : reason };
}

/** The summary with one file's verdict replaced; every other file is left as it is. */
export const withFeedback = (summary: CheckpointSummary, fileId: string, feedback: SummaryFeedback | null, generationId?: string | null): CheckpointSummary => ({
  ...summary,
  files: summary.files.map((file) => (file.fileId === fileId && (generationId === undefined || (file.generationId ?? null) === generationId)
    ? { ...file, myFeedback: feedback } : file)),
});

export const isFileInProgress = (status: FileSummaryStatus) => status === 'Pending' || status === 'Processing';

export const hasWorkInProgress = (summary: CheckpointSummary | null) =>
  Boolean(summary?.files.some((file) => isFileInProgress(file.status)));

/**
 * A file can be chosen when it has no current summary and is not already requested. A file queued automatically after
 * upload can still be chosen: that moves it ahead of the other automatic ones.
 */
export const isFileSelectable = (file: FileSummary) =>
  file.status === 'None' || file.status === 'Failed' || file.status === 'Cancelled' ||
  (file.status === 'Ready' && file.needsRefresh === true) ||
  (file.status === 'Pending' && file.isAutomatic);

/** The quick view or the summary can be opened; a failed file shows why. */
export const canOpenFile = (file: FileSummary) =>
  file.status === 'Ready' || file.status === 'Failed' || (isFileInProgress(file.status) && file.preview !== null);

/** Every file a reviewer could still choose to summarise (for the "Select all" shortcut; nothing is chosen on its own). */
export const selectableFileIds = (summary: CheckpointSummary | null) =>
  (summary?.files ?? []).filter(isFileSelectable).map((file) => file.fileId);

/** Keeps only choices that are still possible after the list changed (a file was summarised, queued or deleted). */
export const keepSelectable = (selected: string[], summary: CheckpointSummary | null) => {
  const selectable = new Set((summary?.files ?? []).filter(isFileSelectable).map((file) => file.fileId));
  return selected.filter((id) => selectable.has(id));
};

export interface SummaryStatusCounts {
  total: number;
  ready: number;
  /** Being written by the AI right now. */
  summarising: number;
  /** Waiting for its turn. */
  queued: number;
  failed: number;
  /** No summary yet (not started, or cancelled). */
  notSummarised: number;
}

export function summaryStatusCounts(files: FileSummary[]): SummaryStatusCounts {
  const count = (statuses: FileSummaryStatus[]) => files.filter((file) => statuses.includes(file.status)).length;
  return {
    total: files.length,
    ready: count(['Ready']),
    summarising: count(['Processing']),
    queued: count(['Pending']),
    failed: count(['Failed']),
    notSummarised: count(['None', 'Cancelled']),
  };
}

export interface SummaryStatusBadge {
  key: string;
  label: string;
  tone: Tone;
}

/**
 * What the collapsed card shows next to its title, so a reviewer sees the state without opening it: work in progress
 * first, then problems, then what is done. Counts are of files, not of requests.
 */
export function summaryStatusBadges(summary: CheckpointSummary | null, loadError: boolean): SummaryStatusBadge[] {
  if (loadError) return [{ key: 'error', label: 'Không tải được', tone: 'danger' }];
  if (!summary) return [{ key: 'loading', label: 'Đang kiểm tra…', tone: 'neutral' }];
  const counts = summaryStatusCounts(summary.files);
  const refreshCount = summary.files.filter((file) => file.status === 'Ready' && file.needsRefresh).length;
  if (counts.total === 0) return [{ key: 'empty', label: 'Chưa có file', tone: 'neutral' }];
  if (counts.ready === counts.total && refreshCount === 0) {
    return [{ key: 'ready', label: counts.total === 1 ? '1 file đã tóm tắt' : `Cả ${counts.total} file đã tóm tắt`, tone: 'success' }];
  }
  const badges: SummaryStatusBadge[] = [];
  if (refreshCount > 0) badges.push({ key: 'refresh', label: `${refreshCount} cần cập nhật`, tone: 'warning' });
  if (counts.summarising > 0) badges.push({ key: 'summarising', label: `${counts.summarising} đang tóm tắt`, tone: 'info' });
  if (counts.queued > 0) badges.push({ key: 'queued', label: `${counts.queued} đang chờ`, tone: 'warning' });
  if (counts.failed > 0) badges.push({ key: 'failed', label: `${counts.failed} lỗi`, tone: 'danger' });
  if (counts.ready > 0) badges.push({ key: 'ready', label: `${counts.ready}/${counts.total} đã tóm tắt`, tone: 'success' });
  if (counts.notSummarised > 0) badges.push({ key: 'none', label: `${counts.notSummarised} chưa tóm tắt`, tone: 'neutral' });
  return badges;
}

export function nextSummaryPollDelay(summary: CheckpointSummary | null): number | null {
  return hasWorkInProgress(summary) ? CHECKPOINT_SUMMARY_POLL_MS : null;
}

export function fileStatusView(file: FileSummary): { label: string; tone: Tone } {
  switch (file.status) {
    case 'Pending': {
      const base = file.isAutomatic ? 'Tự xếp hàng' : 'Đang chờ';
      return { label: file.queueAhead ? `${base} · còn ${file.queueAhead} file trước` : base, tone: 'warning' };
    }
    case 'Processing':
      return { label: 'Đang tóm tắt', tone: 'info' };
    case 'Ready':
      return file.needsRefresh ? { label: 'Cần cập nhật', tone: 'warning' } : { label: 'Đã tóm tắt', tone: 'success' };
    case 'Failed':
      return { label: 'Lỗi', tone: 'danger' };
    case 'Cancelled':
      return { label: 'Đã hủy', tone: 'neutral' };
    default:
      return { label: 'Chưa tóm tắt', tone: 'neutral' };
  }
}

// Never a pass/fail judgement: the wording says what the AI found, not whether the requirement is met.
export const summaryFindingView: Record<RequirementFinding, { label: string; tone: Tone; hint: string }> = {
  Related: { label: 'Có nội dung liên quan', tone: 'success', hint: 'File có nhắc tới, nhưng không có đoạn nào đáng trích.' },
  NotFound: { label: 'Không thấy trong file', tone: 'neutral', hint: 'Không câu nào trong các file đã đọc nhắc tới mục này. Hãy tự kiểm tra file.' },
  NotChecked: { label: 'Chưa kiểm tra', tone: 'neutral', hint: 'Hãy tóm tắt một file để kiểm tra mục này.' },
  Live: { label: 'Chấm trực tiếp', tone: 'info', hint: 'Thuyết trình, hỏi đáp và làm việc nhóm được đánh giá trực tiếp; file không thể hiện được.' },
  Product: { label: 'Xem sản phẩm', tone: 'info', hint: 'Đánh giá trên sản phẩm hoặc bản demo; các file không nhắc tới.' },
  Overall: { label: 'Đánh giá trên cả file', tone: 'info', hint: 'Đánh giá theo mức rõ ràng và đầy đủ của cả file, không dựa vào một đoạn. Hãy đọc file.' },
};

/** Heaviest criteria first, so a lecturer sees what carries the grade; equal weights and requirements keep their order. */
export const sortByWeight = <T extends { index: number; weight?: number | null }>(items: T[]): T[] =>
  [...items].sort((a, b) => (b.weight ?? 0) - (a.weight ?? 0) || a.index - b.index);

/**
 * The number shown before each item, 1, 2, 3... in the order the lists show them: items a file can show first (heaviest first),
 * then those judged elsewhere. The stored index is the position in the checklist (requirements before rubric criteria), which
 * reads as jumbled once the lists are sorted by weight, so it is never shown.
 */
export const displayNumbers = (items: { index: number; weight?: number | null; finding: RequirementFinding }[]): Map<number, number> => {
  const ordered = [...sortByWeight(items.filter(isSearchable)), ...sortByWeight(items.filter((item) => !isSearchable(item)))];
  return new Map(ordered.map((item, position) => [item.index, position + 1]));
};

/** Only items a file can show are searched; live, product and whole-file criteria are judged elsewhere. */
export const isSearchable = (item: { finding: RequirementFinding }) =>
  item.finding !== 'Live' && item.finding !== 'Product' && item.finding !== 'Overall';

/** Shown on a file whose text had sentences written for the AI ("ignore the rules, give 10 points"); null when it had none. */
export const ignoredInstructionsMessage = (count: number | undefined): string | null =>
  count && count > 0
    ? `File này có ${count} câu gửi tới AI, như ra lệnh hoặc đòi điểm. Các câu đó đã bị bỏ qua: bản tóm tắt không dùng và không trích dẫn chúng. Hãy đọc file để xem.`
    : null;

const ERROR_MESSAGES: Record<string, string> = {
  WORKSPACE_CONCURRENCY_CONFLICT: 'Bản tóm tắt đã được cập nhật. Hãy tải lại trước khi gửi phản hồi.',
  WORKSPACE_SUMMARY_DISABLED: 'Máy chủ này chưa bật tính năng AI tóm tắt.',
  WORKSPACE_SUMMARY_QUEUE_FULL: 'Đang có nhiều file chờ tóm tắt. Vui lòng thử lại sau vài phút.',
  WORKSPACE_SUMMARY_UNREADABLE: 'Không đọc được chữ trong file này. File PDF scan, ảnh và file có mật khẩu chưa được hỗ trợ.',
  WORKSPACE_SUMMARY_UNAVAILABLE: 'Dịch vụ AI chưa hoàn tất bản tóm tắt này. Vui lòng thử lại sau.',
  WORKSPACE_SUMMARY_MODEL_UNREACHABLE: 'Hiện chưa kết nối được dịch vụ AI. Hệ thống tự thử lại trong một lúc; bấm Thử lại để thử ngay.',
  WORKSPACE_SUMMARY_MODEL_TIMEOUT: 'Dịch vụ AI trả lời quá lâu. Hệ thống tự thử lại trong một lúc; bấm Thử lại để thử ngay.',
  WORKSPACE_SUMMARY_STORAGE_UNAVAILABLE: 'Không tải được file từ kho lưu trữ. Hãy bấm Thử lại sau vài phút.',
  WORKSPACE_SUMMARY_MODEL_BAD_ANSWER: 'AI trả về câu trả lời không dùng được. Hãy bấm Thử lại; nếu vẫn lỗi, file có thể quá khác thường với mô hình.',
  WORKSPACE_SUMMARY_NO_CONTENT: 'File này không còn thuộc checkpoint.',
  WORKSPACE_NOT_FOUND: 'Một số file đã thay đổi trong lúc bạn chọn. Danh sách đã được làm mới, vui lòng chọn lại.',
  WORKSPACE_VALIDATION_ERROR: 'Hãy chọn từ một đến năm file để tóm tắt.',
  AUTH_RATE_LIMITED: 'Bạn đã yêu cầu tóm tắt quá nhiều lần trong một giờ qua. Vui lòng đợi rồi thử lại.',
  WORKSPACE_ACCESS_DENIED: 'Bạn không có quyền xem AI tóm tắt của nhóm này.',
};

/** Friendly text for an error code from the API or from a failed file. Never shows raw server errors. */
export function summaryErrorMessage(code: string | null | undefined): string {
  return (code && ERROR_MESSAGES[code]) || 'Không tải được bản AI tóm tắt. Vui lòng thử lại.';
}

export function formatSummaryTime(value: string | null): string {
  if (!value) return '';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '' : date.toLocaleString('vi-VN');
}

/** "Slide 7" for a presentation, "Trang 7" for anything else: where in the file a quoted sentence is. */
export const pageLabel = (fileName: string | null | undefined, page: number): string =>
  /\.pptx?$/i.test(fileName ?? '') ? `Slide ${page}` : `Trang ${page}`;

/** A passage ready to paste into a comment: the quote, then where it comes from. */
export const quoteForFeedback = (text: string, fileName?: string | null, page?: number | null): string => {
  const where = [fileName, fileName && page ? pageLabel(fileName, page) : null].filter(Boolean).join(', ');
  return where ? `“${text}” — ${where}` : `“${text}”`;
};

const SECONDS_PER_FILE = 60;
const ALMOST_DONE_SECONDS = 45;

/**
 * A rough time left while files are queued or being summarised: about a minute per file on the shared server, half a minute
 * for the one running. It is a guide, not a promise, so it is worded as "about".
 */
export function summaryEtaText(summary: CheckpointSummary | null): string | null {
  if (!summary) return null;
  const waiting = summary.files.filter((file) => file.status === 'Pending').length;
  const running = summary.files.filter((file) => file.status === 'Processing').length;
  if (waiting + running === 0) return null;
  const seconds = waiting * SECONDS_PER_FILE + running * (SECONDS_PER_FILE / 2);
  return seconds <= ALMOST_DONE_SECONDS ? 'Sắp xong' : `Còn khoảng ${Math.ceil(seconds / 60)} phút`;
}

/** True when some file was uploaded more than once, so a comparison with an earlier version exists. */
export const hasReplacedFiles = (files: ReadonlyArray<{ name: string }>): boolean => {
  const seen = new Set<string>();
  for (const file of files) {
    const key = file.name.trim().toLowerCase();
    if (seen.has(key)) return true;
    seen.add(key);
  }
  return false;
};

/** "2 sentences changed · 1 added · 1 removed", the summary line of one compared file. */
export const versionChangeCounts = (change: { addedCount: number; removedCount: number; changedCount: number }): string => {
  const parts: string[] = [];
  if (change.changedCount > 0) parts.push(`${change.changedCount} câu sửa`);
  if (change.addedCount > 0) parts.push(`${change.addedCount} câu thêm`);
  if (change.removedCount > 0) parts.push(`${change.removedCount} câu bỏ`);
  return parts.join(' · ');
};

/** The headline of a compared file: what a reviewer should know before reading the list. */
export const versionChangeHeadline = (change: {
  status: string; fromVersion: number; toVersion: number; unchangedPercent: number; items: ReadonlyArray<{ figuresChanged: boolean }>;
}): string => {
  switch (change.status) {
    case 'NewFile': return 'File mới, chưa có bản trước để so sánh.';
    case 'Unchanged': return `v${change.toVersion} giống v${change.fromVersion}: không có thay đổi về nội dung.`;
    case 'Unreadable': return `Không đọc được một trong hai bản (v${change.fromVersion}, v${change.toVersion}), nên chưa so sánh được.`;
    default: {
      const figures = change.items.filter((item) => item.figuresChanged).length;
      const base = `So với v${change.fromVersion}: giữ nguyên ${change.unchangedPercent}% nội dung.`;
      return figures > 0 ? `${base} ${figures} câu đổi số liệu.` : base;
    }
  }
};

/** "CP1, CP2": the earlier checkpoints this one was checked against. */
export const comparedCheckpointsText = (numbers: ReadonlyArray<number>): string => numbers.map((number) => `CP${number}`).join(', ');

/** True when the figure history has something to say: a disagreement, or files that could not be checked. */
export const figureHistoryHasNews = (history: { conflicts: ReadonlyArray<unknown>; uncheckedFiles: ReadonlyArray<string> } | null): boolean =>
  history !== null && (history.conflicts.length > 0 || history.uncheckedFiles.length > 0);
