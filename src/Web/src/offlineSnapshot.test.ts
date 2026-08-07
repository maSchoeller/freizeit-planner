import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  clearOfflineSnapshot,
  loadOfflineSnapshot,
  saveOfflineSnapshot,
} from "./offlineSnapshot";

describe("offline snapshot", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-08-07T12:00:00Z"));
  });

  it("stores only the explicitly supported planning areas with a timestamp", () => {
    saveOfflineSnapshot({
      schedule: [{ id: "plan-1" }],
      meals: [{ id: "meal-1" }],
    });
    saveOfflineSnapshot({
      material: [{ id: "material-1" }],
      shopping: [{ id: "list-1" }],
    });

    expect(loadOfflineSnapshot()).toEqual({
      synchronizedAt: "2026-08-07T12:00:00.000Z",
      schedule: [{ id: "plan-1" }],
      meals: [{ id: "meal-1" }],
      material: [{ id: "material-1" }],
      shopping: [{ id: "list-1" }],
    });
  });

  it("removes the snapshot on an organization boundary", () => {
    saveOfflineSnapshot({ schedule: [] });
    clearOfflineSnapshot();
    expect(loadOfflineSnapshot()).toBeNull();
  });
});
