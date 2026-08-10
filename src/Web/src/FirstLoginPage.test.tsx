import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, expect, it, vi } from "vitest";
import { App } from "./App";

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  localStorage.clear();
  sessionStorage.clear();
});

it("creates the first superadmin with names and confirmed password", async () => {
  const user = userEvent.setup();
  let registrationBody = "";
  vi.stubGlobal(
    "fetch",
    vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(
        typeof input === "string"
          ? input
          : input instanceof URL
            ? input.toString()
            : input.url,
        "https://localhost",
      ).pathname;
      if (path === "/api/v1/auth/first-login" && !init?.method)
        return Promise.resolve(json({ available: true }));
      if (path === "/api/v1/auth/antiforgery")
        return Promise.resolve(json({ token: "csrf" }));
      if (path === "/api/v1/auth/first-login" && init?.method === "POST") {
        registrationBody = typeof init.body === "string" ? init.body : "";
        return Promise.resolve(
          json({
            accessToken: "first.access.jwt",
            expiresAt: "2026-08-10T12:15:00Z",
          }),
        );
      }
      return Promise.resolve(new Response(null, { status: 404 }));
    }),
  );

  render(
    <MemoryRouter initialEntries={["/erste-einrichtung"]}>
      <App />
    </MemoryRouter>,
  );

  expect(
    await screen.findByRole("heading", { name: "Ersten Superadmin anlegen" }),
  ).toBeInTheDocument();
  await user.type(screen.getByLabelText("Vorname"), "Erika");
  await user.type(screen.getByLabelText("Nachname"), "Admin");
  await user.type(
    screen.getByLabelText("E-Mail-Adresse"),
    "admin@example.test",
  );
  await user.type(
    screen.getByLabelText("Passwort", { exact: true }),
    "Eine sichere Admin-Passphrase",
  );
  await user.type(
    screen.getByLabelText("Passwort bestätigen"),
    "Eine sichere Admin-Passphrase",
  );
  await user.click(screen.getByRole("button", { name: "Superadmin anlegen" }));

  await waitFor(() =>
    expect(registrationBody).toContain('"firstName":"Erika"'),
  );
  expect(localStorage).toHaveLength(0);
  expect(sessionStorage).toHaveLength(0);
});

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}
