// AI summaries of a team's checkpoint files, shown to admins, lecturers and mentors only. Each file is summarised on its
// own, once per version; the reviewer chooses which files to summarise.
import { createContext, useCallback, useContext, useEffect, useId, useRef, useState } from 'react';
import axios from 'axios';
import toast from 'react-hot-toast';
import { AlertTriangle, ArrowLeftRight, Check, CheckCircle2, ChevronDown, Copy, FileSearch, FileText, HelpCircle, Link2, ListChecks, Loader2, MessageSquarePlus, Minus, Quote, RefreshCw, Sparkles, ThumbsDown, ThumbsUp, X } from 'lucide-react';
import Badge from '../../ui/Badge';
import Button from '../../ui/Button';
import Card from '../../ui/Card';
import { checkpointSummaryApi } from '../../../api/checkpointSummaryApi';
import CheckpointVersionChangesBlock from './CheckpointVersionChanges';
import { parseApiError } from '../../../utils/apiError';
import {
  canOpenFile, fileStatusView, ignoredInstructionsMessage, formatSummaryTime, hasWorkInProgress, isFileInProgress, isFileSelectable,
  isNothingFound, isSearchable, isSummaryFeatureVisible, displayNumbers, keepSelectable, selectableFileIds, sortByWeight, statusItemView,
  summaryStatusBadges, summaryStatusCounts, nextSummaryPollDelay, summaryErrorMessage, summaryFindingView,
  pageLabel, quoteForFeedback, summaryEtaText, hasReplacedFiles, comparedCheckpointsText, figureHistoryHasNews,
  canRateSummary, nextFeedback, withFeedback, SUMMARY_FEEDBACK_REASONS,
} from '../../../utils/checkpointSummary';
import type { CheckpointFigureHistory, CheckpointSummary, CriterionCue, FigureConflict, FileRequirementFinding, FileSummary, RequirementFinding, StatusItem, SummaryFeedback, SummaryFeedbackRating, SummaryFeedbackReason } from '../../../types/checkpointSummary';

interface CheckpointAiSummaryCardProps {
  teamId: string;
  checkpointNumber: number;
  /** Opens a submitted file, at a page or slide when one is given (the page of a quoted sentence). */
  onOpenFile?: (file: { id: string; name: string }, page?: number) => void;
  /** Puts a passage into the comment box of the checkpoint. */
  onQuote?: (text: string) => void;
}

interface SummaryActions {
  onOpenFile?: CheckpointAiSummaryCardProps['onOpenFile'];
  onQuote?: CheckpointAiSummaryCardProps['onQuote'];
  /** Gives, changes or (with null) takes back the reviewer's verdict on a file's summary. */
  onRate?: (fileId: string, feedback: SummaryFeedback | null) => void;
  /** The file whose verdict is being saved, so its buttons wait. */
  ratingFileId?: string | null;
}

// Quotes appear deep inside the lists; the actions reach them through context instead of through every list.
const SummaryActionsContext = createContext<SummaryActions>({});

const SectionLabel = ({ icon: Icon, children }: { icon: typeof FileText; children: string }) => (
  <h4 className="flex items-center gap-1.5 text-overline font-bold uppercase text-slate-500">
    <Icon className="h-3.5 w-3.5" aria-hidden="true" />
    {children}
  </h4>
);

const ActionChip = ({ icon: Icon, onClick, children }: { icon: typeof FileText; onClick: () => void; children: string }) => (
  <button
    type="button"
    onClick={onClick}
    className="inline-flex items-center gap-1 rounded-md border border-slate-200 bg-white px-2 py-1 text-caption font-medium text-slate-600 transition hover:bg-slate-50 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
  >
    <Icon className="h-3 w-3" aria-hidden="true" />
    {children}
  </button>
);

interface QuotedProps {
  text: string;
  /** The file name shown after the quote (when several files are mixed in one list). */
  source?: string | null;
  /** The file the quote comes from and the page in it, so the quote can be opened there. */
  fileId?: string | null;
  fileName?: string | null;
  page?: number | null;
}

