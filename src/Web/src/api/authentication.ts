let accessToken: string | null = null;
let refreshInFlight: Promise<string | null> | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function clearAuthentication() {
  accessToken = null;
}

export async function restoreAuthentication(): Promise<boolean> {
  const token = await refreshAccessToken();
  if (!token) clearAuthentication();
  return token !== null;
}

export async function authenticatedFetch(
  input: RequestInfo | URL,
  init?: RequestInit,
): Promise<Response> {
  const request = input instanceof Request ? new Request(input, init) : null;
  const send = () => {
    if (!accessToken)
      return request
        ? globalThis.fetch(request.clone())
        : globalThis.fetch(input, init);
    const headers = new Headers(request?.headers ?? init?.headers);
    headers.set("Authorization", `Bearer ${accessToken}`);
    return request
      ? globalThis.fetch(new Request(request.clone(), { headers }))
      : globalThis.fetch(input, { ...init, headers });
  };
  let response = await send();
  let requestUrl: string;
  if (request) requestUrl = request.url;
  else if (typeof input === "string") requestUrl = input;
  else if (input instanceof URL) requestUrl = input.href;
  else requestUrl = input.url;
  if (response.status !== 401 || isAuthenticationRequest(requestUrl))
    return response;

  const refreshedToken = await refreshAccessToken();
  if (!refreshedToken) {
    redirectToLogin();
    return response;
  }
  response = await send();
  if (response.status === 401) {
    clearAuthentication();
    redirectToLogin();
  }
  return response;
}

async function refreshAccessToken(): Promise<string | null> {
  refreshInFlight ??= performRefresh().finally(() => {
    refreshInFlight = null;
  });
  return refreshInFlight;
}

async function performRefresh(): Promise<string | null> {
  try {
    const antiforgery = await globalThis.fetch("/api/v1/auth/antiforgery", {
      credentials: "same-origin",
    });
    if (!antiforgery.ok) return null;
    const security: unknown = await antiforgery.json();
    if (!hasStringProperty(security, "token")) return null;
    const response = await globalThis.fetch("/api/v1/auth/refresh", {
      method: "POST",
      credentials: "same-origin",
      headers: { "X-CSRF-TOKEN": security.token },
    });
    if (!response.ok) return null;
    const authentication: unknown = await response.json();
    if (!hasStringProperty(authentication, "accessToken")) return null;
    setAccessToken(authentication.accessToken);
    return authentication.accessToken;
  } catch {
    return null;
  }
}

function hasStringProperty<K extends string>(
  value: unknown,
  key: K,
): value is Record<K, string> {
  return (
    typeof value === "object" &&
    value !== null &&
    key in value &&
    typeof (value as Record<K, unknown>)[key] === "string"
  );
}

function isAuthenticationRequest(url: string): boolean {
  const path = new URL(url, globalThis.location?.origin ?? "http://localhost")
    .pathname;
  return path.startsWith("/api/v1/auth/");
}

function redirectToLogin() {
  if (globalThis.location?.pathname === "/anmelden") return;
  globalThis.history?.replaceState(null, "", "/anmelden");
  globalThis.dispatchEvent?.(new PopStateEvent("popstate"));
}
