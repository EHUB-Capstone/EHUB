import { useState, type FormEvent } from 'react';
import toast from 'react-hot-toast';
import { Loader2, X } from 'lucide-react';
import { classApi } from '../../api/classApi';
import { TEAM_MAJOR_GROUPS } from '../../constants/majors';
import {
  buildAddStudentPayload,
  validateAddStudentForm,
  type AddStudentField,
  type AddStudentFormErrors,
  type AddStudentFormValues,
} from '../../utils/addStudent';
import { parseApiError } from '../../utils/apiError';
import { useTranslation } from 'react-i18next';

interface AddStudentModalProps {
  classId: string;
  onClose: () => void;
  onAdded: () => void;
}

export default function AddStudentModal({ classId, onClose, onAdded }: AddStudentModalProps) {
  const { t } = useTranslation('class');
  const [form, setForm] = useState<AddStudentFormValues>({
    studentCode: '',
    fullName: '',
    email: '',
    majorCode: '',
  });
  const [errors, setErrors] = useState<AddStudentFormErrors>({});
  const [submitting, setSubmitting] = useState(false);

  const updateField = (field: AddStudentField, value: string) => {
    setForm(current => ({ ...current, [field]: value }));
    setErrors(current => {
      if (!current[field]) return current;
      const next = { ...current };
      delete next[field];
      return next;
    });
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validationErrors = validateAddStudentForm(form);
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      toast.error(t('addStudentModal.pleaseCorrectTheHighlightedFields'));
      return;
    }

    setSubmitting(true);
    try {
      await classApi.addStudent(classId, buildAddStudentPayload(form));
      toast.success(t('addStudentModal.studentAddedSuccessfully'));
      onAdded();
    } catch (error) {
      toast.error(parseApiError(error, 'Failed to add the student.').message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button type="button" className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} aria-label={t('addStudentModal.closeAddStudentDialog')} />
      <div className="relative w-full max-w-md rounded-2xl bg-white shadow-float animate-scale-in" role="dialog" aria-modal="true" aria-labelledby="add-student-title">
        <div className="flex items-center justify-between border-b border-slate-100 p-6">
          <div>
            <h2 id="add-student-title" className="text-xl font-bold text-slate-900">{t('addStudentModal.addStudent')}</h2>
            <p className="mt-0.5 text-sm text-slate-400">{t('addStudentModal.manuallyEnrollOneStudentInThis')}</p>
          </div>
          <button type="button" onClick={onClose} className="rounded-xl p-2 text-slate-400 transition-all hover:bg-slate-100 hover:text-slate-600" aria-label={t('common:actions.close')}>
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4 p-6" noValidate>
          <div>
            <label htmlFor="add-student-code" className="mb-1 block text-sm font-medium text-slate-700">{t('addStudentModal.studentCode')}</label>
            <input
              id="add-student-code"
              type="text"
              value={form.studentCode}
              onChange={event => updateField('studentCode', event.target.value.toUpperCase())}
              placeholder={t('addStudentModal.exampleSe123456')}
              maxLength={20}
              aria-invalid={Boolean(errors.studentCode)}
              className={`w-full rounded-xl border px-3 py-2 text-sm uppercase outline-none focus:ring-2 focus:ring-primary/20 ${errors.studentCode ? 'border-red-300 bg-red-50 focus:border-red-400' : 'border-slate-200 focus:border-primary'}`}
            />
            {errors.studentCode && <p className="mt-1 text-xs text-red-600">{errors.studentCode}</p>}
          </div>

          <div>
            <label htmlFor="add-student-name" className="mb-1 block text-sm font-medium text-slate-700">{t('addStudentModal.fullName')}</label>
            <input
              id="add-student-name"
              type="text"
              value={form.fullName}
              onChange={event => updateField('fullName', event.target.value)}
              placeholder={t('addStudentModal.exampleNguyenVanA')}
              maxLength={150}
              aria-invalid={Boolean(errors.fullName)}
              className={`w-full rounded-xl border px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-primary/20 ${errors.fullName ? 'border-red-300 bg-red-50 focus:border-red-400' : 'border-slate-200 focus:border-primary'}`}
            />
            {errors.fullName && <p className="mt-1 text-xs text-red-600">{errors.fullName}</p>}
          </div>

          <div>
            <label htmlFor="add-student-email" className="mb-1 block text-sm font-medium text-slate-700">{t('addStudentModal.email')}</label>
            <input
              id="add-student-email"
              type="email"
              value={form.email}
              onChange={event => updateField('email', event.target.value)}
              placeholder={t('addStudentModal.exampleAnvse123456FptEduVn')}
              maxLength={150}
              aria-invalid={Boolean(errors.email)}
              className={`w-full rounded-xl border px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-primary/20 ${errors.email ? 'border-red-300 bg-red-50 focus:border-red-400' : 'border-slate-200 focus:border-primary'}`}
            />
            {errors.email && <p className="mt-1 text-xs text-red-600">{errors.email}</p>}
          </div>

          <div>
            <label htmlFor="add-student-major" className="mb-1 block text-sm font-medium text-slate-700">
              {t('addStudentModal.major')} <span className="font-normal text-slate-400">{t('addStudentModal.optionalForRegisteredStudents')}</span>
            </label>
            <select
              id="add-student-major"
              value={form.majorCode}
              onChange={event => updateField('majorCode', event.target.value)}
              className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            >
              <option value="">{t('addStudentModal.useTheStudentSRegisteredMajor')}</option>
              {TEAM_MAJOR_GROUPS.map(group => (
                <optgroup key={group.key} label={group.label}>
                  {group.majors.map(major => (
                    <option key={major.code} value={major.code}>{major.code} - {major.name}</option>
                  ))}
                </optgroup>
              ))}
            </select>
            <p className="mt-1.5 text-xs leading-relaxed text-slate-400">
              {t('addStudentModal.leaveThisBlankToUseAn')}
            </p>
          </div>

          <div className="flex gap-3 border-t border-slate-100 pt-4">
            <button type="button" onClick={onClose} className="flex-1 rounded-xl border border-slate-200 px-4 py-2.5 text-sm text-slate-600 transition-all hover:bg-slate-50">
              {t('common:actions.cancel')}
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-primary px-4 py-2.5 text-sm font-medium text-white transition-all hover:bg-primary-700 disabled:opacity-50"
            >
              {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : t('addStudentModal.addStudent2')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
