import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { PwaUpdatePromptView } from "./PwaUpdatePrompt";

describe("PWA update prompt", () => {
  it("offers an explicit update and a later action", async () => {
    const user = userEvent.setup();
    const update = vi.fn();
    const close = vi.fn();
    render(
      <PwaUpdatePromptView
        offlineReady={false}
        needRefresh
        onCloseOfflineReady={vi.fn()}
        onCloseNeedRefresh={close}
        onUpdate={update}
      />,
    );

    expect(
      screen.getByRole("heading", { name: "Neue Version verfügbar" }),
    ).toBeInTheDocument();
    await user.click(
      screen.getByRole("button", { name: "Jetzt aktualisieren" }),
    );
    expect(update).toHaveBeenCalledOnce();

    await user.click(screen.getByRole("button", { name: "Später" }));
    expect(close).toHaveBeenCalledOnce();
  });
});
