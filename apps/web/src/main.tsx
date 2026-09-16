import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { App } from './App';
import { AuthPage } from './AuthPage';
import { WorkspacePage } from './WorkspacePage';
import './styles.css';

const queryClient = new QueryClient();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<App />} />
          <Route path="/workspace" element={<WorkspacePage />} />
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
                <a href="/">Return to platform status</a>
              </main>
            }
          />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
