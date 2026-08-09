import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { App } from "./App";

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
      screen.getByRole("heading", { name: "Heute im Tagesplan" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("navigation", { name: "Camp-Navigation" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Hilfe & Anleitung" }),
    ).toHaveAttribute("href", "/hilfe/");
  });

  it("starts passwordless login with an explicitly labelled email field", () => {
    render(
      <MemoryRouter initialEntries={["/anmelden"]}>
        <App />
      </MemoryRouter>,
    );

    expect(
      screen.getByRole("heading", { name: "Im Freizeit-Cockpit anmelden" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("textbox", { name: "E-Mail-Adresse" }),
    ).toHaveAttribute("autocomplete", "email");
    expect(
      screen.getByRole("button", { name: "Anmeldecode anfordern" }),
    ).toBeInTheDocument();
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
