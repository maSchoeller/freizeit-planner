import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { PasswordResetPage } from "./PasswordResetPage";

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("password reset", () => {
  it("shows the same confirmation after requesting a reset", async () => {
    const user = userEvent.setup();
    let body = "";
    vi.stubGlobal(
      "fetch",
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const path = input instanceof Request ? input.url : input.toString();
        if (path.endsWith("/api/v1/auth/antiforgery"))
          return Promise.resolve(json({ token: "csrf" }));
        body = typeof init?.body === "string" ? init.body : "";
        return Promise.resolve(new Response(null, { status: 202 }));
      }),
    );

    renderPage("/passwort-vergessen");
    await user.type(
      screen.getByLabelText("E-Mail-Adresse"),
      "lea@example.test",
    );
    await user.click(
      screen.getByRole("button", { name: "Reset-Link anfordern" }),
    );

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Falls ein Konto zu dieser E-Mail-Adresse existiert",
    );
    expect(body).toContain('"email":"lea@example.test"');
  });

  it("sets a matching new password from the link", async () => {
    const user = userEvent.setup();
    let confirmationBody = "";
    vi.stubGlobal(
      "fetch",
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const path = input instanceof Request ? input.url : input.toString();
        if (path.endsWith("/api/v1/auth/antiforgery"))
          return Promise.resolve(json({ token: "csrf" }));
        confirmationBody = typeof init?.body === "string" ? init.body : "";
        return Promise.resolve(new Response(null, { status: 204 }));
      }),
    );

    renderPage("/passwort-zuruecksetzen?token=reset-secret");
    const password = "Eine sichere neue Passphrase";
    await user.type(screen.getByLabelText("Neues Passwort"), password);
    await user.type(
      screen.getByLabelText("Neues Passwort bestätigen"),
      password,
    );
    await user.click(
      screen.getByRole("button", { name: "Passwort speichern" }),
    );

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Dein Passwort wurde geändert",
    );
    expect(confirmationBody).toContain('"token":"reset-secret"');
    expect(confirmationBody).toContain(`"newPassword":"${password}"`);
    expect(localStorage).toHaveLength(0);
  });

  it("keeps the form when the password confirmation differs", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    renderPage("/passwort-zuruecksetzen?token=reset-secret");

    await user.type(
      screen.getByLabelText("Neues Passwort"),
      "Eine sichere neue Passphrase",
    );
    await user.type(
      screen.getByLabelText("Neues Passwort bestätigen"),
      "Eine andere sichere Passphrase",
    );
    await user.click(
      screen.getByRole("button", { name: "Passwort speichern" }),
    );

    expect(screen.getByRole("alert")).toHaveTextContent(
      "Die beiden Passwörter stimmen nicht überein.",
    );
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

function renderPage(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <PasswordResetPage />
    </MemoryRouter>,
  );
}

function json(value: unknown, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}
