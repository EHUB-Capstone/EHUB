import { useState } from 'react';
import toast from 'react-hot-toast';
import { mentorAdminApi } from '../../api/mentorAdminApi';
import type { IncompleteMentor } from '../../types/mentorAdmin';
import { parseApiError } from '../../utils/apiError';
import { toFormValues, toPayload, validateFormValues, type IncompleteMentorFormValues } from '../../utils/incompleteMentorForm';
import Modal from '../ui/Modal';
import MentorKindTag from './MentorKindTag';
import { useTranslation } from 'react-i18next';

interface IncompleteMentorEditModalProps {
  mentor: IncompleteMentor;
  onClose: () => void;
  onSaved: () => void;
}

const inputClass = 'w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 disabled:opacity-60';

function Field({ id, label, children, hint }: { id: string; label: string; children: React.ReactNode; hint?: string }) {
  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-xs font-semibold text-slate-700">{label}</label>
      {children}
      {hint && <p className="mt-1 text-caption text-slate-400">{hint}</p>}
    </div>
  );
}

/** Edits what is known about a mentor who has no account yet. The login email arrives through an import. */
export default function IncompleteMentorEditModal({ mentor, onClose, onSaved }: IncompleteMentorEditModalProps) {
  const { t } = useTranslation('admin');
  const [values, setValues] = useState<IncompleteMentorFormValues>(() => toFormValues(mentor));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const isEnterprise = mentor.mentorType === 'Enterprise';

  const set = (key: keyof IncompleteMentorFormValues) => (event: React.ChangeEvent<HTMLInputElement>) => {
    setValues(current => ({ ...current, [key]: event.target.value }));
    setError('');
  };

  const save = async () => {
    if (saving) return;
    const problem = validateFormValues(values, mentor.mentorType);
    if (problem) { setError(problem); return; }
    setSaving(true);
    setError('');
    try {
      const response = await mentorAdminApi.updateIncompleteMentor(mentor.id, toPayload(values, mentor.mentorType));
      const result = response.data;
      if (result?.becameAccount) {
        const teams = result.teamsSwitched ?? 0;
        const semesters = result.semestersMoved ?? 0;
        const carried = [teams > 0 ? `${teams} team${teams === 1 ? '' : 's'} switched` : '', semesters > 0 ? `${semesters} semester${semesters === 1 ? '' : 's'} moved` : ''].filter(Boolean).join(', ');
        toast.success(t('incompleteMentorEditModal.nowHasAnAccount', { v1: values.fullName.trim(), carried: carried ? ` (${carried})` : '' }), { duration: 6000 });
      } else {
        toast.success(`${values.fullName.trim()} updated.`);
      }
      onSaved();
    } catch (requestError: unknown) {
      // The form stays open with every value kept so the admin can fix it and try again.
      setError(parseApiError(requestError, 'Failed to update the mentor').message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal isOpen onClose={saving ? () => undefined : onClose} title={t('incompleteMentorEditModal.editMentorInformation')} size="lg" submitText={values.email.trim() ? t('incompleteMentorEditModal.saveAndCreateAccount') : t('incompleteMentorEditModal.saveChanges')} isSubmitting={saving} onSubmit={save}>
      <div className="space-y-4">
        <div className="flex items-center gap-2">
          <MentorKindTag type={mentor.mentorType} />
          <p className="text-xs text-slate-500">{t('incompleteMentorEditModal.noLoginAccountYetAddThe')}</p>
        </div>

        <Field id="incomplete-email" label={isEnterprise ? t('incompleteMentorEditModal.loginEmail') : t('incompleteMentorEditModal.loginEmailWorkEmail')} hint={t('incompleteMentorEditModal.addingItCreatesTheMentorS')}>
          <input id="incomplete-email" type="email" className={inputClass} value={values.email} onChange={set('email')} disabled={saving} autoComplete="off" />
        </Field>

        <Field id="incomplete-name" label={t('incompleteMentorEditModal.fullName')}>
          <input id="incomplete-name" className={inputClass} value={values.fullName} onChange={set('fullName')} disabled={saving} maxLength={100} />
        </Field>

        {isEnterprise ? (
          <div className="grid gap-3 sm:grid-cols-2">
            <Field id="incomplete-fpt" label={t('incompleteMentorEditModal.fptEmail')} hint={t('incompleteMentorEditModal.optionalNotTheLoginEmail')}>
              <input id="incomplete-fpt" type="email" className={inputClass} value={values.fptEmail} onChange={set('fptEmail')} disabled={saving} />
            </Field>
            <Field id="incomplete-phone" label={t('accountApprovalBoard.phone')}><input id="incomplete-phone" className={inputClass} value={values.phone} onChange={set('phone')} disabled={saving} maxLength={30} /></Field>
            <Field id="incomplete-dob" label={t('mentorProfileDetail.dateOfBirth')}><input id="incomplete-dob" type="date" className={inputClass} value={values.dateOfBirth} onChange={set('dateOfBirth')} disabled={saving} /></Field>
            <Field id="incomplete-contract" label={t('mentorProfileDetail.contractType')}><input id="incomplete-contract" className={inputClass} value={values.contractType} onChange={set('contractType')} disabled={saving} maxLength={100} /></Field>
            <Field id="incomplete-education" label={t('mentorProfileDetail.educationLevel')}><input id="incomplete-education" className={inputClass} value={values.educationLevel} onChange={set('educationLevel')} disabled={saving} maxLength={200} /></Field>
            <Field id="incomplete-company" label={t('incompleteMentorEditModal.company')}><input id="incomplete-company" className={inputClass} value={values.organization} onChange={set('organization')} disabled={saving} maxLength={200} /></Field>
            <Field id="incomplete-title" label={t('mentorProfileDetail.jobTitle')}><input id="incomplete-title" className={inputClass} value={values.jobTitle} onChange={set('jobTitle')} disabled={saving} maxLength={200} /></Field>
            <Field id="incomplete-address" label={t('incompleteMentorEditModal.currentAddress')}><input id="incomplete-address" className={inputClass} value={values.currentAddress} onChange={set('currentAddress')} disabled={saving} maxLength={500} /></Field>
          </div>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            <Field id="incomplete-department" label={t('mentorProfileDetail.department')}><input id="incomplete-department" className={inputClass} value={values.department} onChange={set('department')} disabled={saving} maxLength={200} /></Field>
            <Field id="incomplete-title" label={t('mentorProfileDetail.jobTitle')}><input id="incomplete-title" className={inputClass} value={values.jobTitle} onChange={set('jobTitle')} disabled={saving} maxLength={200} /></Field>
          </div>
        )}

        {error && <p role="alert" className="rounded-xl bg-red-50 px-3 py-2 text-xs text-red-700">{error}</p>}
      </div>
    </Modal>
  );
}
