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

export interface StartupIndustryImportResult {
  importedCount: number;
  skippedExistingCount: number;
  skippedErrorCount: number;
  industries: StartupIndustryDto[];
}

export interface StartupIndustryImportRowPreview {
  rowNumber: number;
  name: string;
  description: string | null;
  isValid: boolean;
  status: 'Ready' | 'Exists' | 'Error';
  errorMessage: string | null;
}

export interface StartupIndustryImportPreviewResult {
  totalRows: number;
  validRowsCount: number;
  existingRowsCount: number;
  errorRowsCount: number;
  rows: StartupIndustryImportRowPreview[];
}
