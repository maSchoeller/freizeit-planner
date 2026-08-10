import { chromium, expect, type FullConfig } from "@playwright/test";

const email = "miriam@example.test";
const password = "Browser-Testpasswort 2026!";

export default async function globalSetup(config: FullConfig) {
  const baseURL = String(
    config.projects[0]?.use.baseURL ?? "http://localhost:5041",
  );
  const mailpitUrl = process.env.MAILPIT_URL;
  if (!mailpitUrl)
    throw new Error(
      "MAILPIT_URL fehlt. Starte Browserprüfungen über scripts/test-browser.ps1.",
    );

  const browser = await chromium.launch();
  try {
    const page = await browser.newPage({
      baseURL,
      locale: "de-DE",
      timezoneId: "Europe/Berlin",
    });
    const authenticationResponses: string[] = [];
    page.on("response", (response) => {
      if (response.url().includes("/api/v1/")) {
        authenticationResponses.push(
          `${new URL(response.url()).pathname}:${response.status()}`,
        );
      }
    });
    await page.goto("/passwort-vergessen");
    await page.getByLabel("E-Mail-Adresse").fill(email);
    await page.getByRole("button", { name: "Reset-Link anfordern" }).click();
    await expect(
      page.getByText(/Falls ein Konto zu dieser E-Mail-Adresse existiert/),
    ).toBeVisible();

    const resetToken = await pollForPasswordResetToken(mailpitUrl);
    await page.goto(`/passwort-zuruecksetzen?token=${resetToken}`);
    await page.getByLabel("Neues Passwort", { exact: true }).fill(password);
    await page
      .getByLabel("Neues Passwort bestätigen", { exact: true })
      .fill(password);
    await page.getByRole("button", { name: "Passwort speichern" }).click();
    await expect(page.getByText("Dein Passwort wurde geändert.")).toBeVisible();

    await page.goto("/anmelden");
    await page.getByLabel("E-Mail-Adresse").fill(email);
    await page.getByLabel("Passwort", { exact: true }).fill(password);
    await page.getByRole("checkbox", { name: /Angemeldet bleiben/ }).check();
    await page.getByRole("button", { name: "Anmelden", exact: true }).click();
    await expect(page).not.toHaveURL(/\/anmelden$/);

    await page.goto("/o/sonnenhoehe/camps");
    await expect(page.getByRole("heading", { name: "Camps" })).toBeVisible();
    if (
      (await page.getByRole("link", { name: "Browser-Testcamp" }).count()) === 0
    ) {
      const createCamp = page.getByRole("button", { name: "Camp anlegen" });
      try {
        await createCamp.waitFor({ state: "visible", timeout: 10_000 });
      } catch {
        const cookieMetadata = (await page.context().cookies()).map(
          ({ name, path: cookiePath, secure, expires }) => ({
            name,
            path: cookiePath,
            secure,
            persistent: expires > 0,
          }),
        );
        throw new Error(
          `Camp-Verwaltung nach Refresh nicht verfügbar (${page.url()}); Auth=${authenticationResponses.join(",")}; Cookies=${JSON.stringify(cookieMetadata)}: ${await page.locator("body").innerText()}`,
        );
      }
      await createCamp.click();
      await page.getByLabel("Name", { exact: true }).fill("Browser-Testcamp");
      await page.getByLabel("Slug", { exact: true }).fill("browser-testcamp");
      await page
        .getByLabel("Beschreibung")
        .fill(
          "Deterministische Daten für Browser- und Accessibility-Prüfungen.",
        );
      await page.getByRole("button", { name: "Camp speichern" }).click();
      await expect(
        page.getByRole("link", { name: "Browser-Testcamp" }),
      ).toBeVisible();
    }
  } finally {
    await browser.close();
  }
}

async function pollForPasswordResetToken(mailpitUrl: string): Promise<string> {
  const deadline = Date.now() + 20_000;
  while (Date.now() < deadline) {
    const response = await fetch(`${mailpitUrl}/api/v1/messages`);
    if (response.ok) {
      const payload: unknown = await response.json();
      if (isMailpitList(payload)) {
        const message = payload.messages.find(
          (item) =>
            item.To.some((recipient) => recipient.Address === email) &&
            item.Subject.includes("Setze dein Passwort"),
        );
        const match = message?.Snippet.match(
          /passwort-zuruecksetzen\?token=([A-F0-9]{64})/,
        );
        if (match) return match[1];
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`Mailpit hat keinen Passwort-Reset für ${email} geliefert.`);
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
