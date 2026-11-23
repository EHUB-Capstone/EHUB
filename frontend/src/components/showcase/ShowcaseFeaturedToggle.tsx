import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Star } from 'lucide-react';
import toast from 'react-hot-toast';
import { showcaseApi } from '../../api/showcaseApi';
import { parseApiError } from '../../utils/apiError';
import { cn } from '../../utils/cn';
import Button from '../ui/Button';
import ConfirmDialog from '../ui/ConfirmDialog';
import { useTranslation } from 'react-i18next';

interface ShowcaseFeaturedToggleProps {
  showcaseId: string;
  title: string;
  isFeatured: boolean;
  /** An icon-only control for a card header. */
  compact?: boolean;
  onChanged?: () => void;
}

/** Moderator-only control. Callers must render it only when the API says the viewer can moderate. */
export default function ShowcaseFeaturedToggle({ showcaseId, title, isFeatured, compact = false, onChanged }: ShowcaseFeaturedToggleProps): React.ReactElement {
  const { t } = useTranslation('showcase');
  const queryClient = useQueryClient();
  const [isOpen, setIsOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const action = isFeatured ? 'Unfeature' : 'Feature';

  const confirm = async () => {
    setIsSubmitting(true);
    try {
      if (isFeatured) await showcaseApi.unfeature(showcaseId);
      else await showcaseApi.feature(showcaseId);
      await queryClient.invalidateQueries({ queryKey: ['showcase'] });
      onChanged?.();
      toast.success(isFeatured ? t('showcaseFeaturedToggle.featuredSelectionRemoved') : t('showcaseFeaturedToggle.projectMarkedAsFeatured'));
      setIsOpen(false);
    } catch (error) {
      toast.error(parseApiError(error, 'Unable to update the featured selection. Try again.').message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return <>
    <Button
      variant={isFeatured ? 'secondary' : 'outline'}
      size={compact ? 'md' : 'sm'}
      icon={Star}
      className={cn(
        'relative z-10',
        compact && 'h-9 w-9 p-0',
        !isFeatured && 'dark:border-slate-600 dark:bg-dark-surface dark:text-slate-100 dark:hover:bg-secondary-700',
      )}
      aria-label={`${action} ${title}`}
      title={action}
      onClick={() => setIsOpen(true)}
    >
      {compact ? <span className="sr-only">{action}</span> : action}
    </Button>
    <ConfirmDialog
      isOpen={isOpen}
      onClose={() => { if (!isSubmitting) setIsOpen(false); }}
      onConfirm={confirm}
      title={`${action} project: ${title}`}
      description={isFeatured
        ? t('showcaseFeaturedToggle.thisProjectWillNoLongerAppear')
        : t('showcaseFeaturedToggle.thisPublishedProjectWillAppearIn')}
      confirmText={action}
      confirmVariant={isFeatured ? 'danger' : 'primary'}
      isSubmitting={isSubmitting}
    />
  </>;
}
