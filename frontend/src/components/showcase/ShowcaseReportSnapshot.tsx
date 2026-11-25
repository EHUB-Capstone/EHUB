import { useTranslation } from 'react-i18next';
import type { ShowcaseReportDto } from '../../types/showcase';
import { showcaseReportSnapshotFields } from '../../utils/showcaseModeration';

export default function ShowcaseReportSnapshot({ report }: { report: ShowcaseReportDto }) {
  const { t } = useTranslation('showcase');
  const fields = showcaseReportSnapshotFields(report.contentSnapshot, !!report.commentId);
  return <details className="mt-4 border-t border-slate-200 pt-3">
    <summary className="min-h-9 cursor-pointer font-semibold text-secondary">{t('reportAudit.snapshotTitle')}</summary>
    <p className="mt-2 text-body-sm text-slate-600">{t('reportAudit.snapshotHelp')}</p>
    {fields.length === 0 ? <p className="mt-2 text-body-sm text-slate-600">{t('reportAudit.snapshotUnavailable')}</p>
      : <dl className="mt-3 space-y-3">{fields.map(field => <div key={field.key}>
        <dt className="text-body-sm font-semibold text-secondary">{t(`reportAudit.fields.${field.key}`)}</dt>
        <dd className="mt-1 whitespace-pre-wrap break-words text-body-sm text-slate-700">{field.value}</dd>
      </div>)}</dl>}
  </details>;
}
