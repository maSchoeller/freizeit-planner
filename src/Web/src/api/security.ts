import { authenticatedFetch } from "./authentication";

export async function getAntiforgeryToken(): Promise<string> {
  const response = await authenticatedFetch("/api/v1/auth/antiforgery", {
    credentials: "same-origin",
  });
  if (!response.ok)
    throw new Error("Sicherheits-Token konnte nicht geladen werden.");
  const value: unknown = await response.json();
  if (
    typeof value === "object" &&
    value !== null &&
    "token" in value &&
    typeof value.token === "string"
  ) {
    return value.token;
  }
  throw new Error("Sicherheits-Token fehlt.");
}

export function readProblemDetail(value: unknown, fallback: string): string {
  if (
    typeof value === "object" &&
    value !== null &&
    "detail" in value &&
    typeof value.detail === "string"
  ) {
    return value.detail;
  }
  return fallback;
}
