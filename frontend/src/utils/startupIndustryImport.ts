import i18n from '../i18n/index.ts';
export const STARTUP_INDUSTRY_IMPORT_ACCEPT = '.xlsx,.xls';
export const STARTUP_INDUSTRY_IMPORT_MAX_BYTES = 5 * 1024 * 1024;

export function validateStartupIndustryImportFile(
  file: Pick<File, 'name' | 'size'>,
): string {
  const extension = file.name.split('.').pop()?.toLocaleLowerCase();
  if (extension !== 'xlsx' && extension !== 'xls') {
    return i18n.t('utils:startupIndustryImport.onlyXlsxAndXlsExcelFiles');
  }

  if (file.size === 0) return i18n.t('utils:mentorMasterImport.theSelectedFileIsEmpty');
  if (file.size > STARTUP_INDUSTRY_IMPORT_MAX_BYTES) {
    return i18n.t('utils:startupIndustryImport.theExcelFileMustNotExceed');
  }

  return '';
}