/** A sentence copied from a file, with what a reviewer does next: open the file at its page, copy it, or put it in a comment. */
const Quoted = ({ text, source, fileId, fileName, page }: QuotedProps) => {
  const { onOpenFile, onQuote } = useContext(SummaryActionsContext);
  const name = fileName ?? source ?? null;
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(quoteForFeedback(text, name, page));
      toast.success('Đã sao chép trích dẫn.');
    } catch {
      toast.error('Không sao chép được. Hãy bôi đen đoạn trích và sao chép thủ công.');
    }
  };
  return (
    <blockquote className="border-l-2 border-slate-300 pl-3 text-body-sm italic text-slate-600">
      “{text}”{source && <span className="ml-1 text-caption not-italic text-slate-500">— {source}</span>}
      <span className="mt-1 flex flex-wrap items-center gap-1.5 not-italic">
        {onOpenFile && fileId && name && (
          <ActionChip icon={FileSearch} onClick={() => onOpenFile({ id: fileId, name }, page ?? undefined)}>
            {page ? `Mở ${pageLabel(name, page)}` : 'Mở file'}
          </ActionChip>
        )}
        <ActionChip icon={Copy} onClick={copy}>Sao chép</ActionChip>
        {onQuote && <ActionChip icon={MessageSquarePlus} onClick={() => onQuote(quoteForFeedback(text, name, page))}>Chèn vào góp ý</ActionChip>}
      </span>
    </blockquote>
  );
};

const KIND_LABEL: Record<string, string> = { Requirement: 'Yêu cầu', Criterion: 'Tiêu chí rubric' };

/** What the Excellent level expects, and which of it the file mentions: presence of evidence, not a grade. */
function Cues({ cues }: { cues: CriterionCue[] }) {
  if (cues.length === 0) return null;
  return (
    <ul className="flex flex-wrap gap-1.5" aria-label="Nội dung cần có">
      {cues.map((cue) => (
        <li key={cue.label} title={cue.evidence ?? (cue.found ? 'File có nhắc tới nhưng không có đoạn để trích' : 'File không nhắc tới')}
          className={`flex items-center gap-1 rounded-md border px-1.5 py-0.5 text-caption ${cue.found
            ? 'border-success-light bg-success-50 text-success-dark' : 'border-slate-200 bg-slate-50 text-slate-500'}`}>
          {cue.found ? <Check className="h-3 w-3" aria-hidden="true" /> : <Minus className="h-3 w-3" aria-hidden="true" />}
          <span className="sr-only">{cue.found ? 'Đã nhắc tới: ' : 'Chưa nhắc tới: '}</span>
          {cue.label}
        </li>
      ))}
    </ul>
  );
}

/** Criteria with their weight, heaviest first. */
function FindingList({ findings, numbers, fileId, fileName }: { findings: FileRequirementFinding[]; numbers: Map<number, number>; fileId?: string; fileName?: string }) {
  return (
    <ul className="space-y-2">
      {sortByWeight(findings).map((requirement) => {
        const finding = summaryFindingView[requirement.finding];
        return (
          <li key={requirement.index} className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-body-sm font-medium text-slate-900">{numbers.get(requirement.index)}. {requirement.title}</span>
              {requirement.weight != null && <Badge variant="neutral">{requirement.weight}%</Badge>}
              <Badge variant="neutral">{KIND_LABEL[requirement.kind] ?? requirement.kind}</Badge>
              <Badge variant={finding.tone}>{finding.label}</Badge>
            </div>
            {requirement.evidence
              ? <Quoted text={requirement.evidence} fileId={fileId} fileName={fileName} page={requirement.evidencePage} />
              : requirement.finding === 'Related' && <p className="text-caption text-slate-500">{finding.hint}</p>}
            <Cues cues={requirement.cues ?? []} />
          </li>
        );
      })}
    </ul>
  );
}

/** Live and product criteria are listed apart, so "nothing found" refers only to what a document can show. */
function OtherCriteria({ findings, numbers }: { findings: { index: number; title: string; weight?: number | null; finding: RequirementFinding }[]; numbers: Map<number, number> }) {
  const others = sortByWeight(findings.filter((item) => !isSearchable(item)));
  if (others.length === 0) return null;
  return (
    <ul className="space-y-1">
      {others.map((item) => {
        const view = summaryFindingView[item.finding];
        return (
          <li key={item.index} className="flex flex-wrap items-center gap-2 text-body-sm text-slate-700" title={view.hint}>
            <span>{numbers.get(item.index)}. {item.title}</span>
            {item.weight != null && <Badge variant="neutral">{item.weight}%</Badge>}
            <Badge variant={view.tone}>{view.label}</Badge>
          </li>
        );
      })}
    </ul>
  );
}

/** One line instead of a row of "Not found" badges, e.g. a software review checked against a business rubric. */
const NothingFound = ({ titles, subject }: { titles: string[]; subject: string }) => (
  <p className="text-body-sm text-slate-600">
    {subject} không nhắc tới mục nào trong {titles.length} yêu cầu và tiêu chí rubric ({titles.join(', ')}).
  </p>
);

/** The file tried to instruct the AI; the sentences were dropped before summarising, but a reviewer should know. */
const IgnoredInstructionsNote = ({ count }: { count?: number }) => {
  const message = ignoredInstructionsMessage(count);
  if (!message) return null;
  return (
    <p className="flex items-start gap-2 rounded-lg border border-warning-light bg-warning-50 px-3 py-2 text-caption text-warning-dark" role="note">
      <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
      {message}
    </p>
  );
};

