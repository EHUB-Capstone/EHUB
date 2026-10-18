import { Award, HandCoins, Landmark, Sparkles, type LucideIcon } from 'lucide-react';
import type { ProjectAchievement } from '../../types/projectData';
import { badgeTones } from '../../components/ui/Badge';

export const ACHIEVEMENT_STYLES: Record<ProjectAchievement, { icon: LucideIcon; className: string }> = {
  Potential: { icon: Sparkles, className: badgeTones.brand },
  'Internally Funded': { icon: Landmark, className: badgeTones.success },
  'Externally Funded': { icon: HandCoins, className: badgeTones.info },
  Awarded: { icon: Award, className: badgeTones.warning },
};
