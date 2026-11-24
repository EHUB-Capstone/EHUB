import { useId, useMemo, useState } from 'react';
import { AlertTriangle, ChevronDown, Pencil, RefreshCw, Undo2 } from 'lucide-react';
import type {
  MentorAllocationConflict,
  MentorAllocationEdit,
  MentorAllocationPreview,
  MentorType,
} from '../../types/mentorAdmin';
import {
  ALL_CLASSES,
  buildTeamGrid,
  filterTeamGrid,
  formatAllocationPreviewReasons,
  getAiReviewAssignments,
  getAvailableFitAlternatives,
  getMentorLoadImpact,
  getMissingProfileFields,
  resolveClassFilter,
  teamClassOptions,
  getMentorCandidates,
  getSubjectColumns,
  skipReasonLabel,
  summarizePreview,
  summarizeAllocationQuality,
  visibleFitCriteria,
  type SlotState,
  type SlotView,
  type TeamGridRow,
} from '../../utils/mentorAllocationPreview';
import { buildChangeReason, isChangeReasonValid } from '../../utils/mentorChangeReasons';
import Badge, { badgeTones, type BadgeTone } from '../ui/Badge';
import Button from '../ui/Button';
import Card from '../ui/Card';
import Modal from '../ui/Modal';
import MentorChangeReasonField from './MentorChangeReasonField';
import MentorSearchSelect from './MentorSearchSelect';
import { useTranslation } from 'react-i18next';
import i18n from '../../i18n';
import { formatNumber as fmtNumber } from '../../i18n/format';

type PreviewTab = 'teams' | 'mentors' | 'attention';
type TeamFilter = 'all' | 'attention' | 'changed';

interface EditorTarget {
  teamId: string;
  mentorType: MentorType;
  presetMentorId?: string;
}

interface MentorAllocationPreviewModalProps {
  isOpen: boolean;
  preview: MentorAllocationPreview | null;
  scopeLabel?: string;
  errorMessage?: string;
  edits: MentorAllocationEdit[];
  isRefreshing: boolean;
  isCommitting: boolean;
  isStale: boolean;
  onClose: () => void;
  onApplyEdit: (edit: MentorAllocationEdit) => void;
  onRemoveEdit: (teamId: string, mentorType: MentorType) => void;
  onRegenerate: () => void;
  onConfirm: () => void;
}

const slotLabel: Record<MentorType, string> = { get Enterprise() { return i18n.t('admin:mentorAllocationPreviewModal.enterpriseMentor'); }, get Academic() { return i18n.t('admin:mentorAllocationPreviewModal.lecturerMentor'); } };

// The same tone is used for a status wherever it appears: the chip in a slot and the figure at the top.
const stateStyle: Record<SlotState, { label: string; tone: BadgeTone }> = {
  existing: { get label() { return i18n.t('admin:mentorAllocationPreviewModal.current'); }, tone: 'neutral' },
  retained: { get label() { return i18n.t('admin:mentorAllocationPreviewModal.kept'); }, tone: 'info' },
  allocated: { get label() { return i18n.t('admin:mentorAllocationPreviewModal.new'); }, tone: 'success' },
  manual: { get label() { return i18n.t('admin:mentorAllocationPreviewModal.manual'); }, tone: 'brand' },
  unfilled: { get label() { return i18n.t('admin:mentorAllocationPreviewModal.missing'); }, tone: 'danger' },
};
const replacedTone: BadgeTone = 'warning';
const criterionLabels: Record<typeof visibleFitCriteria[number], string> = {
  get experience() { return i18n.t('admin:mentorAllocationPreviewModal.experience'); }, get expertise() { return i18n.t('admin:mentorAllocationPreviewModal.expertiseTags'); }, get startupDomain() { return i18n.t('admin:mentorProfileDetail.startupDomain'); },
  get bio() { return i18n.t('admin:mentorAllocationPreviewModal.backgroundEducation'); },
};

