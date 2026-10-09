export const STARTUP_INDUSTRY_IMPORT_ACCEPT = '.xlsx,.xls';
export const STARTUP_INDUSTRY_IMPORT_MAX_BYTES = 5 * 1024 * 1024;

export function validateStartupIndustryImportFile(
  file: Pick<File, 'name' | 'size'>,
): string {
  const extension = file.name.split('.').pop()?.toLocaleLowerCase();
  if (extension !== 'xlsx' && extension !== 'xls') {
    return 'Only .xlsx and .xls Excel files are accepted.';
  }

  if (file.size === 0) return 'The selected file is empty.';
  if (file.size > STARTUP_INDUSTRY_IMPORT_MAX_BYTES) {
    return 'The Excel file must not exceed 5 MB.';
  }

  return '';
}
