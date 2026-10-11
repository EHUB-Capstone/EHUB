import type { MentorTagSet } from '../../types/mentorProfile';
import { mentorTagList } from '../../utils/mentorTags';

interface MentorTagChipsProps {
  tags?: Partial<MentorTagSet> | null;
  /** How many tags are shown before "+N". */
  max?: number;
}

/** A short, quiet row of a mentor's tags for lists; the full set is on the mentor profile. */
export default function MentorTagChips({ tags, max = 3 }: MentorTagChipsProps) {
  const list = mentorTagList(tags);
  if (list.length === 0) return null;
  return (
    <div className="mt-1 flex flex-wrap items-center gap-1" title={list.map(tag => tag.label).join(', ')}>
      {list.slice(0, max).map(tag => (
        <span key={`${tag.category}:${tag.label}`} className="rounded-full border border-slate-200 bg-slate-50 px-1.5 py-0 text-[10px] font-medium text-slate-500">{tag.label}</span>
      ))}
      {list.length > max && <span className="text-[10px] font-medium text-slate-400">+{list.length - max}</span>}
    </div>
  );
}