export default function MentorAllocationPreviewModal({
  isOpen,
  preview,
  scopeLabel,
  errorMessage,
  edits,
  isRefreshing,
  isCommitting,
  isStale,
  onClose,
  onApplyEdit,
  onRemoveEdit,
  onRegenerate,
  onConfirm,
}: MentorAllocationPreviewModalProps) {
  const { t } = useTranslation('admin');
  const [tab, setTab] = useState<PreviewTab>('teams');
  const [filter, setFilter] = useState<TeamFilter>('all');
  const [search, setSearch] = useState('');
  const [classFilter, setClassFilter] = useState(ALL_CLASSES);
  const [editor, setEditor] = useState<EditorTarget | null>(null);

  const grid = useMemo(() => (preview ? buildTeamGrid(preview) : []), [preview]);
  const summary = useMemo(() => (preview ? summarizePreview(preview) : null), [preview]);
  const quality = useMemo(() => (preview ? summarizeAllocationQuality(preview) : null), [preview]);
  const classOptions = useMemo(() => teamClassOptions(grid), [grid]);
  const activeClass = resolveClassFilter(classFilter, classOptions);
  const visibleRows = useMemo(() => filterTeamGrid(grid, { filter, search, classCode: activeClass }), [grid, filter, search, activeClass]);
  const classOfTeam = useMemo(() => new Map(grid.map(row => [row.teamId, row.classCode])), [grid]);

  if (!preview || !summary) return null;

  const busy = isRefreshing || isCommitting;
  const conflicts = preview.conflicts ?? [];
  const skipped = preview.skipped ?? [];
  const unfilled = preview.unfilled ?? [];
  const reviewAssignments = getAiReviewAssignments(preview);
  const attentionCount = conflicts.length + skipped.length + unfilled.length + reviewAssignments.length;

  const openEditor = (target: EditorTarget) => {
    setTab('teams');
    setFilter('all');
    setSearch('');
    setClassFilter(ALL_CLASSES);
    setEditor(target);
  };

  const applyEdit = (edit: MentorAllocationEdit) => {
    setEditor(null);
    onApplyEdit(edit);
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={busy ? () => undefined : onClose}
      title={`${t(preview.strategy === 'Ai' ? 'mentorAllocationPreviewModal.aiPreviewTitle' : 'mentorAllocationPreviewModal.previewTitle')}${scopeLabel ? ` · ${scopeLabel}` : ''}`}
      size="2xl"
      submitText={preview.strategy === 'Ai' ? t('mentorAllocationPreviewModal.acceptAndAssignMentors') : t('mentorAllocationPreviewModal.confirmAndSave')}
      onSubmit={onConfirm}
      isSubmitting={isCommitting}
      submitDisabled={!preview.canCommit || isStale || isRefreshing}
    >
      <div className="space-y-4">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-xs leading-5 text-slate-500">
            {t('mentorAllocationPreviewModal.nothingIsSavedUntilYouConfirm')}
            {' '}{t('mentorAllocationPreviewModal.strategy')} <strong className="text-slate-700">{preview.strategy === 'Ai' ? 'AI' : preview.strategy ?? t('mentorAllocationPreviewModal.balanced')}</strong>.
          </p>
          <Button size="sm" variant="outline" icon={RefreshCw} onClick={onRegenerate} disabled={busy}>
            {t(edits.length > 0 ? 'mentorAllocationPreviewModal.regenerateClearsEdits' : 'mentorAllocationPreviewModal.regenerate')}
          </Button>
        </div>
        {preview.strategy === 'Ai' && (
          <p className="rounded-xl bg-info-50 px-3 py-2 text-body-sm text-info-dark">
            {t('mentorAllocationPreviewModal.aiMatchesBusinessMentorsToProject')}
          </p>
        )}
        {summary.teams > 0 && summary.allocated === 0 && summary.manual === 0 && summary.replacements === 0
          && summary.unfilledEnterprise === 0 && summary.unfilledAcademic === 0 && (
          <p role="status" className="rounded-xl bg-warning-50 px-3 py-2 text-body-sm text-warning-dark">
            {t('mentorAllocationPreviewModal.allMentorSlotsAlreadyFilled', { change: t('classManagement.change') })}
          </p>
        )}
        {(preview.balance?.length ?? 0) > 0 && (
          <section aria-label={t('mentorAllocationPreviewModal.semesterWorkloadBalance')} className="rounded-xl border border-slate-200 p-3 text-body-sm text-slate-600">
            <h3 className="font-semibold text-slate-800">{t('mentorAllocationPreviewModal.chiaDeuTheoHocKy')}</h3>
            <p className="mt-1 text-caption text-slate-500">{t('mentorAllocationPreviewModal.tinhTaiTrenCaHocKy')}</p>
            <ul className="mt-2 space-y-1">
              {preview.balance?.map(item => <li key={item.mentorType}>
                <strong>{slotLabel[item.mentorType]}</strong>: {item.semesterTeamCount} {t('mentorAllocationPreviewModal.nhom')} {item.eligibleMentorCount} {t('mentorAllocationPreviewModal.mentorHopLe')}
                {item.eligibleMentorCount > 0
                  ? t('mentorAllocationPreviewModal.mucTieuNhomMentorSauDe', { v1: item.targetMinTeams === item.targetMaxTeams ? item.targetMinTeams : `${item.targetMinTeams}–${item.targetMaxTeams}`, minLoadAfter: item.minLoadAfter, maxLoadAfter: item.maxLoadAfter })
                  : t('mentorAllocationPreviewModal.chuaCoMentorHopLeDe')}
              </li>)}
            </ul>
          </section>
        )}
        {errorMessage && (
          <div role="alert" className="rounded-xl bg-danger-50 px-3 py-2 text-body-sm text-danger-dark">
            <p>{errorMessage}</p>
            <Button size="sm" variant="outline" className="mt-2" onClick={onRegenerate} disabled={busy}>{t('addSemesterStaffModal.tryAgain')}</Button>
          </div>
        )}

        {isStale && (
          <div role="alert" className="flex items-start gap-2 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>{t('mentorAllocationPreviewModal.theDataChangedAfterThisPreview')}</span>
          </div>
        )}
        {preview.warnings.length > 0 && (
          <ul aria-label={t('mentorAllocationPreviewModal.allocationWarnings')} className="max-h-28 space-y-1 overflow-y-auto rounded-xl bg-amber-50 px-3 py-2 text-sm text-amber-800">
            {preview.warnings.map(message => <li key={message}>{message}</li>)}
          </ul>
        )}

        <div className="grid grid-cols-2 gap-2 text-xs sm:grid-cols-4 lg:grid-cols-7">
          <Metric label={t('classManagement.teams')} value={summary.teams} tone="neutral" />
          <Metric label={stateStyle.retained.label} value={summary.retained} tone={stateStyle.retained.tone} />
          <Metric label={stateStyle.allocated.label} value={summary.allocated} tone={stateStyle.allocated.tone} />
          <Metric label={stateStyle.manual.label} value={summary.manual} tone={stateStyle.manual.tone} />
          <Metric label={t('mentorAllocationPreviewModal.replaced')} value={summary.replacements} tone={summary.replacements > 0 ? replacedTone : 'neutral'} />
          <Metric label={t('mentorAllocationPreviewModal.missingEnterprise')} value={summary.unfilledEnterprise} tone={summary.unfilledEnterprise > 0 ? stateStyle.unfilled.tone : 'neutral'} />
          <Metric label={t('mentorAllocationPreviewModal.missingLecturer')} value={summary.unfilledAcademic} tone={summary.unfilledAcademic > 0 ? stateStyle.unfilled.tone : 'neutral'} />
        </div>

        {quality && <Card tone="inset" hover={false} padding="p-3" role="region" aria-label={t('mentorAllocationPreviewModal.aiEvidenceCoverage')} className="space-y-2 text-body-sm text-slate-600">
          <div className="flex flex-wrap gap-x-5 gap-y-2">
            <p><strong className="text-slate-800">{quality.supported}/{quality.assessed}</strong> {t('mentorAllocationPreviewModal.suggestionsWithSupportingEvidence')}</p>
            <p><strong className="text-slate-800">{quality.needsReview}</strong> {t('mentorAllocationPreviewModal.needFitReview')}</p>
            <p><strong className="text-slate-800">{quality.missingProfileData}</strong> {t('mentorAllocationPreviewModal.haveIncompleteProfileData')}</p>
            {quality.unknownProfileData > 0 && <p>{t('mentorAllocationPreviewModal.profileDataCoverageUnavailableFor')} <strong className="text-slate-800">{quality.unknownProfileData}</strong></p>}
            {quality.averageFit !== null && <p>{t('mentorAllocationPreviewModal.averageFit')} <strong className="text-slate-800">{fmtNumber(quality.averageFit, { maximumFractionDigits: 1 })}/100</strong></p>}
          </div>
          <p className="text-caption text-slate-500">{t('mentorAllocationPreviewModal.profile70MentoringHistoryUpTo')}</p>
        </Card>}

        <div role="tablist" aria-label={t('mentorAllocationPreviewModal.previewViews')} className="flex flex-wrap gap-1 border-b border-slate-200">
          <TabButton active={tab === 'teams'} onClick={() => setTab('teams')}>{t('mentorAllocationPreviewModal.byTeam', { length: grid.length })}</TabButton>
          <TabButton active={tab === 'mentors'} onClick={() => setTab('mentors')}>{t('mentorAllocationPreviewModal.byMentor', { v1: preview.mentorLoads?.length ?? 0 })}</TabButton>
          <TabButton active={tab === 'attention'} onClick={() => setTab('attention')}>{t('mentorAllocationPreviewModal.needsAttention', { attentionCount })}</TabButton>
        </div>

        <div className={isRefreshing ? 'pointer-events-none opacity-60' : ''} aria-busy={isRefreshing}>
          {tab === 'teams' && (
            <section aria-label={t('mentorAllocationPreviewModal.assignmentsByTeam')} className="space-y-3">
              <div className="flex flex-col gap-2 sm:flex-row">
                <input
                  value={search}
                  onChange={event => setSearch(event.target.value)}
                  placeholder={t('mentorAllocationPreviewModal.searchTeamClassOrMentor')}
                  aria-label={t('mentorAllocationPreviewModal.searchTeams')}
                  className="min-w-0 flex-1 rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary"
                />
                {classOptions.length > 1 && (
                  <select
                    value={activeClass}
                    onChange={event => setClassFilter(event.target.value)}
                    aria-label={t('mentorAllocationPreviewModal.filterByClass')}
                    className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-primary"
                  >
                    <option value={ALL_CLASSES}>{t('mentorAllocationPreviewModal.allClasses', { length: grid.length })}</option>
                    {classOptions.map(option => <option key={option.classCode} value={option.classCode}>{option.classCode} ({option.teamCount})</option>)}
                  </select>
                )}
                <select
                  value={filter}
                  onChange={event => setFilter(event.target.value as TeamFilter)}
                  aria-label={t('mentorAllocationPreviewModal.filterTeams')}
                  className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 outline-none focus:border-primary"
                >
                  <option value="all">{t('mentorAllocationPreviewModal.allTeams')}</option>
                  <option value="changed">{t('mentorAllocationPreviewModal.changedByThisPreview')}</option>
                  <option value="attention">{t('mentorAllocationPreviewModal.needsAttention2')}</option>
                </select>
              </div>
              {visibleRows.length === 0 ? (
                <p className="rounded-xl bg-slate-50 px-3 py-6 text-center text-body-sm text-slate-500">{grid.length === 0 ? t('mentorAllocationPreviewModal.thereAreNoActiveTeamsIn') : t('mentorAllocationPreviewModal.noTeamMatchesTheCurrentFilters')}</p>
              ) : (
                <div className="max-h-[48vh] overflow-auto rounded-xl border border-slate-200 bg-white">
                  <table className="w-full min-w-[820px] text-left text-xs">
                    <caption className="sr-only">{t('mentorAllocationPreviewModal.mentorsOfEachTeamAfterThis')}</caption>
                    <thead className="sticky top-0 z-10 bg-slate-50 text-slate-600">
                      <tr>
                        <th scope="col" className="px-3 py-2">{t('mentorAllocationPreviewModal.team')}</th>
                        <th scope="col" className="px-3 py-2">{slotLabel.Enterprise}</th>
                        <th scope="col" className="px-3 py-2">{slotLabel.Academic}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {visibleRows.map(row => (
                        <TeamRow
                          key={row.teamId}
                          row={row}
                          preview={preview}
                          edits={edits}
                          editor={editor}
                          disabled={busy}
                          onOpenEditor={openEditor}
                          onCloseEditor={() => setEditor(null)}
                          onApplyEdit={applyEdit}
                          onRemoveEdit={onRemoveEdit}
                        />
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          )}

          {tab === 'mentors' && <MentorLoadTable preview={preview} />}

          {tab === 'attention' && activeClass !== ALL_CLASSES && (
            <p className="mb-2 text-xs text-slate-500">{t('mentorAllocationPreviewModal.showingOnlyClass')} <strong className="text-slate-700">{activeClass}</strong>. <button type="button" className="font-semibold text-primary-700 underline" onClick={() => setClassFilter(ALL_CLASSES)}>{t('mentorAllocationPreviewModal.showAllClasses')}</button></p>
          )}
          {tab === 'attention' && (
            <AttentionPanel
              conflicts={activeClass === ALL_CLASSES ? conflicts : conflicts.filter(item => item.teamId && classOfTeam.get(item.teamId) === activeClass)}
              skipped={activeClass === ALL_CLASSES ? skipped : skipped.filter(item => item.classCode === activeClass)}
              unfilled={activeClass === ALL_CLASSES ? unfilled : unfilled.filter(item => item.classCode === activeClass)}
              reviewAssignments={activeClass === ALL_CLASSES ? reviewAssignments : reviewAssignments.filter(item => item.classCode === activeClass)}
              onReplace={(conflict) => conflict.teamId && openEditor({
                teamId: conflict.teamId,
                mentorType: conflict.mentorType,
                presetMentorId: conflict.proposedMentorProfileId ?? undefined,
              })}
            />
          )}
        </div>
      </div>
    </Modal>
  );
}

function TeamRow({
  row, preview, edits, editor, disabled, onOpenEditor, onCloseEditor, onApplyEdit, onRemoveEdit,
}: {
  row: TeamGridRow;
  preview: MentorAllocationPreview;
  edits: MentorAllocationEdit[];
  editor: EditorTarget | null;
  disabled: boolean;
  onOpenEditor: (target: EditorTarget) => void;
  onCloseEditor: () => void;
  onApplyEdit: (edit: MentorAllocationEdit) => void;
  onRemoveEdit: (teamId: string, mentorType: MentorType) => void;
}) {
  const { t } = useTranslation('admin');
  return (
    <tr className="border-t border-slate-100 align-top">
      <td className="px-3 py-2">
        <span className="block font-semibold text-slate-800">{row.teamName}</span>
        <span className="text-caption text-slate-400">{row.classCode}{row.subjectCode ? ` · ${row.subjectCode}` : ''}</span>
      </td>
      {(['Enterprise', 'Academic'] as const).map(type => {
        const slot = type === 'Enterprise' ? row.enterprise : row.academic;
        const editing = editor?.teamId === row.teamId && editor.mentorType === type;
        const hasEdit = edits.some(edit => edit.teamId === row.teamId && edit.mentorType === type);
        return (
          <td key={type} className="px-3 py-2">
            <SlotCell slot={slot} />
            {!editing && (
              <div className="mt-1.5 flex flex-wrap gap-1">
                <Button size="xs" variant="ghost" icon={Pencil} disabled={disabled} onClick={() => onOpenEditor({ teamId: row.teamId, mentorType: type })} aria-label={t('mentorAllocationPreviewModal.changeOf', { v1: slotLabel[type], teamName: row.teamName })}>
                  {t('classManagement.change')}
                </Button>
                {hasEdit && (
                  <Button size="xs" variant="ghost" icon={Undo2} disabled={disabled} onClick={() => onRemoveEdit(row.teamId, type)} aria-label={t('mentorAllocationPreviewModal.undoEditOf', { teamName: row.teamName, v2: slotLabel[type] })}>
                    {t('mentorAllocationPreviewModal.undo')}
                  </Button>
                )}
              </div>
            )}
            {editing && (
              <SlotEditor
                teamName={row.teamName}
                type={type}
                slot={slot}
                candidates={getMentorCandidates(preview, type)}
                preview={preview}
                disabled={disabled}
                presetMentorId={editor.presetMentorId}
                onCancel={onCloseEditor}
                onApply={onApplyEdit}
                teamId={row.teamId}
              />
            )}
          </td>
        );
      })}
    </tr>
  );
}

function SlotCell({ slot }: { slot: SlotView }) {
  const { t } = useTranslation('admin');
  const style = stateStyle[slot.state];
  const missingFields = getMissingProfileFields(slot.fitDetails);
  const criterionCount = visibleFitCriteria.length;
  return (
    <div>
      <div className="flex flex-wrap items-center gap-1.5">
        <Badge variant={style.tone} className="rounded-full text-overline">{style.label}</Badge>
        {slot.needsReview && <Badge variant="warning" className="rounded-full text-overline">{t('mentorAllocationPreviewModal.needsReview')}</Badge>}
        {slot.mentorName
          ? <span className="font-medium text-slate-800">{slot.mentorName}</span>
          : <span className="text-slate-400">{t('mentorAllocationPreviewModal.noMentor')}</span>}
      </div>
      {(slot.fitScore != null || (slot.reasons?.length ?? 0) > 0 || slot.fitDetails) && (
        <div className="mt-2 space-y-1 text-caption text-slate-600">
          {slot.fitScore != null && <p className="font-semibold">{t('mentorAllocationPreviewModal.diemPhuHop100', { v1: fmtNumber(slot.fitScore, { maximumFractionDigits: 1 }) })}</p>}
          {missingFields && <p className={missingFields.length ? 'text-warning-dark' : 'text-slate-500'}>
            {t('mentorAllocationPreviewModal.profileDataCriteriaAvailable', { v1: criterionCount - missingFields.length, criterionCount, v3: missingFields.length > 0 ? ` · ${t('mentorAllocationPreviewModal.missingList', { list: missingFields.map(key => criterionLabels[key]).join(', ') })}` : '' })}
          </p>}
          {slot.history && <div role="group" aria-label={t('mentorAllocationPreviewModal.mentoringHistoryFor', { v1: slot.mentorName ?? 'mentor' })} className="space-y-1">
            <p>{slot.history.mentoredTeamCount || slot.history.achievementCount
              ? t('mentorAllocationPreviewModal.nhomDuAnCoThanhTich', { mentoredTeamCount: slot.history.mentoredTeamCount, achievementCount: slot.history.achievementCount })
              : t('mentorAllocationPreviewModal.chuaCoLichSuHuongDan')}</p>
            {(slot.history.historyBonus > 0 || slot.history.achievementBonus > 0) && (
              <p>{t('mentorAllocationPreviewModal.diemNenLichSuThanhTich', { baseFitScore: slot.history.baseFitScore, historyBonus: slot.history.historyBonus, achievementBonus: slot.history.achievementBonus })}</p>
            )}
          </div>}
          {slot.needsReview && <p className="text-warning-dark">{t('mentorAllocationPreviewModal.canKiemTraMucPhuHop')}</p>}
          <AssignmentDetails key={slot.mentorProfileId} mentorName={slot.mentorName} reasons={slot.reasons} fitDetails={slot.fitDetails} />
        </div>
      )}
      {slot.replacesCurrent && slot.currentMentorName && (
        <p className="mt-1 text-caption text-amber-700">{t('mentorAllocationPreviewModal.replaces')} <span className="line-through">{slot.currentMentorName}</span> {t('mentorAllocationPreviewModal.assignmentEndsOnSave')}</p>
      )}
    </div>
  );
}

function AssignmentDetails({ mentorName, reasons, fitDetails }: { mentorName: string | null; reasons?: string[]; fitDetails?: SlotView['fitDetails'] }) {
  const { t } = useTranslation('admin');
  const [expanded, setExpanded] = useState(false);
  const detailsId = useId();
  const previewReasons = fitDetails ? [] : formatAllocationPreviewReasons(reasons);
  if (!previewReasons.length && !fitDetails) return null;
  return (
    <div>
      <Button
        size="xs" variant="ghost" className="w-full justify-between px-0"
        aria-expanded={expanded} aria-controls={detailsId}
        aria-label={t(expanded ? 'mentorAllocationPreviewModal.collapseReview' : 'mentorAllocationPreviewModal.expandReview', { name: mentorName ?? t('mentorAllocationPreviewModal.mentorFallback') })}
        onClick={() => setExpanded(value => !value)}
      >
        {expanded ? t('mentorAllocationPreviewModal.thuGon') : t('mentorAllocationPreviewModal.xemChiTiet')}
        <ChevronDown aria-hidden="true" className={`h-4 w-4 transition-transform ${expanded ? 'rotate-180' : ''}`} />
      </Button>
      {expanded && (
        <div id={detailsId} className="mt-2 space-y-3" aria-label={t('mentorAllocationPreviewModal.assignmentReasonsFor', { v1: mentorName ?? 'mentor' })}>
          {fitDetails && <>
            <p className="font-semibold">{t('mentorAllocationPreviewModal.profileFit100WeightedAt70', { v1: fmtNumber(fitDetails.profileScore, { maximumFractionDigits: 1 }) })}</p>
            {fitDetails.projectNeeds.length > 0 && <div>
              <p className="font-semibold text-slate-700">{t('mentorAllocationPreviewModal.projectSupportNeeds')}</p>
              <ul className="mt-1 list-disc space-y-1 pl-4">{fitDetails.projectNeeds.map((need, index) => <li key={`${index}-${need}`}>{need}</li>)}</ul>
            </div>}
            <dl className="space-y-2" aria-label={t('mentorAllocationPreviewModal.profileCriteriaAndSupportingPassages')}>
              {visibleFitCriteria.map(key => {
                const criterion = fitDetails.criteria.find(item => item.key === key);
                const available = fitDetails.availableProfileFields.includes(key);
                return <div key={key} className="border-l-2 border-slate-200 pl-2">
                  <dt className="font-semibold text-slate-700">{criterionLabels[key]} · {available ? `${criterion?.score ?? 0}/100` : t('mentorAllocationPreviewModal.missingProfileData')}</dt>
                  <dd className="mt-0.5 break-words text-slate-500">{!available
                    ? t('mentorAllocationPreviewModal.addSourceInformationToAssessThis')
                    : criterion?.evidence ? <blockquote>“{criterion.evidence}”</blockquote>
                      : t('mentorAllocationPreviewModal.noRelevantSupportingPassageForThis')}</dd>
                </div>;
              })}
            </dl>
          </>}
          {previewReasons.length > 0 && <ul className="list-disc space-y-1 pl-4">
            {previewReasons.map((reason, index) => <li key={`${index}-${reason}`}>{reason}</li>)}
          </ul>}
        </div>
      )}
    </div>
  );
}

function SlotEditor({
  teamId, teamName, type, slot, candidates, preview, disabled, presetMentorId, onCancel, onApply,
}: {
  teamId: string;
  teamName: string;
  type: MentorType;
  slot: SlotView;
  candidates: ReturnType<typeof getMentorCandidates>;
  preview: MentorAllocationPreview;
  disabled: boolean;
  presetMentorId?: string;
  onCancel: () => void;
  onApply: (edit: MentorAllocationEdit) => void;
}) {
  const { t } = useTranslation('admin');
  const occupied = slot.state === 'existing' || slot.replacesCurrent;
  const [mentorId, setMentorId] = useState(presetMentorId ?? '');
  const [reasonCode, setReasonCode] = useState('');
  const [reasonNote, setReasonNote] = useState('');
  const sameAsCurrent = occupied && mentorId !== '' && mentorId === (slot.currentMentorProfileId ?? slot.mentorProfileId);
  const needsReason = occupied && mentorId !== '' && !sameAsCurrent;
  const reasonOk = !needsReason || isChangeReasonValid(reasonCode, reasonNote);
  const alreadyEmpty = slot.state === 'unfilled' && mentorId === '';
  const canApply = !disabled && !sameAsCurrent && !alreadyEmpty && reasonOk && (mentorId !== '' || !occupied);
  const fieldId = `${teamId}-${type}`;
  const alternatives = getAvailableFitAlternatives(slot, candidates);
  const loadImpact = getMentorLoadImpact(preview, slot, mentorId);
  const selectedAlternative = alternatives.find(candidate => candidate.mentorProfileId === mentorId);
  const options = useMemo(() => candidates.map(candidate => ({
    id: candidate.mentorProfileId,
    label: candidate.mentorName,
    detail: `${candidate.mentorEmail ? `${candidate.mentorEmail} · ` : ''}${candidate.totalAfter} team${candidate.totalAfter === 1 ? '' : 's'}`,
  })), [candidates]);

  const apply = () => {
    if (!canApply) return;
    onApply({
      teamId,
      mentorType: type,
      mentorProfileId: mentorId === '' ? null : mentorId,
      ...(needsReason ? { replace: true, reason: buildChangeReason(reasonCode, reasonNote) } : {}),
    });
  };

  return (
    <fieldset disabled={disabled} aria-label={t('mentorAllocationPreviewModal.editOf', { v1: slotLabel[type], teamName })} className="mt-2 space-y-2 rounded-lg border border-slate-200 bg-slate-50 p-2.5">
      <label htmlFor={`mentor-${fieldId}`} className="block text-caption font-semibold text-slate-600">
        {t('mentorAllocationPreviewModal.for', { v1: slotLabel[type], teamName })}
      </label>
      <MentorSearchSelect
        id={`mentor-${fieldId}`}
        options={options}
        value={mentorId}
        onChange={setMentorId}
        disabled={disabled}
        emptyLabel={occupied ? undefined : 'Leave empty (no mentor)'}
        placeholder={occupied ? t('mentorAllocationPreviewModal.searchOrChooseAMentor') : t('mentorAllocationPreviewModal.searchAMentorOrLeaveEmpty')}
      />
      {alternatives.length > 0 && <div className="space-y-1" aria-label={t('mentorAllocationPreviewModal.assessedAlternativesFor', { teamName, v2: slotLabel[type] })}>
        <p className="text-caption font-semibold text-slate-600">{t('mentorAllocationPreviewModal.alternativesWithSupportingEvidence')}</p>
        <div className="flex flex-wrap gap-1">{alternatives.map(candidate => <Button key={candidate.mentorProfileId}
          variant="outline" size="xs" aria-pressed={mentorId === candidate.mentorProfileId}
          onClick={() => setMentorId(candidate.mentorProfileId)}>
          {candidate.mentorName} · {candidate.fitScore}/100
        </Button>)}</div>
      </div>}
      {loadImpact && <div role="status" aria-live="polite" className="space-y-1 text-caption text-slate-600">
        <p>{t('mentorAllocationPreviewModal.projectedSemesterLoad')} <strong>{t('mentorAllocationPreviewModal.teams', { mentorName: loadImpact.selected.mentorName, before: loadImpact.selected.before, after: loadImpact.selected.after })}</strong></p>
        {loadImpact.previous && <p>{t('mentorAllocationPreviewModal.teams', { mentorName: loadImpact.previous.mentorName, before: loadImpact.previous.before, after: loadImpact.previous.after })}</p>}
        <p className="text-slate-500">{t('mentorAllocationPreviewModal.applyToUpdateThePreviewBefore')}</p>
      </div>}
      {selectedAlternative && <AssignmentDetails key={selectedAlternative.mentorProfileId} mentorName={selectedAlternative.mentorName} reasons={selectedAlternative.reasons} />}
      {candidates.length === 0 && <p className="text-caption text-amber-700">{t('mentorAllocationPreviewModal.noActiveInThisSemester', { v1: slotLabel[type] })}</p>}
      {needsReason && (
        <MentorChangeReasonField
          idPrefix={`reason-${fieldId}`}
          mentorName={slot.currentMentorName ?? slot.mentorName}
          code={reasonCode}
          note={reasonNote}
          onCodeChange={setReasonCode}
          onNoteChange={setReasonNote}
        />
      )}
      <div className="flex justify-end gap-1.5">
        <Button size="xs" variant="ghost" onClick={onCancel}>{t('common:actions.cancel')}</Button>
        <Button size="xs" variant="outline" disabled={!canApply} onClick={apply}>{needsReason ? t('mentorAllocationPreviewModal.replace') : mentorId === '' && !occupied ? t('mentorAllocationPreviewModal.leaveEmpty') : occupied ? t('mentorAllocationPreviewModal.replace') : t('common:actions.apply')}</Button>
      </div>
    </fieldset>
  );
}

function MentorLoadTable({ preview }: { preview: MentorAllocationPreview }) {
  const { t } = useTranslation('admin');
  const subjects = getSubjectColumns(preview);
  const mentors = preview.mentorLoads ?? [];
  if (mentors.length === 0) {
    return <p className="rounded-xl bg-slate-50 px-3 py-6 text-center text-sm text-slate-500">{t('mentorAllocationPreviewModal.noMentorIsActiveInThis')}</p>;
  }
  return (
    <section aria-label={t('mentorAllocationPreviewModal.loadByMentor')} className="max-h-[52vh] overflow-auto rounded-xl border border-slate-200 bg-white">
      <table className="w-full min-w-[720px] text-left text-xs">
        <caption className="sr-only">{t('mentorAllocationPreviewModal.teamsCarriedByEveryActiveMentor')}</caption>
        <thead className="sticky top-0 z-10 bg-slate-50 text-slate-600">
          <tr>
            <th scope="col" className="px-3 py-2">{t('subjectManagement.mentor')}</th>
            <th scope="col" className="px-3 py-2">{t('importMentorsModal.type')}</th>
            {subjects.map(code => <th key={code} scope="col" className="px-3 py-2">{code}</th>)}
            <th scope="col" className="px-3 py-2">{t('importLecturersModal.total')}</th>
            <th scope="col" className="px-3 py-2">{t('mentorProfileDetail.status')}</th>
          </tr>
        </thead>
        <tbody>
          {mentors.map(mentor => {
            const unassigned = mentor.totalAfter === 0;
            return (
              <tr key={mentor.mentorProfileId} className="border-t border-slate-100">
                <td className="px-3 py-2"><span className="block font-semibold text-slate-800">{mentor.mentorName}</span><span className="text-slate-400">{mentor.mentorEmail}</span></td>
                <td className="px-3 py-2">{slotLabel[mentor.mentorType]}{mentor.contractType ? <span className="block text-slate-400">{mentor.contractType}</span> : null}</td>
                {subjects.map(code => {
                  const entry = mentor.subjects.find(item => item.subjectCode === code);
                  return <td key={code} className="px-3 py-2">{entry ? formatChange(entry.before, entry.added, entry.removed ?? 0) : '0'}</td>;
                })}
                <td className="px-3 py-2 font-semibold text-slate-800">{mentor.totalBefore === mentor.totalAfter ? mentor.totalAfter : `${mentor.totalBefore} → ${mentor.totalAfter}`}</td>
                <td className="px-3 py-2">{unassigned ? <span className="rounded-full bg-amber-50 px-2 py-0.5 text-overline font-semibold text-amber-700">{t('classManagement.unassigned')}</span> : <span className="text-slate-500">{t('classManagement.assigned')}</span>}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </section>
  );
}

function formatChange(before: number, added: number, removed: number) {
  if (!added && !removed) return String(before);
  return `${before} → ${before + added - removed}`;
}

function AttentionPanel({
  conflicts, skipped, unfilled, reviewAssignments, onReplace,
}: {
  conflicts: MentorAllocationConflict[];
  skipped: NonNullable<MentorAllocationPreview['skipped']>;
  unfilled: NonNullable<MentorAllocationPreview['unfilled']>;
  reviewAssignments: MentorAllocationPreview['assignments'];
  onReplace: (conflict: MentorAllocationConflict) => void;
}) {
  const { t } = useTranslation('admin');
  if (conflicts.length + skipped.length + unfilled.length + reviewAssignments.length === 0) {
    return <p className="rounded-xl bg-green-50 px-3 py-6 text-center text-sm text-green-700">{t('mentorAllocationPreviewModal.everySlotHasAMentorAnd')}</p>;
  }
  return (
    <div className="max-h-[52vh] space-y-4 overflow-auto">
      {reviewAssignments.length > 0 && (
        <section aria-label={t('mentorAllocationPreviewModal.aiSuggestionsNeedingReview')} className="space-y-2">
          <h3 className="text-sm font-semibold text-slate-800">{t('mentorAllocationPreviewModal.aiSuggestionsNeedingReview2', { length: reviewAssignments.length })}</h3>
          <p className="text-body-sm text-slate-600">{t('mentorAllocationPreviewModal.theseSlotsHaveAMentorWith')}</p>
          <ul className="space-y-2">
            {reviewAssignments.map(item => (
              <li key={`${item.teamId}-${item.mentorType}`} className="rounded-lg border border-warning-light bg-warning-50 px-3 py-2 text-body-sm text-warning-dark">
                <strong>{item.teamName}</strong> · {item.classCode} · {slotLabel[item.mentorType]}
                <p>{item.mentorName} {t('mentorAllocationPreviewModal.fitScore0')} <Badge variant="warning">{t('mentorAllocationPreviewModal.needsReview')}</Badge></p>
                <AssignmentDetails mentorName={item.mentorName} reasons={item.reasons} fitDetails={item.fitDetails} />
              </li>
            ))}
          </ul>
        </section>
      )}
      {conflicts.length > 0 && (
        <section aria-label={t('mentorAllocationPreviewModal.conflicts')} className="space-y-2">
          <h3 className="text-sm font-semibold text-slate-800">{t('mentorAllocationPreviewModal.conflicts2', { length: conflicts.length })}</h3>
          <ul className="space-y-2">
            {conflicts.map((conflict, index) => (
              <li key={`${conflict.teamId}-${conflict.mentorType}-${index}`} className="flex flex-col gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 sm:flex-row sm:items-center sm:justify-between">
                <span><strong>{conflict.teamName || t('mentorAllocationPreviewModal.team')}</strong> · {slotLabel[conflict.mentorType]}: {conflict.message}</span>
                {conflict.kind === 'SlotOccupied' && conflict.teamId && (
                  <Button size="xs" variant="outline" onClick={() => onReplace(conflict)}>{t('mentorAllocationPreviewModal.replace2')}</Button>
                )}
              </li>
            ))}
          </ul>
        </section>
      )}

      {skipped.length > 0 && (
        <section aria-label={t('mentorAllocationPreviewModal.previousMentorsNotCarriedOver')} className="space-y-2">
          <h3 className="text-sm font-semibold text-slate-800">{t('mentorAllocationPreviewModal.previousMentorsNotCarriedOver2', { length: skipped.length })}</h3>
          <ul className="space-y-1.5">
            {skipped.map((item, index) => (
              <li key={`${item.sourceTeamCode}-${item.mentorType}-${index}`} className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-500 opacity-70">
                <span className="font-semibold">{item.mentorName || t('mentorAllocationPreviewModal.unknownMentor')}</span> · {slotLabel[item.mentorType]} · {item.teamName || t('mentorAllocationPreviewModal.team')}
                <span className="ml-2 rounded-full bg-slate-100 px-2 py-0.5 text-overline font-semibold text-slate-600">{skipReasonLabel[item.reason]}</span>
                <span className="mt-0.5 block text-caption">{item.message}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {unfilled.length > 0 && (
        <section aria-label={t('mentorAllocationPreviewModal.slotsWithoutAMentor')} className="space-y-2">
          <h3 className="text-sm font-semibold text-slate-800">{t('mentorAllocationPreviewModal.slotsStillWithoutAMentor', { length: unfilled.length })}</h3>
          <ul className="grid gap-1.5 sm:grid-cols-2">
            {unfilled.slice(0, 60).map(item => (
              <li key={`${item.teamId}-${item.mentorType}`} className="rounded-lg bg-red-50 px-3 py-1.5 text-xs text-red-700">
                <strong>{item.teamName}</strong> · {item.classCode} · {slotLabel[item.mentorType]}
              </li>
            ))}
          </ul>
          {unfilled.length > 60 && <p className="text-xs text-slate-500">{t('mentorAllocationPreviewModal.andMoreUseTheFilterOn', { v1: unfilled.length - 60 })}</p>}
        </section>
      )}
    </div>
  );
}

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={`-mb-px border-b-2 px-3 py-2 text-sm font-semibold transition-colors ${active ? 'border-primary text-primary-700' : 'border-transparent text-slate-500 hover:text-slate-800'}`}
    >
      {children}
    </button>
  );
}

function Metric({ label, value, tone }: { label: string; value: number; tone: BadgeTone }) {
  return (
    <div className={`rounded-lg border px-3 py-2 ${badgeTones[tone]}`}>
      <span className="block text-overline uppercase opacity-70">{label}</span>
      <strong className="text-base">{value}</strong>
    </div>
  );
}
