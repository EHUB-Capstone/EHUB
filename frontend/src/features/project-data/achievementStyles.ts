import { Award, Banknote, Sparkles, type LucideIcon } from 'lucide-react';
import type { ProjectAchievement } from '../../types/projectData';

export const ACHIEVEMENT_STYLES: Record<ProjectAchievement, { icon: LucideIcon; className: string }> = {
  Potential: { icon: Sparkles, className: 'bg-primary-50 text-primary border-primary-100' },
  Funded: { icon: Banknote, className: 'bg-success-50 text-success-dark border-success-light' },
  Awarded: { icon: Award, className: 'bg-warning-50 text-warning-dark border-warning-light' },
};
