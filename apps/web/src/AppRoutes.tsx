import { Route, Routes } from 'react-router-dom';
import { Direction } from 'radix-ui';
import { App } from './App';
import { MarketingPage } from './MarketingPage';
import { AuthPage } from './AuthPage';
import { GoogleSignupPage } from './GoogleSignupPage';
import { WorkspacePage } from './WorkspacePage';
import { tr, useLanguage } from './i18n';

// Switching language re-renders every page with the new text; loaded data stays in the query cache.
export function AppRoutes() {
  const language = useLanguage();
  return (
    <Direction.Provider dir={language === 'ur' ? 'rtl' : 'ltr'}>
      <Routes key={language}>
        <Route path="/" element={<MarketingPage />} />
        <Route path="/status" element={<App />} />
        <Route path="/register/google" element={<GoogleSignupPage />} />
        <Route path="/workspace" element={<WorkspacePage />} />
        <Route path="/workspace/:section" element={<WorkspacePage />} />
        {(
          ['login', 'register', 'confirm', 'forgot', 'reset', 'resend'] as const
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
              <h1>{tr('Page not found')}</h1>
              <a href="/">{tr('Return to the Gas365 home page')}</a>
            </main>
          }
        />
      </Routes>
    </Direction.Provider>
  );
}
