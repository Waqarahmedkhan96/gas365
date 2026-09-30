import { useSyncExternalStore } from 'react';
import { ur } from './ur';

// Interface language. English is the source text; Urdu comes from the reviewed dictionary in ur.ts.
// Missing entries fall back to English so a new screen never shows a blank label.
export type Language = 'en' | 'ur';
const storageKey = 'gas365:language';
const listeners = new Set<() => void>();

function stored(): Language {
  try {
    return localStorage.getItem(storageKey) === 'ur' ? 'ur' : 'en';
  } catch {
    return 'en';
  }
}

let current: Language = stored();

export function applyLanguage() {
  document.documentElement.lang = current;
  document.documentElement.dir = current === 'ur' ? 'rtl' : 'ltr';
}

export function language(): Language {
  return current;
}

export function setLanguage(next: Language) {
  if (next === current) return;
  current = next;
  try {
    localStorage.setItem(storageKey, next);
  } catch {
    /* The choice still applies for this visit. */
  }
  applyLanguage();
  listeners.forEach((listener) => listener());
}

export function useLanguage(): Language {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    () => current,
  );
}

// Translates English interface text. Placeholders such as {name} are filled after translation.
export function tr(text: string, values?: Record<string, string | number>) {
  let result = current === 'ur' ? (ur[text] ?? text) : text;
  if (values)
    for (const [key, value] of Object.entries(values))
      result = result.split(`{${key}}`).join(String(value));
  return result;
}
