import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";

const organizationId = "20000000-0000-0000-0000-000000000001";
const campId = "30000000-0000-0000-0000-000000000001";

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

function membershipsResponse() {
  return new Response(
    JSON.stringify([
      {
        organizationId,
        organizationName: "Sonnenhöhe e. V.",
        organizationSlug: "sonnenhoehe",
        role: 1,
      },
    ]),
    { status: 200, headers: { "Content-Type": "application/json" } },
  );
}

describe("camp lifecycle", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("lists dated camps and creates a new planning space", async () => {
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/account/memberships")
          return Promise.resolve(membershipsResponse());
        if (path === "/api/v1/auth/antiforgery")
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (init?.method === "POST")
          return Promise.resolve(
            new Response(
              JSON.stringify({
                id: "30000000-0000-0000-0000-000000000002",
                organizationId,
                name: "Herbstfreizeit 2027",
                slug: "herbstfreizeit-2027",
                description: null,
                startsOn: "2027-10-10",
                endsOn: "2027-10-17",
                timeZoneId: "Europe/Berlin",
                defaultPortions: 30,
                status: 0,
                period: 0,
                version: 1,
              }),
              { status: 201, headers: { "Content-Type": "application/json" } },
            ),
          );
        return Promise.resolve(
          new Response(
            JSON.stringify([
              {
                id: campId,
                organizationId,
                name: "Sommerfreizeit 2026",
                slug: "sommerfreizeit-2026",
                startsOn: "2026-08-01",
                endsOn: "2026-08-08",
                timeZoneId: "Europe/Berlin",
                defaultPortions: 42,
                status: 0,
                period: 1,
                version: 4,
              },
            ]),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps");

    expect(
      await screen.findByRole("heading", { name: "Freizeiten" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Laufend")).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /Sommerfreizeit 2026/ }),
    ).toHaveAttribute("href", "/o/sonnenhoehe/camps/sommerfreizeit-2026");

    await user.click(screen.getByRole("button", { name: "Camp anlegen" }));
    await user.type(
      screen.getByRole("textbox", { name: "Name" }),
      "Herbstfreizeit 2027",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Slug" }),
      "herbstfreizeit-2027",
    );
    await user.clear(screen.getByLabelText("Startdatum"));
    await user.type(screen.getByLabelText("Startdatum"), "2027-10-10");
    await user.clear(screen.getByLabelText("Enddatum"));
    await user.type(screen.getByLabelText("Enddatum"), "2027-10-17");
    await user.click(screen.getByRole("button", { name: "Camp speichern" }));

    expect(await screen.findByText("Herbstfreizeit 2027")).toBeInTheDocument();
    const createCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("/camps") && init?.method === "POST",
    );
    expect(createCall?.[1]?.headers).toEqual({
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": "csrf-token",
    });
  });

  it("archives and reactivates a camp with antiforgery and the current version", async () => {
    let version = 4;
    let status = 0;
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/account/memberships")
          return Promise.resolve(membershipsResponse());
        if (path === "/api/v1/auth/antiforgery")
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (init?.method === "PUT") {
          if (typeof init.body !== "string")
            throw new Error("Der Änderungs-Request enthält keinen JSON-Text.");
          const body = JSON.parse(init.body) as { name: string };
          version += 1;
          return Promise.resolve(
            new Response(
              JSON.stringify({
                id: campId,
                organizationId,
                name: body.name,
                slug: "sommerfreizeit-2026",
                description: "Am See",
                startsOn: "2026-08-01",
                endsOn: "2026-08-08",
                timeZoneId: "Europe/Berlin",
                defaultPortions: 42,
                status,
                period: 1,
                version,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        }
        if (init?.method === "PATCH") {
          if (typeof init.body !== "string")
            throw new Error("Der Status-Request enthält keinen JSON-Text.");
          const body = JSON.parse(init.body) as { status: number };
          status = body.status;
          version += 1;
          return Promise.resolve(
            new Response(
              JSON.stringify({
                id: campId,
                organizationId,
                name: "Sommerfreizeit 2026",
                slug: "sommerfreizeit-2026",
                description: "Am See",
                startsOn: "2026-08-01",
                endsOn: "2026-08-08",
                timeZoneId: "Europe/Berlin",
                defaultPortions: 42,
                status,
                period: 1,
                version,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        }
        return Promise.resolve(
          new Response(
            JSON.stringify({
              id: campId,
              organizationId,
              name: "Sommerfreizeit 2026",
              slug: "sommerfreizeit-2026",
              description: "Am See",
              startsOn: "2026-08-01",
              endsOn: "2026-08-08",
              timeZoneId: "Europe/Berlin",
              defaultPortions: 42,
              status,
              period: 1,
              version,
            }),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/einstellungen");

    expect(
      await screen.findByRole("heading", { name: "Camp-Einstellungen" }),
    ).toBeInTheDocument();
    const nameField = screen.getByRole("textbox", { name: "Name" });
    await user.clear(nameField);
    await user.type(nameField, "Sommerfreizeit am See");
    await user.click(
      screen.getByRole("button", { name: "Änderungen speichern" }),
    );
    expect(
      await screen.findByText("Camp-Einstellungen wurden gespeichert."),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Camp archivieren" }));
    expect(await screen.findByText(/schreibgeschützt/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Camp reaktivieren" }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledTimes(8);
    });
    const updateCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/camps/${campId}`) &&
        init?.method === "PUT",
    );
    expect(updateCall?.[1]?.headers).toEqual({
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"4"',
    });
    const statusCalls = fetchMock.mock.calls.filter(
      ([request, init]) =>
        requestPath(request).endsWith(`/camps/${campId}/status`) &&
        init?.method === "PATCH",
    );
    expect(statusCalls).toHaveLength(2);
    expect(statusCalls[0]?.[1]?.headers).toEqual({
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"5"',
    });
    expect(statusCalls[1]?.[1]?.headers).toEqual({
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"6"',
    });
  });
});
