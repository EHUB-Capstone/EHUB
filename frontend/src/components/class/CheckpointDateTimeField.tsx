import { useId, useState, type CSSProperties } from 'react';
import { CalendarDays } from 'lucide-react';
import { DayPicker } from 'react-day-picker';
import 'react-day-picker/style.css';
import { checkpointDateParts } from '../../utils/checkpointDateTime';

interface Props {
  label: string;
  value: string;
  defaultTime: string;
  onChange: (value: string) => void;
  minDate?: string;
  error?: string;
}

const pickerStyle = {
  '--rdp-accent-color': 'var(--color-primary)',
  '--rdp-accent-background-color': 'var(--color-primary-50)',
  '--rdp-day-height': '2rem',
  '--rdp-day-width': '2rem',
  '--rdp-day_button-border-radius': '0.5rem',
  '--rdp-day_button-width': '1.75rem',
  '--rdp-day_button-height': '1.75rem',
  '--rdp-nav_button-height': '2rem',
  '--rdp-nav_button-width': '2rem',
} as CSSProperties;

function parseCalendarDate(value: string): Date | undefined {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) return undefined;
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  return date.getFullYear() === Number(match[1]) &&
    date.getMonth() === Number(match[2]) - 1 && date.getDate() === Number(match[3])
    ? date : undefined;
}

function toCalendarValue(value: Date): string {
  const two = (part: number) => String(part).padStart(2, '0');
  return `${value.getFullYear()}-${two(value.getMonth() + 1)}-${two(value.getDate())}`;
}

function displayDate(value: string): string {
  const date = parseCalendarDate(value);
  if (!date) return 'DD/MM/YYYY';
  const two = (part: number) => String(part).padStart(2, '0');
  return `${two(date.getDate())}/${two(date.getMonth() + 1)}/${date.getFullYear()}`;
}

export default function CheckpointDateTimeField({ label, value, defaultTime, onChange, minDate, error }: Props) {
  const id = useId();
  const { date, hour, minute } = checkpointDateParts(value, defaultTime);
  const selected = parseCalendarDate(date);
  const minimum = minDate ? parseCalendarDate(minDate) : undefined;
  const [open, setOpen] = useState(false);
  const [month, setMonth] = useState(() => selected || minimum || new Date());
  const changePart = (nextDate: string, nextHour: string, nextMinute: string) =>
    onChange(`${nextDate}T${nextHour}:${nextMinute}`);
  const changeTime = (part: 'hour' | 'minute', raw: string) => {
    const digits = raw.replace(/\D/g, '').slice(0, 2);
    changePart(date, part === 'hour' ? digits : hour, part === 'minute' ? digits : minute);
  };
  const normalizeTime = (part: 'hour' | 'minute') => {
    const current = part === 'hour' ? hour : minute;
    if (current.length !== 1) return;
    changePart(date, part === 'hour' ? current.padStart(2, '0') : hour,
      part === 'minute' ? current.padStart(2, '0') : minute);
  };

  return (
    <div className="space-y-2">
      <p className="text-sm font-semibold text-slate-700">{label}</p>
      <div className="grid grid-cols-[minmax(0,1fr)_auto] items-end gap-3">
        <div>
          <span className="mb-1 block text-xs font-medium text-slate-500">Date</span>
          <button
            type="button"
            onClick={() => { setMonth(selected || minimum || new Date()); setOpen(current => !current); }}
            aria-expanded={open}
            aria-controls={`${id}-calendar`}
            aria-label={`${label} date`}
            className="flex w-full items-center justify-between rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-left text-sm text-slate-800 outline-none hover:border-primary focus:ring-2 focus:ring-primary/20"
          >
            <span className={selected ? '' : 'text-slate-400'}>{displayDate(date)}</span>
            <CalendarDays className="h-4 w-4 text-slate-400" />
          </button>
        </div>
        <div>
          <span className="mb-1 block text-xs font-medium text-slate-500">Time (24-hour)</span>
          <div className="flex items-center gap-1 text-sm font-semibold text-slate-600">
            <input
              aria-label={`${label} hour (00–23)`}
              inputMode="numeric"
              maxLength={2}
              placeholder="HH"
              value={hour}
              onFocus={event => event.target.select()}
              onChange={event => changeTime('hour', event.target.value)}
              onBlur={() => normalizeTime('hour')}
              className="w-11 rounded-xl border border-slate-200 bg-white px-1 py-2.5 text-center text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
            <span aria-hidden="true">:</span>
            <input
              aria-label={`${label} minute (00–59)`}
              inputMode="numeric"
              maxLength={2}
              placeholder="mm"
              value={minute}
              onFocus={event => event.target.select()}
              onChange={event => changeTime('minute', event.target.value)}
              onBlur={() => normalizeTime('minute')}
              className="w-11 rounded-xl border border-slate-200 bg-white px-1 py-2.5 text-center text-sm outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
            />
          </div>
        </div>
      </div>
      {error && <p role="alert" className="text-xs font-medium text-red-600">{error}</p>}
      {open && (
        <div id={`${id}-calendar`} className="w-fit max-w-full rounded-xl border border-slate-200 bg-white p-2 shadow-sm">
          <DayPicker
            mode="single"
            selected={selected}
            month={month}
            onMonthChange={setMonth}
            weekStartsOn={1}
            disabled={minimum ? { before: minimum } : undefined}
            onSelect={choice => {
              if (!choice) return;
              changePart(toCalendarValue(choice), hour, minute);
              setOpen(false);
            }}
            style={pickerStyle}
            aria-label={`${label} calendar`}
          />
        </div>
      )}
    </div>
  );
}
