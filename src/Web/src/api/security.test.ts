import { afterEach, describe, expect, it, vi } from "vitest";
import { clearAuthentication, setAccessToken } from "./authentication";
import { getAntiforgeryToken } from "./security";

afterEach(() => {
  clearAuthentication();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("getAntiforgeryToken", () => {
  it("binds the token request to the authenticated user", async () => {
    setAccessToken("access.current");
    const fetchMock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
      expect(new Headers(init?.headers).get("Authorization")).toBe(
        "Bearer access.current",
      );
      return Promise.resolve(
        new Response(JSON.stringify({ token: "csrf" }), {
          headers: { "Content-Type": "application/json" },
        }),
      );
    });
    vi.stubGlobal("fetch", fetchMock);

    await expect(getAntiforgeryToken()).resolves.toBe("csrf");
  });
});
