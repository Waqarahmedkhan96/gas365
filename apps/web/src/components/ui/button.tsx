// Adapted from shadcn/ui (MIT). See THIRD_PARTY_NOTICES.md.
import type { ComponentProps } from 'react';
import { Slot } from 'radix-ui';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '../../lib/utils';

const variants = cva(
  'inline-flex min-h-11 items-center justify-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition-colors disabled:pointer-events-none disabled:opacity-60',
  {
    variants: {
      variant: {
        default: 'bg-primary text-primary-foreground hover:bg-navy-900',
        secondary: 'bg-secondary text-secondary-foreground hover:bg-blue-100',
        outline:
          'border border-input bg-surface text-foreground hover:bg-secondary',
        ghost: 'bg-transparent text-foreground hover:bg-secondary',
        destructive: 'bg-destructive text-white hover:bg-red-900',
      },
      size: { default: '', icon: 'w-11 shrink-0 p-0' },
    },
    defaultVariants: { variant: 'default', size: 'default' },
  },
);

export function Button({
  className,
  variant,
  size,
  asChild,
  ...props
}: ComponentProps<'button'> &
  VariantProps<typeof variants> & { asChild?: boolean }) {
  const Component = asChild ? Slot.Root : 'button';
  return (
    <Component
      data-ui="button"
      className={cn(variants({ variant, size }), className)}
      {...props}
    />
  );
}
