import {
  MAX_CHANGE_NOTE_LENGTH,
  MENTOR_CHANGE_REASONS,
  OTHER_REASON_CODE,
} from '../../utils/mentorChangeReasons';
import { useTranslation } from 'react-i18next';

interface MentorChangeReasonFieldProps {
  idPrefix: string;
  /** Who is being replaced, shown in the label. */
  mentorName: string;
  code: string;
  note: string;
  disabled?: boolean;
  onCodeChange: (code: string) => void;
  onNoteChange: (note: string) => void;
}

/** A reason chosen from a short list, plus a note that is optional unless "Other" is picked. */
export default function MentorChangeReasonField({
  idPrefix, mentorName, code, note, disabled = false, onCodeChange, onNoteChange,
}: MentorChangeReasonFieldProps) {
  const { t } = useTranslation('admin');
  const isOther = code === OTHER_REASON_CODE;
  return (
    <div className="space-y-1.5">
      <label htmlFor={`${idPrefix}-code`} className="block text-xs font-semibold text-slate-700">
        {t('mentorChangeReasonField.reasonForReplacing', { mentorName })}
      </label>
      <select
        id={`${idPrefix}-code`}
        value={code}
        disabled={disabled}
        onChange={event => onCodeChange(event.target.value)}
        className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs text-slate-700 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
      >
        <option value="">{t('mentorChangeReasonField.chooseAReason')}</option>
        {MENTOR_CHANGE_REASONS.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
      </select>
      {code !== '' && (
        <textarea
          id={`${idPrefix}-note`}
          aria-label={isOther ? t('mentorChangeReasonField.describeTheReason') : t('mentorChangeReasonField.optionalNote')}
          rows={2}
          value={note}
          disabled={disabled}
          maxLength={MAX_CHANGE_NOTE_LENGTH}
          onChange={event => onNoteChange(event.target.value)}
          placeholder={isOther ? t('mentorChangeReasonField.describeTheReasonAtLeast3') : t('mentorChangeReasonField.addANoteOptional')}
          className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
        />
      )}
      <p className="text-caption text-slate-400">
        {t('mentorChangeReasonField.theLecturerOfTheClassAnd')}
      </p>
    </div>
  );
}
