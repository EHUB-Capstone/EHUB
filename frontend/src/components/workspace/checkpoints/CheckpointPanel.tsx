// @ts-nocheck
// Full-screen checkpoint detail view
import { useEffect, useState, useCallback, useMemo } from 'react';
import { createPortal } from 'react-dom';
import {
  X, CheckCircle2, Award, FileText, Download, Trash2, Loader2,
  MessageSquare, ArrowLeft, Users, BarChart2, Layers, TrendingUp, Upload, Save,
  ClipboardList, Eye, ClipboardCheck, PanelRightOpen, PanelRightClose,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { checkpointApi } from '../../../api/checkpointApi';
import { useAuth } from '../../../hooks/useAuth';
import Button from '../../ui/Button';
import FileUploadZone from './FileUploadZone';
import FeedbackThread from './FeedbackThread';
import EvaluationPanel from '../EvaluationPanel';

const ICONS = { Users, BarChart2, Layers, TrendingUp };

const formatBytes = (bytes) => {
  if (!bytes) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / k ** i).toFixed(1)} ${sizes[i]}`;
};

const FILE_TYPE_STYLES = {
  pdf: { bg: 'bg-red-50', text: 'text-red-600', label: 'PDF' },
  docx: { bg: 'bg-blue-50', text: 'text-blue-600', label: 'DOCX' },
  pptx: { bg: 'bg-orange-50', text: 'text-orange-600', label: 'PPTX' },
};

function SectionTitle({ icon: Icon, children, count, subtitle }) {
  return (
    <div className="space-y-0.5">
      <h3 className="text-sm font-bold text-slate-800 flex items-center gap-2">
        <span className="w-8 h-8 rounded-lg bg-secondary-50 flex items-center justify-center border border-secondary-100">
          <Icon className="w-4 h-4 text-secondary" />
        </span>
        {children}
        {count != null && (
          <span className="ml-auto text-xs font-bold text-primary bg-primary-50 px-2.5 py-0.5 rounded-full border border-primary-100">
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
  teamId,
  isEditable,
  isReadOnly = false,
  proposalId,
  pitchDeckId,
  onClose,
  onRequirementsSaved,
}) {
  const { user } = useAuth();
  const [files, setFiles] = useState([]);
  const [feedbacks, setFeedbacks] = useState([]);
  const [requirementContents, setRequirementContents] = useState({});
  const [savingRequirements, setSavingRequirements] = useState(false);
  const [loading, setLoading] = useState(true);
  const [downloadingId, setDownloadingId] = useState(null);
  const isStudent = user?.role?.toUpperCase() === 'STUDENT';
  const isLecturer = user?.role?.toUpperCase() === 'LECTURER';
  const [showEvaluation, setShowEvaluation] = useState(isLecturer);
  const canEditRequirements = isStudent && isEditable;
  const Icon = ICONS[checkpoint?.icon] || FileText;

  const buildContentsMap = (sub) => {
    const map = {};
    checkpoint.requirements.forEach((label, index) => {
      const saved = sub?.requirementContents?.find(
        (r) => Number(r.index) === index
      );
      map[index] = saved?.content || '';
    });
    return map;
  };

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
  }, [teamId, checkpoint]);

  useEffect(() => { fetchData(); }, [fetchData]);

  useEffect(() => {
    document.body.style.overflow = 'hidden';
    const onKey = (e) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => {
      document.body.style.overflow = '';
      window.removeEventListener('keydown', onKey);
    };
  }, [onClose]);

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
        file.originalName
      );
    } catch (e) {
      toast.error(e?.message || 'Failed to download file.');
    } finally {
      setDownloadingId(null);
    }
  };

  const handleDelete = async (fileId) => {
    if (!window.confirm('Delete this file?')) return;
    try {
      const res = await checkpointApi.deleteFile(teamId, checkpoint.number, fileId);
      if (res.success) {
        setFiles(res.data?.files || []);
        toast.success('File deleted.');
      }
    } catch (e) {
      toast.error(e?.response?.data?.error || 'Failed to delete file.');
    }
  };

  const handleRequirementChange = (index, value) => {
    setRequirementContents((prev) => ({ ...prev, [index]: value }));
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
        onClick={onClose}
        aria-hidden
      />

      <div className="relative flex flex-col h-full w-full bg-slate-50 animate-checkpoint-panel shadow-2xl">
        {/* Header */}
        <header className="shrink-0 relative overflow-hidden bg-gradient-to-r from-secondary via-[#0456b8] to-primary text-white">
          <div className="absolute inset-0 bg-mesh opacity-40 pointer-events-none" />
          <div className="absolute -top-20 right-0 w-72 h-72 rounded-full bg-white/10 blur-3xl pointer-events-none" />

          <div className="relative px-5 sm:px-8 lg:px-10 py-5 lg:py-6 max-w-7xl mx-auto w-full">
            <div className="flex items-start gap-3 sm:gap-4">
              <button
                type="button"
                onClick={onClose}
                className="mt-0.5 flex items-center gap-2 px-3 py-2 rounded-xl bg-white/10 hover:bg-white/20 border border-white/20 text-sm font-semibold transition-all shrink-0"
              >
                <ArrowLeft className="w-4 h-4" />
                <span className="hidden sm:inline">Back</span>
              </button>

              <div className="flex items-start gap-3 sm:gap-4 flex-1 min-w-0">
                <div className="w-12 h-12 sm:w-14 sm:h-14 rounded-2xl bg-white/15 backdrop-blur border border-white/25 flex items-center justify-center shrink-0">
                  <Icon className="w-6 h-6 sm:w-7 sm:h-7" />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-[10px] sm:text-xs font-bold uppercase tracking-widest text-white/75">
                    Checkpoint {checkpoint.number} of 4
                  </p>
                  <h1 className="text-lg sm:text-2xl font-bold mt-0.5 leading-tight">
                    {checkpoint.title}
                  </h1>
                  <p className="text-xs sm:text-sm text-white/85 mt-1.5 leading-relaxed line-clamp-2 hidden sm:block">
                    {checkpoint.shortDescription}
                  </p>
                </div>
              </div>

              <button
                type="button"
                onClick={onClose}
                className="w-9 h-9 sm:w-10 sm:h-10 rounded-xl bg-white/10 hover:bg-white/20 border border-white/20 flex items-center justify-center transition-all shrink-0"
                aria-label="Close"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="flex flex-wrap gap-2 mt-4">
              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white/10 border border-white/15 text-xs font-semibold">
                <FileText className="w-3.5 h-3.5" />
                {loading ? '…' : `${files.length} file${files.length !== 1 ? 's' : ''}`}
              </span>
              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white/10 border border-white/15 text-xs font-semibold">
                <MessageSquare className="w-3.5 h-3.5" />
                {loading ? '…' : `${feedbacks.length} feedback`}
              </span>
              <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white/10 border border-white/15 text-xs font-semibold">
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
                className="inline-flex items-center gap-1.5 rounded-lg border border-white/25 bg-white/15 px-3 py-1.5 text-xs font-bold text-white transition hover:bg-white/25"
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
            <div className="px-5 py-4 border-b border-slate-100 bg-slate-50/80">
              <SectionTitle
                icon={canEditRequirements ? ClipboardList : Eye}
                subtitle={
                  canEditRequirements
                    ? 'Complete all fields, then save once at the bottom.'
                    : 'Submitted answers from the team (read-only).'
                }
              >
                Requirements
              </SectionTitle>
            </div>

            <div className="flex-1 overflow-y-auto scrollbar-thin px-5 py-4 space-y-3">
              {checkpoint.requirements.map((req, i) => {
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
                        className={`w-5 h-5 rounded-md flex items-center justify-center text-[10px] ${filled
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
                              <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-white text-amber-700 border border-amber-200">{r.weight}%</span>
                            </div>
                            {r.description && <p className="text-xs text-slate-600 mt-1">{r.description}</p>}
                            {Array.isArray(r.levels) && r.levels.length > 0 && (
                              <div className="mt-2 flex flex-wrap gap-1.5 text-[10px] text-slate-500">
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

            {canEditRequirements && (
              <div className="shrink-0 px-5 py-4 border-t border-slate-200 bg-white">
                <Button
                  type="button"
                  variant="gradient"
                  size="lg"
                  className="w-full"
                  isLoading={savingRequirements}
                  disabled={loading}
                  icon={Save}
                  onClick={handleSaveAllRequirements}
                >
                  Save all requirements
                </Button>
                <p className="text-[10px] text-slate-400 text-center mt-2">
                  Saves all requirement fields for this checkpoint
                </p>
              </div>
            )}
          </aside>

          {/* Main column */}
          <main className="flex-1 overflow-y-auto scrollbar-thin bg-slate-50/50">
            <div className="p-5 lg:p-8 max-w-4xl mx-auto w-full space-y-6">
              {isEditable && isStudent && (
                <section className="rounded-2xl border border-slate-200/60 bg-white p-5 shadow-sm space-y-4">
                  <SectionTitle
                    icon={Upload}
                    subtitle="PDF, DOCX, or PPTX · Max 15 MB per file"
                  >
                    Upload documents
                  </SectionTitle>
                  <FileUploadZone
                    teamId={teamId}
                    checkpointNumber={checkpoint.number}
                    onUploaded={fetchData}
                    variant="large"
                  />
                </section>
              )}

              <section className="rounded-2xl border border-slate-200/60 bg-white p-5 shadow-sm space-y-4">
                <SectionTitle
                  icon={FileText}
                  count={loading ? '…' : files.length}
                  subtitle="Files attached to this checkpoint"
                >
                  Submitted files
                </SectionTitle>

                {loading ? (
                  <div className="flex flex-col items-center justify-center py-14 rounded-xl border border-dashed border-slate-200 bg-slate-50/50">
                    <Loader2 className="w-7 h-7 text-primary animate-spin" />
                    <p className="text-sm text-slate-500 mt-3">Loading submissions…</p>
                  </div>
                ) : files.length === 0 ? (
                  <div className="flex flex-col items-center justify-center py-14 rounded-xl border border-dashed border-slate-200 bg-slate-50/50 text-center px-6">
                    <div className="w-12 h-12 rounded-xl bg-secondary-50 flex items-center justify-center mb-3 border border-secondary-100">
                      <FileText className="w-6 h-6 text-secondary/60" />
                    </div>
                    <p className="text-sm font-semibold text-slate-700">No documents yet</p>
                    <p className="text-xs text-slate-400 mt-1 max-w-sm">
                      {isStudent && isEditable
                        ? 'Upload milestone files using the form above.'
                        : 'This team has not uploaded files for this checkpoint.'}
                    </p>
                  </div>
                ) : (
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    {files.map((file) => {
                      const isOwner = file.uploadedBy?._id === user?._id;
                      const isAdmin = user?.role?.toUpperCase() === 'ADMIN';
                      const canDelete = isOwner || isAdmin;
                      const style = FILE_TYPE_STYLES[file.fileType] || {
                        bg: 'bg-slate-50',
                        text: 'text-slate-600',
                        label: file.fileType?.toUpperCase() || 'FILE',
                      };

                      return (
                        <div
                          key={file._id}
                          className="flex flex-col p-4 rounded-xl border border-slate-200/80 bg-slate-50/30 hover:border-primary-200 hover:bg-white transition-all"
                        >
                          <div className="flex items-start gap-3">
                            <div
                              className={`w-11 h-11 rounded-lg ${style.bg} flex items-center justify-center shrink-0`}
                            >
                              <span className={`text-[10px] font-extrabold ${style.text}`}>
                                {style.label}
                              </span>
                            </div>
                            <div className="min-w-0 flex-1">
                              <p
                                className="text-sm font-bold text-slate-800 line-clamp-2"
                                title={file.originalName}
                              >
                                {file.originalName}
                              </p>
                              <p className="text-xs text-slate-500 mt-1">
                                {formatBytes(file.fileSize)} · {file.uploadedBy?.name || 'Unknown'}
                              </p>
                              <p className="text-[10px] text-slate-400 mt-0.5">
                                {new Date(file.uploadedAt).toLocaleString()}
                              </p>
                            </div>
                          </div>

                          <div className="flex gap-2 mt-3 pt-3 border-t border-slate-200/80">
                            <button
                              type="button"
                              onClick={() => handleDownload(file)}
                              disabled={downloadingId === file._id}
                              className="flex-1 inline-flex items-center justify-center gap-2 py-2 rounded-lg bg-secondary text-white text-xs font-bold hover:bg-secondary-dark transition-all disabled:opacity-50"
                            >
                              {downloadingId === file._id ? (
                                <Loader2 className="w-3.5 h-3.5 animate-spin" />
                              ) : (
                                <Download className="w-3.5 h-3.5" />
                              )}
                              Download
                            </button>
                            {canDelete && (
                              <button
                                type="button"
                                onClick={() => handleDelete(file._id)}
                                className="px-3 py-2 rounded-lg border border-slate-200 text-slate-500 hover:text-red-600 hover:border-red-200 hover:bg-red-50 transition-all"
                                title="Delete"
                              >
                                <Trash2 className="w-4 h-4" />
                              </button>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </section>

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
                      onPosted={fetchData}
                      fullHeight
                    />
                  )}
                </div>
              </section>
            </div>
          </main>

          {showEvaluation && (
            <button
              type="button"
              className="absolute inset-0 z-10 bg-slate-950/30 backdrop-blur-[1px] lg:hidden"
              aria-label="Close evaluation panel"
              onClick={() => setShowEvaluation(false)}
            />
          )}

          {showEvaluation && <aside
            id="checkpoint-evaluation-panel"
            className="absolute inset-y-0 right-0 z-20 flex w-full max-w-[580px] flex-col border-l border-slate-200 bg-slate-100 shadow-2xl lg:static lg:z-auto lg:w-[420px] lg:max-w-none lg:shrink-0 lg:shadow-none xl:w-[500px] 2xl:w-[560px]"
          >
            <div className="flex shrink-0 items-center justify-between gap-3 border-b border-slate-200 bg-white px-4 py-3">
              <div className="flex min-w-0 items-center gap-2">
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-primary-100 bg-primary-50 text-primary">
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
          </aside>}
        </div>
      </div>
    </div>
  );

  return createPortal(content, document.body);
}
