import { useEffect, useRef } from 'react';

export type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

// Maps server status words to a chip tone; the status text itself is always shown.
export function statusTone(status: string): Tone {
  switch (status) {
    case 'Paid':
    case 'Settled':
    case 'Returned':
    case 'Active':
      return 'success';
    case 'Partially Paid':
    case 'Partly settled':
    case 'Outstanding':
    case 'On loan':
    case 'Draft':
      return 'warning';
    case 'Reversed':
    case 'Lost':
      return 'danger';
    case 'Posted':
      return 'info';
    default:
      return 'neutral';
  }
}

// Moves focus to a view's heading when a full-page view opens, so keyboard and screen-reader users follow the change.
export function useViewFocus<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  useEffect(() => {
    ref.current?.focus();
  }, []);
  return ref;
}
