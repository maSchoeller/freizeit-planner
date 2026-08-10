import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page, type TestInfo } from "@playwright/test";
import { mkdir, copyFile } from "node:fs/promises";
import path from "node:path";

test("passwordless entry point is responsive, keyboard operable and accessible", async ({
  page,
}, testInfo) => {
  await page.goto("/anmelden");
  await expect(
    page.getByRole("heading", { name: "Im Freizeit-Cockpit anmelden" }),
  ).toBeVisible();

  await assertNoHorizontalOverflow(page);
  await expect(page.getByLabel("E-Mail-Adresse")).toBeFocused();

  await page.keyboard.press("Tab");
  await expect(
    page.getByRole("button", { name: "Anmeldecode anfordern" }),
  ).toBeFocused();

  await page.getByLabel("E-Mail-Adresse").fill("keine-adresse");
  await page.getByRole("button", { name: "Anmeldecode anfordern" }).click();
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

test("owner signs in through Mailpit and reaches the camp overview", async ({
  page,
}, testInfo) => {
  await page.goto("/o/sonnenhoehe/camps");

  await expect(page.getByRole("heading", { name: "Camps" })).toBeVisible();
  await assertNoHorizontalOverflow(page);
  await assertAxe(page);
  await capture(page, testInfo, "freizeiten");
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
