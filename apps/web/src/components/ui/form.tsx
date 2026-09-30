import { useId, type ComponentProps } from 'react';
import { cn } from '../../lib/utils';

export function Input({ className, ...props }: ComponentProps<'input'>) {
  return (
    <input
      data-ui="input"
      className={cn(
        props.type === 'checkbox'
          ? 'size-5 shrink-0 accent-primary'
          : 'min-h-11 w-full min-w-0 rounded-lg border border-input bg-surface px-3 py-2 text-sm text-foreground disabled:bg-secondary',
        className,
      )}
      {...props}
    />
  );
}

export function Field({
  label,
  hint,
  error,
  id,
  ...props
}: ComponentProps<typeof Input> & {
  label: string;
  hint?: string;
  error?: string;
}) {
  const generated = useId();
  const inputId = id || generated;
  if (props.type === 'checkbox')
    return (
      <div className="check-field">
        <Input
          id={inputId}
          aria-describedby={error || hint ? `${inputId}-help` : undefined}
          {...props}
        />
        <label htmlFor={inputId}>
          {label}
          {props.required && <span className="sr-only"> (required)</span>}
        </label>
        {(error || hint) && (
          <p
            id={`${inputId}-help`}
            className={error ? 'field-error' : 'text-sm text-muted-foreground'}
          >
            {error || hint}
          </p>
        )}
      </div>
    );
  return (
    <div className="grid gap-2">
      <label htmlFor={inputId}>
        {label}
        {props.required && <span className="sr-only"> (required)</span>}
      </label>
      <Input
        id={inputId}
        aria-invalid={!!error || undefined}
        aria-describedby={error || hint ? `${inputId}-help` : undefined}
        {...props}
      />
      {(error || hint) && (
        <p
          id={`${inputId}-help`}
          className={error ? 'field-error' : 'text-sm text-muted-foreground'}
        >
          {error || hint}
        </p>
      )}
    </div>
  );
}
