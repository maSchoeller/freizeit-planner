import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";
import type { components } from "./api/schema";

const organizationId = "20000000-0000-0000-0000-000000000041";
const userId = "10000000-0000-0000-0000-000000000041";

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

beforeEach(() => {
  vi.stubGlobal("navigator", {
    ...globalThis.navigator,
    onLine: true,
  });
});

describe("user administration", () => {
  it("manages global status and copies a Superadmin invitation", async () => {
    const user = userEvent.setup();
    const copied: string[] = [];
    const requests: Array<{
      path: string;
      method: string;
      version: string | null;
    }> = [];
    vi.stubGlobal("navigator", {
      ...globalThis.navigator,
      onLine: true,
      clipboard: { writeText: vi.fn((value: string) => copied.push(value)) },
    });
    vi.stubGlobal(
      "fetch",
      administrationFetch(({ path, method, request }) => {
        requests.push({
          path,
          method,
          version: request.headers.get("If-Match"),
        });
        if (path === "/api/v1/superadmin/users" && method === "GET")
          return json(page([globalUser()]));
        if (path === "/api/v1/superadmin/organizations" && method === "GET")
          return json([
            {
              organizationId,
              name: "Evangelisches Jugendwerk",
              slug: "ejw",
              status: 0,
              version: 1,
            },
          ]);
        if (path === `/api/v1/superadmin/users/${userId}/superadmin`)
          return json({ ...globalUser(), isSuperAdmin: true, version: 4 });
        if (
          path ===
          `/api/v1/superadmin/users/${userId}/organizations/${organizationId}`
        )
          return json(organizationUser().organizations[0]);
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/invitations/links")
          return json({
            id: "50000000-0000-0000-0000-000000000041",
            token: "shareable-token",
            grant: {
              isSuperAdmin: true,
              organizationId: null,
              organizationRole: null,
              campId: null,
              campRole: null,
            },
            expiresAt: "2026-08-11T12:00:00Z",
            version: 1,
          });
        return empty(404);
      }),
    );

    renderRoute("/superadmin/benutzer");
    expect(await screen.findByText("Erika Muster")).toBeInTheDocument();
    await user.click(
      screen.getByRole("button", { name: "Zum Superadmin machen" }),
    );
    await user.click(
      await screen.findByRole("button", { name: "Als Orgadmin zuweisen" }),
    );
    await user.click(
      screen.getByRole("button", { name: "Superadmin-Link kopieren" }),
    );

    await waitFor(() => expect(copied).toHaveLength(1));
    expect(copied[0]).toContain("/einladung?token=shareable-token");
    expect(
      requests.find((item) => item.path.endsWith("/superadmin"))?.version,
    ).toBe('"3"');
    expect(localStorage).toHaveLength(0);
  });

  it("lets an Orgadmin suspend another Orgadmin only in their organization", async () => {
    const user = userEvent.setup();
    let membershipBody = "";
    let campBody = "";
    vi.stubGlobal(
      "fetch",
      administrationFetch(async ({ path, method, request }) => {
        if (path === "/api/v1/account/memberships")
          return json([
            {
              organizationId,
              organizationName: "Evangelisches Jugendwerk",
              organizationSlug: "ejw",
              role: 1,
            },
          ]);
        if (
          path ===
            `/api/v1/organizations/${organizationId}/administration/users` &&
          method === "GET"
        )
          return json(page([organizationUser()]));
        if (
          path === `/api/v1/organizations/${organizationId}/camps` &&
          method === "GET"
        )
          return json([
            {
              id: "30000000-0000-0000-0000-000000000041",
              organizationId,
              name: "Sommerfreizeit",
              slug: "sommerfreizeit",
              startsOn: "2027-08-01",
              endsOn: "2027-08-08",
              timeZoneId: "Europe/Berlin",
              defaultPortions: 40,
              status: 0,
              period: 0,
              version: 1,
            },
          ]);
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path.endsWith(`/administration/users/${userId}/membership`)) {
          membershipBody = await request.text();
          return json({
            ...organizationUser().organizations[0],
            status: 1,
            version: 6,
          });
        }
        if (path.includes(`/administration/users/${userId}/camps/`)) {
          campBody = await request.text();
          return json({
            campId: "30000000-0000-0000-0000-000000000041",
            campName: "Sommerfreizeit",
            role: 1,
            version: 1,
          });
        }
        return empty(404);
      }),
    );

    renderRoute("/o/ejw/verwaltung/benutzer");
    expect(await screen.findByText("Erika Muster")).toBeInTheDocument();
    await user.selectOptions(
      await screen.findByRole("combobox", {
        name: "Camp-Rolle für Erika Muster in Sommerfreizeit",
      }),
      "1",
    );
    await waitFor(() => expect(campBody).toContain('"role":1'));
    await user.click(
      screen.getByRole("button", { name: "In Organization sperren" }),
    );

    await waitFor(() => expect(membershipBody).toContain('"status":1'));
    expect(
      await screen.findByText("In dieser Organization gesperrt"),
    ).toBeInTheDocument();
  });

  it("shows a clear empty state for a completed search", async () => {
    vi.stubGlobal(
      "fetch",
      administrationFetch(({ path }) => {
        if (path === "/api/v1/superadmin/users") return json(page([]));
        if (path === "/api/v1/superadmin/organizations") return json([]);
        return empty(404);
      }),
    );

    renderRoute("/superadmin/benutzer");

    expect(
      await screen.findByText("Keine passenden Benutzer gefunden."),
    ).toBeInTheDocument();
  });

  it("labels an active Camp worker without Orgadmin rights as a member", async () => {
    vi.stubGlobal(
      "fetch",
      administrationFetch(({ path }) => {
        if (path === "/api/v1/account/memberships")
          return json([
            {
              organizationId,
              organizationName: "Evangelisches Jugendwerk",
              organizationSlug: "ejw",
              role: 1,
            },
          ]);
        if (path === `/api/v1/organizations/${organizationId}/camps`)
          return json([]);
        if (
          path ===
          `/api/v1/organizations/${organizationId}/administration/users`
        ) {
          const campWorker = {
            ...organizationUser(),
            organizations: [
              { ...organizationUser().organizations[0], role: null },
            ],
          };
          return json(page([campWorker]));
        }
        return empty(404);
      }),
    );

    renderRoute("/o/ejw/verwaltung/benutzer");

    expect(await screen.findByText("Mitglied · aktiv")).toBeInTheDocument();
  });

  it("covers suspended accounts, conflicts, Orgadmin links, search and pagination", async () => {
    const user = userEvent.setup();
    const copied: string[] = [];
    const loadedPages: string[] = [];
    vi.stubGlobal("navigator", {
      ...globalThis.navigator,
      onLine: true,
      clipboard: { writeText: vi.fn((value: string) => copied.push(value)) },
    });
    vi.stubGlobal(
      "fetch",
      administrationFetch(async ({ path, method, request }) => {
        if (path === "/api/v1/superadmin/organizations" && method === "GET")
          return json([
            {
              organizationId,
              name: "Evangelisches Jugendwerk",
              slug: "ejw",
              status: 0,
              version: 1,
            },
          ]);
        if (path === "/api/v1/superadmin/users" && method === "GET") {
          const url = new URL(request.url);
          loadedPages.push(url.searchParams.get("page") ?? "1");
          return json({
            items: [
              {
                ...globalUser(),
                firstName: "",
                lastName: "",
                accountStatus: 1,
                isSuperAdmin: true,
              },
              {
                ...globalUser(),
                userId: "10000000-0000-0000-0000-000000000042",
                email: "zweite@example.test",
                firstName: "Zweite",
                lastName: "Person",
              },
            ],
            page: Number(url.searchParams.get("page") ?? "1"),
            pageSize: 25,
            totalCount: 51,
          });
        }
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === `/api/v1/superadmin/users/${userId}/superadmin`)
          return json(
            { detail: "Der letzte Superadmin bleibt geschützt." },
            409,
          );
        if (path === `/api/v1/superadmin/users/${userId}/status`)
          return json({
            ...globalUser(),
            accountStatus: 0,
            isSuperAdmin: true,
            version: 4,
          });
        if (path === "/api/v1/invitations/links") {
          expect(await request.json()).toMatchObject({
            isSuperAdmin: false,
            organizationId,
            organizationRole: 0,
          });
          return json({
            id: "50000000-0000-0000-0000-000000000042",
            token: "orgadmin-token",
            grant: {
              isSuperAdmin: false,
              organizationId,
              organizationRole: 0,
              campId: null,
              campRole: null,
            },
            expiresAt: "2026-08-13T12:00:00Z",
            version: 1,
          });
        }
        return empty(404);
      }),
    );

    renderRoute("/superadmin/benutzer");
    expect(await screen.findByText("Ohne Namen")).toBeInTheDocument();
    expect(screen.getByText("Global gesperrt")).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", { name: "Superadmin entziehen" }),
    );
    expect(
      await screen.findByText("Der letzte Superadmin bleibt geschützt."),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Entsperren" }));
    expect((await screen.findAllByText("Aktiv")).length).toBeGreaterThan(0);

    await user.selectOptions(
      screen.getByRole("combobox", {
        name: "Rolle des nächsten Einladungslinks",
      }),
      "orgadmin",
    );
    await user.click(
      screen.getByRole("button", { name: "Orgadmin-Link kopieren" }),
    );
    await waitFor(() => expect(copied[0]).toContain("orgadmin-token"));

    await user.click(screen.getByRole("button", { name: "Weiter" }));
    expect(await screen.findByText("Seite 2")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Weiter" }));
    expect(await screen.findByText("Seite 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Weiter" })).toBeDisabled();
    await user.type(screen.getByLabelText("Name oder E-Mail"), "erika");
    await user.click(screen.getByRole("button", { name: "Suchen" }));
    await waitFor(() => expect(loadedPages).toContain("2"));
  });
});

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

function globalUser() {
  return {
    userId,
    email: "erika@example.test",
    firstName: "Erika",
    lastName: "Muster",
    accountStatus: 0,
    isSuperAdmin: false,
    loginLockedUntil: null,
    organizations: [],
    version: 3,
  };
}

function organizationUser() {
  return {
    ...globalUser(),
    organizations: [
      {
        organizationId,
        organizationName: "Evangelisches Jugendwerk",
        organizationSlug: "ejw",
        status: 0,
        role: 0,
        camps: [],
        version: 5,
      },
    ],
  };
}

function page(items: components["schemas"]["UserAdministrationView"][]) {
  return { items, page: 1, pageSize: 25, totalCount: items.length };
}

function administrationFetch(
  route: (request: {
    path: string;
    method: string;
    request: Request;
  }) => Response | Promise<Response>,
) {
  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request =
      input instanceof Request
        ? new Request(input, init)
        : new Request(new URL(input.toString(), "http://localhost"), init);
    const url = new URL(request.url, "http://localhost");
    return route({ path: url.pathname, method: request.method, request });
  });
}

function json(value: unknown, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function empty(status: number) {
  return new Response(null, { status });
}
