import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  activateOfflineOrganization,
  clearOfflineOrganization,
  clearOfflineSession,
  clearOfflineSnapshot,
  loadOfflineSnapshot,
  saveOfflineSnapshot,
} from "./offlineSnapshot";

const workspace = {
  organizationId: "organization-1",
  organizationName: "Sonnenhöhe e. V.",
  organizationSlug: "sonnenhoehe",
  organizationRole: 1,
  campId: "camp-1",
  campSlug: "sommerfreizeit-2026",
  campBase: "/o/sonnenhoehe/camps/sommerfreizeit-2026",
  camp: { id: "camp-1", name: "Sommerfreizeit 2026" },
};

describe("offline snapshot", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-08-07T12:00:00Z"));
  });

  it("stores only the explicitly supported planning areas with a timestamp", () => {
    saveOfflineSnapshot(workspace, {
      schedule: [{ id: "plan-1" }],
      meals: [{ id: "meal-1" }],
    });
    saveOfflineSnapshot(workspace, {
      material: {
        summaries: [{ id: "material-1" }],
        requirements: [{ id: "material-1", note: "Seile" }],
      },
      shopping: {
        summaries: [{ id: "list-1" }],
        lists: [{ id: "list-1", items: [{ name: "Äpfel" }] }],
      },
    });

    expect(loadOfflineSnapshot()).toEqual({
      version: 1,
      workspace,
      synchronizedAt: "2026-08-07T12:00:00.000Z",
      schedule: [{ id: "plan-1" }],
      meals: [{ id: "meal-1" }],
      material: {
        summaries: [{ id: "material-1" }],
        requirements: [{ id: "material-1", note: "Seile" }],
      },
      shopping: {
        summaries: [{ id: "list-1" }],
        lists: [{ id: "list-1", items: [{ name: "Äpfel" }] }],
      },
    });
    expect(
      loadOfflineSnapshot({
        organizationSlug: "sonnenhoehe",
        campSlug: "sommerfreizeit-2026",
      }),
    ).not.toBeNull();
    expect(
      loadOfflineSnapshot({
        organizationSlug: "fremd",
        campSlug: "sommerfreizeit-2026",
      }),
    ).toBeNull();
  });

  it("removes the snapshot on an organization boundary and logout", () => {
    saveOfflineSnapshot(workspace, { schedule: [] });
    activateOfflineOrganization("organization-2");
    expect(loadOfflineSnapshot()).toBeNull();

    saveOfflineSnapshot(
      { ...workspace, organizationId: "organization-2" },
      { schedule: [] },
    );
    clearOfflineSession();
    expect(loadOfflineSnapshot()).toBeNull();

    saveOfflineSnapshot(workspace, { schedule: [] });
    clearOfflineSnapshot();
    expect(loadOfflineSnapshot()).toBeNull();
  });

  it("clears only the organization that is being left", () => {
    saveOfflineSnapshot(workspace, { schedule: [{ id: "schedule-1" }] });

    clearOfflineOrganization("another-organization");
    expect(loadOfflineSnapshot()).not.toBeNull();

    clearOfflineOrganization(workspace.organizationId);
    expect(loadOfflineSnapshot()).toBeNull();
  });
});
