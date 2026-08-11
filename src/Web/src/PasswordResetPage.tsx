import { FormEvent, useState } from "react";
import type { ReactNode } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";
import { PasswordField } from "./LoginPage";
import { AuthShell } from "./AuthShell";

export function PasswordResetPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token");
  return token ? (
    <PasswordResetConfirmation token={token} />
  ) : (
    <PasswordResetRequest />
  );
}

function PasswordResetRequest() {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!event.currentTarget.checkValidity()) {
      event.currentTarget.querySelector<HTMLInputElement>(":invalid")?.focus();
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const response = await fetch("/api/v1/auth/password-reset/request", {
        method: "POST",
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": await getAntiforgeryToken(),
        },
        body: JSON.stringify({ email }),
      });
      if (!response.ok)
        throw new Error("Die Anfrage konnte nicht gesendet werden.");
      setSent(true);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Die Anfrage konnte nicht gesendet werden.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <ResetLayout eyebrow="Passwort-Hilfe" heading="Passwort zurücksetzen">
      {sent ? (
        <div className="notice-card" role="status">
          <p>
            Falls ein Konto zu dieser E-Mail-Adresse existiert, wurde ein Link
            zum Zurücksetzen versendet.
          </p>
          <Link className="primary-action" to="/anmelden">
            Zur Anmeldung
          </Link>
        </div>
      ) : (
        <form onSubmit={(event) => void submit(event)} noValidate>
          <p>Wir senden dir einen einmalig verwendbaren Link per E-Mail.</p>
          <div className="field">
            <label htmlFor="reset-email">E-Mail-Adresse</label>
            <input
              id="reset-email"
              type="email"
              autoComplete="email"
              autoFocus
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          </div>
          <button className="primary-action" disabled={busy} type="submit">
            {busy ? "Link wird angefordert …" : "Reset-Link anfordern"}
          </button>
        </form>
      )}
      {error ? (
        <div className="error-message" role="alert">
          {error}
        </div>
      ) : null}
    </ResetLayout>
  );
}

function PasswordResetConfirmation({ token }: { token: string }) {
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [completed, setCompleted] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!event.currentTarget.checkValidity()) {
      event.currentTarget.querySelector<HTMLInputElement>(":invalid")?.focus();
      return;
    }
    if (password !== confirmation) {
      setError("Die beiden Passwörter stimmen nicht überein.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const response = await fetch("/api/v1/auth/password-reset/confirm", {
        method: "POST",
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": await getAntiforgeryToken(),
        },
        body: JSON.stringify({ token, newPassword: password }),
      });
      if (!response.ok) {
        const problem: unknown = await response.json();
        throw new Error(
          readProblemDetail(
            problem,
            "Der Link ist ungültig, abgelaufen oder wurde bereits verwendet.",
          ),
        );
      }
      setCompleted(true);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Das Passwort konnte nicht gesetzt werden.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <ResetLayout eyebrow="Neues Passwort" heading="Passwort neu festlegen">
      {completed ? (
        <div className="notice-card" role="status">
          <p>Dein Passwort wurde geändert. Du kannst dich jetzt anmelden.</p>
          <Link className="primary-action" to="/anmelden">
            Jetzt anmelden
          </Link>
        </div>
      ) : (
        <form onSubmit={(event) => void submit(event)} noValidate>
          <p className="field-hint">
            15 bis 128 Zeichen. Leerzeichen und Unicode sind erlaubt.
          </p>
          <PasswordField
            id="reset-password"
            label="Neues Passwort"
            autoComplete="new-password"
            value={password}
            show={showPassword}
            onChange={setPassword}
            onToggle={() => setShowPassword((current) => !current)}
          />
          <PasswordField
            id="reset-password-confirmation"
            label="Neues Passwort bestätigen"
            autoComplete="new-password"
            value={confirmation}
            show={showPassword}
            onChange={setConfirmation}
            onToggle={() => setShowPassword((current) => !current)}
          />
          <button className="primary-action" disabled={busy} type="submit">
            {busy ? "Passwort wird gesetzt …" : "Passwort speichern"}
          </button>
        </form>
      )}
      {error ? (
        <div className="error-message" role="alert">
          {error}
        </div>
      ) : null}
    </ResetLayout>
  );
}

function ResetLayout({
  eyebrow,
  heading,
  children,
}: {
  eyebrow: string;
  heading: string;
  children: ReactNode;
}) {
  return (
    <AuthShell eyebrow={eyebrow} heading={heading}>
      {children}
    </AuthShell>
  );
}
