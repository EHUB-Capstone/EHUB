import { Award, HandCoins, Landmark, Monitor, Presentation, Sparkles, type LucideIcon } from 'lucide-react';
import type { ProjectAchievement } from '../../types/projectData';
import { badgeTones } from '../../components/ui/Badge';

export const ACHIEVEMENT_STYLES: Record<ProjectAchievement, { icon: LucideIcon; className: string }> = {
  Potential: { icon: Sparkles, className: badgeTones.brand },
  'Internally Funded': { icon: Landmark, className: badgeTones.success },
  'Externally Funded': { icon: HandCoins, className: badgeTones.info },
  Awarded: { icon: Award, className: badgeTones.warning },
  Pitching: { icon: Presentation, className: badgeTones.navy },
  Demo: { icon: Monitor, className: badgeTones.neutral },
};
