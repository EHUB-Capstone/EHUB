// @ts-nocheck
// Full-screen checkpoint detail view
import { useEffect, useState, useCallback, useMemo } from 'react';
import { createPortal } from 'react-dom';
import { AnimatePresence, motion } from 'framer-motion';
import {
  X, CheckCircle2, Award, FileText, Loader2,
  MessageSquare, ArrowLeft, Users, BarChart2, Layers, TrendingUp, Save,
  ClipboardList, Eye, ClipboardCheck, PanelRightOpen, PanelRightClose,
  ChevronDown,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { checkpointApi } from '../../../api/checkpointApi';
import { useAuth } from '../../../hooks/useAuth';
import Button from '../../ui/Button';
import ConfirmDialog from '../../ui/ConfirmDialog';
import CheckpointSubmissionCard from './CheckpointSubmissionCard';
import CheckpointFilePreviewModal from './CheckpointFilePreviewModal';
import FeedbackThread from './FeedbackThread';
import EvaluationPanel from '../EvaluationPanel';
import { subscribeProjectDirectionRealtime } from '../../../api/projectDirectionRealtime';

const ICONS = { Users, BarChart2, Layers, TrendingUp };

function SectionTitle({ icon: Icon, children, count, subtitle }) {
  return (
    <div className="space-y-0.5">
      <h3 className="text-sm font-bold text-slate-800 flex items-center gap-2">
        <span className="w-8 h-8 rounded-lg bg-primary-50 flex items-center justify-center border border-primary-100">
          <Icon className="w-4 h-4 text-primary" />
        </span>
        {children}
        {count != null && (
          <span className="ml-auto text-xs font-bold text-primary-700 bg-primary-50 px-2.5 py-0.5 rounded-full border border-primary-100">
            {count}
          </span>
        )}
      </h3>
      {subtitle && <p className="text-xs text-slate-500 pl-10">{subtitle}</p>}
    </div>
  );
}

export default function CheckpointPanel({
  checkpoint,
  checkpointCount,
  teamId,
  workspaceClassName,
  workspaceTeamName,
  isEditable,
  isReadOnly = false,
  proposalId,
  pitchDeckId,
  onClose,
  onRequirementsSaved,
}) {
  const { user } = useAuth();
  const [files, setFiles] = useState([]);
  const [links, setLinks] = useState([]);
  const [feedbacks, setFeedbacks] = useState([]);
  const [requirementContents, setRequirementContents] = useState({});
  const [savingRequirements, setSavingRequirements] = useState(false);
  const [requirementsOpen, setRequirementsOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  const [submissionMeta, setSubmissionMeta] = useState({ status: null, submittedAt: null });
  const [downloadingId, setDownloadingId] = useState(null);
  const [previewFile, setPreviewFile] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleting, setDeleting] = useState(false);
  const [editingLink, setEditingLink] = useState(null);
  const [deleteLinkTarget, setDeleteLinkTarget] = useState(null);
  const [deletingLink, setDeletingLink] = useState(false);
  const [hasUnsentFiles, setHasUnsentFiles] = useState(false);
  const [leaveConfirmOpen, setLeaveConfirmOpen] = useState(false);
  const isStudent = user?.role?.toUpperCase() === 'STUDENT';
  const isLecturer = user?.role?.toUpperCase() === 'LECTURER';
  const [showEvaluation, setShowEvaluation] = useState(false);
  const canEditRequirements = isStudent && isEditable && checkpoint?.canUpload;
  const Icon = ICONS[checkpoint?.icon] || FileText;

  const buildContentsMap = useCallback((sub) => {
    const map = {};
    checkpoint.requirements.forEach((label, index) => {
      const saved = sub?.requirementContents?.find(
        (r) => Number(r.index) === index
      );
      map[index] = saved?.content || '';
    });
    return map;
  }, [checkpoint.requirements]);

  const fetchData = useCallback(async () => {
    if (!teamId || !checkpoint) return;
    setLoading(true);
    try {
      const res = await checkpointApi.getCheckpointData(String(teamId));
      if (res.success) {
        const sub = res.data.submissions.find(
          (s) => Number(s.checkpointNumber) === Number(checkpoint.number)
        );
        setFiles(sub?.files || []);
        setSubmissionMeta({ status: sub?.status ?? null, submittedAt: sub?.submittedAt ?? null });
        setLinks(sub?.links || []);
        setRequirementContents(buildContentsMap(sub));
        setFeedbacks(
          res.data.feedbacks.filter(
            (f) => Number(f.checkpointNumber) === Number(checkpoint.number)
          )
        );
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  }, [teamId, checkpoint, buildContentsMap]);

  const addFeedback = useCallback((feedback) => {
    if (!feedback?._id) return;
    setFeedbacks((current) => current.some((item) => item._id === feedback._id)
      ? current
      : [...current, feedback]);
  }, []);

  const removeFeedback = useCallback((feedbackId) => {
    setFeedbacks((current) => current.filter((item) => item._id !== feedbackId));
  }, []);

  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { fetchData(); }, [fetchData]);

  useEffect(() => subscribeProjectDirectionRealtime((event) => {
    if (event.eventType === 'CheckpointFeedbackPosted'
      && String(event.teamId) === String(teamId)
      && Number(event.checkpointNumber) === Number(checkpoint.number)) {
      addFeedback(event.feedback);
    }
    if (event.eventType === 'CheckpointFeedbackDeleted'
      && String(event.teamId) === String(teamId)
      && Number(event.checkpointNumber) === Number(checkpoint.number)) {
      removeFeedback(event.feedbackId);
    }
    if (event.eventType === 'CheckpointRequirementsUpdated'
      && String(event.teamId) === String(teamId)
      && Number(event.checkpointNumber) === Number(checkpoint.number)) {
      void fetchData();
      onRequirementsSaved?.();
    }
  }, (reconnected) => {
    if (reconnected) void fetchData();
  }), [addFeedback, checkpoint.number, fetchData, onRequirementsSaved, removeFeedback, teamId]);

  // Leaving with selected or uploading files would discard them, so ask in the system dialog first.
  const requestClose = useCallback(() => {
    if (hasUnsentFiles) setLeaveConfirmOpen(true);
    else onClose();
  }, [hasUnsentFiles, onClose]);

  useEffect(() => {
    document.body.style.overflow = 'hidden';
    const onKey = (e) => e.key === 'Escape' && !previewFile && !leaveConfirmOpen && requestClose();
    window.addEventListener('keydown', onKey);
    return () => {
      document.body.style.overflow = '';
      window.removeEventListener('keydown', onKey);
    };
  }, [requestClose, previewFile, leaveConfirmOpen]);

  const reqFilledCount = useMemo(
    () =>
      checkpoint.requirements.filter((_, i) =>
        String(requirementContents[i] || '').trim()
      ).length,
    [checkpoint.requirements, requirementContents]
  );

  const handleDownload = async (file) => {
    setDownloadingId(file._id);
    try {
      await checkpointApi.downloadFile(
        teamId,
        checkpoint.number,
        file._id,
        file.originalName,
        { canDirectDownload: file.canDirectDownload }
      );
    } catch (e) {
      toast.error(e?.message || 'Failed to download file.');
    } finally {
      setDownloadingId(null);
    }
  };

  const handleDelete = async (fileId) => {
    setDeleting(true);
    try {
      const res = await checkpointApi.deleteFile(teamId, checkpoint.number, fileId);
      if (res.success) {
        setFiles((current) => current.filter((file) => file._id !== fileId));
        setDeleteTarget(null);
        toast.success('File deleted.');
        onRequirementsSaved?.();
      }
    } catch (e) {
      toast.error(e?.response?.data?.error || 'Failed to delete file.');
    } finally {
      setDeleting(false);
    }
  };

  const handleRequirementChange = (index, value) => {
    setRequirementContents((prev) => ({ ...prev, [index]: value }));
  };

  const handleDeleteLink = async (linkId) => {
    setDeletingLink(true);
    try {
      const res = await checkpointApi.deleteLink(teamId, checkpoint.number, linkId);
      if (res.success) {
        setLinks((current) => current.filter((link) => link._id !== linkId));
        setDeleteLinkTarget(null);
        if (editingLink?._id === linkId) setEditingLink(null);
        toast.success('Submitted link deleted.');
        onRequirementsSaved?.();
      }
    } catch (error) {
      toast.error(error?.response?.data?.message || 'Unable to delete the submitted link.');
    } finally {
      setDeletingLink(false);
    }
  };

  const handleSaveAllRequirements = async () => {
    setSavingRequirements(true);
    try {
      const contents = checkpoint.requirements.map((_, index) => ({
        index,
        content: requirementContents[index] || '',
      }));
      const res = await checkpointApi.updateRequirements(
        teamId,
        checkpoint.number,
        contents
      );
      if (res.success) {
        setRequirementContents(buildContentsMap(res.data));
        toast.success('Requirements saved successfully.');
        onRequirementsSaved?.();
      }
    } catch (e) {
      toast.error(e?.response?.data?.error || 'Failed to save requirements.');
    } finally {
      setSavingRequirements(false);
    }
  };

  if (!checkpoint) return null;

  const content = (
    <div className="fixed inset-0 z-[100] flex flex-col">
      <div
        className="absolute inset-0 bg-slate-900/50 backdrop-blur-sm animate-checkpoint-overlay"
        onClick={requestClose}
        aria-hidden
      />

      <div className="relative flex flex-col h-full w-full bg-slate-50 animate-checkpoint-panel shadow-2xl">
        {/* Header */}
        <header className="shrink-0 relative overflow-hidden border-b border-orange-100/80 text-slate-900 bg-white">
          <div className="absolute -left-16 -top-24 h-64 w-64 rounded-full bg-orange-200/30 blur-3xl pointer-events-none" />
          <div className="absolute -top-20 right-0 w-72 h-72 rounded-full bg-amber-100/50 blur-3xl pointer-events-none" />

          <div className="relative px-5 sm:px-8 lg:px-10 py-5 lg:py-6 max-w-7xl mx-auto w-full">
            <div className="flex items-start gap-3 sm:gap-4">
              <button
                type="button"
                onClick={requestClose}
                className="mt-0.5 flex items-center gap-2 px-3 py-2 rounded-xl bg-white/80 hover:bg-orange-50 border border-orange-200/80 text-slate-700 text-sm font-semibold shadow-sm transition-all shrink-0"
              >
                <ArrowLeft className="w-4 h-4" />
                <span className="hidden sm:inline">Back</span>
              </button>

              <div className="flex items-start gap-3 sm:gap-4 flex-1 min-w-0">
                <div className="w-12 h-12 sm:w-14 sm:h-14 rounded-2xl text-white border border-orange-400/50 shadow-md shadow-orange-200/60 flex items-center justify-center shrink-0 bg-primary">
                  <Icon className="w-6 h-6 sm:w-7 sm:h-7" />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-overline sm:text-xs font-bold uppercase tracking-widest text-primary-700">
                    Checkpoint {checkpoint.number} of {checkpointCount}
                  </p>
                  <h1 className="text-lg sm:text-2xl font-bold mt-0.5 leading-tight">
                    {checkpoint.title}
                  </h1>
                  {(workspaceClassName || workspaceTeamName) && (
                    <div className="mt-2 flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1 text-xs font-semibold text-slate-900">
                      {workspaceClassName && (
                        <span className="inline-flex min-w-0 items-center gap-1.5">
                          <span>Class</span>
                          <span className="max-w-48 truncate" title={workspaceClassName}>{workspaceClassName}</span>
                        </span>
                      )}
                      {workspaceClassName && workspaceTeamName && (
                        <span className="text-slate-900" aria-hidden="true">•</span>
                      )}
                      {workspaceTeamName && (
                        <span className="inline-flex min-w-0 items-center gap-1.5">
                          <span>Team</span>
                          <span className="max-w-64 truncate" title={workspaceTeamName}>{workspaceTeamName}</span>
                        </span>
                      )}
                    </div>
                  )}
                  <p className="text-xs sm:text-sm text-slate-600 mt-1.5 leading-relaxed line-clamp-2 hidden sm:block">
                    {checkpoint.shortDescription}
                  </p>
                </div>
              </div>

              <button
                type="button"
                onClick={requestClose}
                className="w-9 h-9 sm:w-10 sm:h-10 rounded-xl bg-white/80 hover:bg-orange-50 border border-orange-200/80 text-slate-600 hover:text-primary-700 shadow-sm flex items-center justify-center transition-all shrink-0"
                aria-label="Close"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="flex flex-wrap gap-2 mt-4">
              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white/80 border border-orange-200/70 text-slate-700 text-xs font-semibold shadow-sm">
                <FileText className="w-3.5 h-3.5" />
                {loading ? '…' : `${files.length} file${files.length !== 1 ? 's' : ''}`}
              </span>
              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white/80 border border-orange-200/70 text-slate-700 text-xs font-semibold shadow-sm">
                <MessageSquare className="w-3.5 h-3.5" />
                {loading ? '…' : `${feedbacks.length} feedback`}
              </span>
              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white/80 border border-orange-200/70 text-slate-700 text-xs font-semibold shadow-sm">
                <ClipboardList className="w-3.5 h-3.5" />
                {loading
                  ? '…'
                  : `${reqFilledCount}/${checkpoint.requirements.length} requirements`}
              </span>
              <button
                type="button"
                onClick={() => setShowEvaluation((current) => !current)}
                aria-expanded={showEvaluation}
                aria-controls="checkpoint-evaluation-panel"
                className="inline-flex items-center gap-1.5 rounded-lg border border-orange-200 bg-orange-100/70 px-3 py-1.5 text-xs font-bold text-primary-700 transition hover:bg-orange-100"
              >
                {showEvaluation ? <PanelRightClose className="h-3.5 w-3.5" /> : <PanelRightOpen className="h-3.5 w-3.5" />}
                {showEvaluation ? 'Hide evaluation' : isLecturer ? 'Open grading' : 'Show evaluation'}
              </button>
            </div>
          </div>
        </header>

        {/* Body */}
        <div className="relative flex-1 flex flex-col lg:flex-row min-h-0 overflow-hidden">
          {/* Requirements sidebar */}
          <aside className={`lg:w-[380px] xl:w-[420px] shrink-0 flex flex-col bg-white border-b lg:border-b-0 lg:border-r border-slate-200/80 min-h-0 ${showEvaluation ? 'lg:hidden' : ''}`}>
            <button
              type="button"
              id="checkpoint-requirements-toggle"
              aria-expanded={requirementsOpen}
              aria-controls="checkpoint-requirements-content"
              onClick={() => setRequirementsOpen((open) => !open)}
              className="w-full flex items-center gap-3 px-5 py-4 border-b border-orange-100/70 bg-orange-50/40 text-left focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/40"
            >
              <span className="w-8 h-8 shrink-0 rounded-lg bg-primary-50 flex items-center justify-center border border-primary-100">
                {canEditRequirements ? <ClipboardList className="w-4 h-4 text-primary" /> : <Eye className="w-4 h-4 text-primary" />}
              </span>
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-bold text-slate-800">
                  Requirements <span className="font-medium text-slate-400">(optional)</span>
                </span>
                <span className="block text-xs text-slate-500">
                  {canEditRequirements
                    ? 'Complete all fields, then save once below.'
                    : isStudent && isEditable
                      ? checkpoint.availabilityReason || 'This checkpoint is not open for submission.'
                      : 'Submitted answers from the team (read-only).'}
                </span>
              </span>
              {checkpoint.requirements.length > 0 && (
                <span className="shrink-0 text-xs font-bold text-primary-700 bg-primary-50 px-2.5 py-0.5 rounded-full border border-primary-100">
                  {reqFilledCount}/{checkpoint.requirements.length}
                </span>
              )}
              <ChevronDown className={`w-4 h-4 shrink-0 text-slate-500 transition-transform ${requirementsOpen ? 'rotate-180' : ''}`} />
            </button>

            <div className="flex-1 overflow-y-auto scrollbar-thin px-5 py-4 space-y-3">
              {requirementsOpen && checkpoint.requirements.map((req, i) => {
                const filled = Boolean(String(requirementContents[i] || '').trim());
                return (
                  <div
                    key={i}
                    className={`rounded-xl border bg-white transition-colors ${filled
                        ? 'border-primary-200 shadow-sm'
                        : 'border-slate-200/80'
                      }`}
                  >
                    <div
                      className={`flex items-center gap-2 px-3 py-2 border-b text-xs font-bold uppercase tracking-wide ${filled
                          ? 'bg-primary-50/80 border-primary-100 text-primary-800'
                          : 'bg-slate-50 border-slate-100 text-slate-500'
                        }`}
                    >
                      <span
                        className={`w-5 h-5 rounded-md flex items-center justify-center text-overline ${filled
                            ? 'bg-primary text-white'
                            : 'bg-slate-200 text-slate-600'
                          }`}
                      >
                        {i + 1}
                      </span>
                      <span className="truncate">{req}</span>
                      {filled && !canEditRequirements && (
                        <CheckCircle2 className="w-3.5 h-3.5 text-primary ml-auto shrink-0" />
                      )}
                    </div>

                    <div className="p-3">
                      {canEditRequirements ? (
                        <textarea
                          value={requirementContents[i] ?? ''}
                          onChange={(e) => handleRequirementChange(i, e.target.value)}
                          placeholder={`Describe your ${req.toLowerCase()}…`}
                          rows={3}
                          className="w-full text-sm text-slate-700 rounded-lg border border-slate-200 bg-slate-50/50 px-3 py-2.5 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary focus:bg-white resize-y min-h-[80px] transition-colors"
                        />
                      ) : loading ? (
                        <div className="h-16 flex items-center justify-center">
                          <Loader2 className="w-5 h-5 text-primary animate-spin" />
                        </div>
                      ) : filled ? (
                        <p className="text-sm text-slate-700 whitespace-pre-wrap leading-relaxed">
                          {requirementContents[i]}
                        </p>
                      ) : (
                        <p className="text-sm text-slate-400 italic">No content submitted yet.</p>
                      )}
                    </div>
                  </div>
                );
              })}

              {requirementsOpen && canEditRequirements && checkpoint.requirements.length > 0 && (
                <div id="checkpoint-requirements-actions" className="pt-1">
                  <Button
                    type="button"
                    variant="primary"
                    size="lg"
                    className="w-full"
                    isLoading={savingRequirements}
                    disabled={loading}
                    icon={Save}
                    onClick={handleSaveAllRequirements}
                  >
                    Save all requirements
                  </Button>
                  <p className="text-overline text-slate-400 text-center mt-2">
                    Saves all requirement fields for this checkpoint
                  </p>
                </div>
              )}

              {checkpoint.rubrics?.length > 0 && (
                <section className="space-y-3">
                  <SectionTitle icon={Award}>Evaluation rubric</SectionTitle>
                  <ul className="space-y-2">
                    {checkpoint.rubrics.map((r, i) => (
                      <li
                        key={i}
                        className="p-3 rounded-xl bg-amber-50/80 border border-amber-100 text-sm text-slate-700"
                      >
                        <div className="flex items-start gap-3">
                          <Award className="w-4 h-4 text-amber-500 shrink-0 mt-0.5" />
                          <div className="min-w-0">
                            <div className="font-semibold text-slate-900 flex items-center gap-2 flex-wrap">
                              <span>{r.label}</span>
                              <span className="text-overline font-bold px-2 py-0.5 rounded-full bg-white text-amber-700 border border-amber-200">{r.weight}%</span>
                            </div>
                            {r.description && <p className="text-xs text-slate-600 mt-1">{r.description}</p>}
                            {Array.isArray(r.levels) && r.levels.length > 0 && (
                              <div className="mt-2 flex flex-wrap gap-1.5 text-overline text-slate-500">
                                {r.levels.map((level) => (
                                  <span key={level.key} className="px-2 py-1 rounded-full bg-white border border-slate-200">
                                    {level.label} {level.range}
                                  </span>
                                ))}
                              </div>
                            )}
                          </div>
                        </div>
                      </li>
                    ))}
                  </ul>
                </section>
              )}
            </div>

          </aside>

          {/* Main column */}
          <main className="flex-1 overflow-y-auto scrollbar-thin bg-orange-50/20">
            <div className="p-5 lg:p-8 max-w-4xl mx-auto w-full space-y-6">
              <CheckpointSubmissionCard
                checkpoint={checkpoint}
                status={submissionMeta.status}
                submittedAt={submissionMeta.submittedAt}
                files={files}
                links={links}
                loading={loading}
                isStudent={isStudent}
                isEditable={isEditable}
                currentUserId={user?._id}
                teamId={String(teamId)}
                downloadingId={downloadingId}
                editingLink={editingLink}
                onRefresh={fetchData}
                onPreview={setPreviewFile}
                onDownload={handleDownload}
                onRequestDeleteFile={setDeleteTarget}
                onRequestDeleteLink={setDeleteLinkTarget}
                onEditLink={setEditingLink}
                onUnsentFilesChange={setHasUnsentFiles}
              />

              <section className="rounded-2xl border border-slate-200/60 bg-white p-5 shadow-sm space-y-4 pb-2">
                <SectionTitle
                  icon={MessageSquare}
                  count={feedbacks.length}
                  subtitle="Comments from mentors and lecturers"
                >
                  Feedback
                </SectionTitle>
                <div className="rounded-xl border border-slate-100 bg-slate-50/30 p-4">
                  {loading ? (
                    <div className="flex justify-center py-8">
                      <Loader2 className="w-6 h-6 text-primary animate-spin" />
                    </div>
                  ) : (
                    <FeedbackThread
                      feedbacks={feedbacks}
                      teamId={teamId}
                      checkpointNumber={checkpoint.number}
                      onPosted={addFeedback}
                      onDeleted={removeFeedback}
                      fullHeight
                    />
                  )}
                </div>
              </section>
            </div>
          </main>

          <AnimatePresence>
          {showEvaluation && (
            <motion.button
              type="button"
              className="absolute inset-0 z-10 bg-slate-950/30 backdrop-blur-[1px] lg:hidden"
              aria-label="Close evaluation panel"
              onClick={() => setShowEvaluation(false)}
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.2 }}
            />
          )}

          {showEvaluation && <motion.aside
            id="checkpoint-evaluation-panel"
            className="absolute inset-y-0 right-0 z-20 flex w-full max-w-[580px] flex-col border-l border-slate-200 bg-slate-100 shadow-2xl lg:static lg:z-auto lg:w-[420px] lg:max-w-none lg:shrink-0 lg:shadow-none xl:w-[500px] 2xl:w-[560px]"
            initial={{ opacity: 0, x: 48 }}
            animate={{ opacity: 1, x: 0 }}
            exit={{ opacity: 0, x: 48 }}
            transition={{ duration: 0.28, ease: [0.22, 1, 0.36, 1] }}
          >
            <div className="flex shrink-0 items-center justify-between gap-3 border-b border-slate-200 bg-white px-4 py-3">
              <div className="flex min-w-0 items-center gap-2">
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-primary-100 bg-primary-50 text-primary-700">
                  <ClipboardCheck className="h-4 w-4" />
                </span>
                <div className="min-w-0">
                  <p className="truncate text-sm font-bold text-slate-900">Evaluation & grading</p>
                  <p className="truncate text-xs text-slate-500">Checkpoint {checkpoint.number} · {checkpoint.title}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setShowEvaluation(false)}
                className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-slate-500 transition hover:bg-slate-100 hover:text-slate-800"
                aria-label="Hide evaluation panel"
              >
                <PanelRightClose className="h-4 w-4" />
              </button>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto scrollbar-thin">
              <EvaluationPanel
                teamId={teamId}
                proposalId={proposalId}
                pitchDeckId={pitchDeckId}
                isReadOnly={isReadOnly}
                checkpointNumber={checkpoint.number}
                embedded
              />
            </div>
          </motion.aside>}
          </AnimatePresence>
        </div>
      </div>

      <ConfirmDialog
        isOpen={leaveConfirmOpen}
        onClose={() => setLeaveConfirmOpen(false)}
        onConfirm={() => { setLeaveConfirmOpen(false); onClose(); }}
        title="Leave without submitting?"
        description="You have files that haven't been submitted. If you leave now, the selected files will be discarded and any upload in progress will be cancelled."
        confirmText="Discard and leave"
        cancelText="Keep editing"
      />
      <ConfirmDialog
        isOpen={Boolean(deleteTarget)}
        onClose={() => { if (!deleting) setDeleteTarget(null); }}
        onConfirm={() => { if (deleteTarget) return handleDelete(deleteTarget._id); }}
        title="Delete submitted file?"
        description={`“${deleteTarget?.originalName || 'This file'}” will be permanently removed from this checkpoint.`}
        confirmText="Delete file"
        isSubmitting={deleting}
      />
      <ConfirmDialog
        isOpen={Boolean(deleteLinkTarget)}
        onClose={() => { if (!deletingLink) setDeleteLinkTarget(null); }}
        onConfirm={() => { if (deleteLinkTarget) return handleDeleteLink(deleteLinkTarget._id); }}
        title="Delete submitted link?"
        description={`“${deleteLinkTarget?.name || 'This link'}” will be removed from this checkpoint.`}
        confirmText="Delete link"
        isSubmitting={deletingLink}
      />
      <CheckpointFilePreviewModal
        teamId={String(teamId)}
        checkpointNumber={checkpoint.number}
        file={previewFile}
        onClose={() => setPreviewFile(null)}
      />
    </div>
  );

  return createPortal(content, document.body);
}
