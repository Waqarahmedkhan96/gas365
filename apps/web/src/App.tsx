import { useQuery } from '@tanstack/react-query';
import { getHealth } from './health';

export function App() {
  const health = useQuery({
    queryKey: ['platform-health'],
    queryFn: getHealth,
    retry: false,
    refetchInterval: 30000,
  });
  const ready = !health.isError && health.data?.status === 'Healthy';
  const title = health.isPending
    ? 'Checking services…'
    : ready
      ? 'All systems ready'
      : 'Service needs attention';

  return (
    <>
      <header>
        <a href="/" className="brand">
          Gas<span>365</span>
        </a>
        <a href="/workspace">Open workspace</a>
      </header>
      <main>
        <p className="eyebrow">PLATFORM STATUS</p>
        <h1>
          A foundation for
          <br />
          your daily operations.
        </h1>
        <p className="intro">
          The Gas365 platform is taking shape. This page checks that the
          application and database are connected.
        </p>
        <section className="status-card" aria-labelledby="status-heading">
          <div className="status-top">
            <span
              className={ready ? 'indicator ready' : 'indicator'}
              aria-hidden="true"
            />
            <h2 id="status-heading" role="status">
              {title}
            </h2>
          </div>
          <dl>
            <div>
              <dt>Application</dt>
              <dd>
                {health.isPending
                  ? 'Checking'
                  : health.isError
                    ? 'Unavailable'
                    : 'Connected'}
              </dd>
            </div>
            <div>
              <dt>Database</dt>
              <dd>
                {health.isPending
                  ? 'Checking'
                  : health.isError
                    ? 'Unknown'
                    : health.data?.database}
              </dd>
            </div>
          </dl>
          {health.isError && (
            <p role="alert">
              Unable to check platform status. Check the connection and try
              again.
            </p>
          )}
          <button
            onClick={() => void health.refetch()}
            disabled={health.isFetching}
          >
            {health.isFetching ? 'Checking…' : 'Check again'}
          </button>
        </section>
        <p className="note">
          Business features will be added in the next development stages.
        </p>
      </main>
      <footer>
        Gas365 <span>LPG operations, connected.</span>
      </footer>
    </>
  );
}
