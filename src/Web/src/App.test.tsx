import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { App } from "./App";

describe("Dashboard", () => {
  it("exposes the next plan and core navigation in German", () => {
    render(<App />);
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
});
