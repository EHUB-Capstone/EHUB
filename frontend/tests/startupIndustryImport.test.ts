import assert from 'node:assert/strict';
import test from 'node:test';
import {
  STARTUP_INDUSTRY_IMPORT_MAX_BYTES,
  validateStartupIndustryImportFile,
} from '../src/utils/startupIndustryImport.ts';

test('startup industry import accepts supported Excel files', () => {
  assert.equal(validateStartupIndustryImportFile({ name: 'Industry.xlsx', size: 1024 }), '');
  assert.equal(validateStartupIndustryImportFile({ name: 'Industry.XLS', size: 1024 }), '');
});

test('startup industry import rejects empty, oversized, and unsupported files', () => {
  assert.match(validateStartupIndustryImportFile({ name: 'Industry.csv', size: 1024 }), /Only/);
  assert.match(validateStartupIndustryImportFile({ name: 'Industry.xlsx', size: 0 }), /empty/);
  assert.match(validateStartupIndustryImportFile({
    name: 'Industry.xlsx',
    size: STARTUP_INDUSTRY_IMPORT_MAX_BYTES + 1,
  }), /5 MB/);
});
