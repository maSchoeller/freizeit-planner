import { FormEvent, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "./api/client";

const genericMessage =
  "Wenn die Adresse registriert ist, wurde ein Anmeldecode versendet.";

export function LoginPage() {
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  const [rememberMe, setRememberMe] = useState(false);
  const [step, setStep] = useState<"email" | "code">("email");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const codeHeading = useRef<HTMLHeadingElement>(null);
  const navigate = useNavigate();

  useEffect(() => {
    if (step === "code") codeHeading.current?.focus();
  }, [step]);

  async function requestCode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const { response } = await api.POST("/api/v1/auth/code", {
        headers: await mutationHeaders(),
        body: { email },
      });
      if (!response.ok)
        throw new Error("Der Code konnte nicht angefordert werden.");
      setStep("code");
    } catch {
      setError(
        "Die Anmeldung ist gerade nicht erreichbar. Bitte versuche es erneut.",
      );
    } finally {
      setBusy(false);
    }
  }

  async function verifyCode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const { error, response } = await api.POST("/api/v1/auth/verify", {
        headers: await mutationHeaders(),
        body: { email, code, rememberMe },
      });
      if (!response.ok) {
        throw new Error(readProblemDetail(error));
      }
      void navigate("/o/sonnenhoehe/camps/sommerfreizeit-2026", {
        replace: true,
      });
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Der Anmeldecode konnte nicht geprüft werden.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-layout">
      <main id="main" className="login-card">
        <div className="login-brand" aria-label="Freizeit-Cockpit">
          <span className="brand-mark" aria-hidden="true">
            F
          </span>
          <span>Freizeit-Cockpit</span>
        </div>
        {step === "email" ? (
          <>
            <p className="eyebrow">Willkommen zurück</p>
            <h1>Im Freizeit-Cockpit anmelden</h1>
            <p>
              Du erhältst einen sechsstelligen Code per E-Mail. Ein Passwort
              brauchst du nicht.
            </p>
            <form onSubmit={(event) => void requestCode(event)} noValidate>
              <div className="field">
                <label htmlFor="login-email">E-Mail-Adresse</label>
                <input
                  id="login-email"
                  name="email"
                  type="email"
                  autoComplete="email"
                  required
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                />
              </div>
              <button className="primary-action" disabled={busy} type="submit">
                {busy ? "Code wird angefordert …" : "Anmeldecode anfordern"}
              </button>
            </form>
          </>
        ) : (
          <>
            <p className="eyebrow">E-Mail prüfen</p>
            <h1 ref={codeHeading} tabIndex={-1}>
              Anmeldecode eingeben
            </h1>
            <p role="status">{genericMessage}</p>
            <p className="muted">
              Der Code ist zehn Minuten gültig und kann nur einmal verwendet
              werden.
            </p>
            <form onSubmit={(event) => void verifyCode(event)} noValidate>
              <div className="field">
                <label htmlFor="login-code">Sechsstelliger Anmeldecode</label>
                <input
                  id="login-code"
                  name="code"
                  className="code-input"
                  type="text"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  pattern="[0-9]{6}"
                  minLength={6}
                  maxLength={6}
                  required
                  value={code}
                  onChange={(event) =>
                    setCode(event.target.value.replace(/\D/g, "").slice(0, 6))
                  }
                />
              </div>
              <label className="checkbox-field">
                <input
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(event) => setRememberMe(event.target.checked)}
                />
                <span>
                  Angemeldet bleiben
                  <small>Widerrufbare Sitzung für 30 Tage</small>
                </span>
              </label>
              <div className="login-actions">
                <button
                  className="primary-action"
                  disabled={busy}
                  type="submit"
                >
                  {busy ? "Code wird geprüft …" : "Anmelden"}
                </button>
                <button
                  className="secondary-action"
                  type="button"
                  onClick={() => {
                    setStep("email");
                    setCode("");
                    setError(null);
                  }}
                >
                  E-Mail-Adresse ändern
                </button>
              </div>
            </form>
          </>
        )}
        {error ? (
          <div className="error-message" role="alert">
            <strong>Anmeldung nicht möglich.</strong> {error}
          </div>
        ) : null}
        <p className="login-help">
          Probleme bei der Anmeldung?{" "}
          <a href="/hilfe/anmeldung">Hilfe öffnen</a>
        </p>
      </main>
    </div>
  );
}

async function mutationHeaders(): Promise<Record<string, string>> {
  const response = await fetch("/api/v1/auth/antiforgery");
  if (!response.ok)
    throw new Error("Die Sicherheitsprüfung ist fehlgeschlagen.");
  const payload: unknown = await response.json();
  if (
    typeof payload !== "object" ||
    payload === null ||
    !("token" in payload) ||
    typeof payload.token !== "string"
  ) {
    throw new Error("Die Sicherheitsprüfung ist fehlgeschlagen.");
  }
  return { "Content-Type": "application/json", "X-CSRF-TOKEN": payload.token };
}

function readProblemDetail(problem: unknown): string {
  if (
    typeof problem === "object" &&
    problem !== null &&
    "detail" in problem &&
    typeof problem.detail === "string"
  ) {
    return problem.detail;
  }
  return "Der Anmeldecode ist ungültig oder abgelaufen.";
}
