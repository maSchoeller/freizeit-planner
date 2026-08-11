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
  it("shows First Login only while the installation is still uninitialized", async () => {
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path }) =>
        path === "/api/v1/auth/first-login"
          ? json({ available: false })
          : empty(404),
      ),
    );

    renderRoute("/anmelden");

    await waitFor(() =>
      expect(
        screen.queryByRole("link", { name: "Erste Einrichtung" }),
      ).toBeNull(),
    );
  });

  it("returns to the requested internal page after login", async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method }) => {
        if (path === "/api/v1/auth/first-login")
          return json({ available: false });
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/auth/login" && method === "POST")
          return json({ accessToken: "access.jwt" });
        if (path === "/api/v1/superadmin/users")
          return json({ items: [], page: 1, pageSize: 25, totalCount: 0 });
        if (path === "/api/v1/superadmin/organizations") return json([]);
        return empty(404);
      }),
    );

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter
          initialEntries={[
            {
              pathname: "/anmelden",
              state: { returnTo: "/superadmin/benutzer" },
            },
          ]}
        >
          <App />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    await user.type(
      screen.getByLabelText("E-Mail-Adresse"),
      "lea@example.test",
    );
    await user.type(
      screen.getByLabelText("Passwort"),
      "Eine sichere Passphrase",
    );
    await user.click(screen.getByRole("button", { name: "Anmelden" }));

    expect(
      await screen.findByRole("heading", { name: "Benutzer verwalten" }),
    ).toBeInTheDocument();
  });

  it("exposes account areas as direct navigation instead of one long page", async () => {
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path }) => {
        if (path === "/api/v1/account")
          return json({
            id: userId,
            email: "lea@example.test",
            displayName: "Lea Beispiel",
            firstName: "Lea",
            lastName: "Beispiel",
            deletionScheduledAt: null,
            isSuperAdmin: false,
            version: 1,
          });
        if (path === "/api/v1/account/memberships") return json([]);
        if (path === "/api/v1/auth/sessions") return json([]);
        return empty(404);
      }),
    );

    renderRoute("/konto/sicherheit");

    expect(
      await screen.findByRole("navigation", { name: "Kontobereiche" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Sicherheit" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(
      screen.getByRole("heading", { name: "Passwort ändern" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Profil" })).toBeNull();
  });

  it("completes password login with a persistent refresh session", async () => {
    const user = userEvent.setup();
    let loginBody = "";
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method, body }) => {
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/auth/login" && method === "POST") {
          loginBody = body;
          return json({
            accessToken: "access.jwt",
            expiresAt: "2026-08-10T12:15:00Z",
          });
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
    await user.type(
      screen.getByLabelText("Passwort"),
      "Eine sichere Passphrase",
    );
    await user.click(
      screen.getByRole("checkbox", {
        name: /Auf diesem Gerät angemeldet bleiben/,
      }),
    );
    await user.click(screen.getByRole("button", { name: "Anmelden" }));
    await waitFor(() => expect(loginBody).toContain('"rememberMe":true'));
    expect(loginBody).toContain('"password":"Eine sichere Passphrase"');
    expect(localStorage).toHaveLength(0);
  });

  it("keeps login values and explains invalid credentials", async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path }) => {
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/auth/login")
          return json(
            { detail: "E-Mail-Adresse oder Passwort ist nicht korrekt." },
            401,
          );
        return empty(404);
      }),
    );

    renderRoute("/anmelden");
    const email = screen.getByLabelText("E-Mail-Adresse");
    await user.type(email, "lea@example.test");
    await user.type(screen.getByLabelText("Passwort"), "Falsches Passwort");
    await user.click(screen.getByRole("button", { name: "Anmelden" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "E-Mail-Adresse oder Passwort ist nicht korrekt.",
    );
    expect(email).toHaveValue("lea@example.test");
    expect(screen.getByLabelText("Passwort")).toHaveValue("Falsches Passwort");
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
    await user.type(
      screen.getByLabelText("Passwort"),
      "Eine sichere Passphrase",
    );
    await user.click(screen.getByRole("button", { name: "Anmelden" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Sicherheits-Token fehlt",
    );
  });

  it("updates the profile and email, leaves an organization and schedules account deletion", async () => {
    const user = userEvent.setup();
    const account = {
      id: "10000000-0000-0000-0000-000000000001",
      email: "miriam@example.test",
      displayName: "Miriam König",
      firstName: "Miriam",
      lastName: "König",
      deletionScheduledAt: null,
      isSuperAdmin: false,
      version: 1,
    };
    const fetchMock = routeFetch(({ path, method }) => {
      if (path === "/api/v1/account" && method === "GET") return json(account);
      if (path === "/api/v1/account/memberships")
        return json([
          {
            organizationId,
            organizationName: "CVJM Sonnenhöhe",
            organizationSlug: "sonnenhoehe",
            role: 1,
          },
        ]);
      if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
      if (path === "/api/v1/account/profile" && method === "PATCH")
        return json({
          ...account,
          displayName: "Miriam Neu",
          firstName: "Miriam",
          lastName: "Neu",
          version: 2,
        });
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
    const lastName = await screen.findByLabelText("Nachname");
    await user.clear(lastName);
    await user.type(lastName, "Neu");
    await user.click(screen.getByRole("button", { name: "Namen speichern" }));
    await waitFor(() => expect(lastName).toHaveValue("Neu"));

    await user.click(screen.getByRole("link", { name: "Sicherheit" }));

    await user.type(
      await screen.findByLabelText("Neue E-Mail-Adresse"),
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

    await user.click(screen.getByRole("link", { name: "Organisationen" }));

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

    await user.click(screen.getByRole("link", { name: "Datenschutz" }));
    await user.click(
      await screen.findByRole("button", {
        name: "Konto zur Löschung vormerken",
      }),
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
            firstName: "Super",
            lastName: "Admin",
            deletionScheduledAt: "2026-08-09T12:00:00Z",
            isSuperAdmin: true,
            version: 1,
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

    renderRoute("/konto/datenschutz");
    await user.click(
      await screen.findByRole("button", { name: "Löschung abbrechen" }),
    );
    await expect(
      screen.findByRole("button", { name: "Konto zur Löschung vormerken" }),
    ).resolves.toBeInTheDocument();
    await user.click(screen.getByRole("link", { name: "Profil" }));
    await user.click(screen.getByRole("button", { name: "Namen speichern" }));
    await expect(screen.findByRole("alert")).resolves.toHaveTextContent(
      "Der Name ist bereits vergeben.",
    );
    expect(
      screen.getAllByRole("link", { name: "Plattform verwalten" })[0],
    ).toBeInTheDocument();
  });

  it("lists sessions, revokes all other sessions and then the current session", async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method }) => {
        if (path === "/api/v1/account" && method === "GET")
          return json({
            id: userId,
            email: "lea@example.test",
            displayName: "Lea Beispiel",
            firstName: "Lea",
            lastName: "Beispiel",
            deletionScheduledAt: null,
            isSuperAdmin: false,
            version: 1,
          });
        if (path === "/api/v1/account/memberships") return json([]);
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

    renderRoute("/konto/sicherheit");
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

  it("previews a transferable invitation and registers a new global account", async () => {
    const user = userEvent.setup();
    let registrationBody = "";
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method, body }) => {
        if (path === "/api/v1/invitations/einmal-token/preview")
          return json({
            grant: {
              isSuperAdmin: false,
              organizationId,
              organizationRole: 0,
              campId: null,
              campRole: null,
            },
            organizationName: "CVJM Sonnenhöhe",
            campName: null,
            expiresAt: "2026-08-13T12:00:00Z",
            status: 0,
          });
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/auth/refresh") return empty(401);
        if (
          path === "/api/v1/invitations/einmal-token/register" &&
          method === "POST"
        ) {
          registrationBody = body;
          return empty(202);
        }
        return empty(404);
      }),
    );

    renderRoute("/einladung?token=einmal-token");
    await screen.findByText(/Orgadmin für CVJM Sonnenhöhe/);
    await user.type(screen.getByLabelText("Vorname"), "Neue");
    await user.type(screen.getByLabelText("Nachname"), "Person");
    await user.type(
      screen.getByLabelText("E-Mail-Adresse"),
      "neu@example.test",
    );
    await user.type(
      screen.getByLabelText("Passwort", { exact: true }),
      "Eine sichere Registrierungs-Passphrase",
    );
    await user.type(
      screen.getByLabelText("Passwort bestätigen"),
      "Eine sichere Registrierungs-Passphrase",
    );
    await user.click(screen.getByRole("button", { name: "Konto erstellen" }));
    await expect(
      screen.findByRole("heading", { name: "E-Mail-Adresse bestätigen" }),
    ).resolves.toBeInTheDocument();
    expect(registrationBody).toContain('"firstName":"Neue"');
    expect(registrationBody).toContain('"passwordConfirmation"');
    expect(localStorage).toHaveLength(0);
  });

  it("confirms invitation registration and keeps tokens out of browser storage", async () => {
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method }) => {
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/invitations/confirm" && method === "POST")
          return json({
            accessToken: "invitation.access.jwt",
            expiresAt: "2026-08-11T12:15:00Z",
          });
        return empty(404);
      }),
    );

    renderRoute("/einladung-bestaetigen?token=bestaetigung");

    await expect(
      screen.findByRole("heading", { name: "E-Mail-Adresse bestätigt" }),
    ).resolves.toBeInTheDocument();
    expect(localStorage).toHaveLength(0);
    expect(sessionStorage).toHaveLength(0);
  });

  it("shows a clear state for revoked transferable invitations", async () => {
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path }) =>
        path === "/api/v1/invitations/widerrufen/preview"
          ? json({
              grant: {
                isSuperAdmin: true,
                organizationId: null,
                organizationRole: null,
                campId: null,
                campRole: null,
              },
              organizationName: null,
              campName: null,
              expiresAt: "2026-08-11T12:00:00Z",
              status: 3,
            })
          : empty(404),
      ),
    );

    renderRoute("/einladung?token=widerrufen");
    await expect(screen.findByRole("alert")).resolves.toHaveTextContent(
      "widerrufen",
    );
  });

  it.each([
    [1, 0, "Campleitung", "reserviert"],
    [2, 1, "Mitarbeit", "bereits verwendet"],
    [4, 2, "Leserechte", "abgelaufen"],
  ])(
    "shows invitation status %s and camp role %s",
    async (status, campRole, roleText, statusText) => {
      vi.stubGlobal(
        "fetch",
        routeFetch(({ path }) =>
          path === "/api/v1/invitations/status/preview"
            ? json({
                grant: {
                  isSuperAdmin: false,
                  organizationId,
                  organizationRole: null,
                  campId: "30000000-0000-0000-0000-000000000001",
                  campRole,
                },
                organizationName: "CVJM Sonnenhöhe",
                campName: "Sommerfreizeit",
                expiresAt: "2026-08-13T12:00:00Z",
                status,
              })
            : empty(404),
        ),
      );

      renderRoute("/einladung?token=status");

      await expect(
        screen.findByText(new RegExp(roleText)),
      ).resolves.toBeInTheDocument();
      await expect(screen.findByRole("alert")).resolves.toHaveTextContent(
        statusText,
      );
    },
  );

  it("lets a signed-in user attach a grant to the existing global account", async () => {
    const user = userEvent.setup();
    let refreshCompleted = false;
    let authenticatedAntiforgeryRequested = false;
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method, headers }) => {
        if (path === "/api/v1/invitations/bestehend/preview")
          return json({
            grant: {
              isSuperAdmin: false,
              organizationId,
              organizationRole: 0,
              campId: null,
              campRole: null,
            },
            organizationName: "CVJM Sonnenhöhe",
            campName: null,
            expiresAt: "2026-08-13T12:00:00Z",
            status: 0,
          });
        if (path === "/api/v1/auth/antiforgery") {
          if (refreshCompleted)
            authenticatedAntiforgeryRequested =
              headers.get("Authorization") === "Bearer restored.jwt";
          return json({ token: "csrf" });
        }
        if (path === "/api/v1/auth/refresh" && method === "POST") {
          refreshCompleted = true;
          return json({ accessToken: "restored.jwt" });
        }
        if (
          path === "/api/v1/invitations/bestehend/accept" &&
          method === "POST"
        )
          return json({ outcome: 0, grant: { isSuperAdmin: false } });
        return empty(404);
      }),
    );

    renderRoute("/einladung?token=bestehend");
    await user.click(
      await screen.findByRole("button", { name: "Einladung annehmen" }),
    );

    await expect(
      screen.findByRole("heading", { name: "Einladung angenommen" }),
    ).resolves.toBeInTheDocument();
    expect(authenticatedAntiforgeryRequested).toBe(true);
    expect(screen.queryByLabelText("E-Mail-Adresse")).not.toBeInTheDocument();
  });

  it("keeps registration input when passwords differ", async () => {
    const user = userEvent.setup();
    let registered = false;
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method }) => {
        if (path === "/api/v1/invitations/abweichend/preview")
          return availableSuperAdminPreview();
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/auth/refresh") return empty(401);
        if (path.endsWith("/register") && method === "POST") {
          registered = true;
          return empty(202);
        }
        return empty(404);
      }),
    );
    renderRoute("/einladung?token=abweichend");
    await user.type(await screen.findByLabelText("Vorname"), "Neue");
    await user.type(screen.getByLabelText("Nachname"), "Person");
    await user.type(
      screen.getByLabelText("E-Mail-Adresse"),
      "neu@example.test",
    );
    await user.type(
      screen.getByLabelText("Passwort", { exact: true }),
      "Eine sichere Registrierungs-Passphrase",
    );
    await user.type(
      screen.getByLabelText("Passwort bestätigen"),
      "Eine andere sichere Registrierungs-Passphrase",
    );

    await user.click(screen.getByRole("button", { name: "Konto erstellen" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "stimmen nicht überein",
    );
    expect(registered).toBe(false);
    expect(screen.getByLabelText("E-Mail-Adresse")).toHaveValue(
      "neu@example.test",
    );
  });

  it("explains failed email confirmation and incomplete invitation links", async () => {
    const view = renderRoute("/einladung");
    await expect(screen.findByRole("alert")).resolves.toHaveTextContent(
      "unvollständig",
    );
    view.unmount();
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path }) => {
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/invitations/confirm")
          return json(
            { detail: "Der Bestätigungslink wurde bereits verwendet." },
            400,
          );
        return empty(404);
      }),
    );

    renderRoute("/einladung-bestaetigen?token=verwendet");

    await expect(screen.findByRole("alert")).resolves.toHaveTextContent(
      "bereits verwendet",
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

  it("changes organization metadata without exposing tenant content", async () => {
    const user = userEvent.setup();
    vi.stubGlobal(
      "fetch",
      routeFetch(({ path, method }) => {
        if (path === "/api/v1/superadmin/organizations" && method === "GET")
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
          path.endsWith(`/superadmin/organizations/${organizationId}/status`) &&
          method === "PATCH"
        )
          return json({ status: 1, version: 3 });
        return empty(404);
      }),
    );

    renderRoute("/superadmin/organisationen");
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
    headers: Headers;
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
    const headers =
      input instanceof Request ? input.headers : new Headers(init?.headers);
    return handler({
      path: new URL(url, "https://localhost").pathname,
      method,
      body,
      headers,
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

function availableSuperAdminPreview() {
  return json({
    grant: {
      isSuperAdmin: true,
      organizationId: null,
      organizationRole: null,
      campId: null,
      campRole: null,
    },
    organizationName: null,
    campName: null,
    expiresAt: "2026-08-13T12:00:00Z",
    status: 0,
  });
}
