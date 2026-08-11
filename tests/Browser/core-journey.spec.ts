import AxeBuilder from "@axe-core/playwright";
import {
  expect,
  test as base,
  type APIRequestContext,
  type Page,
  type TestInfo,
} from "@playwright/test";
import { mkdir, copyFile } from "node:fs/promises";
import { randomUUID } from "node:crypto";
import path from "node:path";

const browserUserEmail = "miriam@example.test";
const browserUserPassword = "Browser-Testpasswort 2026!";
const browserSuperAdminEmail = "superadmin@example.test";
const browserSuperAdminPassword = "Browser-Superadminpasswort 2026!";
type WorkerAuthentication = {
  accessToken: string;
  storageState: {
    cookies: Array<{
      name: string;
      value: string;
      domain: string;
      path: string;
      expires: number;
      httpOnly: boolean;
      secure: boolean;
      sameSite: "Strict" | "Lax" | "None";
    }>;
    origins: Array<{
      origin: string;
      localStorage: Array<{ name: string; value: string }>;
    }>;
  };
};

const test = base.extend<{}, { workerAuthentication: WorkerAuthentication }>({
  workerAuthentication: [
    async ({}, use) => {
      const accessToken = process.env.BROWSER_MEMBER_ACCESS_TOKEN;
      const serializedStorageState = process.env.BROWSER_MEMBER_STORAGE_STATE;
      if (!accessToken || !serializedStorageState) {
        throw new Error(
          "Die Browser-Anmeldung aus dem Global-Setup ist nicht verfügbar.",
        );
      }
      await use({
        accessToken,
        storageState: JSON.parse(
          serializedStorageState,
        ) as WorkerAuthentication["storageState"],
      });
    },
    { scope: "worker" },
  ],
});

test.beforeEach(async ({ context, workerAuthentication }, testInfo) => {
  await context.clearCookies();
  if (
    testInfo.title.includes(
      "password login is responsive, keyboard operable and accessible",
    )
  )
    return;

  await context.setExtraHTTPHeaders({
    Authorization: `Bearer ${workerAuthentication.accessToken}`,
  });
});

test("@smoke password login is responsive, keyboard operable and accessible", async ({
  page,
}, testInfo) => {
  await page.goto("/anmelden");
  await expect(
    page.getByRole("heading", { name: "Im Freizeit-Cockpit anmelden" }),
  ).toBeVisible();

  await assertNoHorizontalOverflow(page);
  await expect(page.getByLabel("E-Mail-Adresse")).toBeFocused();

  await page.keyboard.press("Tab");
  await expect(page.getByLabel("Passwort", { exact: true })).toBeFocused();

  await page.keyboard.press("Tab");
  await expect(
    page.getByRole("button", { name: "Passwort anzeigen" }),
  ).toBeFocused();

  await page.getByLabel("E-Mail-Adresse").fill("keine-adresse");
  await page.getByRole("button", { name: "Anmelden", exact: true }).click();
  await expect(
    page.getByText("Gib eine gültige E-Mail-Adresse ein."),
  ).toBeVisible();
  await expect(page.getByLabel("E-Mail-Adresse")).toHaveAttribute(
    "aria-invalid",
    "true",
  );

  await assertAxe(page);
  await capture(page, testInfo, "anmeldung");

  await page.getByLabel("E-Mail-Adresse").fill(browserUserEmail);
  await page.getByLabel("Passwort", { exact: true }).fill(browserUserPassword);
  await page.getByRole("checkbox", { name: /angemeldet bleiben/i }).check();
  const loginResponsePromise = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/v1/auth/login") &&
      response.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Anmelden", exact: true }).click();
  expect((await loginResponsePromise).ok()).toBe(true);
  await expect(page).not.toHaveURL(/\/anmelden$/);
});

test("member signs in with a persistent refresh session and reaches the camp overview", async ({
  browser,
  page,
  workerAuthentication,
}, testInfo) => {
  test.skip(
    testInfo.project.name !== "chromium-desktop",
    "Der persistente Secure-Cookie wird einmal im Chromium-Desktop-Neustart geprüft.",
  );

  const restartedContext = await browser.newContext({
    baseURL: String(testInfo.project.use.baseURL ?? "http://localhost:5041"),
    storageState: workerAuthentication.storageState,
    viewport: page.viewportSize() ?? { width: 1440, height: 1000 },
  });
  const restartedPage = await restartedContext.newPage();
  await restartedPage.goto("/o/sonnenhoehe/camps");

  await expect(
    restartedPage.getByRole("heading", { name: "Freizeiten" }),
  ).toBeVisible();
  await assertNoHorizontalOverflow(restartedPage);
  await assertAxe(restartedPage);
  await restartedContext.close();

  // The Secure refresh cookie above only proves the persistent-session contract;
  // it is unreliable over plain HTTP once restored into a fresh context. Capture
  // the help screenshot through the already-authenticated Bearer-token page instead.
  await page.goto("/o/sonnenhoehe/camps");
  await expect(page.getByRole("heading", { name: "Freizeiten" })).toBeVisible();
  await capture(page, testInfo, "freizeiten");
});

