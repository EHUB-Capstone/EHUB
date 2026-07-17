import { cn } from '../../utils/cn';
import { motion } from 'framer-motion';

const Card = ({ children, className, hover = true, padding = 'p-6', ...props }) => {
  const Comp = hover ? motion.div : 'div';
  const motionProps = hover ? {
    whileHover: { y: -2, boxShadow: '0 8px 30px -6px rgb(0 0 0 / 0.1)' },
    transition: { duration: 0.2 },
  } : {};

  return (
    <Comp
      className={cn(
        'bg-white rounded-2xl border border-slate-200/60 shadow-card',
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
      <div className="w-9 h-9 rounded-xl bg-primary-50 flex items-center justify-center">
        <Icon className="w-[18px] h-[18px] text-primary" />
      </div>
    )}
    <h3 className="text-subheading text-slate-900">{children}</h3>
  </div>
);

Card.Header = CardHeader;
Card.Title = CardTitle;

export default Card;
