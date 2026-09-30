import { Languages } from 'lucide-react';
import { setLanguage, useLanguage } from '../i18n';

// Offers the other language, written in that language so a reader of it can find the switch.
export function LanguageToggle({ className = '' }: { className?: string }) {
  const current = useLanguage();
  const next = current === 'ur' ? 'en' : 'ur';
  return (
    <button
      type="button"
      className={`language-toggle ${className}`.trim()}
      onClick={() => setLanguage(next)}
    >
      <Languages aria-hidden="true" size={18} />
      <span lang={next}>{next === 'ur' ? 'اردو' : 'English'}</span>
    </button>
  );
}
