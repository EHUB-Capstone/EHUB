import { useTranslation } from 'react-i18next';

interface CharacterCounterProps {
  value: string;
  max: number;
}

export default function CharacterCounter({ value, max }: CharacterCounterProps) {
  const { t } = useTranslation();
  const length = value.length;
  const isOver = length > max;

  return (
    <span
      className={`text-[11px] font-medium tabular-nums ${isOver ? 'text-red-600' : 'text-slate-400'}`}
      aria-label={t('misc.charactersCount', { length, max })}
    >
      {length}/{max}
    </span>
  );
}
