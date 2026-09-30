import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { setNonce } from 'get-nonce';
import { App } from './App';
import { MarketingPage } from './MarketingPage';
import { AuthPage } from './AuthPage';
import { WorkspacePage } from './WorkspacePage';
import './styles.css';
import '@fontsource/inter/400.css';
import '@fontsource/inter/500.css';
import '@fontsource/inter/600.css';
import '@fontsource/inter/700.css';

const queryClient = new QueryClient();
const styleNonce = document.querySelector<HTMLMetaElement>(
  'meta[name="style-nonce"]',
)?.content;
if (styleNonce && styleNonce !== '__STYLE_NONCE__') setNonce(styleNonce);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<MarketingPage />} />
          <Route path="/status" element={<App />} />
          <Route path="/workspace" element={<WorkspacePage />} />
          <Route path="/workspace/:section" element={<WorkspacePage />} />
          {(
            [
              'login',
              'register',
              'confirm',
              'forgot',
              'reset',
              'resend',
            ] as const
          ).map((mode) => (
            <Route
              key={mode}
              path={`/${mode}`}
              element={<AuthPage key={mode} mode={mode} />}
            />
          ))}
          <Route
            path="*"
            element={
              <main>
                <h1>Page not found</h1>
                <a href="/">Return to the Gas365 home page</a>
              </main>
            }
          />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
