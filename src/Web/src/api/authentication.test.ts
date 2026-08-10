import { afterEach, describe, expect, it, vi } from "vitest";
import {
  authenticatedFetch,
  clearAuthentication,
  setAccessToken,
} from "./authentication";

afterEach(() => {
  clearAuthentication();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  globalThis.history.replaceState(null, "", "/");
});

describe("authenticatedFetch", () => {
  it("forwards an unauthenticated request without changing its options", async () => {
    const expected = new Response(null, { status: 204 });
    const fetchMock = vi.fn().mockResolvedValue(expected);
    vi.stubGlobal("fetch", fetchMock);

    const init = { headers: { Accept: "application/json" } };
    const response = await authenticatedFetch("/api/v1/public", init);

    expect(response).toBe(expected);
    expect(fetchMock).toHaveBeenCalledWith("/api/v1/public", init);
  });

  it("derives the URL from a compatible request-info object", async () => {
    const compatibleInput = {
      url: "https://localhost/api/v1/public",
    } as RequestInfo;
    const expected = new Response(null, { status: 204 });
    const fetchMock = vi.fn().mockResolvedValue(expected);
    vi.stubGlobal("fetch", fetchMock);

    const response = await authenticatedFetch(compatibleInput);

    expect(response).toBe(expected);
    expect(fetchMock).toHaveBeenCalledWith(compatibleInput, undefined);
  });

  it("adds the in-memory access token to string, URL and Request inputs", async () => {
    setAccessToken("access.old");
    const calls: Array<{ input: RequestInfo | URL; init?: RequestInit }> = [];
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      calls.push({ input, init });
      return Promise.resolve(new Response(null, { status: 204 }));
    });
    vi.stubGlobal("fetch", fetchMock);

    await authenticatedFetch("/api/v1/camps", {
      headers: { "X-Request": "string" },
    });
    await authenticatedFetch(new URL("https://localhost/api/v1/account"));
    await authenticatedFetch(
      new Request("https://localhost/api/v1/sessions", {
        headers: { "X-Request": "request" },
      }),
    );

    const stringHeaders = new Headers(calls[0]?.init?.headers);
    const urlHeaders = new Headers(calls[1]?.init?.headers);
    const request = calls[2]?.input;
    if (!(request instanceof Request)) throw new Error("Request input missing");
    expect(stringHeaders.get("Authorization")).toBe("Bearer access.old");
    expect(stringHeaders.get("X-Request")).toBe("string");
    expect(urlHeaders.get("Authorization")).toBe("Bearer access.old");
    expect(request.headers.get("Authorization")).toBe("Bearer access.old");
    expect(request.headers.get("X-Request")).toBe("request");
  });

  it("does not recursively refresh authentication endpoints", async () => {
    setAccessToken("access.old");
    const unauthorized = new Response(null, { status: 401 });
    const fetchMock = vi.fn().mockResolvedValue(unauthorized);
    vi.stubGlobal("fetch", fetchMock);

    const response = await authenticatedFetch("/api/v1/auth/logout");

    expect(response).toBe(unauthorized);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("shares one refresh between concurrent requests and retries with the rotated token", async () => {
    setAccessToken("access.old");
    let antiforgeryCalls = 0;
    let refreshCalls = 0;
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = input instanceof Request ? input.url : input.toString();
        if (url.endsWith("/api/v1/auth/antiforgery")) {
          antiforgeryCalls += 1;
          await Promise.resolve();
          return json({ token: "csrf" });
        }
        if (url.endsWith("/api/v1/auth/refresh")) {
          refreshCalls += 1;
          expect(new Headers(init?.headers).get("X-CSRF-TOKEN")).toBe("csrf");
          return json({ accessToken: "access.new" });
        }
        const headers =
          input instanceof Request ? input.headers : new Headers(init?.headers);
        return new Response(null, {
          status:
            headers.get("Authorization") === "Bearer access.new" ? 204 : 401,
        });
      },
    );
    vi.stubGlobal("fetch", fetchMock);

    const [first, second] = await Promise.all([
      authenticatedFetch("/api/v1/camps"),
      authenticatedFetch(new Request("https://localhost/api/v1/account")),
    ]);

    expect(first.status).toBe(204);
    expect(second.status).toBe(204);
    expect(antiforgeryCalls).toBe(1);
    expect(refreshCalls).toBe(1);
  });

  it.each([
    ["failed antiforgery request", () => new Response(null, { status: 503 })],
    ["malformed antiforgery body", () => json({ token: 42 })],
    ["failed refresh request", () => json({ token: "csrf" })],
  ])("redirects after a %s", async (_name, antiforgeryResponse) => {
    setAccessToken("access.old");
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = input instanceof Request ? input.url : input.toString();
      if (url.endsWith("/api/v1/auth/antiforgery"))
        return Promise.resolve(antiforgeryResponse());
      if (url.endsWith("/api/v1/auth/refresh"))
        return Promise.resolve(new Response(null, { status: 401 }));
      return Promise.resolve(new Response(null, { status: 401 }));
    });
    vi.stubGlobal("fetch", fetchMock);

    await authenticatedFetch("/api/v1/camps");

    expect(globalThis.location.pathname).toBe("/anmelden");
  });

  it("clears authentication and redirects when the retried request remains unauthorized", async () => {
    setAccessToken("access.old");
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = input instanceof Request ? input.url : input.toString();
      if (url.endsWith("/api/v1/auth/antiforgery"))
        return Promise.resolve(json({ token: "csrf" }));
      if (url.endsWith("/api/v1/auth/refresh"))
        return Promise.resolve(json({ accessToken: "access.new" }));
      return Promise.resolve(new Response(null, { status: 401 }));
    });
    vi.stubGlobal("fetch", fetchMock);

    const response = await authenticatedFetch("/api/v1/camps");
    await authenticatedFetch("/api/v1/auth/logout");

    expect(response.status).toBe(401);
    expect(globalThis.location.pathname).toBe("/anmelden");
    expect(fetchMock.mock.calls.at(-1)).toEqual([
      "/api/v1/auth/logout",
      undefined,
    ]);
  });

  it("keeps the login route when refresh throws", async () => {
    globalThis.history.replaceState(null, "", "/anmelden");
    setAccessToken("access.old");
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = input instanceof Request ? input.url : input.toString();
      if (url.endsWith("/api/v1/auth/antiforgery"))
        return Promise.reject(new Error("offline"));
      return Promise.resolve(new Response(null, { status: 401 }));
    });
    vi.stubGlobal("fetch", fetchMock);

    await authenticatedFetch("/api/v1/camps");

    expect(globalThis.location.pathname).toBe("/anmelden");
  });
});

function json(value: unknown, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}
