export type Notification = {
  id: string;
  kind: 'LowStock' | 'OverdueCredit' | 'RegisterOpen';
  title: string;
  body: string;
  branchId: string | null;
  createdAt: string;
  read: boolean;
};
export type ImportField = {
  key: string;
  label: string;
  required: boolean;
  hint: string;
};
export type ImportType = {
  key: string;
  title: string;
  description: string;
  fields: ImportField[];
};
export type ImportBatch = {
  id: string;
  type: string;
  fileName: string;
  status:
    | 'Uploaded'
    | 'Validated'
    | 'Invalid'
    | 'Committing'
    | 'Committed'
    | 'PartiallyCommitted';
  headers: string[];
  mapping: Record<string, string | null>;
  rowCount: number;
  errorCount: number;
  committedCount: number;
  createdAt: string;
  committedAt: string | null;
  version: number;
  preview: {
    rowNumber: number;
    status: string;
    values: Record<string, string>;
    errors: string[];
  }[];
};
export type AuditEntry = {
  id: string;
  action: string;
  area: string;
  actorId: string | null;
  actor: string;
  subjectId: string | null;
  occurredAt: string;
};
export type Correction = {
  module: string;
  kind: string;
  originalId: string;
  reversalId: string;
  branch: string;
  businessDate: string;
  amount: number;
  reason: string;
  actor: string;
  postedAt: string;
};
export const importStatusText: Record<ImportBatch['status'], string> = {
  Uploaded: 'Uploaded',
  Validated: 'Ready to import',
  Invalid: 'Needs fixes',
  Committing: 'Importing',
  Committed: 'Imported',
  PartiallyCommitted: 'Partly imported',
};

// Workspace downloads need the X-Workspace header, so files are fetched and saved from a blob URL.
export async function downloadFile(
  path: string,
  tenantId: string,
  fileName: string,
) {
  const response = await fetch(path, {
    credentials: 'same-origin',
    headers: { 'X-Workspace': tenantId },
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.title || 'The download could not be completed.');
  }
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
