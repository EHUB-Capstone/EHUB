import {
  MAX_CHANGE_NOTE_LENGTH,
  MENTOR_CHANGE_REASONS,
  OTHER_REASON_CODE,
} from '../../utils/mentorChangeReasons';

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
  const isOther = code === OTHER_REASON_CODE;
  return (
    <div className="space-y-1.5">
      <label htmlFor={`${idPrefix}-code`} className="block text-xs font-semibold text-slate-700">
        Reason for replacing {mentorName} *
      </label>
      <select
        id={`${idPrefix}-code`}
        value={code}
        disabled={disabled}
        onChange={event => onCodeChange(event.target.value)}
        className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs text-slate-700 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
      >
        <option value="">Choose a reason...</option>
        {MENTOR_CHANGE_REASONS.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
      </select>
      {code !== '' && (
        <textarea
          id={`${idPrefix}-note`}
          aria-label={isOther ? 'Describe the reason' : 'Optional note'}
          rows={2}
          value={note}
          disabled={disabled}
          maxLength={MAX_CHANGE_NOTE_LENGTH}
          onChange={event => onNoteChange(event.target.value)}
          placeholder={isOther ? 'Describe the reason (at least 3 characters)' : 'Add a note (optional)'}
          className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
        />
      )}
      <p className="text-[11px] text-slate-400">
        The lecturer of the class and the replaced mentor (if they have an account) are notified with this reason.
      </p>
    </div>
  );
}
