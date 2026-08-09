const snapshotKey = "freizeit-cockpit:offline:v1";
const activeOrganizationKey = "freizeit-cockpit:offline:organization:v1";

export type OfflineWorkspace = {
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  organizationRole: number;
  campId: string;
  campSlug: string;
  campBase: string;
  camp: unknown;
};

export type OfflineSnapshot = {
  version: 1;
  workspace: OfflineWorkspace;
  synchronizedAt: string;
  schedule?: unknown[];
  meals?: unknown[];
  material?: {
    summaries: unknown[];
    requirements: unknown[];
  };
  shopping?: {
    summaries: unknown[];
    lists: unknown[];
  };
};

type SnapshotMatch =
  | { organizationId: string; campId: string }
  | { organizationSlug: string; campSlug: string };

export function loadOfflineSnapshot(
  match?: SnapshotMatch,
): OfflineSnapshot | null {
  const value = localStorage.getItem(snapshotKey);
  if (!value) return null;
  try {
    const snapshot = JSON.parse(value) as Partial<OfflineSnapshot>;
    if (
      snapshot.version !== 1 ||
      !snapshot.workspace ||
      typeof snapshot.synchronizedAt !== "string"
    ) {
      clearOfflineSnapshot();
      return null;
    }
    if (match && !matches(snapshot.workspace, match)) return null;
    return snapshot as OfflineSnapshot;
  } catch {
    clearOfflineSnapshot();
    return null;
  }
}

export function saveOfflineSnapshot(
  workspace: OfflineWorkspace,
  update: Omit<
    Partial<OfflineSnapshot>,
    "version" | "workspace" | "synchronizedAt"
  >,
): void {
  activateOfflineOrganization(workspace.organizationId);
  const previous = loadOfflineSnapshot({
    organizationId: workspace.organizationId,
    campId: workspace.campId,
  });
  localStorage.setItem(
    snapshotKey,
    JSON.stringify({
      ...previous,
      ...update,
      version: 1,
      workspace,
      synchronizedAt: new Date().toISOString(),
    } satisfies OfflineSnapshot),
  );
}

export function activateOfflineOrganization(organizationId: string): void {
  const activeOrganization = localStorage.getItem(activeOrganizationKey);
  if (activeOrganization && activeOrganization !== organizationId)
    clearOfflineSnapshot();
  localStorage.setItem(activeOrganizationKey, organizationId);
}

export function clearOfflineSnapshot(): void {
  localStorage.removeItem(snapshotKey);
}

export function clearOfflineSession(): void {
  clearOfflineSnapshot();
  localStorage.removeItem(activeOrganizationKey);
}

export function clearOfflineOrganization(organizationId: string): void {
  if (localStorage.getItem(activeOrganizationKey) !== organizationId) return;
  clearOfflineSession();
}

function matches(workspace: OfflineWorkspace, match: SnapshotMatch): boolean {
  if ("organizationId" in match)
    return (
      workspace.organizationId === match.organizationId &&
      workspace.campId === match.campId
    );
  return (
    workspace.organizationSlug === match.organizationSlug &&
    workspace.campSlug === match.campSlug
  );
}
