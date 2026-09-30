import { createContext, useContext, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

// The workspace heading owns a slot; pages place their primary actions there, next to the title.
const ActionsSlot = createContext<HTMLElement | null>(null);
export const PageActionsSlot = ActionsSlot.Provider;

export function PageActions({ children }: { children: ReactNode }) {
  const slot = useContext(ActionsSlot);
  return slot ? createPortal(children, slot) : null;
}
