import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  localStorage.clear();
});

describe("Dashboard", () => {
  it("exposes the next plan and core navigation in German", () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    queryClient.setQueryData(
      ["camp-workspace", "sonnenhoehe", "sommerfreizeit-2026"],
      {
        organizationId: "20000000-0000-0000-0000-000000000001",
        organizationName: "Sonnenhöhe e. V.",
        organizationSlug: "sonnenhoehe",
        organizationRole: 1,
        campId: "30000000-0000-0000-0000-000000000001",
        campSlug: "sommerfreizeit-2026",
        campBase: "/o/sonnenhoehe/camps/sommerfreizeit-2026",
        camp: {
          id: "30000000-0000-0000-0000-000000000001",
          organizationId: "20000000-0000-0000-0000-000000000001",
          name: "Sommerfreizeit 2026",
          slug: "sommerfreizeit-2026",
          description: "Gemeinsame Woche am See",
          startsOn: "2026-08-01",
          endsOn: "2026-08-08",
          timeZoneId: "Europe/Berlin",
          defaultPortions: 42,
          status: 0,
          period: 1,
          version: 4,
        },
      },
    );
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter
          initialEntries={["/o/sonnenhoehe/camps/sommerfreizeit-2026"]}
        >
          <App />
        </MemoryRouter>
      </QueryClientProvider>,
    );
    expect(
      screen.getByRole("heading", { name: /Tagesplan$/ }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("navigation", { name: "Camp-Navigation" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Hilfe & Anleitung" }),
    ).toHaveAttribute("href", "/hilfe/");
  });

  it("starts passwordless login with an explicitly labelled and focused email field", () => {
    render(
      <MemoryRouter initialEntries={["/anmelden"]}>
        <App />
      </MemoryRouter>,
    );

    expect(
      screen.getByRole("heading", { name: "Im Freizeit-Cockpit anmelden" }),
    ).toBeInTheDocument();
    const email = screen.getByRole("textbox", { name: "E-Mail-Adresse" });
    expect(email).toHaveAttribute("autocomplete", "email");
    expect(email).toHaveFocus();
    expect(
      screen.getByRole("button", { name: "Anmeldecode anfordern" }),
    ).toBeInTheDocument();
  });

  it("keeps an invalid login address in the form with an associated error", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    render(
      <MemoryRouter initialEntries={["/anmelden"]}>
        <App />
      </MemoryRouter>,
    );

    const email = screen.getByRole("textbox", { name: "E-Mail-Adresse" });
    await user.type(email, "keine-adresse");
    await user.click(
      screen.getByRole("button", { name: "Anmeldecode anfordern" }),
    );

    expect(email).toHaveAttribute("aria-invalid", "true");
    expect(email).toHaveAccessibleDescription(
      "Gib eine gültige E-Mail-Adresse ein.",
    );
    expect(email).toHaveFocus();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("offers an understandable loading state while sessions are retrieved", () => {
    render(
      <MemoryRouter initialEntries={["/konto/sitzungen"]}>
        <App />
      </MemoryRouter>,
    );

    expect(
      screen.getByRole("heading", { name: "Aktive Sitzungen" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent(
      "Sitzungen werden geladen",
    );
  });

  it("explains an incomplete invitation link", () => {
    render(
      <MemoryRouter initialEntries={["/einladung"]}>
        <App />
      </MemoryRouter>,
    );
    expect(
      screen.getByRole("heading", { name: "Einladung annehmen" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent(
      "Einladungslink ist unvollständig",
    );
  });

  it("shows a loading state for account self-service", () => {
    render(
      <MemoryRouter initialEntries={["/konto"]}>
        <App />
      </MemoryRouter>,
    );
    expect(
      screen.getByRole("heading", { name: "Mein Konto" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent(
      "Kontodaten werden geladen",
    );
  });

  it("keeps identity and administration unavailable offline", () => {
    vi.spyOn(window.navigator, "onLine", "get").mockReturnValue(false);
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    render(
      <MemoryRouter initialEntries={["/konto"]}>
        <App />
      </MemoryRouter>,
    );

    expect(
      screen.getByRole("heading", { name: "Offline nicht verfügbar" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Mein Konto" })).toBeNull();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("clears the offline snapshot when the user logs out", async () => {
    localStorage.setItem("freizeit-cockpit:offline:v1", "cached planning");
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path =
          typeof request === "string"
            ? request
            : request instanceof URL
              ? request.toString()
              : request.url;
        const pathname = new URL(path, "https://localhost").pathname;
        if (pathname === "/api/v1/account")
          return Promise.resolve(
            new Response(
              JSON.stringify({
                id: "10000000-0000-0000-0000-000000000001",
                email: "lea@example.test",
                displayName: "Lea Beispiel",
                deletionScheduledAt: null,
                isPlatformAdmin: false,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (pathname === "/api/v1/account/memberships")
          return Promise.resolve(
            new Response(JSON.stringify([]), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (pathname === "/api/v1/auth/antiforgery")
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        const method =
          init?.method ?? (request instanceof Request ? request.method : "GET");
        if (pathname === "/api/v1/auth/logout" && method === "POST")
          return Promise.resolve(new Response(null, { status: 204 }));
        return Promise.resolve(new Response(null, { status: 404 }));
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    render(
      <MemoryRouter initialEntries={["/konto"]}>
        <App />
      </MemoryRouter>,
    );

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
    await user.click(await screen.findByRole("button", { name: "Abmelden" }));

    await waitFor(() =>
      expect(
        screen.getByRole("heading", { name: "Im Freizeit-Cockpit anmelden" }),
      ).toBeInTheDocument(),
    );
    expect(localStorage.getItem("freizeit-cockpit:offline:v1")).toBeNull();
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/v1/auth/logout",
      expect.objectContaining({
        method: "POST",
        headers: { "X-CSRF-TOKEN": "csrf-token" },
      }),
    );
  });

  it("shows an accessible loading state for member administration", () => {
    render(
      <MemoryRouter
        initialEntries={[
          "/o/sonnenhoehe/einstellungen/mitglieder?organizationId=20000000-0000-0000-0000-000000000001",
        ]}
      >
        <App />
      </MemoryRouter>,
    );
    expect(
      screen.getByRole("heading", { name: "Mitglieder verwalten" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent(
      "Mitglieder werden geladen",
    );
  });

  it("explains the platform metadata boundary", () => {
    render(
      <MemoryRouter initialEntries={["/plattform/organisationen"]}>
        <App />
      </MemoryRouter>,
    );
    expect(
      screen.getByRole("heading", { name: "Organizations" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/Fachliche Inhalte der Mandanten/),
    ).toBeInTheDocument();
  });
});
