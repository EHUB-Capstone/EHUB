export type StartupIndustryStatus = 'active' | 'inactive';
export type StartupIndustrySort = 'name-asc' | 'name-desc';

export interface StartupIndustryDto {
  id: string;
  name: string;
  status: StartupIndustryStatus;
}

export interface SaveStartupIndustryPayload {
  name: string;
  status: StartupIndustryStatus;
}