test("@smoke superadmin invitation registers and confirms a new global account", async ({
  page,
  playwright,
  workerAuthentication,
}, testInfo) => {
  const baseURL = String(
    testInfo.project.use.baseURL ?? "http://localhost:5041",
  );
  const mailpitUrl = process.env.MAILPIT_URL;
  if (!mailpitUrl) throw new Error("MAILPIT_URL fehlt.");
  await page.context().setExtraHTTPHeaders({});
  const api = await playwright.request.newContext({ baseURL });
  try {
    const superAdminAccessToken = requireSuperAdminAccessToken();
    const invitationCsrf = await getAntiforgery(api, superAdminAccessToken);
    const invitationResponse = await api.post("/api/v1/invitations/links", {
      headers: {
        Authorization: `Bearer ${superAdminAccessToken}`,
        "X-CSRF-TOKEN": invitationCsrf,
      },
      data: {
        isSuperAdmin: true,
        organizationId: null,
        organizationRole: null,
        campId: null,
        campRole: null,
        newOrganization: null,
      },
    });
    expect(invitationResponse.ok()).toBe(true);
    const invitation = (await invitationResponse.json()) as { token: string };
    const invitationRunId = randomUUID().slice(0, 8);
    const newEmail = `einladung-${testInfo.project.name}-${invitationRunId}@example.test`;

    await page.goto(`/einladung?token=${invitation.token}`);
    await expect(page.getByText(/Superadmin für das gesamte/)).toBeVisible();
    await page.getByLabel("Vorname").fill("Neue");
    await page.getByLabel("Nachname").fill("Person");
    await page.getByLabel("E-Mail-Adresse").fill(newEmail);
    await page
      .getByLabel("Passwort", { exact: true })
      .fill("Neue sichere Browser-Passphrase");
    await page
      .getByLabel("Passwort bestätigen")
      .fill("Neue sichere Browser-Passphrase");
    await page.getByRole("button", { name: "Konto erstellen" }).click();
    await expect(
      page.getByRole("heading", { name: "E-Mail-Adresse bestätigen" }),
    ).toBeVisible();

    const confirmationToken = await pollForInvitationConfirmation(
      mailpitUrl,
      newEmail,
    );
    await page.goto(`/einladung-bestaetigen?token=${confirmationToken}`);
    await expect(
      page.getByRole("heading", { name: "E-Mail-Adresse bestätigt" }),
    ).toBeVisible();
    await assertNoHorizontalOverflow(page);
    await assertAxe(page);
    expect(workerAuthentication.accessToken).not.toBe(confirmationToken);
  } finally {
    await api.dispose();
  }
});

test("@smoke Superadmin and Orgadmin user pages are responsive and accessible", async ({
  page,
  playwright,
}, testInfo) => {
  await page.goto("/o/sonnenhoehe/verwaltung/benutzer");
  await expect(
    page.getByRole("heading", { name: "Team verwalten" }),
  ).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await assertAxe(page);
  await capture(page, testInfo, "orgadmin-benutzer");

  const api = await playwright.request.newContext({
    baseURL: String(testInfo.project.use.baseURL ?? "http://localhost:5041"),
  });
  try {
    await page.context().setExtraHTTPHeaders({
      Authorization: `Bearer ${requireSuperAdminAccessToken()}`,
    });
    await page.goto("/superadmin/benutzer");
    await expect(
      page.getByRole("heading", { name: "Benutzer verwalten" }),
    ).toBeVisible();
    await assertNoHorizontalOverflow(page);
    await assertAxe(page);
    await capture(page, testInfo, "superadmin-benutzer");
  } finally {
    await api.dispose();
  }
});

