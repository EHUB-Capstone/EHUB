import { Award, Banknote, Sparkles, type LucideIcon } from 'lucide-react';
import type { ProjectAchievement } from '../../types/projectData';
import { badgeTones } from '../../components/ui/Badge';

export const ACHIEVEMENT_STYLES: Record<ProjectAchievement, { icon: LucideIcon; className: string }> = {
  Potential: { icon: Sparkles, className: badgeTones.brand },
  Funded: { icon: Banknote, className: badgeTones.success },
  Awarded: { icon: Award, className: badgeTones.warning },
};
