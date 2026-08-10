import AxeBuilder from "@axe-core/playwright";
import {
  expect,
  test as base,
  type APIRequestContext,
  type Page,
  type TestInfo,
} from "@playwright/test";
import { mkdir, copyFile } from "node:fs/promises";
import path from "node:path";

const browserUserEmail = "miriam@example.test";
const browserUserPassword = "Browser-Testpasswort 2026!";
const superAdminEmail = "platform-admin@example.test";
const superAdminPassword = "Browser-Superadminpasswort 2026!";

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
    async ({ playwright }, use, workerInfo) => {
      const api = await playwright.request.newContext({
        baseURL: String(
          workerInfo.project.use.baseURL ?? "http://localhost:5041",
        ),
      });
      try {
        const antiforgeryResponse = await api.get("/api/v1/auth/antiforgery");
        expect(antiforgeryResponse.ok()).toBe(true);
        const antiforgery = (await antiforgeryResponse.json()) as {
          token: string;
        };
        const loginResponse = await api.post("/api/v1/auth/login", {
          headers: { "X-CSRF-TOKEN": antiforgery.token },
          data: {
            email: browserUserEmail,
            password: browserUserPassword,
            rememberMe: true,
          },
        });
        expect(loginResponse.ok()).toBe(true);
        const authentication = (await loginResponse.json()) as {
          accessToken: string;
        };
        await use({
          accessToken: authentication.accessToken,
          storageState: await api.storageState(),
        });
      } finally {
        await api.dispose();
      }
    },
    { scope: "worker" },
  ],
});

test.beforeEach(async ({ context, workerAuthentication }, testInfo) => {
  await context.clearCookies();
  if (
    testInfo.title ===
    "password login is responsive, keyboard operable and accessible"
  )
    return;

  await context.setExtraHTTPHeaders({
    Authorization: `Bearer ${workerAuthentication.accessToken}`,
  });
});

test("password login is responsive, keyboard operable and accessible", async ({
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
    restartedPage.getByRole("heading", { name: "Camps" }),
  ).toBeVisible();
  await assertNoHorizontalOverflow(restartedPage);
  await assertAxe(restartedPage);
  await capture(restartedPage, testInfo, "freizeiten");
  await restartedContext.close();
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
    const loginCsrf = await getAntiforgery(api);
    const login = await api.post("/api/v1/auth/login", {
      headers: { "X-CSRF-TOKEN": loginCsrf },
      data: {
        email: superAdminEmail,
        password: superAdminPassword,
        rememberMe: false,
      },
    });
    expect(login.ok()).toBe(true);
    const authentication = (await login.json()) as { accessToken: string };
    const invitationCsrf = await getAntiforgery(
      api,
      authentication.accessToken,
    );
    const invitationResponse = await api.post("/api/v1/invitations/links", {
      headers: {
        Authorization: `Bearer ${authentication.accessToken}`,
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
    const newEmail = `einladung-${testInfo.project.name}@example.test`;

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
    { path: "/essen", heading: "Essen & Rezepte", screenshot: "essen" },
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
      heading: "Suche & Papierkorb",
      screenshot: "suche",
    },
  ] as const;

  for (const item of pages) {
    await page.goto(`/o/sonnenhoehe/camps/browser-testcamp${item.path}`);
    await expect(
      page.getByRole("heading", { name: item.heading, level: 1 }),
    ).toBeVisible();
    await expect(
      page.getByRole("navigation", { name: "Camp-Navigation" }),
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
  await expect(page.getByText("Camps werden geladen …")).toBeVisible();
  await expect(page.getByRole("alert")).toContainText(
    "Die Camps konnten nicht geladen werden.",
  );
  await assertAxe(page);
  await capture(page, testInfo, "fehlerzustand");
});

test("organization owner receives a designed permission error at the platform boundary", async ({
  page,
}, testInfo) => {
  await page.goto("/plattform/organisationen");
  await expect(
    page.getByRole("heading", { name: "Organizations" }),
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
    page.getByRole("heading", { name: "Camp-Einstellungen" }),
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
    "Camp-Einstellungen wurden gespeichert.",
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
      overflow: document.documentElement.scrollWidth - viewportWidth,
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
  const artifact = testInfo.outputPath(`${name}.png`);
  await page.screenshot({ path: artifact, fullPage: true });
  await testInfo.attach(`${name}-${testInfo.project.name}`, {
    path: artifact,
    contentType: "image/png",
  });

  if (
    process.env.UPDATE_HELP_SCREENSHOTS === "1" &&
    testInfo.project.name === "chromium-desktop" &&
    new Set(["anmeldung", "freizeiten", "uebersicht", "tagesplan"]).has(name)
  ) {
    const helpDirectory = path.resolve("src/Help/docs/public/screenshots");
    await mkdir(helpDirectory, { recursive: true });
    await copyFile(artifact, path.join(helpDirectory, `${name}-desktop.png`));
  }
}
