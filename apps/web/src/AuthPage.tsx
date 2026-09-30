import {
  useEffect,
  useId,
  useState,
  type ComponentProps,
  type FormEvent,
  type ReactNode,
} from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import {
  ArrowRight,
  Building2,
  CheckCircle2,
  Eye,
  EyeOff,
  Lock,
  Mail,
  ShieldCheck,
  User,
  type LucideIcon,
} from 'lucide-react';
import { api } from './access-api';
import { Button } from './components/ui/button';
import { Input } from './components/ui/form';
import { BrandMark } from './components/icons';

type Mode = 'login' | 'register' | 'confirm' | 'forgot' | 'reset' | 'resend';
const titles: Record<Mode, string> = {
  login: 'Welcome back',
  register: 'Create your workspace',
  confirm: 'Verify your email',
  forgot: 'Reset your password',
  reset: 'Choose a new password',
  resend: 'Send a verification link',
};
const subtitles: Record<Mode, string> = {
  login: 'Sign in to manage your business, branches and team.',
  register:
    'Set up your LPG business in a few minutes. Account links expire after one hour.',
  confirm: 'Confirm your email address to finish setting up your account.',
  forgot:
    'Enter your email and we will send you a reset link. Links expire after one hour.',
  reset: 'Use a strong password that you do not use anywhere else.',
  resend: 'Enter your email and we will send a new verification link.',
};

function IconField({
  label,
  icon: Icon,
  help,
  end,
  ...props
}: ComponentProps<typeof Input> & {
  label: string;
  icon: LucideIcon;
  help?: string;
  end?: ReactNode;
}) {
  const id = useId();
  return (
    <div className="auth-field">
      <label htmlFor={id}>{label}</label>
      <div className="input-with-icon">
        <Icon aria-hidden="true" size={18} />
        <Input
          id={id}
          aria-describedby={help ? `${id}-help` : undefined}
          {...props}
        />
        {end}
      </div>
      {help && <small id={`${id}-help`}>{help}</small>}
    </div>
  );
}

