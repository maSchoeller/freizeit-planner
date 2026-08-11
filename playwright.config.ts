import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.WEB_BASE_URL ?? "http://localhost:5041";

const viewports = [
  {
    name: "mobile",
    use: { ...devices["iPhone 13"], viewport: { width: 390, height: 844 } },
  },
  { name: "tablet", use: { viewport: { width: 768, height: 1024 } } },
  { name: "desktop", use: { viewport: { width: 1440, height: 900 } } },
] as const;

const browsers =
  process.env.PLAYWRIGHT_GOOGLE_CHROME === "1"
    ? ([
        { name: "chrome", browserName: "chromium", channel: "chrome" },
      ] as const)
    : ([
        { name: "chromium", browserName: "chromium" },
        { name: "firefox", browserName: "firefox" },
        { name: "webkit", browserName: "webkit" },
      ] as const);

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
  },
  projects: browsers.flatMap((browser) =>
    viewports.map(({ name, use }) => ({
      name: `${browser.name}-${name}`,
      use: {
        ...use,
        browserName: browser.browserName,
        ...(browser.name === "chrome" ? { channel: browser.channel } : {}),
      },
    })),
  ),
});
