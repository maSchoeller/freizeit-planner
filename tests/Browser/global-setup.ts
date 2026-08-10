import { chromium, expect, type FullConfig } from "@playwright/test";
import { mkdir } from "node:fs/promises";
import path from "node:path";

const email = "miriam@example.test";

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
    await page.goto("/anmelden");
    await page.getByLabel("E-Mail-Adresse").fill(email);
    await page.getByRole("button", { name: "Anmeldecode anfordern" }).click();
    await expect(
      page.getByRole("heading", { name: "Anmeldecode eingeben" }),
    ).toBeFocused();

    const code = await pollForLoginCode(mailpitUrl);
    await page.getByLabel("Sechsstelliger Anmeldecode").fill(code);
    await page.getByRole("button", { name: "Anmelden", exact: true }).click();
    await expect(page).not.toHaveURL(/\/anmelden$/);

    await page.goto("/o/sonnenhoehe/camps");
    await expect(page.getByRole("heading", { name: "Camps" })).toBeVisible();
    if (
      (await page.getByRole("link", { name: "Browser-Testcamp" }).count()) === 0
    ) {
      await page.getByRole("button", { name: "Camp anlegen" }).click();
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

    const statePath = path.resolve(".artifacts/playwright/auth/owner.json");
    await mkdir(path.dirname(statePath), { recursive: true });
    await page.context().storageState({ path: statePath });
  } finally {
    await browser.close();
  }
}

async function pollForLoginCode(mailpitUrl: string): Promise<string> {
  const deadline = Date.now() + 20_000;
  while (Date.now() < deadline) {
    const response = await fetch(`${mailpitUrl}/api/v1/messages`);
    if (response.ok) {
      const payload: unknown = await response.json();
      if (isMailpitList(payload)) {
        const message = payload.messages.find(
          (item) =>
            item.To.some((recipient) => recipient.Address === email) &&
            item.Subject.includes("Anmeldecode"),
        );
        const match = message?.Snippet.match(/\b(\d{6})\b/);
        if (match) return match[1];
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`Mailpit hat keinen Anmeldecode für ${email} geliefert.`);
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
