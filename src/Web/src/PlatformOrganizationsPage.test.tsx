import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { PlatformOrganizationsPage } from "./PlatformOrganizationsPage";

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("platform organization administration", () => {
  it("keeps a newly created setup link visible when clipboard access is denied", async () => {
    const user = userEvent.setup();
    vi.stubGlobal("navigator", {
      ...globalThis.navigator,
      clipboard: {
        writeText: vi.fn(() =>
          Promise.reject(new DOMException("Not allowed", "NotAllowedError")),
        ),
      },
    });
    vi.stubGlobal(
      "fetch",
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        const request =
          input instanceof Request
            ? new Request(input, init)
            : new Request(new URL(input.toString(), "http://localhost"), init);
        const path = new URL(request.url).pathname;
        if (
          path === "/api/v1/superadmin/organizations" &&
          request.method === "GET"
        )
          return json([]);
        if (path === "/api/v1/auth/antiforgery") return json({ token: "csrf" });
        if (path === "/api/v1/invitations/links" && request.method === "POST")
          return json({ token: "setup-token" }, 201);
        return new Response(null, { status: 404 });
      }),
    );

    render(
      <MemoryRouter>
        <PlatformOrganizationsPage />
      </MemoryRouter>,
    );

    await screen.findByRole("heading", { name: "Organisationen verwalten" });
    await user.click(
      screen.getByRole("button", { name: "Organisation einrichten" }),
    );
    const dialog = screen.getByRole("dialog", {
      name: "Organisation einrichten",
    });
    await user.type(within(dialog).getByLabelText("Name"), "Jugendwerk Nord");
    await user.type(
      within(dialog).getByLabelText("Kurzname für die URL"),
      "jugendwerk-nord",
    );
    await user.click(
      within(dialog).getByRole("button", {
        name: "Einrichtungslink erstellen & kopieren",
      }),
    );

    expect(
      await screen.findByText(/Einrichtungslink ist bereit/),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /setup-token/ })).toHaveClass(
      "copy-value",
    );
    expect(screen.queryByRole("alert")).toBeNull();
  });
});

function json(value: unknown, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}
