import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";

function renderRoute(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function requestPath(request: RequestInfo | URL) {
  if (typeof request === "string") return request;
  return request instanceof URL ? request.toString() : request.url;
}

describe("camp workspace", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("offers every planning area as a real route", () => {
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/logistik");
    expect(
      screen.getByRole("heading", { name: "Material & Einkaufslisten" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Andachten" })).toHaveAttribute(
      "href",
      "/o/sonnenhoehe/camps/sommerfreizeit-2026/andachten",
    );
    expect(
      screen.getByRole("checkbox", { name: "12 kg Kartoffeln" }),
    ).toBeEnabled();
  });

  it("marks offline mode and prevents planning writes", () => {
    vi.spyOn(window.navigator, "onLine", "get").mockReturnValue(false);
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/dateien");
    expect(screen.getByText(/^Offline/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /hochladen/ })).toBeDisabled();
  });

  it("searches the current camp and exposes real CSV downloads", async () => {
    const fetchMock = vi.fn((request: RequestInfo | URL) => {
      const path = requestPath(request);
      return Promise.resolve(
        new Response(
          JSON.stringify(
            path.endsWith("/trash")
              ? []
              : [
                  {
                    objectType: "Meal",
                    objectId: "40000000-0000-0000-0000-000000000001",
                    title: "Kartoffelsuppe aus der API",
                    metadata: { portions: "42" },
                    updatedAt: "2026-08-04T10:00:00Z",
                    version: 1,
                  },
                ],
          ),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      );
    });
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/suche");

    await user.type(
      screen.getByRole("searchbox", { name: "Camp durchsuchen" }),
      "Kartoffel",
    );

    expect(
      await screen.findByText("Kartoffelsuppe aus der API"),
    ).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("/search?query=Kartoffel"),
      expect.objectContaining({ credentials: "same-origin" }),
    );
    expect(
      screen.getByRole("link", { name: "Zeitplan als CSV" }),
    ).toHaveAttribute("href", expect.stringContaining("/exports/schedule.csv"));
  });

  it("loads the camp trash and restores an item with antiforgery and version", async () => {
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/auth/antiforgery") {
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        }
        if (init?.method === "POST")
          return Promise.resolve(new Response(null, { status: 200 }));
        return Promise.resolve(
          new Response(
            JSON.stringify([
              {
                objectType: "Note",
                objectId: "41000000-0000-0000-0000-000000000001",
                title: "Packliste",
                deletedAt: "2026-08-09T10:00:00Z",
                purgeAt: "2026-09-08T10:00:00Z",
                version: 3,
                restorePath: "/api/v1/restore-note",
              },
            ]),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/suche");

    expect(await screen.findByText("Packliste")).toBeInTheDocument();
    await user.click(
      screen.getByRole("button", { name: "Packliste wiederherstellen" }),
    );

    const restoreCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request) === "/api/v1/restore-note" &&
        init?.method === "POST",
    );
    expect(restoreCall?.[1]?.headers).toEqual({
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"3"',
    });
  });
});
