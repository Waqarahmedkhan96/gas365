// Public website settings. Fill these in before going live; empty values are hidden on the site.
// Owner decision (2026-09-30): contact details stay as placeholders until provided. Never invent them.
export const siteConfig = {
  // International format without spaces, for example 923001234567 (used for the WhatsApp link).
  whatsappNumber: '',
  // Shown as written, for example +92 300 1234567.
  phoneDisplay: '',
  email: '',
  city: '',
};

export const hasContact = () =>
  !!(siteConfig.whatsappNumber || siteConfig.email || siteConfig.phoneDisplay);
