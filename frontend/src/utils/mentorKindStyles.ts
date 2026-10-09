import { Building2, UserCheck, type LucideIcon } from 'lucide-react';
import type { MentorType } from '../types/mentorAdmin';

/** The one place that decides how the two mentor types look, so every screen shows the same colours and icons. */
export const MENTOR_KIND_STYLES: Record<MentorType, { label: string; hint: string; Icon: LucideIcon; badge: string; avatar: string; accent: string }> = {
  Enterprise: {
    label: 'Enterprise mentor',
    hint: 'Mentor from a company',
    Icon: Building2,
    badge: 'border-orange-200 bg-orange-50 text-orange-700 dark:border-orange-400/40 dark:bg-orange-500/15 dark:text-orange-300',
    avatar: 'bg-orange-100 text-orange-700 dark:bg-orange-500/20 dark:text-orange-300',
    accent: 'border-l-orange-500',
  },
  Academic: {
    label: 'Lecturer mentor',
    hint: 'Mentor from the faculty',
    Icon: UserCheck,
    badge: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-400/40 dark:bg-emerald-500/15 dark:text-emerald-300',
    avatar: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300',
    accent: 'border-l-emerald-500',
  },
};
