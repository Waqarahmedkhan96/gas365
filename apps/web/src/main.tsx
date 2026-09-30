import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import { setNonce } from 'get-nonce';
import { AppRoutes } from './AppRoutes';
import './styles.css';
import '@fontsource/inter/400.css';
import '@fontsource/inter/500.css';
import '@fontsource/inter/600.css';
import '@fontsource/inter/700.css';
import { applyLanguage } from './i18n';

const queryClient = new QueryClient();
const styleNonce = document.querySelector<HTMLMetaElement>(
  'meta[name="style-nonce"]',
)?.content;
if (styleNonce && styleNonce !== '__STYLE_NONCE__') setNonce(styleNonce);
applyLanguage();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