test("@smoke Superadmin creates an Organization setup link after UI login", async ({
  page,
  context,
}, testInfo) => {
  const setupRunId = randomUUID().slice(0, 8);
  await context.setExtraHTTPHeaders({});
  await page.goto("/anmelden");
  await page.getByLabel("E-Mail-Adresse").fill(browserSuperAdminEmail);
  await page
    .getByLabel("Passwort", { exact: true })
    .fill(browserSuperAdminPassword);
  const loginResponsePromise = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/v1/auth/login") &&
      response.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Anmelden", exact: true }).click();
  expect((await loginResponsePromise).ok()).toBe(true);
  await expect(page).not.toHaveURL(/\/anmelden$/);
  await page.getByLabel(/Kontomenü.*öffnen/).click();
  await page
    .locator(".profile-menu-panel")
    .getByRole("link", { name: "Plattform verwalten" })
    .click();
  await expect(
    page.getByRole("heading", { name: "Organisationen verwalten" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Organisation einrichten" }).click();
  await page.getByLabel("Name", { exact: true }).fill("Browser Organization");
  await page
    .getByLabel("Kurzname für die URL", { exact: true })
    .fill(`browser-organization-${testInfo.project.name}-${setupRunId}`);
  const linkResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/v1/invitations/links") &&
      response.request().method() === "POST" &&
      response.status() === 201,
  );
  await page
    .getByRole("button", { name: "Einrichtungslink erstellen & kopieren" })
    .click();

  expect((await linkResponse).status()).toBe(201);
  await expect(
    page.getByText(/Einrichtungslink (wurde kopiert|ist bereit)/),
  ).toBeVisible();
  const invitationUrl = await page.locator(".copy-value").textContent();
  expect(invitationUrl).toBeTruthy();
  await page.locator(".copy-value").click();
  await page.getByRole("button", { name: "Einladung annehmen" }).click();
  await expect(
    page.getByRole("heading", { name: "Einladung angenommen" }),
  ).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await assertAxe(page);
});

function requireSuperAdminAccessToken(): string {
  const accessToken = process.env.BROWSER_SUPERADMIN_ACCESS_TOKEN;
  if (!accessToken) {
    throw new Error(
      "Die Superadmin-Anmeldung aus dem Global-Setup ist nicht verfügbar.",
    );
  }
  return accessToken;
}

test("central camp pages render their responsive empty states without accessibility violations", async ({
  page,
}, testInfo) => {
  const pages = [
    { path: "", heading: /Hallo, Miriam König/, screenshot: "uebersicht" },
    {
      path: "/tagesplan",
      heading: "Tages- und Wochenplan",
      screenshot: "tagesplan",
    },
    { path: "/essen", heading: "Verpflegung", screenshot: "essen" },
    {
      path: "/logistik",
      heading: "Material & Einkaufslisten",
      screenshot: "logistik",
    },
    { path: "/andachten", heading: "Andachten", screenshot: "andachten" },
    { path: "/notizen", heading: "Notizbuch", screenshot: "notizen" },
    { path: "/dateien", heading: "Dateien", screenshot: "dateien" },
    {
      path: "/suche",
      heading: "Suche",
      screenshot: "suche",
    },
  ] as const;

  for (const item of pages) {
    await page.goto(`/o/sonnenhoehe/camps/browser-testcamp${item.path}`);
    await expect(
      page.getByRole("heading", { name: item.heading, level: 1 }),
    ).toBeVisible();
    await expect(
      page.getByRole("navigation", { name: "Freizeit-Navigation" }),
    ).toBeVisible();
    await assertNoHorizontalOverflow(page);
    await assertAxe(page);
    await capture(page, testInfo, item.screenshot);
  }
});

test("camp list exposes a designed loading and server-error state", async ({
  page,
}, testInfo) => {
  await page.route("**/api/v1/organizations/*/camps", async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 750));
    await route.fulfill({
      status: 503,
      contentType: "application/problem+json",
      body: JSON.stringify({ title: "Testfehler" }),
    });
  });

  await page.goto("/o/sonnenhoehe/camps");
  await expect(page.getByText("Freizeiten werden geladen …")).toBeVisible();
  await expect(page.getByRole("alert")).toContainText(
    "Die Freizeiten konnten nicht geladen werden.",
  );
  await assertAxe(page);
  await capture(page, testInfo, "fehlerzustand");
});

test("organization member receives a designed permission error at the Superadmin boundary", async ({
  page,
}, testInfo) => {
  await page.goto("/superadmin/organisationen");
  await expect(
    page.getByRole("heading", { name: "Organisationen verwalten" }),
  ).toBeVisible();
  await expect(page.getByRole("alert")).toBeVisible();
  await assertAxe(page);
  await capture(page, testInfo, "berechtigungsfehler");
});

