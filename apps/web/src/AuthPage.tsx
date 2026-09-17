import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { api } from './access-api';
import { Button } from './components/ui/button';
import { Input } from './components/ui/form';

type Mode = 'login' | 'register' | 'confirm' | 'forgot' | 'reset' | 'resend';
const titles: Record<Mode, string> = {
  login: 'Welcome back',
  register: 'Create your workspace',
  confirm: 'Verify your email',
  forgot: 'Reset your password',
  reset: 'Choose a new password',
  resend: 'Send a verification link',
};

export function AuthPage({ mode }: { mode: Mode }) {
  const [params] = useState(() => new URLSearchParams(window.location.search));
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const navigate = useNavigate();
  const client = useQueryClient();
  useEffect(() => {
    if (params.has('token'))
      window.history.replaceState(null, '', window.location.pathname);
  }, [params]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError('');
    setMessage('');
    setBusy(true);
    const form = new FormData(event.currentTarget);
    const body = Object.fromEntries(form.entries());
    if (mode === 'reset' || mode === 'confirm')
      body.token = params.get('token') || '';
    try {
      await api(`/api/auth/${mode}`, 'POST', body);
      if (mode === 'login') {
        client.clear();
        navigate('/workspace');
      } else
        setMessage(
          mode === 'register'
            ? 'Your workspace is ready. Check your email to verify the account before signing in.'
            : mode === 'confirm'
              ? 'Email verified. You can now sign in.'
              : mode === 'reset'
                ? 'Password updated. Sign in with your new password.'
                : 'If the account is eligible, an email is on its way.',
        );
    } catch (failure) {
      setError(
        failure instanceof Error ? failure.message : 'Please try again.',
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <header>
        <Link to="/" className="brand">
          Gas<span>365</span>
        </Link>
        <Link to={mode === 'login' ? '/register' : '/login'}>
          {mode === 'login' ? 'Create a workspace' : 'Sign in'}
        </Link>
      </header>
      <main className="auth-page">
        <p className="eyebrow">YOUR BUSINESS, CONNECTED</p>
        <h1>{titles[mode]}</h1>
        <p className="intro">
          {mode === 'login'
            ? 'Sign in to manage your business, branches and team.'
            : 'Use your own email address. Account links expire after one hour.'}
        </p>
        <form className="status-card stack" onSubmit={submit}>
          {mode === 'register' && (
            <>
              <label>
                Your name
                <Input
                  name="name"
                  autoComplete="name"
                  maxLength={120}
                  required
                />
              </label>
              <label>
                Business name
                <Input
                  name="businessName"
                  autoComplete="organization"
                  maxLength={120}
                  required
                />
              </label>
            </>
          )}
          <label>
            Email address
            <Input
              name="email"
              type="email"
              autoComplete="email"
              defaultValue={params.get('email') || ''}
              maxLength={254}
              required
            />
          </label>
          {(mode === 'login' || mode === 'register' || mode === 'reset') && (
            <label>
              Password
              <Input
                name="password"
                aria-label="Password"
                aria-describedby={
                  mode !== 'login' ? 'password-help' : undefined
                }
                type="password"
                autoComplete={
                  mode === 'login' ? 'current-password' : 'new-password'
                }
                minLength={mode === 'login' ? undefined : 12}
                maxLength={128}
                required
              />
              {mode !== 'login' && (
                <small id="password-help">
                  At least 12 characters, including uppercase, lowercase, a
                  number and a symbol.
                </small>
              )}
            </label>
          )}
          {error && <p role="alert">{error}</p>}
          {message && (
            <p role="status" className="success-text">
              {message}
            </p>
          )}
          <Button disabled={busy}>
            {busy
              ? 'Please wait…'
              : mode === 'login'
                ? 'Sign in'
                : mode === 'register'
                  ? 'Create workspace'
                  : mode === 'confirm'
                    ? 'Verify email'
                    : mode === 'reset'
                      ? 'Save password'
                      : 'Send email'}
          </Button>
        </form>
        {mode === 'login' && (
          <p className="auth-links">
            <Link to="/forgot">Forgot password?</Link>
            <Link to="/resend">Resend verification</Link>
          </p>
        )}
        {message && (
          <p>
            <Link to="/login">Continue to sign in</Link>
          </p>
        )}
      </main>
    </>
  );
}
