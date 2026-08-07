import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { App } from "./App";

describe("Dashboard", () => {
  it("exposes the next plan and core navigation in German", () => {
    render(
      <MemoryRouter
        initialEntries={["/o/sonnenhoehe/camps/sommerfreizeit-2026"]}
      >
        <App />
      </MemoryRouter>,
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
});
