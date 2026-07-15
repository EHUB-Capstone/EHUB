// @ts-nocheck
export const TEACHING_DAYS = [
  'MONDAY',
  'TUESDAY',
  'WEDNESDAY',
  'THURSDAY',
  'FRIDAY',
  'SATURDAY',
];

export const SLOT_TIMES = {
  1: { startTime: '07:00', endTime: '09:15' },
  2: { startTime: '09:30', endTime: '11:45' },
  3: { startTime: '12:30', endTime: '14:45' },
  4: { startTime: '15:00', endTime: '17:15' },
};

export const SLOT_OPTIONS = Object.entries(SLOT_TIMES).map(([slot, time]) => ({
  val: Number(slot),
  label: `Slot ${slot} (${time.startTime} - ${time.endTime})`,
}));

export const formatSlotTime = (slot) => {
  const time = SLOT_TIMES[slot];
  return time ? `${time.startTime} - ${time.endTime}` : '';
};
