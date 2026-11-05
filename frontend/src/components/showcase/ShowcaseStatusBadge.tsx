import Badge from '../ui/Badge';
import { SHOWCASE_STATUS_META } from '../../utils/showcase';
import type { ShowcaseStatus } from '../../types/showcase';

export default function ShowcaseStatusBadge({ status, className }: { status: ShowcaseStatus; className?: string }) {
  const meta = SHOWCASE_STATUS_META[status];
  return <Badge variant={meta.tone} dot size="md" className={className}>{meta.label}</Badge>;
}
