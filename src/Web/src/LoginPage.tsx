import { FormEvent, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { setAccessToken } from "./api/authentication";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";

export function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [emailError, setEmailError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!event.currentTarget.checkValidity()) {
      const invalid =
        event.currentTarget.querySelector<HTMLInputElement>(":invalid");
      if (invalid?.name === "email")
        setEmailError("Gib eine gültige E-Mail-Adresse ein.");
      invalid?.focus();
      return;
    }
    setBusy(true);
    setEmailError(null);
    setError(null);
    try {
      const response = await fetch("/api/v1/auth/login", {
        method: "POST",
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": await getAntiforgeryToken(),
        },
        body: JSON.stringify({ email, password, rememberMe }),
      });
      const body: unknown = await response.json();
      if (!response.ok)
        throw new Error(
          readProblemDetail(
            body,
            "E-Mail-Adresse oder Passwort ist nicht korrekt.",
          ),
        );
      if (!hasAccessToken(body))
        throw new Error("Die Anmeldung ist unvollständig.");
      setAccessToken(body.accessToken);
      void navigate("/", { replace: true });
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Die Anmeldung ist gerade nicht erreichbar.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-layout">
      <main id="main" className="login-card">
        <Brand />
        <p className="eyebrow">Willkommen zurück</p>
        <h1>Im Freizeit-Cockpit anmelden</h1>
        <p>Melde dich mit deiner E-Mail-Adresse und deinem Passwort an.</p>
        <form onSubmit={(event) => void submit(event)} noValidate>
          <div className="field">
            <label htmlFor="login-email">E-Mail-Adresse</label>
            <input
              id="login-email"
              name="email"
              type="email"
              autoComplete="email"
              autoFocus
              required
              aria-describedby={emailError ? "login-email-error" : undefined}
              aria-invalid={emailError ? "true" : undefined}
              value={email}
              onChange={(event) => {
                setEmail(event.target.value);
                setEmailError(null);
              }}
            />
            {emailError ? (
              <span id="login-email-error" className="field-error">
                {emailError}
              </span>
            ) : null}
          </div>
          <PasswordField
            id="login-password"
            label="Passwort"
            autoComplete="current-password"
            value={password}
            show={showPassword}
            onChange={setPassword}
            onToggle={() => setShowPassword((current) => !current)}
          />
          <label className="checkbox-field">
            <input
              type="checkbox"
              checked={rememberMe}
              onChange={(event) => setRememberMe(event.target.checked)}
            />
            <span>
              Angemeldet bleiben
              <small>Die Sitzung wird bei Nutzung jeweils verlängert.</small>
            </span>
          </label>
          <button className="primary-action" disabled={busy} type="submit">
            {busy ? "Anmeldung läuft …" : "Anmelden"}
          </button>
        </form>
        {error ? (
          <div className="error-message" role="alert">
            {error}
          </div>
        ) : null}
        <p className="login-help">
          <Link to="/passwort-vergessen">Passwort vergessen?</Link>
          {" · "}
          <Link to="/erste-einrichtung">Erste Einrichtung</Link>
        </p>
      </main>
    </div>
  );
}

export function PasswordField(props: {
  id: string;
  label: string;
  autoComplete: string;
  value: string;
  show: boolean;
  onChange: (value: string) => void;
  onToggle: () => void;
  minLength?: number;
  maxLength?: number;
}) {
  return (
    <div className="field">
      <label htmlFor={props.id}>{props.label}</label>
      <input
        id={props.id}
        name={props.id}
        type={props.show ? "text" : "password"}
        autoComplete={props.autoComplete}
        required
        minLength={props.minLength}
        maxLength={props.maxLength}
        value={props.value}
        onChange={(event) => props.onChange(event.target.value)}
      />
      <button className="text-action" type="button" onClick={props.onToggle}>
        {props.show ? "Passwort verbergen" : "Passwort anzeigen"}
      </button>
    </div>
  );
}

export function Brand() {
  return (
    <div className="login-brand" aria-label="Freizeit-Cockpit">
      <span className="brand-mark" aria-hidden="true">
        F
      </span>
      <span>Freizeit-Cockpit</span>
    </div>
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
