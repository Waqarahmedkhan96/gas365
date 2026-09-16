export type Me = {
  userId: string;
  name: string;
  email: string;
  tenantId: string;
  membershipId: string;
  permissions: string[];
  isOwner: boolean;
  workspaces: { membershipId: string; tenantId: string; name: string }[];
};
export type Branch = {
  id: string;
  name: string;
  active: boolean;
  version: number;
};
export type Role = {
  id: string;
  name: string;
  isOwner: boolean;
  permissions: string[];
  version: number;
};
export type Member = {
  id: string;
  name: string;
  email: string;
  roleId: string;
  active: boolean;
  branchIds: string[];
  version: number;
};
export type Tenant = {
  tenantId: string;
  name: string;
  timeZone: string;
  maxBranches: number;
  maxUsers: number;
  maxAdmins: number;
};
export type Session = {
  id: string;
  createdAt: string;
  expiresAt: string;
  current: boolean;
};
export type Audit = {
  id: string;
  action: string;
  actorId: string | null;
  subjectId: string | null;
  occurredAt: string;
};

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

export async function api<T>(
  path: string,
  method = 'GET',
  body?: unknown,
  workspace?: string,
): Promise<T> {
  const headers: Record<string, string> = {};
  if (workspace) headers['X-Workspace'] = workspace;
  if (method !== 'GET') {
    const csrf = await fetch('/api/auth/csrf', {
      credentials: 'same-origin',
      cache: 'no-store',
    });
    if (!csrf.ok)
      throw new ApiError(csrf.status, 'Please wait a moment and try again.');
    headers['X-CSRF-TOKEN'] = (await csrf.json()).token;
    headers['Content-Type'] = 'application/json';
  }
  const response = await fetch(path, {
    method,
    headers,
    credentials: 'same-origin',
    cache: 'no-store',
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new ApiError(
      response.status,
      problem.title ||
        (response.status === 401
          ? 'Please sign in again.'
          : 'This request could not be completed.'),
    );
  }
  return response.status === 204 ? (undefined as T) : response.json();
}
