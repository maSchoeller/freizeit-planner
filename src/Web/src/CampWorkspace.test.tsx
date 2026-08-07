import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";

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

describe("camp workspace", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it("offers every planning area as a real route", () => {
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/logistik");
    expect(
      screen.getByRole("heading", { name: "Material & Einkaufslisten" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Andachten" })).toHaveAttribute(
      "href",
      "/o/sonnenhoehe/camps/sommerfreizeit-2026/andachten",
    );
    expect(
      screen.getByRole("checkbox", { name: "12 kg Kartoffeln" }),
    ).toBeEnabled();
  });

  it("marks offline mode and prevents planning writes", () => {
    vi.spyOn(window.navigator, "onLine", "get").mockReturnValue(false);
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/dateien");
    expect(screen.getByText(/^Offline/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /hochladen/ })).toBeDisabled();
  });
});
