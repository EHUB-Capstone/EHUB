import { cn } from '../../utils/cn';
import { motion } from 'framer-motion';

const tones = {
  default: 'bg-white border border-slate-200 shadow-card',
  // One per page at most: the single most important block (primary KPI, hero).
  featured: 'bg-secondary text-white border border-secondary-700 shadow-elevated',
  // For content nested inside another card, instead of a card within a card.
  inset: 'bg-slate-50 border border-slate-200',
};

const Card = ({ children, className, hover = true, padding = 'p-6', tone = 'default', ...props }) => {
  const Comp = hover && tone === 'default' ? motion.div : 'div';
  const motionProps = hover && tone === 'default' ? {
    whileHover: { y: -2, boxShadow: '0 8px 30px -6px rgb(0 0 0 / 0.1)' },
    transition: { duration: 0.2 },
  } : {};

  return (
    <Comp
      className={cn(
        'rounded-2xl',
        tones[tone as keyof typeof tones] ?? tones.default,
        padding,
        className
      )}
      {...motionProps}
      {...props}
    >
      {children}
    </Comp>
  );
};

const CardHeader = ({ children, className }) => (
  <div className={cn('flex items-center justify-between mb-5', className)}>
    {children}
  </div>
);

const CardTitle = ({ children, className, icon: Icon }) => (
  <div className={cn('flex items-center gap-2.5', className)}>
    {Icon && (
      <div className="w-9 h-9 rounded-lg bg-primary-50 flex items-center justify-center">
        <Icon className="w-[18px] h-[18px] text-primary-700" />
      </div>
    )}
    <h3 className="text-subheading text-secondary">{children}</h3>
  </div>
);

Card.Header = CardHeader;
Card.Title = CardTitle;

export default Card;
