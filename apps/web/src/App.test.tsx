import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';

function renderPage() {
  return render(
    <QueryClientProvider
      client={
        new QueryClient({
          defaultOptions: { queries: { retry: false, gcTime: 0 } },
        })
      }
    >
      <App />
    </QueryClientProvider>,
  );
}

afterEach(() => vi.unstubAllGlobals());

describe('platform status', () => {
  it('shows connected services after a valid health response', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            status: 'Healthy',
            service: 'Gas365 API',
            database: 'Healthy',
          }),
        ),
      ),
    );
    renderPage();
    expect(await screen.findByText('All systems ready')).toBeInTheDocument();
    expect(screen.getByText('Healthy')).toBeInTheDocument();
  });

  it('shows database failure when the API returns 503', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            status: 'Unhealthy',
            service: 'Gas365 API',
            database: 'Unhealthy',
          }),
          { status: 503 },
        ),
      ),
    );
    renderPage();
    expect(
      await screen.findByText('Service needs attention'),
    ).toBeInTheDocument();
    expect(screen.getByText('Unhealthy')).toBeInTheDocument();
    expect(screen.queryByText('All systems ready')).not.toBeInTheDocument();
  });

  it('recovers from a network failure when the user retries', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockRejectedValueOnce(new Error('offline'))
        .mockResolvedValueOnce(
          new Response(
            JSON.stringify({
              status: 'Healthy',
              service: 'Gas365 API',
              database: 'Healthy',
            }),
          ),
        ),
    );
    renderPage();
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Check again' }));
    expect(await screen.findByText('All systems ready')).toBeInTheDocument();
  });

  it('rejects malformed API responses', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}')));
    renderPage();
    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });
});
