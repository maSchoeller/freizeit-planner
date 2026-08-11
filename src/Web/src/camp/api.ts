import { getAntiforgeryToken } from "../api/security";
import { authenticatedFetch as fetch } from "../api/authentication";

export async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, { credentials: "same-origin" });
  if (!response.ok)
    throw new Error(
      response.status === 401
        ? "Bitte melde dich erneut an."
        : "Daten konnten nicht geladen werden.",
    );
  return (await response.json()) as T;
}

export async function mutateCateringJson<T>(
  path: string,
  method: "POST" | "PUT" | "PATCH" | "DELETE",
  body: unknown,
  version?: number,
  conflictMessage?: string,
) {
  const token = await getAntiforgeryToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    "X-CSRF-TOKEN": token,
  };
  if (version !== undefined) headers["If-Match"] = `"${version}"`;
  const response = await fetch(path, {
    method,
    credentials: "same-origin",
    headers,
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string;
    } | null;
    throw new Error(
      problem?.detail ??
        (response.status === 412 ? conflictMessage : undefined) ??
        "Die Änderung konnte nicht gespeichert werden.",
    );
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}