test("the synchronized schedule stays explicitly read-only during a real network outage", async ({
  page,
  context,
}, testInfo) => {
  await page.goto("/o/sonnenhoehe/camps/browser-testcamp/tagesplan");
  await expect(
    page.getByRole("heading", { name: "Tages- und Wochenplan" }),
  ).toBeVisible();
  await expect
    .poll(() =>
      page.evaluate(() => localStorage.getItem("freizeit-cockpit:offline:v1")),
    )
    .not.toBeNull();
  await page.evaluate(async () => navigator.serviceWorker.ready);
  if (
    !(await page.evaluate(() => Boolean(navigator.serviceWorker.controller)))
  ) {
    await page.reload();
    await expect(
      page.getByRole("heading", { name: "Tages- und Wochenplan" }),
    ).toBeVisible();
    await expect
      .poll(() =>
        page.evaluate(() =>
          localStorage.getItem("freizeit-cockpit:offline:v1"),
        ),
      )
      .not.toBeNull();
  }

  await context.setOffline(true);
  try {
    expect(await page.evaluate(() => navigator.onLine)).toBe(false);
    await page.evaluate(() => window.dispatchEvent(new Event("offline")));
    await expect(
      page.getByText("Offline · nur gespeicherter Stand"),
    ).toHaveText("Offline · nur gespeicherter Stand");
    if (testInfo.project.name === "chromium-desktop") {
      await page.addInitScript(() => {
        Object.defineProperty(navigator, "onLine", {
          configurable: true,
          get: () => false,
        });
      });
      await page.reload({ waitUntil: "domcontentloaded" });
    }
    await expect(
      page.getByText(/Offline-Snapshot · Zuletzt synchronisiert:/),
    ).toBeVisible();
    await expect(
      page.getByText("Offline · nur gespeicherter Stand"),
    ).toHaveText("Offline · nur gespeicherter Stand");
    await expect(
      page.getByRole("heading", { name: "Tages- und Wochenplan" }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Eintrag erstellen" }),
    ).toBeDisabled();
    await assertAxe(page);
    await capture(page, testInfo, "offline-tagesplan");
  } finally {
    await context.setOffline(false);
  }
});

test("a stale camp edit remains visible after a genuine version conflict", async ({
  page,
  context,
}, testInfo) => {
  test.skip(
    testInfo.project.name !== "chromium-desktop",
    "One real conflict proof is sufficient; the central journey runs in every project.",
  );

  const settingsUrl = "/o/sonnenhoehe/camps/browser-testcamp/einstellungen";
  await page.goto(settingsUrl);
  await expect(
    page.getByRole("heading", { name: "Freizeit-Einstellungen" }),
  ).toBeVisible();

  const concurrentPage = await context.newPage();
  await concurrentPage.goto(settingsUrl);
  await concurrentPage
    .getByLabel("Beschreibung")
    .fill("Zwischenzeitlich gespeichert");
  await concurrentPage
    .getByRole("button", { name: "Änderungen speichern" })
    .click();
  await expect(concurrentPage.getByRole("status")).toContainText(
    "Freizeit-Einstellungen wurden gespeichert.",
  );
  await concurrentPage.close();

  await page
    .getByLabel("Beschreibung")
    .fill("Mein noch nicht gespeicherter Entwurf");
  await page.getByRole("button", { name: "Änderungen speichern" }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.getByLabel("Beschreibung")).toHaveValue(
    "Mein noch nicht gespeicherter Entwurf",
  );
  await assertAxe(page);
  await capture(page, testInfo, "versionskonflikt");
});

async function getAntiforgery(
  api: APIRequestContext,
  accessToken?: string,
): Promise<string> {
  const response = await api.get("/api/v1/auth/antiforgery", {
    headers: accessToken
      ? { Authorization: `Bearer ${accessToken}` }
      : undefined,
  });
  expect(response.ok()).toBe(true);
  const body = (await response.json()) as { token: string };
  return body.token;
}

async function pollForInvitationConfirmation(
  mailpitUrl: string,
  email: string,
): Promise<string> {
  const deadline = Date.now() + 20_000;
  while (Date.now() < deadline) {
    const response = await fetch(`${mailpitUrl}/api/v1/messages`);
    if (response.ok) {
      const payload: unknown = await response.json();
      if (isMailpitList(payload)) {
        const message = payload.messages.find(
          (item) =>
            item.To.some((recipient) => recipient.Address === email) &&
            item.Subject.includes("Bestätige deine Registrierung"),
        );
        const match = message?.Snippet.match(
          /einladung-bestaetigen\?token=([A-F0-9]{64})/,
        );
        if (match) return match[1];
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(
    `Mailpit hat keine Einladungsbestätigung für ${email} geliefert.`,
  );
}

function isMailpitList(value: unknown): value is {
  messages: Array<{
    To: Array<{ Address: string }>;
    Subject: string;
    Snippet: string;
  }>;
} {
  return (
    typeof value === "object" &&
    value !== null &&
    "messages" in value &&
    Array.isArray(value.messages)
  );
}

test("@smoke visible administration entries follow the signed-in role matrix", async ({
  page,
}) => {
  await page.goto("/o/sonnenhoehe/camps/browser-testcamp");
  await page.getByLabel(/Kontomenü.*öffnen/).click();
  const profileMenu = page.locator(".profile-menu-panel");
  await expect(
    profileMenu.getByRole("link", { name: "Organisation verwalten" }),
  ).toBeVisible();
  await expect(
    profileMenu.getByRole("link", { name: "Plattform verwalten" }),
  ).toHaveCount(0);

  await profileMenu
    .getByRole("link", { name: "Organisation verwalten" })
    .click();
  await expect(
    page.getByRole("heading", { name: "Team verwalten", level: 1 }),
  ).toBeVisible();
  const organizationNavigation = page.getByRole("navigation", {
    name: "Organisationsverwaltung",
  });
  await expect(
    organizationNavigation.getByRole("link", { name: "Team & Rechte" }),
  ).toHaveAttribute("aria-current", "page");
  await organizationNavigation
    .getByRole("link", { name: "Freizeiten" })
    .click();
  await expect(
    page.getByRole("heading", { name: "Freizeiten", level: 1 }),
  ).toBeVisible();

  await page.goto("/superadmin/benutzer");
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.getByText("@example.test")).toHaveCount(0);

  await page.context().setExtraHTTPHeaders({
    Authorization: `Bearer ${requireSuperAdminAccessToken()}`,
  });
  await page.goto("/superadmin/organisationen");
  const platformNavigation = page.getByRole("navigation", {
    name: "Plattformverwaltung",
  });
  await expect(
    platformNavigation.getByRole("link", { name: "Organisationen" }),
  ).toHaveAttribute("aria-current", "page");
  await platformNavigation.getByRole("link", { name: "Benutzer" }).click();
  await expect(
    page.getByRole("heading", { name: "Benutzer verwalten", level: 1 }),
  ).toBeVisible();
  await expect(
    page.getByRole("navigation", { name: "Organisationsverwaltung" }),
  ).toHaveCount(0);
});

test("@smoke a combined Orgadmin and Superadmin sees both named administration areas", async ({
  page,
}) => {
  await page.route("**/api/v1/account", async (route) => {
    const response = await route.fetch();
    const account = (await response.json()) as Record<string, unknown>;
    await route.fulfill({
      response,
      json: { ...account, isSuperAdmin: true },
    });
  });

  await page.goto("/o/sonnenhoehe/camps/browser-testcamp");
  await page.getByLabel(/Kontomenü.*öffnen/).click();
  const profileMenu = page.locator(".profile-menu-panel");
  await expect(
    profileMenu.getByRole("link", { name: "Organisation verwalten" }),
  ).toBeVisible();
  await expect(
    profileMenu.getByRole("link", { name: "Plattform verwalten" }),
  ).toBeVisible();
  await assertAxe(page);
});

test("@smoke administration and camp pages stay usable at 200 % text size and 400 % zoom", async ({
  page,
}, testInfo) => {
  test.slow();
  for (const target of [
    { path: "/o/sonnenhoehe/verwaltung/team", heading: "Team verwalten" },
    {
      path: "/o/sonnenhoehe/camps/browser-testcamp/logistik",
      heading: "Material & Einkaufslisten",
    },
  ]) {
    await page.goto(target.path);
    await expect(
      page.getByRole("heading", { name: target.heading, level: 1 }),
    ).toBeVisible();
    await page.addStyleTag({ content: ":root { font-size: 200%; }" });
    await expect(
      page.getByRole("heading", { name: target.heading, level: 1 }),
    ).toBeVisible();
    await assertNoHorizontalOverflow(page);
    await assertAxe(page);
  }
  await capture(page, testInfo, "textvergroesserung");

  // Firefox hangs indefinitely on setViewportSize while iPhone 13 touch/mobile
  // emulation is active; the 200 % text-size pass above already covers this project.
  const zoomed = page.viewportSize();
  if (zoomed && testInfo.project.name !== "firefox-mobile") {
    await page.setViewportSize({
      width: Math.max(320, Math.round(zoomed.width / 4)),
      height: Math.max(256, Math.round(zoomed.height / 4)),
    });
    await page.goto("/o/sonnenhoehe/verwaltung/team");
    await expect(
      page.getByRole("heading", { name: "Team verwalten", level: 1 }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Person einladen" }),
    ).toBeVisible();
    await assertNoHorizontalOverflow(page);
    await assertAxe(page);
    await capture(page, testInfo, "vierhundert-prozent-zoom");
    await page.setViewportSize(zoomed);
  }
});

test("@smoke forced colors and reduced motion keep navigation, dialogs and focus usable", async ({
  page,
}, testInfo) => {
  await page.emulateMedia({ forcedColors: "active", reducedMotion: "reduce" });
  await page.goto("/o/sonnenhoehe/verwaltung/team");
  await expect(
    page.getByRole("heading", { name: "Team verwalten", level: 1 }),
  ).toBeVisible();
  await expect(
    page
      .getByRole("navigation", { name: "Organisationsverwaltung" })
      .getByRole("link", { name: "Team & Rechte" }),
  ).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await assertAxe(page);

  await page.getByRole("button", { name: "Person einladen" }).click();
  const invitation = page.getByRole("dialog", { name: "Person einladen" });
  await expect(invitation).toBeVisible();
  await expect(
    invitation.getByRole("combobox", {
      name: "Rolle des nächsten Einladungslinks",
    }),
  ).toBeFocused();
  await assertAxe(page);
  await capture(page, testInfo, "forced-colors");
  await page.keyboard.press("Escape");
  await expect(invitation).toHaveCount(0);
  await page.emulateMedia({ forcedColors: null, reducedMotion: null });
});

async function assertAxe(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"])
    .analyze();
  expect(
    results.violations,
    results.violations.map((item) => `${item.id}: ${item.help}`).join("\n"),
  ).toEqual([]);
}

async function assertNoHorizontalOverflow(page: Page) {
  const result = await page.evaluate(() => {
    const viewportWidth = document.documentElement.clientWidth;
    return {
      overflow: document.body.getBoundingClientRect().width - viewportWidth,
      offenders: [...document.querySelectorAll("body *")]
        .map((element) => {
          const rect = element.getBoundingClientRect();
          return {
            element: `${element.tagName.toLowerCase()}.${[...element.classList].join(".")}`,
            left: Math.round(rect.left * 10) / 10,
            right: Math.round(rect.right * 10) / 10,
            width: Math.round(rect.width * 10) / 10,
          };
        })
        .filter((item) => item.left < -1 || item.right > viewportWidth + 1)
        .slice(0, 12),
    };
  });
  expect(
    result.overflow,
    `Horizontal overflow offenders: ${JSON.stringify(result.offenders)}`,
  ).toBeLessThanOrEqual(1);
}

async function capture(page: Page, testInfo: TestInfo, name: string) {
  const updatePrompt = page.getByRole("button", { name: "Hinweis schließen" });
  const modalOpen = await page.locator("dialog[open]").count();
  if (
    modalOpen === 0 &&
    (await updatePrompt.count()) > 0 &&
    (await updatePrompt.isVisible())
  ) {
    await updatePrompt.click();
  }
  const artifact = testInfo.outputPath(`${name}.png`);
  await page.screenshot({ path: artifact, fullPage: true });
  await testInfo.attach(`${name}-${testInfo.project.name}`, {
    path: artifact,
    contentType: "image/png",
  });

  if (
    process.env.UPDATE_HELP_SCREENSHOTS === "1" &&
    new Set(["chromium-desktop", "chrome-desktop"]).has(
      testInfo.project.name,
    ) &&
    new Set(["anmeldung", "freizeiten", "uebersicht", "tagesplan"]).has(name)
  ) {
    const helpDirectory = path.resolve("src/Help/docs/public/screenshots");
    await mkdir(helpDirectory, { recursive: true });
    await copyFile(artifact, path.join(helpDirectory, `${name}-desktop.png`));
  }
}
