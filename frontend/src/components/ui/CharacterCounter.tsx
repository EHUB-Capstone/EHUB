interface CharacterCounterProps {
  value: string;
  max: number;
}

export default function CharacterCounter({ value, max }: CharacterCounterProps) {
  const length = value.length;
  const isOver = length > max;

  return (
    <span
      className={`text-[11px] font-medium tabular-nums ${isOver ? 'text-red-600' : 'text-slate-400'}`}
      aria-label={`${length} of ${max} characters`}
    >
      {length}/{max}
    </span>
  );
}
