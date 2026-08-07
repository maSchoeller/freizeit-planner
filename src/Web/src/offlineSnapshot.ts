const snapshotKey = "freizeit-cockpit:offline:v1";

export type OfflineSnapshot = {
  synchronizedAt: string;
  schedule?: unknown[];
  meals?: unknown[];
  material?: unknown[];
  shopping?: unknown[];
};

export function loadOfflineSnapshot(): OfflineSnapshot | null {
  const value = localStorage.getItem(snapshotKey);
  if (!value) return null;
  try {
    return JSON.parse(value) as OfflineSnapshot;
  } catch {
    clearOfflineSnapshot();
    return null;
  }
}

export function saveOfflineSnapshot(
  update: Omit<Partial<OfflineSnapshot>, "synchronizedAt">,
): void {
  const previous = loadOfflineSnapshot();
  localStorage.setItem(
    snapshotKey,
    JSON.stringify({
      ...previous,
      ...update,
      synchronizedAt: new Date().toISOString(),
    }),
  );
}

export function clearOfflineSnapshot(): void {
  localStorage.removeItem(snapshotKey);
}
