import { useEffect, useMemo, useRef, useState, type ChangeEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { AlertTriangle, ArrowLeft, BookOpen, Edit3, FileSpreadsheet, Globe2, Plus, Save, Trash2, Upload } from 'lucide-react';
import toast from 'react-hot-toast';
import Button from '../../components/ui/Button';
import ConfirmDialog from '../../components/ui/ConfirmDialog';
import EmptyState from '../../components/ui/EmptyState';
import ErrorState from '../../components/ui/ErrorState';
import LoadingSkeleton from '../../components/ui/LoadingSkeleton';
import Modal from '../../components/ui/Modal';
import Badge from '../../components/ui/Badge';
import { subjectApi } from '../../api/subjectApi';
import { parseApiError } from '../../utils/apiError';
import { resolveCriterionKey } from '../../utils/rubricKey';
import {
  mergeRubricImport,
  readRubricImportFile,
  RUBRIC_IMPORT_ACCEPT,
  RubricImportValidationError,
  type RubricImportPreview,
} from '../../utils/rubricImport';
import type { SubjectCheckpointDraft, SubjectOtherAssessmentDraft } from '../../types/subjects';
import { useTranslation } from 'react-i18next';

const weeks = Array.from({ length: 10 }, (_, index) => index + 1);
const emptyForm = { title: '', description: '', weekNumber: 1, priority: 'MEDIUM', estimatedHours: '', tags: '' };
type RoadmapFormErrors = Partial<Record<'title' | 'description', string>>;

const normalizeRoadmapValue = (value: string) => value.trim().toLowerCase();

export default function SubjectDetail() {
  const { t } = useTranslation('admin');
  const navigate = useNavigate();
  const { subjectCode = '' } = useParams();
  const code = subjectCode.toUpperCase();
  const [data, setData] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [tab, setTab] = useState<'roadmap' | 'rubric'>('roadmap');
  const [week, setWeek] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<any>(null);
  const [form, setForm] = useState(emptyForm);
  const [roadmapFormErrors, setRoadmapFormErrors] = useState<RoadmapFormErrors>({});
  const [saving, setSaving] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<any>(null);
  const [deleting, setDeleting] = useState(false);
  const [rubricModal, setRubricModal] = useState<any>(null);
  const [criterionModal, setCriterionModal] = useState<any>(null);
  const [rubricForm, setRubricForm] = useState<any>({ name: '', description: '', checkpointNumber: '', totalWeight: 100, status: 'DRAFT' });
  const [criterionForm, setCriterionForm] = useState<any>({ name: '', description: '', maxScore: 10, weight: 0, displayOrder: 0 });

  const load = async () => {
    setLoading(true); setNotFound(false);
    try {
      const response = await subjectApi.getCurriculum(code);
      setData(response.data);
    } catch (error: any) {
      setNotFound(error?.response?.status === 404);
      if (error?.response?.status !== 404) toast.error(parseApiError(error, t('subjectDetail.failedToLoadSubjectCurriculum')).message);
    } finally { setLoading(false); }
  };
  useEffect(() => { if (code) void load(); }, [code]);

  const items = useMemo(() => (data?.roadmapItems ?? []).filter((item: any) => item.weekNumber === week), [data, week]);
  const openCreate = () => { setEditing(null); setForm({ ...emptyForm, weekNumber: week }); setRoadmapFormErrors({}); setModalOpen(true); };
  const openEdit = (item: any) => { setEditing(item); setForm({ title: item.title, description: item.description ?? '', weekNumber: item.weekNumber, priority: item.priority, estimatedHours: item.estimatedHours ?? '', tags: (item.tags ?? []).join(', ') }); setRoadmapFormErrors({}); setModalOpen(true); };
  const save = async () => {
    const title = form.title.trim();
    const description = form.description.trim();
    const otherItems = (data?.roadmapItems ?? []).filter((item: any) => item._id !== editing?._id);
    const errors: RoadmapFormErrors = {};
    if (!title) errors.title = 'Title is required.';
    else if (otherItems.some((item: any) => normalizeRoadmapValue(item.title) === normalizeRoadmapValue(title))) {
      errors.title = 'A roadmap item with this title already exists for this subject.';
    }
    if (!description) errors.description = 'Description is required.';
    else if (otherItems.some((item: any) => normalizeRoadmapValue(item.description ?? '') === normalizeRoadmapValue(description))) {
      errors.description = 'A roadmap item with this description already exists for this subject.';
    }
    setRoadmapFormErrors(errors);
    if (Object.keys(errors).length > 0) return;
    const hours = form.estimatedHours === '' ? null : Number(form.estimatedHours);
    if (hours !== null && (Number.isNaN(hours) || hours < 0)) return void toast.error(t('subjectDetail.estimatedHoursMustBeZeroOr'));
    const payload = { title, description, taskType: 'COURSE_TEMPLATE', courseCode: code, weekNumber: Number(form.weekNumber), priority: form.priority, estimatedHours: hours, tags: form.tags.split(',').map(tag => tag.trim()).filter(Boolean) };
    setSaving(true);
    try {
      const response = editing
        ? await subjectApi.updateRoadmapItem(code, editing._id, payload)
        : await subjectApi.createRoadmapItem(code, payload);
      const savedItem = response.data;
      setData((current: any) => ({
        ...current,
        roadmapItems: editing
          ? current.roadmapItems.map((item: any) => item._id === editing._id ? savedItem : item)
          : [...current.roadmapItems, savedItem],
      }));
      toast.success(editing ? t('subjectDetail.roadmapItemUpdated') : t('subjectDetail.roadmapItemCreated')); setModalOpen(false); setWeek(payload.weekNumber);
    } catch (error) { toast.error(parseApiError(error, t('subjectDetail.failedToSaveRoadmapItem')).message); } finally { setSaving(false); }
  };
  const remove = async () => {
    if (!deleteTarget) return; setDeleting(true);
    try { if (deleteTarget.type === 'rubric') await subjectApi.deleteRubric(code, deleteTarget.rubric._id); else if (deleteTarget.type === 'criterion') await subjectApi.deleteCriterion(code, deleteTarget.rubric._id, deleteTarget.criterion._id); else await subjectApi.deleteRoadmapItem(code, deleteTarget._id); toast.success(t('subjectDetail.deletedSuccessfully')); setDeleteTarget(null); await load(); }
    catch (error) { toast.error(parseApiError(error, t('subjectDetail.failedToDeleteItem')).message); } finally { setDeleting(false); }
  };
  const saveRubric = async () => {
    if (!rubricForm.name.trim()) return void toast.error(t('subjectDetail.rubricNameIsRequired')); setSaving(true);
    try { const payload = { ...rubricForm, name: rubricForm.name.trim(), checkpointNumber: rubricForm.checkpointNumber === '' ? null : Number(rubricForm.checkpointNumber), totalWeight: Number(rubricForm.totalWeight) }; if (rubricModal?.rubric) await subjectApi.updateRubric(code, rubricModal.rubric._id, payload); else await subjectApi.createRubric(code, payload); toast.success(t('subjectDetail.rubricSaved')); setRubricModal(null); await load(); } catch (error) { toast.error(parseApiError(error, t('subjectDetail.failedToSaveRubric')).message); } finally { setSaving(false); }
  };
  const saveCriterion = async () => {
    if (!criterionForm.name.trim()) return void toast.error(t('subjectDetail.criterionNameIsRequired')); setSaving(true);
    try { const payload = { ...criterionForm, name: criterionForm.name.trim(), maxScore: Number(criterionForm.maxScore), weight: Number(criterionForm.weight), displayOrder: Number(criterionForm.displayOrder) }; if (criterionModal.criterion) await subjectApi.updateCriterion(code, criterionModal.rubric._id, criterionModal.criterion._id, payload); else await subjectApi.createCriterion(code, criterionModal.rubric._id, payload); toast.success(t('subjectDetail.criterionSaved')); setCriterionModal(null); await load(); } catch (error) { toast.error(parseApiError(error, t('subjectDetail.failedToSaveCriterion')).message); } finally { setSaving(false); }
  };

  if (loading) return <div className="space-y-6"><LoadingSkeleton variant="text" lines={2} /><LoadingSkeleton variant="card" /><LoadingSkeleton variant="table" lines={5} /></div>;
  if (notFound) return <EmptyState icon={BookOpen} title={t('subjectDetail.subjectNotFound')} description={t('subjectDetail.noSubjectExistsWithCode', { code })} action={{ label: t('subjectDetail.backToSubjectList'), onClick: () => navigate('/admin/subjects') }} />;
  if (!data) return <ErrorState title={t('subjectDetail.unableToLoadSubject')} message={t('subjectDetail.pleaseTryAgain')} onRetry={() => void load()} />;
  const subject = data.subject;

  return <div className="space-y-6">
    <button type="button" onClick={() => navigate('/admin/subjects')} className="inline-flex items-center gap-2 text-sm font-medium text-slate-500 hover:text-primary-700"><ArrowLeft className="h-4 w-4" /> {t('subjectDetail.backToSubjectList')}</button>
    <section className="rounded-2xl border border-slate-200/70 bg-white p-5 shadow-sm sm:p-6"><div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between"><div><div className="flex items-center gap-3"><h1 className="font-mono text-display text-secondary">{subject.subjectCode}</h1><Badge variant="Submitted">{t('subjectDetail.globalCourseSetup')}</Badge></div><p className="mt-2 text-lg font-medium text-slate-700">{subject.subjectName}</p></div></div><div className="mt-5 flex gap-3 rounded-xl border border-success-light bg-success-50 p-4 text-sm leading-6 text-success-dark"><Globe2 className="mt-0.5 h-5 w-5 shrink-0" /><span>{t('subjectDetail.changesHereApplyToEveryClass', { subjectCode: subject.subjectCode })}</span></div></section>
    <div className="flex gap-2 overflow-x-auto pb-1"><button type="button" onClick={() => setTab('roadmap')} className={`shrink-0 rounded-xl px-4 py-2.5 text-sm font-semibold ${tab === 'roadmap' ? 'bg-primary text-white' : 'border border-slate-200 bg-white text-slate-600'}`}>{t('subjectDetail.courseRoadmap')}</button><button type="button" onClick={() => setTab('rubric')} className={`shrink-0 rounded-xl px-4 py-2.5 text-sm font-semibold ${tab === 'rubric' ? 'bg-primary text-white' : 'border border-slate-200 bg-white text-slate-600'}`}>{t('subjectDetail.evaluationRubric')}</button></div>
    {tab === 'rubric' && <FlexibleCheckpointRubricEditor subjectCode={code} checkpoints={data.checkpoints ?? []} otherAssessments={data.otherAssessments ?? []} onSaved={load} />}
    {tab === 'roadmap' && <section className="space-y-5"><div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between"><div className="flex gap-2 overflow-x-auto pb-1">{weeks.map(value => <button key={value} type="button" onClick={() => setWeek(value)} className={`shrink-0 rounded-lg px-3 py-2 text-sm font-semibold ${week === value ? 'bg-primary text-white' : 'bg-white text-slate-500 ring-1 ring-slate-200'}`}>{t('subjectDetail.week', { value })}</button>)}</div><Button icon={Plus} onClick={openCreate}>{t('subjectDetail.addRoadmapItem')}</Button></div>{items.length === 0 ? <EmptyState icon={BookOpen} title={t('subjectDetail.noRoadmapItemsExistForThis')} description={t('subjectDetail.addTheFirstTaskToGet')} action={{ label: t('subjectDetail.addRoadmapItem'), onClick: openCreate }} /> : <div className="grid gap-4">{items.map((item: any) => <article key={item._id} className="rounded-2xl border border-slate-200/70 bg-white p-5 shadow-sm"><div className="flex gap-4"><div className="min-w-0 flex-1"><div className="flex flex-wrap items-center gap-2"><h2 className="font-bold text-slate-900">{item.title}</h2><Badge variant={item.priority === 'CRITICAL' ? 'Overdue' : item.priority === 'HIGH' ? 'Improving' : 'Draft'}>{item.priority}</Badge>{item.estimatedHours > 0 && <span className="text-xs text-slate-500">{t('subjectDetail.hours', { estimatedHours: item.estimatedHours })}</span>}</div>{item.description && <p className="mt-2 text-sm text-slate-600">{item.description}</p>}<div className="mt-3 flex flex-wrap gap-1.5">{item.tags?.map((tag: string) => <span key={tag} className="rounded-full bg-slate-100 px-2 py-1 text-xs font-medium text-slate-600">{tag}</span>)}</div></div><button title={t('subjectDetail.editRoadmapItem')} onClick={() => openEdit(item)} className="h-9 w-9 rounded-lg text-slate-400 hover:bg-primary-50 hover:text-primary-700"><Edit3 className="mx-auto h-4 w-4" /></button><button title={t('subjectDetail.deleteRoadmapItem')} onClick={() => setDeleteTarget(item)} className="h-9 w-9 rounded-lg text-slate-400 hover:bg-danger-50 hover:text-danger"><Trash2 className="mx-auto h-4 w-4" /></button></div></article>)}</div>}</section>}
    <Modal isOpen={modalOpen} onClose={() => setModalOpen(false)} title={editing ? t('subjectDetail.editRoadmapItem') : t('subjectDetail.addRoadmapItem')} submitText={saving ? t('subjectDetail.saving') : t('subjectDetail.saveRoadmapItem')} isSubmitting={saving} onSubmit={save}>
      <div className="space-y-4">
        <label className="block text-sm font-medium text-slate-700">
          {t('subjectDetail.title')}
          <input
            value={form.title}
            onChange={event => { setForm({ ...form, title: event.target.value }); setRoadmapFormErrors(current => ({ ...current, title: undefined })); }}
            aria-invalid={Boolean(roadmapFormErrors.title)}
            aria-describedby={roadmapFormErrors.title ? 'roadmap-title-error' : undefined}
            className={`mt-1.5 w-full rounded-xl border px-3 py-2.5 outline-none focus:border-primary ${roadmapFormErrors.title ? 'border-danger' : 'border-slate-200'}`}
          />
          {roadmapFormErrors.title && <span id="roadmap-title-error" className="mt-1 block text-xs font-normal text-danger">{roadmapFormErrors.title}</span>}
        </label>
        <label className="block text-sm font-medium text-slate-700">
          {t('subjectDetail.description')}
          <textarea
            value={form.description}
            onChange={event => { setForm({ ...form, description: event.target.value }); setRoadmapFormErrors(current => ({ ...current, description: undefined })); }}
            aria-invalid={Boolean(roadmapFormErrors.description)}
            aria-describedby={roadmapFormErrors.description ? 'roadmap-description-error' : undefined}
            className={`mt-1.5 min-h-24 w-full rounded-xl border px-3 py-2.5 outline-none focus:border-primary ${roadmapFormErrors.description ? 'border-danger' : 'border-slate-200'}`}
          />
          {roadmapFormErrors.description && <span id="roadmap-description-error" className="mt-1 block text-xs font-normal text-danger">{roadmapFormErrors.description}</span>}
        </label>
        <div className="grid grid-cols-2 gap-3"><label className="text-sm font-medium text-slate-700">{t('subjectDetail.week2')}<select value={form.weekNumber} onChange={event => { setForm({ ...form, weekNumber: Number(event.target.value) }); setRoadmapFormErrors({}); }} className="mt-1.5 w-full rounded-xl border border-slate-200 p-2.5">{weeks.map(value => <option key={value} value={value}>{t('subjectDetail.week', { value })}</option>)}</select></label><label className="text-sm font-medium text-slate-700">{t('subjectDetail.priority')}<select value={form.priority} onChange={event => setForm({ ...form, priority: event.target.value })} className="mt-1.5 w-full rounded-xl border border-slate-200 p-2.5">{['LOW','MEDIUM','HIGH','CRITICAL'].map(value => <option key={value}>{value}</option>)}</select></label></div>
        <label className="block text-sm font-medium text-slate-700">{t('subjectDetail.estimatedHours')}<input type="number" min="0" step="0.5" value={form.estimatedHours} onChange={event => setForm({ ...form, estimatedHours: event.target.value })} className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 outline-none focus:border-primary" /></label>
        <label className="block text-sm font-medium text-slate-700">{t('subjectDetail.tags')}<input value={form.tags} onChange={event => setForm({ ...form, tags: event.target.value })} placeholder={t('subjectDetail.researchValidation')} className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 outline-none focus:border-primary" /></label>
      </div>
    </Modal>
    <Modal isOpen={Boolean(rubricModal)} onClose={() => setRubricModal(null)} title={rubricModal?.rubric ? t('subjectDetail.editRubric') : t('subjectDetail.addRubric')} submitText={t('subjectDetail.saveRubric')} isSubmitting={saving} onSubmit={saveRubric}><div className="space-y-3"><input value={rubricForm.name} onChange={e => setRubricForm({ ...rubricForm, name: e.target.value })} placeholder={t('subjectDetail.rubricName')} className="w-full rounded-xl border p-2.5" /><textarea value={rubricForm.description} onChange={e => setRubricForm({ ...rubricForm, description: e.target.value })} placeholder={t('startupIndustryManagement.description')} className="min-h-20 w-full rounded-xl border p-2.5" /><div className="grid grid-cols-3 gap-2"><input type="number" min="1" value={rubricForm.checkpointNumber} onChange={e => setRubricForm({ ...rubricForm, checkpointNumber: e.target.value })} placeholder={t('classManagement.checkpoint')} className="rounded-xl border p-2.5" /><input type="number" min="0" max="100" value={rubricForm.totalWeight} onChange={e => setRubricForm({ ...rubricForm, totalWeight: e.target.value })} placeholder={t('subjectDetail.weight')} className="rounded-xl border p-2.5" /><select value={rubricForm.status} onChange={e => setRubricForm({ ...rubricForm, status: e.target.value })} className="rounded-xl border p-2.5"><option>{t('subjectDetail.draft')}</option><option>{t('subjectDetail.active')}</option><option>{t('subjectDetail.archived')}</option></select></div></div></Modal>
    <Modal isOpen={Boolean(criterionModal)} onClose={() => setCriterionModal(null)} title={criterionModal?.criterion ? t('subjectDetail.editCriterion') : t('subjectDetail.addCriterion')} submitText={t('subjectDetail.saveCriterion')} isSubmitting={saving} onSubmit={saveCriterion}><div className="space-y-3"><input value={criterionForm.name} onChange={e => setCriterionForm({ ...criterionForm, name: e.target.value })} placeholder={t('subjectDetail.criterionName')} className="w-full rounded-xl border p-2.5" /><textarea value={criterionForm.description} onChange={e => setCriterionForm({ ...criterionForm, description: e.target.value })} placeholder={t('startupIndustryManagement.description')} className="min-h-20 w-full rounded-xl border p-2.5" /><div className="grid grid-cols-3 gap-2"><input type="number" min="0.5" step="0.5" value={criterionForm.maxScore} onChange={e => setCriterionForm({ ...criterionForm, maxScore: e.target.value })} placeholder={t('subjectDetail.maxScore')} className="rounded-xl border p-2.5" /><input type="number" min="0" max="100" value={criterionForm.weight} onChange={e => setCriterionForm({ ...criterionForm, weight: e.target.value })} placeholder={t('subjectDetail.weight2')} className="rounded-xl border p-2.5" /><input type="number" min="0" value={criterionForm.displayOrder} onChange={e => setCriterionForm({ ...criterionForm, displayOrder: e.target.value })} placeholder={t('subjectDetail.order')} className="rounded-xl border p-2.5" /></div></div></Modal>
    <ConfirmDialog isOpen={Boolean(deleteTarget)} onClose={() => setDeleteTarget(null)} onConfirm={remove} isSubmitting={deleting} title={t('subjectDetail.deleteRoadmapItem')} description={t('subjectDetail.thisRemovesTheSharedRoadmapTemplate')} confirmText={t('common:actions.delete')} />
  </div>;
}

function RubricManager({ rubrics, onEditRubric, onAddCriterion, onEditCriterion, onDelete }: any) {
  const { t } = useTranslation('admin');
  return <div className="space-y-3">
    {rubrics.map((rubric: any) => <div key={`manage-${rubric._id}`} className="rounded-xl border border-slate-200 bg-slate-50 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2"><span className="font-semibold text-slate-800">{t('subjectDetail.manage', { name: rubric.name })}</span><div className="flex gap-1"><button title={t('subjectDetail.editRubric')} onClick={() => onEditRubric(rubric)} className="rounded-lg p-2 text-primary-700 hover:bg-primary-50"><Edit3 className="h-4 w-4" /></button><button title={t('subjectDetail.deleteRubric')} onClick={() => onDelete({ type: 'rubric', rubric })} className="rounded-lg p-2 text-danger hover:bg-danger-50"><Trash2 className="h-4 w-4" /></button></div></div>
      <div className="mt-3 space-y-1">{rubric.criteria.map((criterion: any) => <div key={criterion._id} className="flex items-center justify-between rounded-lg bg-white px-3 py-2 text-sm"><span>{criterion.name} ({criterion.weight}%)</span><div className="flex gap-1"><button title={t('subjectDetail.editCriterion')} onClick={() => onEditCriterion(rubric, criterion)} className="p-1 text-primary-700"><Edit3 className="h-3.5 w-3.5" /></button><button title={t('subjectDetail.deleteCriterion')} onClick={() => onDelete({ type: 'criterion', rubric, criterion })} className="p-1 text-danger"><Trash2 className="h-3.5 w-3.5" /></button></div></div>)}</div>
      <Button size="sm" variant="outline" icon={Plus} className="mt-3" onClick={() => onAddCriterion(rubric)}>{t('subjectDetail.addCriterion')}</Button>
    </div>)}
  </div>;
}

function CheckpointRubricEditor({ subjectCode, checkpoints, onSaved }: any) {
  const { t } = useTranslation('admin');
  const [draft, setDraft] = useState<any[]>(() => [...checkpoints].sort((a: any, b: any) => a.number - b.number));
  const [selected, setSelected] = useState(1); const [dirty, setDirty] = useState(false); const [saving, setSaving] = useState(false);
  useEffect(() => { setDraft([...checkpoints].sort((a: any, b: any) => a.number - b.number)); setSelected(checkpoints[0]?.number ?? 1); setDirty(false); }, [checkpoints]);
  useEffect(() => { const warn = (event: BeforeUnloadEvent) => { if (dirty) { event.preventDefault(); event.returnValue = ''; } }; window.addEventListener('beforeunload', warn); return () => window.removeEventListener('beforeunload', warn); }, [dirty]);
  const checkpoint = draft.find(item => item.number === selected); const update = (next: any) => { setDraft(current => current.map(item => item.number === selected ? next : item)); setDirty(true); };
  const addCheckpoint = () => { const number = Array.from({ length: 10 }, (_, index) => index + 1).find(value => !draft.some(item => item.number === value)); if (!number) return void toast.error(t('subjectDetail.aSubjectCanHaveUpTo')); setDraft(current => [...current, { number, title: t('subjectDetail.checkpoint', { number }), shortDescription: '', requirements: [], rubrics: [{ key: '', label: '', description: '', weight: 100, levels: [] }] }].sort((a, b) => a.number - b.number)); setSelected(number); setDirty(true); };
  const deleteCheckpoint = () => { if (draft.length === 1) return void toast.error(t('subjectDetail.aSubjectNeedsAtLeastOne')); const remaining = draft.filter(item => item.number !== selected); setDraft(remaining); setSelected(remaining[0].number); setDirty(true); };
  const save = async () => { setSaving(true); try { await subjectApi.synchronizeCheckpoints(subjectCode, { checkpoints: draft }); toast.success(t('subjectDetail.rubricSynchronizedForEveryClassIn')); setDirty(false); await onSaved(); } catch (error) { toast.error(parseApiError(error, t('subjectDetail.failedToSynchronizeRubric')).message); } finally { setSaving(false); } };
  const total = checkpoint.rubrics.reduce((sum: number, item: any) => sum + Number(item.weight || 0), 0);
  return <section className="grid gap-5 lg:grid-cols-[240px_minmax(0,1fr)]"><aside className="flex gap-2 overflow-x-auto lg:block lg:space-y-2">{draft.map(item => <button key={item.number} onClick={() => setSelected(item.number)} className={`min-w-52 rounded-xl p-3 text-left lg:w-full ${selected === item.number ? 'bg-primary text-white' : 'border border-slate-200 bg-white text-slate-600'}`}><span className="text-overline font-semibold">{t('subjectDetail.checkpoint2', { number: item.number })}</span><span className="mt-1 block text-sm font-semibold">{item.title}</span></button>)}</aside><div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><div className="mb-4 flex flex-wrap items-center justify-between gap-2"><h2 className="text-lg font-bold text-slate-900">{t('subjectDetail.checkpoint3', { selected })}</h2><Badge variant={total === 100 ? 'Active' : 'Improving'}>{t('subjectDetail.totalWeight', { total })}</Badge></div><div className="space-y-4"><label className="block text-sm font-medium">{t('subjectDetail.checkpointTitle')}<input aria-label={t('subjectDetail.checkpointTitle')} value={checkpoint.title} onChange={e => update({ ...checkpoint, title: e.target.value })} className="mt-1 w-full rounded-xl border p-2.5" /></label><label className="block text-sm font-medium">{t('subjectDetail.requirements')}<textarea aria-label={t('subjectDetail.requirements')} value={checkpoint.requirements.join('\n')} onChange={e => update({ ...checkpoint, requirements: e.target.value.split('\n').map(value => value.trim()).filter(Boolean) })} className="mt-1 min-h-20 w-full rounded-xl border p-2.5" /></label><label className="block text-sm font-medium">{t('subjectDetail.shortDescription')}<textarea aria-label={t('subjectDetail.shortDescription')} value={checkpoint.shortDescription ?? ''} onChange={e => update({ ...checkpoint, shortDescription: e.target.value })} className="mt-1 min-h-20 w-full rounded-xl border p-2.5" /></label><div className="space-y-3">{checkpoint.rubrics.map((criterion: any, index: number) => <div key={index} className="rounded-xl border border-slate-200 p-4"><div className="mb-3 flex justify-between"><Badge>{t('subjectDetail.criterion', { v1: index + 1 })}</Badge><button aria-label={t('subjectDetail.deleteCriterion')} disabled={checkpoint.rubrics.length === 1} onClick={() => update({ ...checkpoint, rubrics: checkpoint.rubrics.filter((_: any, position: number) => position !== index) })} className="text-danger disabled:opacity-40"><Trash2 className="h-4 w-4" /></button></div><div className="grid gap-3 sm:grid-cols-2"><input aria-label={t('subjectDetail.criterionKey')} value={criterion.key} onChange={e => { const rubrics = [...checkpoint.rubrics]; rubrics[index] = { ...criterion, key: e.target.value }; update({ ...checkpoint, rubrics }); }} placeholder={t('subjectDetail.key')} className="rounded-xl border p-2.5 font-mono" /><input aria-label={t('subjectDetail.criterionLabel')} value={criterion.label} onChange={e => { const rubrics = [...checkpoint.rubrics]; rubrics[index] = { ...criterion, label: e.target.value, key: resolveCriterionKey(criterion.key, criterion.label, e.target.value) }; update({ ...checkpoint, rubrics }); }} placeholder={t('subjectDetail.label')} className="rounded-xl border p-2.5" /><input aria-label={t('subjectDetail.criterionWeight')} type="number" step="0.5" min="0" value={criterion.weight} onChange={e => { const rubrics = [...checkpoint.rubrics]; rubrics[index] = { ...criterion, weight: e.target.value }; update({ ...checkpoint, rubrics }); }} placeholder={t('subjectDetail.weight3')} className="rounded-xl border p-2.5" /><textarea aria-label={t('subjectDetail.criterionDescription')} value={criterion.description ?? ''} onChange={e => { const rubrics = [...checkpoint.rubrics]; rubrics[index] = { ...criterion, description: e.target.value }; update({ ...checkpoint, rubrics }); }} placeholder={t('startupIndustryManagement.description')} className="rounded-xl border p-2.5" /></div></div>)}</div><Button variant="outline" icon={Plus} onClick={() => update({ ...checkpoint, rubrics: [...checkpoint.rubrics, { key: '', label: '', description: '', weight: 0, levels: [] }] })}>{t('subjectDetail.addCriterion')}</Button><div className="flex flex-wrap items-center justify-between gap-3 border-t pt-4"><span className="text-sm text-slate-500">{t('subjectDetail.changesHereApplyToEveryClass2', { subjectCode })}</span><Button icon={Save} isLoading={saving} onClick={save}>{dirty ? t('subjectDetail.saveSynchronizeSubject') : t('subjectDetail.saveSynchronizeSubject')}</Button></div></div></div></section>;
}

interface FlexibleCheckpointRubricEditorProps {
  subjectCode: string;
  checkpoints: SubjectCheckpointDraft[];
  otherAssessments: SubjectOtherAssessmentDraft[];
  onSaved: () => void | Promise<void>;
}

type RubricDeleteRequest =
  | { type: 'checkpoint' }
  | { type: 'criterion' | 'other'; index: number; label?: string };

function FlexibleCheckpointRubricEditor({ subjectCode, checkpoints, otherAssessments, onSaved }: FlexibleCheckpointRubricEditorProps) {
  const { t } = useTranslation('admin');
  const [items, setItems] = useState<SubjectCheckpointDraft[]>([]); const [otherItems, setOtherItems] = useState<SubjectOtherAssessmentDraft[]>([]); const [selected, setSelected] = useState<number | null>(null); const [dirty, setDirty] = useState(false); const [saving, setSaving] = useState(false); const [deleteRequest, setDeleteRequest] = useState<RubricDeleteRequest | null>(null);
  const [importModalOpen, setImportModalOpen] = useState(false); const [importPreview, setImportPreview] = useState<RubricImportPreview | null>(null); const [importErrors, setImportErrors] = useState<string[]>([]); const [readingImport, setReadingImport] = useState(false); const importInputRef = useRef<HTMLInputElement>(null);
  useEffect(() => { const next = [...checkpoints].sort((a, b) => a.number - b.number); setItems(next); setOtherItems([...otherAssessments]); setSelected(next[0]?.number ?? null); setDirty(false); }, [checkpoints, otherAssessments]);
  const current = items.find(item => item.number === selected);
  const update = (next: SubjectCheckpointDraft) => { setItems(values => values.map(item => item.number === next.number ? next : item)); setDirty(true); };
  const add = () => { const number = Array.from({ length: 10 }, (_, index) => index + 1).find(value => !items.some(item => item.number === value)); if (!number) return void toast.error(t('subjectDetail.aSubjectCanHaveUpTo')); const next = { number, title: t('subjectDetail.checkpoint', { number }), shortDescription: '', courseWeight: 0, requirements: [], rubrics: [{ key: '', label: '', description: '', weight: 100, levels: [] }] }; setItems(values => [...values, next].sort((a, b) => a.number - b.number)); setSelected(number); setDirty(true); };
  const addOtherAssessment = () => { setOtherItems(values => [...values, { name: '', weight: 0 }]); setDirty(true); };
  const remove = () => { if (!deleteRequest || !current) return; if (deleteRequest.type === 'checkpoint') { if (items.length === 1) return void toast.error(t('subjectDetail.aSubjectNeedsAtLeastOne')); const next = items.filter(item => item.number !== current.number); setItems(next); setSelected(next[0].number); } else if (deleteRequest.type === 'other') { setOtherItems(values => values.filter((_, index) => index !== deleteRequest.index)); } else { update({ ...current, rubrics: current.rubrics.filter((_, index) => index !== deleteRequest.index) }); } setDeleteRequest(null); setDirty(true); };
  const chooseImportFile = () => { if (importInputRef.current) importInputRef.current.value = ''; importInputRef.current?.click(); };
  const handleImportFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]; if (!file) return;
    setReadingImport(true); setImportPreview(null); setImportErrors([]); setImportModalOpen(true);
    try { setImportPreview(await readRubricImportFile(file, subjectCode)); }
    catch (error) { setImportErrors(error instanceof RubricImportValidationError ? error.issues : ['The rubric workbook could not be read.']); }
    finally { setReadingImport(false); }
  };
  const applyImport = () => {
    if (!importPreview) return;
    const merged = mergeRubricImport(items, importPreview.checkpoints);
    setItems(merged.checkpoints); setSelected(importPreview.checkpoints[0]?.number ?? selected); setDirty(true); setImportModalOpen(false);
    toast.success(t('subjectDetail.checkpointRubricsAddedToTheDraft', { count: importPreview.checkpoints.length }));
    if (merged.createdCheckpointNumbers.length > 0) toast(t('subjectDetail.setCourseWeightsForNewCheckpoints', { v1: merged.createdCheckpointNumbers.join(', ') }));
  };
  const courseWeightTotal = items.reduce((sum, item) => sum + Number(item.courseWeight || 0), 0) + otherItems.reduce((sum, item) => sum + Number(item.weight || 0), 0);
  const courseWeightsValid = items.every(item => Number(item.courseWeight) > 0 && Number(item.courseWeight) <= 100) && otherItems.every(item => Number(item.weight) > 0 && Number(item.weight) <= 100);
  const canSave = courseWeightTotal === 100 && courseWeightsValid;
  const save = async () => { if (!courseWeightsValid) return void toast.error(t('subjectDetail.setEveryCheckpointAndOtherAssessment')); if (courseWeightTotal !== 100) return void toast.error(t('subjectDetail.courseWeightsMustTotalExactly100', { v1: courseWeightTotal.toFixed(1) })); setSaving(true); try { await subjectApi.synchronizeCheckpoints(subjectCode, { checkpoints: items.map(item => ({ ...item, courseWeight: Number(item.courseWeight) })), otherAssessments: otherItems.map(item => ({ ...item, weight: Number(item.weight) })) }); toast.success(t('subjectDetail.rubricAndCourseWeightsSynchronizedFor')); setDirty(false); await onSaved(); } catch (error) { toast.error(parseApiError(error, t('subjectDetail.failedToSynchronizeRubric')).message); } finally { setSaving(false); } };
  const importPanel = <div className="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between">
    <div><h2 className="font-bold text-slate-900">{t('subjectDetail.importEvaluationRubric')}</h2><p className="mt-1 text-sm text-slate-500">{t('subjectDetail.useTheWorksheetMatchingExistingCheckpoint', { subjectCode })}</p></div>
    <input ref={importInputRef} type="file" accept={RUBRIC_IMPORT_ACCEPT} onChange={handleImportFile} className="hidden" aria-label={t('subjectDetail.chooseRubricExcelFile')} />
    <Button variant="outline" icon={Upload} onClick={chooseImportFile}>{t('subjectDetail.importRubric')}</Button>
  </div>;
  const importReviewModal = <Modal isOpen={importModalOpen} onClose={() => setImportModalOpen(false)} title={t('subjectDetail.reviewRubricImport')} submitText={t('subjectDetail.applyToDraft')} submitDisabled={!importPreview || importErrors.length > 0 || readingImport} isSubmitting={readingImport} onSubmit={applyImport} size="lg">
    {readingImport && <div className="flex min-h-40 items-center justify-center text-sm text-slate-500">{t('subjectDetail.readingAndValidatingWorkbook')}</div>}
    {!readingImport && importErrors.length > 0 && <div className="rounded-xl border border-danger/20 bg-danger-50 p-4"><div className="flex items-center gap-2 font-bold text-danger"><AlertTriangle className="h-4 w-4" />{t('subjectDetail.theWorkbookCannotBeImported')}</div><ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-danger-dark">{importErrors.map((error, index) => <li key={`${index}-${error}`}>{error}</li>)}</ul></div>}
    {!readingImport && importPreview && <div className="space-y-4">
      <div className="flex items-start gap-3 rounded-xl border border-primary/20 bg-primary-50 p-4"><FileSpreadsheet className="mt-0.5 h-5 w-5 shrink-0 text-primary" /><div className="min-w-0"><p className="truncate font-bold text-slate-900">{importPreview.fileName}</p><p className="mt-1 text-sm text-slate-600">{t('subjectDetail.worksheet')} <span className="font-semibold">{importPreview.sheetName}</span> · {importPreview.checkpoints.length} {t('subjectDetail.checkpoints')} {importPreview.checkpoints.reduce((sum, item) => sum + item.criteria.length, 0)} {t('subjectDetail.criteria')}</p></div></div>
      {importPreview.warnings.map(warning => <p key={warning} className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">{warning}</p>)}
      {importPreview.checkpoints.some(imported => !items.some(item => item.number === imported.number)) && <p className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">{t('subjectDetail.newCheckpointsWillBeCreatedWith')}</p>}
      <div className="space-y-2">{importPreview.checkpoints.map(checkpoint => <div key={checkpoint.number} className="rounded-xl border border-slate-200 p-3"><div className="flex items-center justify-between gap-3"><span className="font-bold text-slate-800">{t('subjectDetail.checkpoint', { number: checkpoint.number })}</span><span className="text-xs font-semibold text-emerald-700">{checkpoint.criteria.reduce((sum, criterion) => sum + Number(criterion.weight), 0).toFixed(1)}%</span></div><p className="mt-1 text-sm text-slate-500">{checkpoint.criteria.map(criterion => criterion.label).join(', ')}</p></div>)}</div>
      <p className="text-xs leading-5 text-slate-500">{t('subjectDetail.applyingTheImportOnlyUpdatesThis')}</p>
    </div>}
  </Modal>;
  if (!current) return <>{importPanel}<div className="rounded-2xl border border-slate-200 bg-white p-6"><EmptyState title={t('subjectDetail.noCheckpointsConfigured')} description={t('subjectDetail.importAnExcelRubricOrAdd')} action={{ label: t('subjectDetail.addCheckpoint'), onClick: add }} /></div>{importReviewModal}</>;
  const total = current.rubrics.reduce((sum, item) => sum + Number(item.weight || 0), 0);
  return <>
    {importPanel}
    <section className="grid gap-5 lg:grid-cols-[240px_minmax(0,1fr)]">
      <aside className="min-w-0">
        <div className="flex gap-2 overflow-x-auto lg:block lg:space-y-2">
        {items.map(item => <div key={item.number} className={`flex min-w-60 items-stretch overflow-hidden rounded-xl lg:w-full ${selected === item.number ? 'bg-primary text-white' : 'border border-slate-200 bg-white text-slate-600'}`}><button type="button" onClick={() => setSelected(item.number)} className="min-w-0 flex-1 p-3 text-left"><span className="text-overline font-semibold">{t('subjectDetail.checkpoint2', { number: item.number })}</span><span className="mt-1 block truncate text-sm font-semibold" title={item.title}>{item.title}</span></button><label className={`flex w-[76px] shrink-0 flex-col justify-center border-l px-2 ${selected === item.number ? 'border-white/30' : 'border-slate-200'}`}><span className={`text-[9px] font-bold uppercase ${selected === item.number ? 'text-white/75' : 'text-slate-400'}`}>{t('subjectDetail.weight')}</span><span className="flex items-center gap-1"><input aria-label={t('subjectDetail.checkpointCourseWeight', { number: item.number })} type="number" inputMode="decimal" min="0" max="100" step="0.1" value={item.courseWeight ?? 0} onChange={event => { const value = event.target.value; setItems(values => values.map(valueItem => valueItem.number === item.number ? { ...valueItem, courseWeight: value } : valueItem)); setDirty(true); }} className={`w-11 appearance-none border-0 bg-transparent p-0 text-right text-sm font-black outline-none [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none ${selected === item.number ? 'text-white' : 'text-slate-800'}`} /><span className="text-xs">%</span></span></label></div>)}
        <Button variant="outline" size="sm" icon={Plus} className="mt-2" onClick={add}>{t('subjectDetail.addCheckpoint')}</Button>
        </div>
        <div className="mt-5 border-t border-slate-200 pt-4">
          <div className="mb-2 flex items-center justify-between"><h3 className="text-xs font-black uppercase tracking-wider text-slate-500">{t('subjectDetail.otherAssessments')}</h3><button type="button" onClick={addOtherAssessment} className="rounded-lg p-1.5 text-primary-700 hover:bg-primary-50" title={t('subjectDetail.addOtherAssessment')} aria-label={t('subjectDetail.addOtherAssessment')}><Plus className="h-4 w-4" /></button></div>
          <div className="space-y-2">{otherItems.map((item, index) => <div key={item._id ?? `other-${index}`} className="rounded-xl border border-slate-200 bg-white p-2"><div className="flex items-center gap-2"><input aria-label={t('subjectDetail.otherAssessmentName', { v1: index + 1 })} value={item.name} onChange={event => { const name = event.target.value; setOtherItems(values => values.map((value, position) => position === index ? { ...value, name } : value)); setDirty(true); }} placeholder={t('subjectDetail.assessmentName')} className="min-w-0 flex-1 border-0 bg-transparent px-1 text-sm font-semibold outline-none" /><button type="button" onClick={() => setDeleteRequest({ type: 'other', index, label: item.name })} className="p-1 text-danger" aria-label={`Delete ${item.name || 'other assessment'}`}><Trash2 className="h-3.5 w-3.5" /></button></div><label className="mt-1 flex items-center gap-1 border-t border-slate-100 px-1 pt-2 text-xs text-slate-500"><span className="flex-1">{t('subjectDetail.courseWeight')}</span><input aria-label={t('subjectDetail.courseWeight2', { v1: item.name || `Other assessment ${index + 1}` })} type="number" inputMode="decimal" min="0" max="100" step="0.1" value={item.weight ?? 0} onChange={event => { const weight = event.target.value; setOtherItems(values => values.map((value, position) => position === index ? { ...value, weight } : value)); setDirty(true); }} className="w-14 appearance-none rounded-md border border-slate-200 px-1.5 py-1 text-right font-bold text-slate-800 outline-none [appearance:textfield] focus:border-primary [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none" /><span>%</span></label></div>)}</div>
          {otherItems.length === 0 && <p className="text-xs leading-5 text-slate-400">{t('subjectDetail.noAssessmentsOutsideCheckpoints')}</p>}
        </div>
        <div className={`mt-4 rounded-xl border p-3 ${canSave ? 'border-emerald-200 bg-emerald-50 text-emerald-700' : 'border-amber-200 bg-amber-50 text-amber-800'}`}><div className="flex items-center justify-between text-xs font-bold"><span>{t('subjectDetail.totalCourseWeight')}</span><span className="tabular-nums">{courseWeightTotal.toFixed(1)}%</span></div>{!courseWeightsValid ? <p className="mt-1 text-caption leading-4">{t('subjectDetail.setEveryWeightToAValue')}</p> : courseWeightTotal !== 100 && <p className="mt-1 text-caption leading-4">{t('subjectDetail.checkpointAndOtherAssessmentWeightsMust')}</p>}</div>
      </aside>
      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="mb-4 flex flex-wrap items-center justify-between gap-2"><h2 className="text-lg font-bold">{t('subjectDetail.checkpoint', { number: current.number })}</h2><div className="flex gap-2"><Badge variant={total === 100 ? 'Active' : 'Improving'}>{t('subjectDetail.totalWeight', { total })}</Badge><button aria-label={t('subjectDetail.deleteCheckpoint')} onClick={() => setDeleteRequest({ type: 'checkpoint' })} className="rounded-lg p-2 text-danger hover:bg-danger-50"><Trash2 className="h-4 w-4" /></button></div></div>
        <div className="space-y-4">
          <input aria-label={t('subjectDetail.checkpointTitle')} value={current.title} onChange={e => update({ ...current, title: e.target.value })} className="w-full rounded-xl border p-2.5" placeholder={t('subjectDetail.checkpointTitle')} />
          <textarea aria-label={t('subjectDetail.requirements')} value={current.requirements.join('\n')} onChange={e => update({ ...current, requirements: e.target.value.split('\n').map(value => value.trim()).filter(Boolean) })} className="min-h-20 w-full rounded-xl border p-2.5" placeholder={t('subjectDetail.requirementsOnePerLine')} />
          <textarea aria-label={t('subjectDetail.shortDescription')} value={current.shortDescription ?? ''} onChange={e => update({ ...current, shortDescription: e.target.value })} className="min-h-20 w-full rounded-xl border p-2.5" placeholder={t('subjectDetail.shortDescription')} />
          {current.rubrics.map((criterion, index) => <div key={index} className="rounded-xl border p-3">
            <div className="mb-2 flex justify-between"><Badge>{t('subjectDetail.criterion', { v1: index + 1 })}</Badge><button aria-label={t('subjectDetail.deleteCriterion')} disabled={current.rubrics.length === 1} onClick={() => setDeleteRequest({ type: 'criterion', index, label: criterion.label })} className="text-danger disabled:opacity-40"><Trash2 className="h-4 w-4" /></button></div>
            <div className="grid gap-2 sm:grid-cols-2">
              <input aria-label={t('subjectDetail.criterionLabel')} value={criterion.label} onChange={e => { const key = resolveCriterionKey(criterion.key, criterion.label, e.target.value); const rubrics = [...current.rubrics]; rubrics[index] = { ...criterion, label: e.target.value, key }; update({ ...current, rubrics }); }} className="rounded-lg border p-2" placeholder={t('subjectDetail.label')} />
              <input aria-label={t('subjectDetail.criterionKey')} value={criterion.key} onChange={e => { const rubrics = [...current.rubrics]; rubrics[index] = { ...criterion, key: e.target.value }; update({ ...current, rubrics }); }} className="rounded-lg border p-2 font-mono" placeholder={t('subjectDetail.key')} />
              <input aria-label={t('subjectDetail.criterionWeight')} type="number" step="0.5" value={criterion.weight} onChange={e => { const rubrics = [...current.rubrics]; rubrics[index] = { ...criterion, weight: e.target.value }; update({ ...current, rubrics }); }} className="rounded-lg border p-2" placeholder={t('subjectDetail.weight3')} />
              <textarea aria-label={t('subjectDetail.criterionDescription')} value={criterion.description ?? ''} onChange={e => { const rubrics = [...current.rubrics]; rubrics[index] = { ...criterion, description: e.target.value }; update({ ...current, rubrics }); }} className="rounded-lg border p-2" placeholder={t('startupIndustryManagement.description')} />
            </div>
            {criterion.levels.length > 0 && <details className="mt-3 rounded-lg bg-slate-50 p-3"><summary className="cursor-pointer text-xs font-bold text-slate-600">{t('subjectDetail.importedPerformanceLevels', { length: criterion.levels.length })}</summary><div className="mt-2 grid gap-2 sm:grid-cols-2">{criterion.levels.map(level => <div key={level.key} className="rounded-lg border border-slate-200 bg-white p-2"><div className="flex items-center justify-between gap-2 text-xs font-bold text-slate-700"><span>{level.label}</span><span className="text-slate-400">{level.range}</span></div><p className="mt-1 text-xs leading-5 text-slate-500">{level.description || t('subjectDetail.noDescriptionProvided')}</p></div>)}</div></details>}
          </div>)}
          <Button variant="outline" icon={Plus} onClick={() => update({ ...current, rubrics: [...current.rubrics, { key: '', label: '', description: '', weight: '', levels: [] }] })}>{t('subjectDetail.addCriterion')}</Button>
          <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-4"><span className={`text-sm ${canSave ? 'text-emerald-700' : 'text-amber-700'}`}>{t('subjectDetail.courseTotal', { v1: courseWeightTotal.toFixed(1) })}</span><Button icon={Save} isLoading={saving} disabled={!canSave || !dirty} onClick={save}>{t('subjectDetail.saveSynchronizeSubject')}</Button></div>
        </div>
      </div>
    </section>
    {importReviewModal}
    <ConfirmDialog isOpen={Boolean(deleteRequest)} onClose={() => setDeleteRequest(null)} onConfirm={remove} title={deleteRequest?.type === 'checkpoint' ? t('subjectDetail.deleteCheckpoint') : t('subjectDetail.deleteNamed', { name: deleteRequest?.label || (deleteRequest?.type === 'other' ? t('subjectDetail.otherAssessmentFallback') : t('subjectDetail.criterionFallback')) })} description={deleteRequest?.type === 'checkpoint' ? t('subjectDetail.checkpointAndItsRubricConfigurationWill', { number: current.number }) : deleteRequest?.type === 'other' ? t('subjectDetail.theAssessmentWillBeRemovedFrom', { v1: deleteRequest?.label || '' }) : t('subjectDetail.theCriterionWillBeRemovedWhen', { v1: deleteRequest?.label || '' })} confirmText={t('common:actions.delete')} />
  </>;
}
