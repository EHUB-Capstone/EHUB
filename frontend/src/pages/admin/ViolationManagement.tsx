import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import PageHeader from '../../components/ui/PageHeader';
import ShowcaseModerationRules from '../../components/showcase/ShowcaseModerationRules';

export default function ViolationManagement() {
  const { t } = useTranslation('showcase');
  return <div>
    <PageHeader title={t('violationManagement.title')} subtitle={t('violationManagement.subtitle')} />
    <div className="mb-5"><Link to="/showcase/moderation?tab=reports" className="inline-flex min-h-9 items-center text-body-sm font-semibold text-primary-700 hover:underline">{t('violationManagement.reviewProposals')}</Link></div>
    <ShowcaseModerationRules standalone />
  </div>;
}
