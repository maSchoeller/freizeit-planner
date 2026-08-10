import { FormEvent, useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { setAccessToken } from "./api/authentication";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";
import { Brand, PasswordField } from "./LoginPage";

export function FirstLoginPage() {
  const [available, setAvailable] = useState<boolean | null>(null);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    const controller = new AbortController();
    void fetch("/api/v1/auth/first-login", { signal: controller.signal })
      .then(async (response) => {
        const body: unknown = await response.json();
        setAvailable(response.ok && hasAvailability(body) && body.available);
      })
      .catch(() =>
        setError("Die Ersteinrichtung konnte nicht geprüft werden."),
      );
    return () => controller.abort();
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!event.currentTarget.checkValidity()) {
      event.currentTarget.querySelector<HTMLInputElement>(":invalid")?.focus();
      return;
    }
    if (password !== confirmation) {
      setError("Die beiden Passwörter stimmen nicht überein.");
      document.getElementById("first-password-confirmation")?.focus();
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const response = await fetch("/api/v1/auth/first-login", {
        method: "POST",
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": await getAntiforgeryToken(),
        },
        body: JSON.stringify({ email, password, firstName, lastName }),
      });
      const body: unknown = await response.json();
      if (!response.ok)
        throw new Error(
          readProblemDetail(
            body,
            "Der Superadmin konnte nicht angelegt werden.",
          ),
        );
      if (!hasAccessToken(body))
        throw new Error("Die Anmeldung ist unvollständig.");
      setAccessToken(body.accessToken);
      void navigate("/superadmin/organisationen", { replace: true });
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Die Ersteinrichtung ist fehlgeschlagen.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-layout">
      <main id="main" className="login-card">
        <Brand />
        <p className="eyebrow">Einmalige Ersteinrichtung</p>
        <h1>Ersten Superadmin anlegen</h1>
        {available === null && !error ? (
          <p role="status">Verfügbarkeit wird geprüft …</p>
        ) : null}
        {available === false ? (
          <div className="notice-card">
            <p>Die Ersteinrichtung wurde bereits abgeschlossen.</p>
            <Link className="primary-action" to="/anmelden">
              Zur Anmeldung
            </Link>
          </div>
        ) : null}
        {available ? (
          <form onSubmit={(event) => void submit(event)} noValidate>
            <div className="field-row">
              <div className="field">
                <label htmlFor="first-name">Vorname</label>
                <input
                  id="first-name"
                  autoComplete="given-name"
                  required
                  maxLength={80}
                  value={firstName}
                  onChange={(event) => setFirstName(event.target.value)}
                />
              </div>
              <div className="field">
                <label htmlFor="last-name">Nachname</label>
                <input
                  id="last-name"
                  autoComplete="family-name"
                  required
                  maxLength={80}
                  value={lastName}
                  onChange={(event) => setLastName(event.target.value)}
                />
              </div>
            </div>
            <div className="field">
              <label htmlFor="first-email">E-Mail-Adresse</label>
              <input
                id="first-email"
                type="email"
                autoComplete="email"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            </div>
            <p className="field-hint">
              Mindestens 15 Zeichen. Leerzeichen und Unicode sind erlaubt.
            </p>
            <PasswordField
              id="first-password"
              label="Passwort"
              autoComplete="new-password"
              value={password}
              show={showPassword}
              onChange={setPassword}
              onToggle={() => setShowPassword((current) => !current)}
            />
            <PasswordField
              id="first-password-confirmation"
              label="Passwort bestätigen"
              autoComplete="new-password"
              value={confirmation}
              show={showPassword}
              onChange={setConfirmation}
              onToggle={() => setShowPassword((current) => !current)}
            />
            <button className="primary-action" disabled={busy} type="submit">
              {busy ? "Superadmin wird angelegt …" : "Superadmin anlegen"}
            </button>
          </form>
        ) : null}
        {error ? (
          <div className="error-message" role="alert">
            {error}
          </div>
        ) : null}
      </main>
    </div>
  );
}

function hasAvailability(value: unknown): value is { available: boolean } {
  return (
    typeof value === "object" &&
    value !== null &&
    "available" in value &&
    typeof value.available === "boolean"
  );
}

function hasAccessToken(value: unknown): value is { accessToken: string } {
  return (
    typeof value === "object" &&
    value !== null &&
    "accessToken" in value &&
    typeof value.accessToken === "string"
  );
}