export function AuthPage({ mode }: { mode: Mode }) {
  const [params] = useState(() => new URLSearchParams(window.location.search));
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const navigate = useNavigate();
  const client = useQueryClient();
  useEffect(() => {
    if (params.has('token'))
      window.history.replaceState(null, '', window.location.pathname);
  }, [params]);
  useEffect(() => {
    document.title = `${titles[mode]} · Gas365`;
  }, [mode]);
  // Google appears only when the server has it configured.
  const [google, setGoogle] = useState(false);
  useEffect(() => {
    if (mode !== 'login' && mode !== 'register') return;
    let live = true;
    fetch('/api/auth/providers', { credentials: 'same-origin' })
      .then((r) => (r.ok ? r.json() : { google: false }))
      .then((p: { google?: boolean }) => live && setGoogle(!!p.google))
      .catch(() => undefined);
    return () => {
      live = false;
    };
  }, [mode]);
  const googleFailed = params.get('error') === 'google';

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

  const needsPassword =
    mode === 'login' || mode === 'register' || mode === 'reset';
  return (
    <div className="auth-layout">
      <aside className="auth-aside" aria-label="About Gas365">
        <Link to="/" className="auth-aside-brand">
          <BrandMark size={36} />
          <span>
            Gas<span>365</span>
          </span>
        </Link>
        <div className="auth-aside-copy">
          <h2>
            {mode === 'register'
              ? 'Start running your LPG business digitally'
              : 'Every branch, cylinder and rupee in one place'}
          </h2>
          <p>
            Sales, stock, udhaar and daily cash closing for LPG retailers and
            distributors across Pakistan.
          </p>
          <ul>
            {[
              'Filled and empty cylinders tracked per branch',
              'Customer credit and payments in one ledger',
              'Daily cash closing with clear reports',
            ].map((point) => (
              <li key={point}>
                <CheckCircle2 aria-hidden="true" size={18} />
                {point}
              </li>
            ))}
          </ul>
        </div>
        <p className="auth-aside-foot">
          <ShieldCheck aria-hidden="true" size={16} /> Secure sign-in · PKR ·
          Your data stays private to your business
        </p>
      </aside>
      <main className="auth-main">
        <div className="auth-card">
          <Link to="/" className="auth-card-brand">
            <BrandMark size={30} />
            <span>
              Gas<span>365</span>
            </span>
          </Link>
          <h1>{titles[mode]}</h1>
          <p className="auth-subtitle">{subtitles[mode]}</p>
          {googleFailed && mode === 'login' && (
            <p role="alert" className="auth-alert">
              Google sign-in could not be completed. Try again, or use your
              email and password.
            </p>
          )}
          {google && (
            <>
              <a className="google-button" href="/api/auth/google/start">
                <span className="google-mark" aria-hidden="true">
                  G
                </span>
                Continue with Google
              </a>
              <p className="auth-divider">
                <span>or use your email</span>
              </p>
            </>
          )}
          <form className="auth-form" onSubmit={submit}>
            {mode === 'register' && (
              <>
                <IconField
                  label="Your name"
                  icon={User}
                  name="name"
                  autoComplete="name"
                  placeholder="Full name"
                  maxLength={120}
                  required
                />
                <IconField
                  label="Business name"
                  icon={Building2}
                  name="businessName"
                  autoComplete="organization"
                  placeholder="e.g. Karachi Gas Agency"
                  maxLength={120}
                  required
                />
              </>
            )}
            <IconField
              label="Email address"
              icon={Mail}
              name="email"
              type="email"
              autoComplete="email"
              placeholder="name@business.pk"
              defaultValue={params.get('email') || ''}
              maxLength={254}
              required
            />
            {needsPassword && (
              <IconField
                label="Password"
                icon={Lock}
                name="password"
                type={showPassword ? 'text' : 'password'}
                autoComplete={
                  mode === 'login' ? 'current-password' : 'new-password'
                }
                minLength={mode === 'login' ? undefined : 12}
                maxLength={128}
                required
                help={
                  mode !== 'login'
                    ? 'At least 12 characters, including uppercase, lowercase, a number and a symbol.'
                    : undefined
                }
                end={
                  <button
                    type="button"
                    className="password-toggle"
                    aria-label={
                      showPassword ? 'Hide password' : 'Show password'
                    }
                    aria-pressed={showPassword}
                    onClick={() => setShowPassword((value) => !value)}
                  >
                    {showPassword ? (
                      <EyeOff aria-hidden="true" size={18} />
                    ) : (
                      <Eye aria-hidden="true" size={18} />
                    )}
                  </button>
                }
              />
            )}
            {mode === 'login' && (
              <p className="auth-inline-link">
                <Link to="/forgot">Forgot password?</Link>
              </p>
            )}
            {error && (
              <p role="alert" className="auth-alert">
                {error}
              </p>
            )}
            {message && (
              <p role="status" className="auth-success">
                <CheckCircle2 aria-hidden="true" size={18} />
                {message}
              </p>
            )}
            <Button disabled={busy} className="w-full">
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
              {!busy && <ArrowRight aria-hidden="true" size={16} />}
            </Button>
          </form>
          {message && (
            <p className="auth-switch">
              <Link to="/login">Continue to sign in</Link>
            </p>
          )}
          <p className="auth-switch">
            {mode === 'login' ? (
              <>
                New to Gas365? <Link to="/register">Create a workspace</Link>
              </>
            ) : (
              <>
                Already have an account? <Link to="/login">Sign in</Link>
              </>
            )}
          </p>
          {mode === 'login' && (
            <p className="auth-switch">
              Did not get the email?{' '}
              <Link to="/resend">Resend verification</Link>
            </p>
          )}
        </div>
        <p className="auth-legal">© Gas365 · LPG operations, connected.</p>
      </main>
    </div>
  );
}
