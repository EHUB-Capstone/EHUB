export type StartupIndustryStatus = 'active' | 'inactive';
export type StartupIndustrySort = 'name-asc' | 'name-desc';

export interface StartupIndustryDto {
  id: string;
  name: string;
  description: string | null;
  status: StartupIndustryStatus;
}

export interface SaveStartupIndustryPayload {
  name: string;
  description: string | null;
  status: StartupIndustryStatus;
}
