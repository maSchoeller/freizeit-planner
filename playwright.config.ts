import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.WEB_BASE_URL ?? "http://localhost:5041";

const viewports = [
  {
    name: "mobile",
    use: { ...devices["iPhone 13"], viewport: { width: 390, height: 844 } },
  },
  { name: "tablet", use: { viewport: { width: 834, height: 1112 } } },
  { name: "desktop", use: { viewport: { width: 1440, height: 1000 } } },
] as const;

const browsers = ["chromium", "firefox", "webkit"] as const;

export default defineConfig({
  globalSetup: "./tests/Browser/global-setup.ts",
  testDir: "./tests/Browser",
  fullyParallel: false,
  workers: 1,
  timeout: 90_000,
  expect: { timeout: 10_000 },
  reporter: [["line"], ["html", { open: "never" }]],
  outputDir: ".artifacts/playwright/results",
  use: {
    baseURL,
    locale: "de-DE",
    timezoneId: "Europe/Berlin",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
    storageState: ".artifacts/playwright/auth/owner.json",
  },
  projects: browsers.flatMap((browserName) =>
    viewports.map(({ name, use }) => ({
      name: `${browserName}-${name}`,
      use: { ...use, browserName },
    })),
  ),
});
