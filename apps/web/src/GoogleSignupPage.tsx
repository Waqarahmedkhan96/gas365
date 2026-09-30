import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, Building2, User } from 'lucide-react';
import { api } from './access-api';
import { Button } from './components/ui/button';
import { Input } from './components/ui/form';
import { BrandMark } from './components/icons';
import { tr } from './i18n';
import { LanguageToggle } from './components/LanguageToggle';

// Finishes sign-up for a new owner who arrived through Google: only the name and business name are needed.
export function GoogleSignupPage() {
  const navigate = useNavigate();
  const client = useQueryClient();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  useEffect(() => {
    document.title = 'Finish setting up · Gas365';
  }, []);
  const pending = useQuery({
    queryKey: ['google-pending'],
    queryFn: () =>
      api<{ email: string; name: string }>('/api/auth/google/pending'),
    retry: false,
  });
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError('');
    try {
      await api('/api/auth/google/register', 'POST', {
        name: String(data.get('name') ?? ''),
        businessName: String(data.get('businessName') ?? ''),
      });
      client.clear();
      navigate('/workspace/setup');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="auth-layout single">
      <main className="auth-main">
        <div className="auth-card">
          <div className="auth-card-top">
            <LanguageToggle />
          </div>
          <Link to="/" className="auth-card-brand">
            <BrandMark size={30} />
            <span>
              Gas
              <span>365</span>
            </span>
          </Link>
          <h1>{tr('Finish setting up')}</h1>
          {pending.isPending && (
            <p role="status">{tr('Checking your Google sign-in…')}</p>
          )}
          {pending.error && (
            <>
              <p role="alert" className="auth-alert">
                {tr('Your Google sign-in has expired or was not completed.')}
              </p>
              <p className="auth-switch">
                <Link to="/register">{tr('Start again')}</Link>
              </p>
            </>
          )}
          {pending.data && (
            <>
              <p className="auth-subtitle">
                {tr('Signed in with Google as')}{' '}
                <strong>{pending.data.email}</strong>
                {tr(
                  '. Tell us your name and your business name to start your free trial.',
                )}
              </p>
              <form className="auth-form" onSubmit={submit}>
                <div className="auth-field">
                  <label htmlFor="google-name">{tr('Your name')}</label>
                  <div className="input-with-icon">
                    <User aria-hidden="true" size={18} />
                    <Input
                      id="google-name"
                      name="name"
                      autoComplete="name"
                      defaultValue={pending.data.name}
                      maxLength={120}
                      required
                    />
                  </div>
                </div>
                <div className="auth-field">
                  <label htmlFor="google-business">{tr('Business name')}</label>
                  <div className="input-with-icon">
                    <Building2 aria-hidden="true" size={18} />
                    <Input
                      id="google-business"
                      name="businessName"
                      autoComplete="organization"
                      placeholder={tr('e.g. Karachi Gas Agency')}
                      maxLength={120}
                      required
                    />
                  </div>
                </div>
                {error && (
                  <p role="alert" className="auth-alert">
                    {error}
                  </p>
                )}
                <Button disabled={busy} className="w-full">
                  {busy ? tr('Please wait…') : tr('Create workspace')}
                  {!busy && <ArrowRight aria-hidden="true" size={16} />}
                </Button>
              </form>
            </>
          )}
        </div>
      </main>
    </div>
  );
}
