import { createContext, useContext, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

// The workspace heading owns a slot; pages place their primary actions there, next to the title.
// Outside the workspace (for example in isolated tests) actions render in place.
const ActionsSlot = createContext<HTMLElement | null | undefined>(undefined);
export const PageActionsSlot = ActionsSlot.Provider;

export function PageActions({ children }: { children: ReactNode }) {
  const slot = useContext(ActionsSlot);
  if (slot === undefined)
    return <div className="page-heading-actions">{children}</div>;
  return slot ? createPortal(children, slot) : null;
}
