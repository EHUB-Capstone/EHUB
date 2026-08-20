import { parseApiError } from './apiError.ts';

export interface BulkClassActionTarget {
  _id: string;
  classCode: string;
}

export interface BulkClassActionFailure {
  classId: string;
  classCode: string;
  code: string;
  message: string;
}

export interface BulkClassActionResult {
  succeeded: string[];
  failed: BulkClassActionFailure[];
}

/**
 * Runs mutations sequentially so that later classes see schedule, lecturer,
 * lifecycle, and row-version changes committed by earlier classes.
 */
export async function executeBulkClassAction<T extends BulkClassActionTarget>(
  targets: readonly T[],
  action: (target: T) => Promise<unknown>,
  fallbackMessage: string,
): Promise<BulkClassActionResult> {
  const result: BulkClassActionResult = { succeeded: [], failed: [] };

  for (const target of targets) {
    try {
      await action(target);
      result.succeeded.push(target.classCode);
    } catch (error) {
      const parsed = parseApiError(error, fallbackMessage);
      result.failed.push({
        classId: target._id,
        classCode: target.classCode,
        code: parsed.code,
        message: parsed.message,
      });
    }
  }

  return result;
}
