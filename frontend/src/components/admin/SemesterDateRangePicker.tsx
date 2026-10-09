import { useEffect, useId, useMemo, useRef, useState, type CSSProperties } from 'react';
import { CalendarDays, ChevronDown, ChevronLeft, ChevronRight, X } from 'lucide-react';
import { DayPicker } from 'react-day-picker';
import 'react-day-picker/style.css';
import type { SemesterCode } from '../../types/subjects';

type ActiveField = 'start' | 'end';

interface SemesterDateRangePickerProps {
  semester: SemesterCode;
  year: number;
  startDate: string;
  endDate: string;
  error?: string;
  onChange: (startDate: string, endDate: string) => void;
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
  '--rdp-nav-height': '2.25rem',
  '--rdp-weekday-padding': '0.25rem 0',
} as CSSProperties;

function parseIsoDate(value: string): Date | undefined {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) return undefined;
  return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
}

function toIsoDate(value: Date): string {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function displayDate(value: string): string {
  const date = parseIsoDate(value);
  return date ? new Intl.DateTimeFormat('en-GB').format(date) : value;
}

function monthStart(value: Date): Date {
  return new Date(value.getFullYear(), value.getMonth(), 1);
}

function addMonths(value: Date, amount: number): Date {
  return new Date(value.getFullYear(), value.getMonth() + amount, 1);
}

const monthNames = Array.from({ length: 12 }, (_, month) =>
  new Intl.DateTimeFormat('en', { month: 'short' }).format(new Date(2026, month, 1)));

const HiddenMonthCaption = () => null;

export default function SemesterDateRangePicker({
  semester,
  year,
  startDate,
  endDate,
  error,
  onChange,
}: SemesterDateRangePickerProps) {
  const helpId = useId();
  const endButtonRef = useRef<HTMLButtonElement>(null);
  const [activeField, setActiveField] = useState<ActiveField | null>(null);
  const [displayedMonth, setDisplayedMonth] = useState(() => new Date(year, 0, 1));
  const [monthSelectionOpen, setMonthSelectionOpen] = useState(false);
  const selectedStart = useMemo(() => parseIsoDate(startDate), [startDate]);
  const selectedEnd = useMemo(() => parseIsoDate(endDate), [endDate]);
  const firstAllowedDate = useMemo(() => new Date(year, 0, 1), [year]);
  const lastStartDate = useMemo(() => new Date(year, 11, semester === 'FA' ? 31 : 30), [semester, year]);
  const lastStartMonth = useMemo(() => new Date(year, 11, 1), [year]);
  const lastEndDate = useMemo(() => semester === 'FA'
    ? new Date(year + 1, 0, 31)
    : new Date(year, 11, 31), [semester, year]);

  useEffect(() => {
    setActiveField(null);
    setDisplayedMonth(new Date(year, 0, 1));
    setMonthSelectionOpen(false);
  }, [semester, year]);

  useEffect(() => {
    if (activeField === 'end' && selectedStart) {
      endButtonRef.current?.focus({ preventScroll: true });
    }
  }, [activeField, selectedStart]);

  const isStartDisabled = (date: Date) => date < firstAllowedDate
    || date > lastStartDate;

  const isEndDisabled = (date: Date) => !selectedStart
    || date <= selectedStart
    || date > lastEndDate;

  const openPicker = (field: ActiveField) => {
    if (field === 'end' && !selectedStart) return;
    setActiveField(field);
    setMonthSelectionOpen(false);
    setDisplayedMonth(field === 'start'
      ? selectedStart ?? firstAllowedDate
      : selectedEnd ?? selectedStart ?? firstAllowedDate);
  };

  const selectDate = (date: Date | undefined) => {
    if (!date || !activeField) return;
    if (activeField === 'start') {
      if (isStartDisabled(date)) return;
      const nextStartDate = toIsoDate(date);
      const canKeepEndDate = selectedEnd && !isEndDisabledForStart(selectedEnd, date, lastEndDate);
      onChange(nextStartDate, canKeepEndDate ? endDate : '');
      setActiveField('end');
      setDisplayedMonth(date);
      return;
    }

    if (isEndDisabled(date)) return;
    onChange(startDate, toIsoDate(date));
    setActiveField(null);
  };

  const helperText = error
    ?? (activeField === 'start'
      ? `Choose a start date in ${year}.`
      : activeField === 'end' && selectedStart
        ? `Choose an end date after ${displayDate(startDate)}.`
        : selectedStart && selectedEnd
          ? `${displayDate(startDate)} – ${displayDate(endDate)}`
          : `Choose Start date first. End date will unlock afterwards.`);

  const firstCalendarMonth = monthStart(firstAllowedDate);
  const lastCalendarMonth = monthStart(activeField === 'start' ? lastStartMonth : lastEndDate);
  const currentCalendarMonth = monthStart(displayedMonth);
  const previousMonth = addMonths(currentCalendarMonth, -1);
  const nextMonth = addMonths(currentCalendarMonth, 1);
  const canGoPrevious = previousMonth >= firstCalendarMonth;
  const canGoNext = nextMonth <= lastCalendarMonth;
  const calendarYears = Array.from(
    { length: lastCalendarMonth.getFullYear() - firstCalendarMonth.getFullYear() + 1 },
    (_, index) => firstCalendarMonth.getFullYear() + index,
  );

  const chooseMonth = (month: number) => {
    const candidate = new Date(displayedMonth.getFullYear(), month, 1);
    if (candidate < firstCalendarMonth || candidate > lastCalendarMonth) return;
    setDisplayedMonth(candidate);
    setMonthSelectionOpen(false);
  };

  return (
    <div className="space-y-2">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="block text-sm font-medium text-slate-700">
          Start date *
          <button
            type="button"
            onClick={() => openPicker('start')}
            className={`mt-1.5 flex w-full items-center justify-between rounded-xl border bg-white px-3 py-2.5 text-left text-sm outline-none transition ${activeField === 'start' ? 'border-primary ring-2 ring-primary/20' : 'border-slate-200 hover:border-slate-300'}`}
            aria-describedby={helpId}
            aria-expanded={activeField === 'start'}
          >
            <span className={startDate ? 'text-slate-800' : 'text-slate-400'}>{startDate ? displayDate(startDate) : 'dd/mm/yyyy'}</span>
            <CalendarDays className="h-4 w-4 text-slate-400" />
          </button>
        </label>
        <label className="block text-sm font-medium text-slate-700">
          End date *
          <button
            ref={endButtonRef}
            type="button"
            disabled={!selectedStart}
            onClick={() => openPicker('end')}
            className={`mt-1.5 flex w-full items-center justify-between rounded-xl border px-3 py-2.5 text-left text-sm outline-none transition disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-300 ${activeField === 'end' ? 'border-primary bg-white ring-2 ring-primary/20' : 'border-slate-200 bg-white hover:border-slate-300'}`}
            aria-describedby={helpId}
            aria-expanded={activeField === 'end'}
          >
            <span className={endDate ? 'text-slate-800' : 'text-slate-400'}>{endDate ? displayDate(endDate) : 'dd/mm/yyyy'}</span>
            <CalendarDays className="h-4 w-4 text-slate-400" />
          </button>
        </label>
      </div>

      <p id={helpId} className={`whitespace-pre-line text-xs ${error ? 'font-medium text-danger' : 'text-slate-500'}`} aria-live="polite">
        {helperText}
      </p>

      {activeField && (
        <div className="mx-auto w-full max-w-sm animate-scale-in rounded-2xl border border-slate-200 bg-slate-50/70 px-3 pb-3 pt-2 shadow-card">
          <div className="relative flex min-h-8 items-center justify-center px-9">
            <p className="text-center text-xs font-semibold uppercase tracking-wide text-slate-600">
              {activeField === 'start' ? 'Select start date' : 'Select end date'}
            </p>
            <button type="button" onClick={() => setActiveField(null)} className="absolute right-0 rounded-lg p-1.5 text-slate-400 transition hover:bg-white hover:text-slate-700" aria-label="Close calendar">
              <X className="h-4 w-4" />
            </button>
          </div>
          <div className="flex justify-center rounded-xl bg-white px-2 py-1">
            <div className="w-full max-w-[16rem]">
              <div className="flex h-10 items-center justify-between">
                <button
                  type="button"
                  disabled={!canGoPrevious}
                  onClick={() => canGoPrevious && setDisplayedMonth(previousMonth)}
                  className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-50 hover:text-primary disabled:cursor-not-allowed disabled:opacity-30"
                  aria-label="Go to the previous month"
                >
                  <ChevronLeft className="h-4 w-4" />
                </button>
                <button
                  type="button"
                  onClick={() => setMonthSelectionOpen(current => !current)}
                  className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-sm font-bold text-slate-800 transition hover:bg-slate-50"
                  aria-expanded={monthSelectionOpen}
                  aria-label="Choose calendar month and year"
                >
                  {new Intl.DateTimeFormat('en', { month: 'long', year: 'numeric' }).format(displayedMonth)}
                  <ChevronDown className={`h-3.5 w-3.5 text-slate-400 transition-transform ${monthSelectionOpen ? 'rotate-180' : ''}`} />
                </button>
                <button
                  type="button"
                  disabled={!canGoNext}
                  onClick={() => canGoNext && setDisplayedMonth(nextMonth)}
                  className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-50 hover:text-primary disabled:cursor-not-allowed disabled:opacity-30"
                  aria-label="Go to the next month"
                >
                  <ChevronRight className="h-4 w-4" />
                </button>
              </div>

              {monthSelectionOpen ? (
                <div className="min-h-64 py-2" aria-label="Calendar month and year selection">
                  <div className="mb-3 flex justify-center gap-2">
                    {calendarYears.map(calendarYear => (
                      <button
                        key={calendarYear}
                        type="button"
                        onClick={() => setDisplayedMonth(current => {
                          const candidate = new Date(calendarYear, current.getMonth(), 1);
                          if (candidate < firstCalendarMonth) return firstCalendarMonth;
                          if (candidate > lastCalendarMonth) return lastCalendarMonth;
                          return candidate;
                        })}
                        className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition ${displayedMonth.getFullYear() === calendarYear ? 'bg-primary text-white' : 'bg-slate-50 text-slate-600 hover:bg-primary-50 hover:text-primary'}`}
                      >
                        {calendarYear}
                      </button>
                    ))}
                  </div>
                  <div className="grid grid-cols-3 gap-2">
                    {monthNames.map((monthName, month) => {
                      const candidate = new Date(displayedMonth.getFullYear(), month, 1);
                      const disabled = candidate < firstCalendarMonth || candidate > lastCalendarMonth;
                      const selected = candidate.getTime() === currentCalendarMonth.getTime();
                      return (
                        <button
                          key={monthName}
                          type="button"
                          disabled={disabled}
                          onClick={() => chooseMonth(month)}
                          className={`rounded-lg px-2 py-2 text-xs font-semibold transition disabled:cursor-not-allowed disabled:opacity-30 ${selected ? 'bg-primary-50 text-primary ring-1 ring-primary/30' : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900'}`}
                        >
                          {monthName}
                        </button>
                      );
                    })}
                  </div>
                </div>
              ) : (
                <DayPicker
                  mode="single"
                  selected={activeField === 'start' ? selectedStart : selectedEnd}
                  onSelect={selectDate}
                  month={displayedMonth}
                  onMonthChange={setDisplayedMonth}
                  startMonth={firstAllowedDate}
                  endMonth={activeField === 'start' ? lastStartMonth : lastEndDate}
                  disabled={activeField === 'start' ? isStartDisabled : isEndDisabled}
                  hideNavigation
                  components={{ MonthCaption: HiddenMonthCaption }}
                  fixedWeeks
                  aria-label={activeField === 'start' ? 'Choose semester start date' : 'Choose semester end date'}
                  className="m-0 text-sm"
                  style={pickerStyle}
                />
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function isEndDisabledForStart(
  end: Date,
  start: Date,
  lastEndDate: Date,
): boolean {
  return end <= start
    || end > lastEndDate;
}
