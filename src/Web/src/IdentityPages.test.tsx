import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";

const organizationId = "20000000-0000-0000-0000-000000000001";
const userId = "10000000-0000-0000-0000-000000000002";

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  localStorage.clear();
});

describe("identity self-service pages", () => {
  it("completes passwordless login with a sanitized code and persistent session", async () => {
    const user = userEvent.setup();
    let verificationBody = "";
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method, body }) => {
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/auth/code" && method === "POST")
          return empty(202);
        if (path === "/api/v1/auth/verify" && method === "POST") {
          verificationBody = body;
          return empty(204);
        }
        if (path === "/api/v1/account/memberships") return json([]);
        return empty(404);
      }),
    );

    renderRoute("/anmelden");
    await user.type(
      screen.getByLabelText("E-Mail-Adresse"),
      "lea@example.test",
    );
    await user.click(
      screen.getByRole("button", { name: "Anmeldecode anfordern" }),
    );
    const code = await screen.findByLabelText("Sechsstelliger Anmeldecode");
    expect(
      screen.getByRole("heading", { name: "Anmeldecode eingeben" }),
    ).toHaveFocus();
    await user.type(code, "12a34567");
    expect(code).toHaveValue("123456");
    await user.click(
      screen.getByRole("checkbox", { name: /Angemeldet bleiben/ }),
    );
    await user.click(screen.getByRole("button", { name: "Anmelden" }));
    await waitFor(() =>
      expect(verificationBody).toContain('"rememberMe":true'),
    );
  });

  it("keeps the login context when requesting or verifying a code fails", async () => {
    const user = userEvent.setup();
    let codeRequests = 0;
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path }) => {
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/auth/code") {
          codeRequests += 1;
          return codeRequests === 1 ? empty(503) : empty(202);
        }
        if (path === "/api/v1/auth/verify")
          return json({ detail: "Der Code wurde bereits verwendet." }, 400);
        return empty(404);
      }),
    );

    renderRoute("/anmelden");
    const email = screen.getByLabelText("E-Mail-Adresse");
    await user.type(email, "lea@example.test");
    await user.click(
      screen.getByRole("button", { name: "Anmeldecode anfordern" }),
    );
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Die Anmeldung ist gerade nicht erreichbar",
    );
    await user.click(
      screen.getByRole("button", { name: "Anmeldecode anfordern" }),
    );
    await user.type(
      await screen.findByLabelText("Sechsstelliger Anmeldecode"),
      "123456",
    );
    await user.click(screen.getByRole("button", { name: "Anmelden" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Der Code wurde bereits verwendet.",
    );
    await user.click(
      screen.getByRole("button", { name: "E-Mail-Adresse ändern" }),
    );
    expect(screen.getByLabelText("E-Mail-Adresse")).toHaveValue(
      "lea@example.test",
    );
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("rejects malformed antiforgery responses during login", async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path }) =>
        path === "/api/v1/auth/antiforgery" ? json({ token: 42 }) : empty(404),
      ),
    );

    renderRoute("/anmelden");
    await user.type(
      screen.getByLabelText("E-Mail-Adresse"),
      "lea@example.test",
    );
    await user.click(
      screen.getByRole("button", { name: "Anmeldecode anfordern" }),
    );
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Die Anmeldung ist gerade nicht erreichbar",
    );
  });

  it("updates the profile and email, leaves an organization and schedules account deletion", async () => {
    const user = userEvent.setup();
    const account = {
      id: "10000000-0000-0000-0000-000000000001",
      email: "miriam@example.test",
      displayName: "Miriam König",
      deletionScheduledAt: null,
      isPlatformAdmin: false,
    };
    const fetchMock = routeFetch(({ path, method }) => {
      if (path === "/api/v1/account" && method === "GET") return json(account);
      if (path === "/api/v1/account/memberships")
        return json([
          {
            organizationId,
            organizationName: "CVJM Sonnenhöhe",
            organizationSlug: "sonnenhoehe",
            role: 0,
          },
        ]);
      if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
      if (path === "/api/v1/account/profile" && method === "PATCH")
        return json({ ...account, displayName: "Miriam Neu" });
      if (path === "/api/v1/account/email-change" && method === "POST")
        return json({ message: "Code versendet" }, 202);
      if (path === "/api/v1/account/email-change/confirm" && method === "POST")
        return json({ outcome: 0, email: "neu@example.test" });
      if (
        path.endsWith(`/organizations/${organizationId}/leave`) &&
        method === "POST"
      )
        return empty(204);
      if (path === "/api/v1/account/deletion" && method === "POST")
        return json({ scheduledAt: "2026-08-10T12:00:00Z" });
      return empty(404);
    });
    vi.stubGlobal("fetch", fetchMock);

    renderRoute("/konto");
    const name = await screen.findByLabelText("Anzeigename");
    await user.clear(name);
    await user.type(name, "Miriam Neu");
    await user.click(
      screen.getByRole("button", { name: "Anzeigename speichern" }),
    );
    await waitFor(() => expect(name).toHaveValue("Miriam Neu"));

    await user.type(
      screen.getByLabelText("Neue E-Mail-Adresse"),
      "neu@example.test",
    );
    await user.click(
      screen.getByRole("button", { name: "Einmalcode anfordern" }),
    );
    await user.type(await screen.findByLabelText("Einmalcode"), "123456");
    await user.click(
      screen.getByRole("button", { name: "Adresse bestätigen" }),
    );
    await waitFor(() =>
      expect(
        screen.getByText("Anmeldung: neu@example.test"),
      ).toBeInTheDocument(),
    );

    localStorage.setItem(
      "freizeit-cockpit:offline:organization:v1",
      organizationId,
    );
    localStorage.setItem("freizeit-cockpit:offline:v1", "snapshot");
    await user.click(
      screen.getByRole("button", { name: "Organisation verlassen" }),
    );
    await waitFor(() =>
      expect(
        screen.getByText("Keine aktive Mitgliedschaft."),
      ).toBeInTheDocument(),
    );
    expect(localStorage.getItem("freizeit-cockpit:offline:v1")).toBeNull();

    await user.click(
      screen.getByRole("button", { name: "Konto zur Löschung vormerken" }),
    );
    await user.click(screen.getByRole("button", { name: "Konto vormerken" }));
    await expect(
      screen.findByText(/endgültige Löschung erfolgt nach 30 Tagen/),
    ).resolves.toBeInTheDocument();
  });

  it("cancels a scheduled deletion and reports a failed mutation", async () => {
    const user = userEvent.setup();
    let failProfile = true;
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method }) => {
        if (path === "/api/v1/account" && method === "GET")
          return json({
            id: userId,
            email: "admin@example.test",
            displayName: "Admin",
            deletionScheduledAt: "2026-08-09T12:00:00Z",
            isPlatformAdmin: true,
          });
        if (path === "/api/v1/account/memberships") return json([]);
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/account/deletion" && method === "DELETE")
          return empty(204);
        if (
          path === "/api/v1/account/profile" &&
          method === "PATCH" &&
          failProfile
        ) {
          failProfile = false;
          return json({ detail: "Der Name ist bereits vergeben." }, 409);
        }
        return empty(404);
      }),
    );

    renderRoute("/konto");
    await user.click(
      await screen.findByRole("button", { name: "Löschung abbrechen" }),
    );
    await expect(
      screen.findByRole("button", { name: "Konto zur Löschung vormerken" }),
    ).resolves.toBeInTheDocument();
    await user.click(
      screen.getByRole("button", { name: "Anzeigename speichern" }),
    );
    await expect(screen.findByRole("alert")).resolves.toHaveTextContent(
      "Der Name ist bereits vergeben.",
    );
    expect(
      screen.getByRole("link", { name: /Organizations auf Plattformebene/ }),
    ).toBeInTheDocument();
  });

  it("lists sessions, revokes all other sessions and then the current session", async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method }) => {
        if (path === "/api/v1/auth/sessions" && method === "GET")
          return json([
            {
              id: "40000000-0000-0000-0000-000000000001",
              createdAt: "2026-08-10T08:00:00Z",
              expiresAt: "2026-08-10T20:00:00Z",
              ipAddress: "127.0.0.1",
              isCurrent: true,
            },
            {
              id: "40000000-0000-0000-0000-000000000002",
              createdAt: "2026-08-09T08:00:00Z",
              expiresAt: "2026-08-11T20:00:00Z",
              ipAddress: "192.0.2.1",
              isCurrent: false,
            },
          ]);
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/auth/sessions/revoke-others" && method === "POST")
          return empty(204);
        if (
          path.endsWith("40000000-0000-0000-0000-000000000001") &&
          method === "DELETE"
        )
          return empty(204);
        return empty(404);
      }),
    );
    localStorage.setItem("freizeit-cockpit:offline:v1", "snapshot");

    renderRoute("/konto/sitzungen");
    await user.click(
      await screen.findByRole("button", { name: "Alle anderen widerrufen" }),
    );
    await waitFor(() =>
      expect(screen.queryByText("Weitere Sitzung")).toBeNull(),
    );
    await user.click(
      screen.getByRole("button", { name: "Sitzung widerrufen" }),
    );
    await expect(
      screen.findByRole("heading", { name: "Im Freizeit-Cockpit anmelden" }),
    ).resolves.toBeInTheDocument();
    expect(localStorage.getItem("freizeit-cockpit:offline:v1")).toBeNull();
  });

  it("accepts an invitation and explains an API rejection", async () => {
    const user = userEvent.setup();
    let accepted = true;
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path }) => {
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/invitations/accept")
          return accepted
            ? empty(204)
            : json({ detail: "Die Einladung wurde widerrufen." }, 400);
        return empty(404);
      }),
    );

    const view = renderRoute("/einladung?token=einmal-token");
    await user.type(screen.getByLabelText("Anzeigename"), "Neue Person");
    await user.click(
      screen.getByRole("button", { name: "Einladung annehmen" }),
    );
    await expect(
      screen.findByRole("heading", { name: "Willkommen im Team" }),
    ).resolves.toBeInTheDocument();

    accepted = false;
    view.unmount();
    renderRoute("/einladung?token=widerrufen");
    await user.type(screen.getByLabelText("Anzeigename"), "Neue Person");
    await user.click(
      screen.getByRole("button", { name: "Einladung annehmen" }),
    );
    await expect(screen.findByRole("alert")).resolves.toHaveTextContent(
      "Die Einladung wurde widerrufen.",
    );
  });

  it("changes and removes organization members with version headers", async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method }) => {
        if (
          path === `/api/v1/organizations/${organizationId}/members` &&
          method === "GET"
        )
          return json([
            {
              userId,
              displayName: "Alex",
              email: "alex@example.test",
              role: 3,
              version: 4,
            },
          ]);
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path.endsWith(`/members/${userId}/role`) && method === "PATCH")
          return json({
            userId,
            displayName: "Alex",
            email: "alex@example.test",
            role: 4,
            version: 5,
          });
        if (path.endsWith(`/members/${userId}`) && method === "DELETE")
          return empty(204);
        return empty(404);
      }),
    );

    renderRoute(
      `/o/sonnenhoehe/einstellungen/mitglieder?organizationId=${organizationId}`,
    );
    const role = await screen.findByRole("combobox", {
      name: "Rolle für Alex",
    });
    await user.selectOptions(role, "4");
    await waitFor(() => expect(role).toHaveValue("4"));
    await user.click(screen.getByRole("button", { name: "Entfernen" }));
    await waitFor(() =>
      expect(
        screen.getByText("Keine Mitglieder gefunden."),
      ).toBeInTheDocument(),
    );
  });

  it("changes platform metadata without exposing tenant content", async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method }) => {
        if (path === "/api/v1/platform/organizations" && method === "GET")
          return json([
            {
              organizationId,
              name: "CVJM Sonnenhöhe",
              slug: "sonnenhoehe",
              status: 0,
              version: 2,
            },
          ]);
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (
          path.endsWith(`/platform/organizations/${organizationId}/status`) &&
          method === "PATCH"
        )
          return json({ status: 1, version: 3 });
        return empty(404);
      }),
    );

    renderRoute("/plattform/organisationen");
    const item = await screen.findByText("CVJM Sonnenhöhe");
    const row = item.closest("li");
    expect(row).not.toBeNull();
    await user.click(within(row!).getByRole("button", { name: "Sperren" }));
    await waitFor(() =>
      expect(within(row!).getByText(/Gesperrt/)).toBeInTheDocument(),
    );
    expect(
      screen.getByText(/Fachliche Inhalte der Mandanten/),
    ).toBeInTheDocument();
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

function routeFetch(
  handler: (request: {
    path: string;
    method: string;
    body: string;
  }) => Response,
) {
  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url =
      typeof input === "string"
        ? input
        : input instanceof URL
          ? input.toString()
          : input.url;
    const method =
      init?.method ?? (input instanceof Request ? input.method : "GET");
    const body =
      input instanceof Request
        ? await input.clone().text()
        : typeof init?.body === "string"
          ? init.body
          : "";
    return handler({
      path: new URL(url, "https://localhost").pathname,
      method,
      body,
    });
  });
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function empty(status: number) {
  return new Response(null, { status });
}