const LittleTextNote = () => (
  <p className="flex items-start gap-2 rounded-lg border border-warning-light bg-warning-50 px-3 py-2 text-caption text-warning-dark">
    <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
    File này chủ yếu là hình ảnh hoặc sơ đồ. Chỉ đọc được tiêu đề và vài dòng chữ, nên bản tóm tắt bị giới hạn ở đó; hãy mở
    file để xem các sơ đồ.
  </p>
);

/** Figures the file gives twice with different values: worth asking the team which one is right. */
function FigureConflicts({ conflicts, acrossFiles = false, earlier = false }: { conflicts: FigureConflict[]; acrossFiles?: boolean; earlier?: boolean }) {
  if (conflicts.length === 0) return null;
  return (
    <div className="space-y-2">
      <SectionLabel icon={ArrowLeftRight}>{earlier ? 'Số liệu khác với các checkpoint trước' : acrossFiles ? 'Số liệu khác nhau giữa các file' : 'Số liệu mâu thuẫn'}</SectionLabel>
      <p className="text-caption text-slate-500">
        {earlier
          ? 'Checkpoint này ghi một con số khác với những gì nhóm đã nộp trước đó. Số liệu có thể đổi hợp lý (khảo sát thêm người), nên hãy hỏi nhóm lý do trước khi kết luận.'
          : acrossFiles
          ? 'Hai file ghi giá trị khác nhau cho một con số có vẻ giống nhau. Có thể chúng mô tả hai việc khác nhau, nên hãy hỏi nhóm trước khi cho rằng file nào sai.'
          : 'File ghi các số liệu này với giá trị khác nhau. Hãy hỏi nhóm giá trị nào đúng.'}
      </p>
      <ul className="space-y-2">
        {conflicts.map((conflict, position) => (
          <li key={`${conflict.subject}-${position}`} className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-body-sm font-medium text-slate-900">{conflict.subject}</span>
              {conflict.values.map((item) => <Badge key={`${item.value}-${item.source ?? ''}`} variant="warning">{item.value}</Badge>)}
            </div>
            {conflict.values.map((item) => <Quoted key={`${item.source ?? ''}-${item.quote}`} text={item.quote} source={item.source} />)}
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * Figures of this checkpoint against the team's earlier checkpoints. Uses figures already taken from each file (nothing is
 * downloaded or read again), so it loads by itself; a failure only hides it, since the rest of the card does not depend on it.
 */
function FigureHistory({ teamId, checkpointNumber }: { teamId: string; checkpointNumber: number }) {
  const [history, setHistory] = useState<CheckpointFigureHistory | null>(null);
  useEffect(() => {
    if (checkpointNumber <= 1) return undefined;
    const controller = new AbortController();
    checkpointSummaryApi.figureHistory(teamId, checkpointNumber, controller.signal)
      .then((response) => setHistory(response.data))
      .catch(() => setHistory(null));
    return () => controller.abort();
  }, [teamId, checkpointNumber]);

  if (!history || history.comparedCheckpoints.length === 0) return null;
  const compared = comparedCheckpointsText(history.comparedCheckpoints);
  return (
    <div className="space-y-2">
      <FigureConflicts conflicts={history.conflicts} earlier />
      {history.conflicts.length === 0 && (
        <p className="flex items-start gap-1.5 text-caption text-slate-500">
          <ArrowLeftRight className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
          Đã đối chiếu số liệu với {compared}: không thấy chỗ nào lệch.
        </p>
      )}
      {figureHistoryHasNews(history) && history.uncheckedFiles.length > 0 && (
        <p className="text-caption text-slate-500">
          Chưa đối chiếu được {history.uncheckedFiles.length} file vì chưa đọc số liệu: {history.uncheckedFiles.join(', ')}.
        </p>
      )}
    </div>
  );
}

/** Lines of the file that say where work stands (DONE, IN PROGRESS, PENDING…), quoted as written. */
function StatusLines({ items }: { items: StatusItem[] }) {
  if (items.length === 0) return null;
  return (
    <div className="space-y-2">
      <SectionLabel icon={CheckCircle2}>Trạng thái ghi trong file</SectionLabel>
      <ul className="space-y-1.5">
        {items.map((item) => {
          const view = statusItemView[item.status];
          return (
            <li key={`${item.status}-${item.request ?? ''}-${item.text}`} className="flex items-start gap-2 text-body-sm text-slate-700">
              <Badge variant={view.tone}>{view.label}</Badge>
              <span className="min-w-0 flex-1">
                {item.request && <span className="block font-medium text-slate-900">{item.request}</span>}
                <span className={item.request ? 'block text-slate-600' : undefined}>{item.text}</span>
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function QuickView({ highlights, highlightPages, requirements, conflicts, statusItems, fileId, fileName }: {
  highlights: string[];
  highlightPages?: number[];
  requirements: FileRequirementFinding[];
  conflicts: FigureConflict[];
  statusItems: StatusItem[];
  fileId: string;
  fileName: string;
}) {
  return (
    <>
      <StatusLines items={statusItems} />
      <FigureConflicts conflicts={conflicts} />
      {requirements.length > 0 && (
        <div className="space-y-2">
          <SectionLabel icon={ListChecks}>Yêu cầu và tiêu chí rubric trong file này</SectionLabel>
          {isNothingFound(requirements)
            ? <NothingFound titles={requirements.filter(isSearchable).map((item) => item.title)} subject="File này" />
            : <FindingList findings={requirements.filter(isSearchable)} numbers={displayNumbers(requirements)} fileId={fileId} fileName={fileName} />}
          <OtherCriteria findings={requirements} numbers={displayNumbers(requirements)} />
        </div>
      )}
      {highlights.length > 0 && (
        <div className="space-y-2">
          <SectionLabel icon={Quote}>Câu then chốt</SectionLabel>
          {highlights.map((sentence, position) => (
            <Quoted key={sentence} text={sentence} fileId={fileId} fileName={fileName} page={highlightPages?.[position] || null} />
          ))}
        </div>
      )}
    </>
  );
}

/** "Was this summary useful?" The answer is kept per reviewer and is how the quality of the summaries is measured. */
function FeedbackBar({ file }: { file: FileSummary }) {
  const { onRate, ratingFileId } = useContext(SummaryActionsContext);
  if (!onRate || !canRateSummary(file)) return null;
  const current = file.myFeedback ?? null;
  const saving = ratingFileId === file.fileId;
  const press = (rating: SummaryFeedbackRating, reason: SummaryFeedbackReason | null = null) =>
    onRate(file.fileId, nextFeedback(current, rating, reason));
  const buttonClass = (active: boolean) =>
    `inline-flex h-9 w-9 items-center justify-center rounded-lg border transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 disabled:cursor-wait disabled:opacity-60 ${
      active ? 'border-primary-600 bg-primary-50 text-primary-700' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'}`;
  return (
    <div className="space-y-2 border-t border-slate-200 pt-3" role="group" aria-label="Đánh giá bản tóm tắt">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-body-sm text-slate-600">Bản tóm tắt này có hữu ích không?</span>
        <button type="button" className={buttonClass(current?.rating === 'Helpful')} aria-pressed={current?.rating === 'Helpful'}
          aria-label="Hữu ích" title="Hữu ích" disabled={saving} onClick={() => press('Helpful')}>
          <ThumbsUp className="h-4 w-4" aria-hidden="true" />
        </button>
        <button type="button" className={buttonClass(current?.rating === 'NotHelpful')} aria-pressed={current?.rating === 'NotHelpful'}
          aria-label="Chưa hữu ích" title="Chưa hữu ích" disabled={saving} onClick={() => press('NotHelpful')}>
          <ThumbsDown className="h-4 w-4" aria-hidden="true" />
        </button>
        {saving && <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />}
        {current && !saving && <span className="text-caption text-slate-500" role="status">Đã ghi nhận ý kiến của bạn.</span>}
      </div>
      {current?.rating === 'NotHelpful' && (
        <div className="flex flex-wrap items-center gap-2" role="group" aria-label="Lý do chưa hữu ích">
          <span className="text-caption text-slate-500">Vì sao (không bắt buộc):</span>
          {SUMMARY_FEEDBACK_REASONS.map((reason) => (
            <button key={reason.value} type="button" aria-pressed={current.reason === reason.value} disabled={saving}
              onClick={() => press('NotHelpful', reason.value)}
              className={`min-h-9 rounded-full border px-3 text-caption transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 disabled:cursor-wait disabled:opacity-60 ${
                current.reason === reason.value ? 'border-primary-600 bg-primary-50 text-primary-700' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'}`}>
              {reason.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

/** The summary of one file, shown when its row is opened. While the AI is still writing, the quick view is shown. */
function FileSummaryDetails({ file, maxCharacters }: { file: FileSummary; maxCharacters: number }) {
  if (file.status === 'Failed') return <p className="text-body-sm text-danger-dark">{summaryErrorMessage(file.errorCode)}</p>;
  const content = file.content;
  if (!content) {
    if (!file.preview) return null;
    return (
      <div className="space-y-3">
        <p className="flex items-center gap-2 text-caption text-slate-500" role="status">
          <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />
          Đang hiển thị bản xem nhanh từ chữ trong file. AI vẫn đang viết phần tổng quan.
        </p>
        <IgnoredInstructionsNote count={file.preview.ignoredInstructions} />
        {file.preview.littleText && <LittleTextNote />}
        <QuickView highlights={file.preview.highlights} highlightPages={file.preview.highlightPages} requirements={file.preview.requirements} conflicts={file.preview.conflicts ?? []} statusItems={file.preview.statusItems ?? []} fileId={file.fileId} fileName={file.name} />
      </div>
    );
  }
  return (
    <div className="space-y-3">
      <IgnoredInstructionsNote count={content.ignoredInstructions} />
      {content.littleText && <LittleTextNote />}
      <p className="whitespace-pre-line text-body text-slate-700">{content.overview}</p>
      {content.keyPoints.length > 0 && (
        <ul className="list-disc space-y-1 pl-5 text-body-sm text-slate-700">
          {content.keyPoints.map((point) => <li key={point}>{point}</li>)}
        </ul>
      )}
      <QuickView highlights={content.highlights} highlightPages={content.highlightPages} requirements={content.requirements} conflicts={content.conflicts ?? []} statusItems={content.statusItems ?? []} fileId={file.fileId} fileName={file.name} />
      {content.questions.length > 0 && (
        <div className="space-y-2">
          <SectionLabel icon={HelpCircle}>Câu hỏi gợi ý cho nhóm</SectionLabel>
          <ul className="list-disc space-y-1 pl-5 text-body-sm text-slate-700">
            {content.questions.map((question) => <li key={question}>{question}</li>)}
          </ul>
        </div>
      )}
      <p className="text-caption text-slate-500">
        {file.inputTruncated && `File dài: AI chỉ đọc các câu quan trọng nhất (khoảng ${maxCharacters.toLocaleString('vi-VN')} ký tự). `}
        {file.generatedAt && `Tóm tắt lúc ${formatSummaryTime(file.generatedAt)}.`}
      </p>
      <FeedbackBar file={file} />
    </div>
  );
}

function FileRow({ file, selected, onToggleSelected, onRetry, maxCharacters, disabled }: {
  file: FileSummary;
  selected: boolean;
  onToggleSelected: () => void;
  onRetry: () => void;
  maxCharacters: number;
  disabled: boolean;
}) {
  const [open, setOpen] = useState(false);
  const regionId = useId();
  const status = fileStatusView(file);
  const canOpen = canOpenFile(file);
  const selectable = isFileSelectable(file);
  return (
    <li className="rounded-lg border border-slate-200">
      <div className="flex items-center gap-2 px-3 py-2">
        <input
          type="checkbox"
          className="h-4 w-4 shrink-0 accent-primary disabled:opacity-40"
          checked={selected}
          disabled={!selectable || disabled}
          onChange={onToggleSelected}
          aria-label={selectable ? `Tóm tắt ${file.name}` : `${file.name}: ${status.label}`}
        />
        <button
          type="button"
          onClick={() => setOpen((value) => !value)}
          disabled={!canOpen}
          aria-expanded={canOpen ? open : undefined}
          aria-controls={canOpen ? regionId : undefined}
          className="flex min-w-0 flex-1 flex-wrap items-center gap-2 rounded-md px-1 py-1 text-left text-body-sm transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 disabled:cursor-default disabled:hover:bg-transparent"
        >
          <span className="min-w-0 flex-1 truncate font-medium text-slate-900">{file.name}</span>
          <span className="text-caption text-slate-500">v{file.versionNumber}</span>
          {isFileInProgress(file.status) && <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />}
          <Badge variant={status.tone}>{status.label}</Badge>
          {canOpen && <ChevronDown className={`h-4 w-4 shrink-0 text-slate-500 transition-transform ${open ? 'rotate-180' : ''}`} aria-hidden="true" />}
        </button>
        {file.status === 'Failed' && (
          <Button variant="outline" size="sm" icon={RefreshCw} disabled={disabled} onClick={onRetry} aria-label={`Thử lại tóm tắt ${file.name}`}>
            Thử lại
          </Button>
        )}
      </div>
      {canOpen && open && (
        <div id={regionId} className="border-t border-slate-200 px-4 py-3">
          <FileSummaryDetails file={file} maxCharacters={maxCharacters} />
        </div>
      )}
    </li>
  );
}

export default function CheckpointAiSummaryCard({ teamId, checkpointNumber, onOpenFile, onQuote }: CheckpointAiSummaryCardProps) {
  const regionId = useId();
  const [summary, setSummary] = useState<CheckpointSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [actionError, setActionError] = useState('');
  // Closed until the reviewer opens it: the checkpoint page stays short, and the header already tells the state.
  const [expanded, setExpanded] = useState(false);
  const [selected, setSelected] = useState<string[]>([]);
  const [requesting, setRequesting] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const busyRef = useRef(false);
  // Rating a summary has its own lock: it must not wait for, or block, a request to summarise files.
  const [ratingFileId, setRatingFileId] = useState<string | null>(null);
  const ratingRef = useRef(false);
  const ratingControllerRef = useRef<AbortController | null>(null);
  const actionControllerRef = useRef<AbortController | null>(null);
  const retryControllerRef = useRef<AbortController | null>(null);

  const applySummary = useCallback((next: CheckpointSummary) => {
    setSummary(next);
    // Nothing is chosen on the reviewer's behalf; a choice only survives while the file can still be chosen.
    setSelected((current) => keepSelectable(current, next));
  }, []);

  const load = useCallback(async (signal: AbortSignal) => {
    try {
      const response = await checkpointSummaryApi.get(teamId, checkpointNumber, signal);
      applySummary(response.data);
      setLoadError('');
    } catch (error) {
      if (axios.isCancel(error)) return;
      setLoadError(summaryErrorMessage(parseApiError(error, '').code));
    } finally {
      if (!signal.aborted) setLoading(false);
    }
  }, [teamId, checkpointNumber, applySummary]);

  // A different team or checkpoint starts from a clean state; leaving the screen cancels the request.
  useEffect(() => {
    const controller = new AbortController();
    setSummary(null);
    setSelected([]);
    setLoading(true);
    setLoadError('');
    setActionError('');
    load(controller.signal);
    return () => controller.abort();
  }, [load]);

  // Ask again while a file is queued or being summarised. Each answer schedules the next check.
  useEffect(() => {
    const delay = nextSummaryPollDelay(summary);
    if (delay === null) return undefined;
    const controller = new AbortController();
    const timer = window.setTimeout(() => load(controller.signal), delay);
    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [summary, load]);

  useEffect(() => () => {
    actionControllerRef.current?.abort();
    retryControllerRef.current?.abort();
    ratingControllerRef.current?.abort();
  }, []);

  // The verdict is shown only after the server has kept it, so the buttons never claim something that was not saved.
  const rate = useCallback(async (fileId: string, feedback: SummaryFeedback | null) => {
    if (ratingRef.current) return;
    ratingRef.current = true;
    setRatingFileId(fileId);
    const controller = new AbortController();
    ratingControllerRef.current = controller;
    try {
      if (feedback) await checkpointSummaryApi.setFeedback(teamId, checkpointNumber, fileId, feedback, controller.signal);
      else await checkpointSummaryApi.clearFeedback(teamId, checkpointNumber, fileId, controller.signal);
      setSummary((current) => (current ? withFeedback(current, fileId, feedback) : current));
    } catch (error) {
      if (axios.isCancel(error)) return;
      toast.error(summaryErrorMessage(parseApiError(error, '').code));
    } finally {
      ratingRef.current = false;
      if (!controller.signal.aborted) setRatingFileId(null);
    }
  }, [teamId, checkpointNumber]);

  const retryLoad = () => {
    retryControllerRef.current?.abort();
    const controller = new AbortController();
    retryControllerRef.current = controller;
    setLoading(true);
    load(controller.signal);
  };

  // Request and cancel share one lock so a double click never sends two actions.
  const runAction = async (action: (signal: AbortSignal) => Promise<CheckpointSummary>, setBusy: (value: boolean) => void, success: string) => {
    if (busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    setActionError('');
    const controller = new AbortController();
    actionControllerRef.current = controller;
    try {
      const next = await action(controller.signal);
      applySummary(next);
      toast.success(success);
    } catch (error) {
      if (axios.isCancel(error)) return;
      const message = summaryErrorMessage(parseApiError(error, '').code);
      setActionError(message);
      toast.error(message);
      // The list may have changed (a file deleted or already summarised); show the current one.
      load(controller.signal);
    } finally {
      busyRef.current = false;
      if (!controller.signal.aborted) setBusy(false);
    }
  };

  const requestSelected = () => {
    const count = selected.length;
    return runAction(
      async (signal) => (await checkpointSummaryApi.request(teamId, checkpointNumber, selected, signal)).data,
      setRequesting,
      `Đã xếp hàng ${count} file. Bản tóm tắt hiện ở từng dòng khi xong.`);
  };

  const retryFile = (fileId: string) => runAction(
    async (signal) => (await checkpointSummaryApi.request(teamId, checkpointNumber, [fileId], signal)).data,
    setRequesting,
    'Đã xếp hàng lại file. Bản tóm tắt hiện ở dòng của file khi xong.');

  const cancelAll = () => runAction(
    async (signal) => (await checkpointSummaryApi.cancel(teamId, checkpointNumber, signal)).data,
    setCancelling,
    'Đã hủy AI tóm tắt. File đã xong vẫn giữ bản tóm tắt.');

  const toggleFile = (fileId: string) => {
    setSelected((current) => (current.includes(fileId) ? current.filter((id) => id !== fileId) : [...current, fileId]));
  };

  const files = summary?.files ?? [];
  const numbers = displayNumbers(summary?.requirements ?? []);
  const selectableIds = selectableFileIds(summary);
  const allSelected = selectableIds.length > 0 && selectableIds.every((id) => selected.includes(id));
  const inProgress = hasWorkInProgress(summary);
  const statusBadges = summaryStatusBadges(summary, Boolean(loadError));
  const counts = summaryStatusCounts(files);
  const busy = requesting || cancelling;
  const eta = summaryEtaText(summary);
  const actions = { onOpenFile, onQuote, onRate: rate, ratingFileId };

  // The server decides per environment whether AI summaries exist. Until it has answered, and whenever the feature is
  // switched off, nothing is drawn, so the screen looks as if the feature was never there. A failed load still shows
  // the card so the reviewer can retry.
  if (!isSummaryFeatureVisible(summary) && !loadError) return null;

  return (
    <SummaryActionsContext.Provider value={actions}>
    <section className="rounded-2xl border border-slate-200/60 bg-white shadow-sm" aria-label="AI tóm tắt">
      <button
        type="button"
        onClick={() => setExpanded((value) => !value)}
        aria-expanded={expanded}
        aria-controls={regionId}
        className="flex w-full flex-wrap items-center gap-x-3 gap-y-2 rounded-2xl px-5 py-4 text-left transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600"
      >
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary-50">
          <Sparkles className="h-[18px] w-[18px] text-primary-700" aria-hidden="true" />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block text-subheading text-secondary">AI tóm tắt</span>
          <span className="block text-caption text-slate-500">Chỉ mentor, giảng viên và admin nhìn thấy</span>
        </span>
        {/* On a narrow screen the status drops under the title instead of squeezing it. */}
        <span className="order-last flex basis-full flex-wrap items-center gap-1.5 pl-12 sm:order-none sm:basis-auto sm:justify-end sm:pl-0">
          {inProgress && <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" aria-hidden="true" />}
          {statusBadges.map((badge) => <Badge key={badge.key} variant={badge.tone}>{badge.label}</Badge>)}
          {eta && <span className="text-caption text-slate-500">{eta}</span>}
          <span className="sr-only" role="status" aria-live="polite">
            {loadError ? 'Không tải được AI tóm tắt.' : `${counts.ready}/${counts.total} file đã tóm tắt, ${counts.summarising + counts.queued} đang xử lý.`}
          </span>
        </span>
        <ChevronDown className={`h-4 w-4 shrink-0 text-slate-500 transition-transform ${expanded ? 'rotate-180' : ''}`} aria-hidden="true" />
      </button>

      {expanded && (
        <div id={regionId} className="space-y-4 px-5 pb-5">
          {loading ? (
            <div className="flex items-center gap-2 py-4 text-body-sm text-slate-500" role="status">
              <Loader2 className="h-4 w-4 animate-spin text-primary" aria-hidden="true" /> Đang tải AI tóm tắt…
            </div>
          ) : loadError ? (
            <div className="flex flex-wrap items-center gap-3 py-2" role="alert">
              <p className="text-body-sm text-danger-dark">{loadError}</p>
              <Button variant="outline" size="sm" icon={RefreshCw} onClick={retryLoad}>Thử lại</Button>
            </div>
          ) : files.length === 0 ? (
            <p className="text-body-sm text-slate-500">Nhóm chưa nộp file nào cho checkpoint này.</p>
          ) : (
            <>
              <div className="flex items-start gap-2 text-caption text-slate-500">
                <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                <p>Do AI tạo, có thể chưa chính xác. Hãy kiểm tra file trước khi dựa vào; không dùng để chấm điểm.</p>
              </div>

              <div className="space-y-2">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <SectionLabel icon={FileText}>Các file</SectionLabel>
                  {selectableIds.length > 1 && summary.enabled && (
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => setSelected(allSelected ? [] : selectableIds)}
                      className="rounded-md px-1.5 py-0.5 text-caption font-semibold text-primary-700 transition hover:bg-primary-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-600 disabled:opacity-50"
                    >
                      {allSelected ? 'Bỏ chọn' : `Chọn tất cả (${selectableIds.length})`}
                    </button>
                  )}
                </div>
                <p className="text-caption text-slate-500">
                  File mới nộp được tự xếp hàng tóm tắt. Đánh dấu file để tóm tắt trước; mở một file để đọc bản tóm tắt.
                </p>
                <ul className="space-y-2">
                  {files.map((file) => (
                    <FileRow
                      key={file.fileId}
                      file={file}
                      selected={selected.includes(file.fileId)}
                      onToggleSelected={() => toggleFile(file.fileId)}
                      onRetry={() => retryFile(file.fileId)}
                      maxCharacters={summary.filePromptCharacters}
                      disabled={busy || !summary.enabled}
                    />
                  ))}
                </ul>
              </div>

              {actionError && <p className="text-body-sm text-danger-dark" role="alert">{actionError}</p>}
              {summary.enabled && (
                <div className="flex flex-wrap items-center gap-2">
                  {files.some(isFileSelectable) ? (
                    <Button variant="primary" size="sm" icon={Sparkles} isLoading={requesting}
                      disabled={selected.length === 0 || cancelling} onClick={requestSelected}>
                      {selected.length > 0 ? `Tóm tắt ${selected.length} file` : 'Chọn file để tóm tắt'}
                    </Button>
                  ) : !inProgress && (
                    <span className="text-body-sm text-slate-600">Mọi file đã được tóm tắt. File mới nộp sẽ hiện ở đây.</span>
                  )}
                  {inProgress && (
                    <Button variant="danger-outline" size="sm" icon={X} isLoading={cancelling} disabled={requesting} onClick={cancelAll}>
                      Hủy
                    </Button>
                  )}
                  {inProgress && <span className="text-caption text-slate-500">{eta ? `${eta}. ` : ''}Hệ thống tóm tắt lần lượt từng file; trang tự cập nhật.</span>}
                </div>
              )}

              {summary.enabled && hasReplacedFiles(files.map((file) => ({ name: file.name }))) && (
                <CheckpointVersionChangesBlock teamId={teamId} checkpointNumber={checkpointNumber} onOpenFile={onOpenFile} />
              )}

              <FigureConflicts conflicts={summary.conflicts ?? []} acrossFiles />

              {summary.enabled && <FigureHistory teamId={teamId} checkpointNumber={checkpointNumber} />}

              {summary.requirements.length > 0 && (
                <Card tone="inset" padding="p-4" hover={false} className="space-y-3">
                  <SectionLabel icon={ListChecks}>Yêu cầu và tiêu chí rubric</SectionLabel>
                  <p className="text-caption text-slate-500">Tổng hợp từ các file đã đọc. Các trích dẫn được sao chép từ file, không phải do AI viết.</p>
                  {isNothingFound(summary.requirements) ? (
                    <NothingFound titles={summary.requirements.filter(isSearchable).map((item) => item.title)} subject="Các file đã đọc" />
                  ) : (
                    <ul className="space-y-3">
                      {sortByWeight(summary.requirements.filter(isSearchable)).map((requirement) => {
                        const finding = summaryFindingView[requirement.finding];
                        return (
                          <li key={requirement.index} className="space-y-1">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="text-body-sm font-semibold text-slate-900">{numbers.get(requirement.index)}. {requirement.title}</span>
                              {requirement.weight != null && <Badge variant="neutral">{requirement.weight}%</Badge>}
                              <Badge variant="neutral">{KIND_LABEL[requirement.kind] ?? requirement.kind}</Badge>
                              <Badge variant={finding.tone}>{finding.label}</Badge>
                            </div>
                            {requirement.evidence
                              ? <Quoted text={requirement.evidence} source={requirement.source} fileId={requirement.sourceFileId} page={requirement.evidencePage} />
                              : <p className="text-caption text-slate-500">{finding.hint}</p>}
                            <Cues cues={requirement.cues ?? []} />
                          </li>
                        );
                      })}
                    </ul>
                  )}
                  <OtherCriteria findings={summary.requirements} numbers={numbers} />
                </Card>
              )}

              {summary.links.length > 0 && (
                <div className="space-y-2">
                  <SectionLabel icon={Link2}>Liên kết</SectionLabel>
                  <p className="text-caption text-slate-500">AI không mở liên kết; chỉ liệt kê tên và tên miền.</p>
                  <ul className="space-y-1 text-body-sm text-slate-700">
                    {summary.links.map((link) => <li key={`${link.name}-${link.domain}`}>{link.name} <span className="text-slate-500">({link.domain})</span></li>)}
                  </ul>
                </div>
              )}
            </>
          )}
        </div>
      )}
    </section>
    </SummaryActionsContext.Provider>
  );
}
