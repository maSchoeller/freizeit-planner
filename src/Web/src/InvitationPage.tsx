import { FormEvent, useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { api } from "./api/client";
import { restoreAuthentication, setAccessToken } from "./api/authentication";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";
import type { components } from "./api/schema";
import { PasswordField } from "./LoginPage";
import { AuthShell } from "./AuthShell";

type InvitationPreview = components["schemas"]["InvitationPreview"];

export function InvitationPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const [preview, setPreview] = useState<InvitationPreview | null>(null);
  const [signedIn, setSignedIn] = useState(false);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [passwordConfirmation, setPasswordConfirmation] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [registered, setRegistered] = useState(false);
  const [accepted, setAccepted] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      setError("Der Einladungslink ist unvollständig.");
      return;
    }
    const controller = new AbortController();
    void fetch(`/api/v1/invitations/${encodeURIComponent(token)}/preview`, {
      signal: controller.signal,
    })
      .then(async (response) => {
        if (!response.ok) throw new Error("Die Einladung ist ungültig.");
        const value: unknown = await response.json();
        if (!isPreview(value)) throw new Error("Die Einladung ist ungültig.");
        setPreview(value);
        if (value.status === 0) setSignedIn(await restoreAuthentication());
      })
      .catch((caught: unknown) => {
        if (caught instanceof DOMException && caught.name === "AbortError")
          return;
        setError(
          caught instanceof Error
            ? caught.message
            : "Die Einladung konnte nicht geladen werden.",
        );
      });
    return () => controller.abort();
  }, [token]);

  async function register(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!event.currentTarget.checkValidity()) {
      event.currentTarget.querySelector<HTMLInputElement>(":invalid")?.focus();
      return;
    }
    if (password !== passwordConfirmation) {
      setError("Die beiden Passwörter stimmen nicht überein.");
      document.getElementById("invitation-password-confirmation")?.focus();
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const response = await fetch(
        `/api/v1/invitations/${encodeURIComponent(token)}/register`,
        {
          method: "POST",
          credentials: "same-origin",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": await getAntiforgeryToken(),
          },
          body: JSON.stringify({
            email,
            password,
            passwordConfirmation,
            firstName,
            lastName,
          }),
        },
      );
      if (!response.ok) {
        const problem: unknown = await response.json();
        throw new Error(
          readProblemDetail(problem, "Das Konto konnte nicht erstellt werden."),
        );
      }
      setRegistered(true);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Das Konto konnte nicht erstellt werden.",
      );
    } finally {
      setBusy(false);
    }
  }

  async function acceptExisting() {
    setBusy(true);
    setError(null);
    try {
      const antiforgery = await getAntiforgeryToken();
      const { error: problem, response } = await api.POST(
        "/api/v1/invitations/{token}/accept",
        {
          params: { path: { token } },
          headers: { "X-CSRF-TOKEN": antiforgery },
        },
      );
      if (!response.ok)
        throw new Error(
          readProblemDetail(
            problem,
            "Die Einladung konnte nicht angenommen werden.",
          ),
        );
      setAccepted(true);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Die Einladung konnte nicht angenommen werden.",
      );
    } finally {
      setBusy(false);
    }
  }

  const unavailable = preview ? invitationStatusText(preview.status) : null;
  return (
    <AuthShell
      eyebrow="Einladung"
      heading={
        registered
          ? "E-Mail-Adresse bestätigen"
          : accepted
            ? "Einladung angenommen"
            : "Einladung annehmen"
      }
    >
      {!preview && !error ? (
        <p role="status">Einladung wird geprüft …</p>
      ) : null}
      {preview ? (
        <div className="notice-card">
          <strong>{grantDescription(preview)}</strong>
          <p>
            Gültig bis {new Date(preview.expiresAt).toLocaleString("de-DE")}.
          </p>
        </div>
      ) : null}
      {unavailable ? (
        <div className="error-message" role="alert">
          {unavailable}
        </div>
      ) : null}
      {registered ? (
        <p role="status">
          Wir haben dir einen Bestätigungslink gesendet. Öffne ihn innerhalb
          einer Stunde, um Registrierung und Einladung abzuschließen.
        </p>
      ) : null}
      {accepted ? (
        <p role="status">
          Die neue Berechtigung gehört jetzt zu deinem bestehenden Konto.
        </p>
      ) : null}
      {preview?.status === 0 && !registered && !accepted && signedIn ? (
        <button
          className="primary-action"
          disabled={busy}
          type="button"
          onClick={() => void acceptExisting()}
        >
          {busy ? "Einladung wird angenommen …" : "Einladung annehmen"}
        </button>
      ) : null}
      {preview?.status === 0 && !registered && !accepted && !signedIn ? (
        <form onSubmit={(event) => void register(event)} noValidate>
          <p>
            Erstelle dein Konto. Die E-Mail-Adresse bestätigst du anschließend
            per Link.
          </p>
          <div className="field-row">
            <div className="field">
              <label htmlFor="invitation-first-name">Vorname</label>
              <input
                id="invitation-first-name"
                autoComplete="given-name"
                required
                maxLength={80}
                value={firstName}
                onChange={(event) => setFirstName(event.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="invitation-last-name">Nachname</label>
              <input
                id="invitation-last-name"
                autoComplete="family-name"
                required
                maxLength={80}
                value={lastName}
                onChange={(event) => setLastName(event.target.value)}
              />
            </div>
          </div>
          <div className="field">
            <label htmlFor="invitation-email">E-Mail-Adresse</label>
            <input
              id="invitation-email"
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
            id="invitation-password"
            label="Passwort"
            autoComplete="new-password"
            value={password}
            show={showPassword}
            onChange={setPassword}
            onToggle={() => setShowPassword((current) => !current)}
            minLength={15}
            maxLength={128}
          />
          <PasswordField
            id="invitation-password-confirmation"
            label="Passwort bestätigen"
            autoComplete="new-password"
            value={passwordConfirmation}
            show={showPassword}
            onChange={setPasswordConfirmation}
            onToggle={() => setShowPassword((current) => !current)}
            minLength={15}
            maxLength={128}
          />
          <button className="primary-action" disabled={busy} type="submit">
            {busy ? "Konto wird vorbereitet …" : "Konto erstellen"}
          </button>
          <p className="login-help">
            Schon registriert? <Link to="/anmelden">Zuerst anmelden</Link>
          </p>
        </form>
      ) : null}
      {error ? (
        <div className="error-message" role="alert">
          {error}
        </div>
      ) : null}
      <p className="login-help">
        <a href="/hilfe/organisationen-camps-rollen">Hilfe zu Einladungen</a>
      </p>
    </AuthShell>
  );
}

export function InvitationConfirmationPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      setError("Der Bestätigungslink ist unvollständig.");
      return;
    }
    const controller = new AbortController();
    void (async () => {
      try {
        const response = await fetch("/api/v1/invitations/confirm", {
          method: "POST",
          credentials: "same-origin",
          signal: controller.signal,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": await getAntiforgeryToken(),
          },
          body: JSON.stringify({ token }),
        });
        const body: unknown = await response.json();
        if (!response.ok)
          throw new Error(
            readProblemDetail(body, "Der Bestätigungslink ist nicht gültig."),
          );
        if (!hasAccessToken(body))
          throw new Error("Die Anmeldung ist unvollständig.");
        setAccessToken(body.accessToken);
        setConfirmed(true);
      } catch (caught) {
        if (caught instanceof DOMException && caught.name === "AbortError")
          return;
        setError(
          caught instanceof Error
            ? caught.message
            : "Die E-Mail-Adresse konnte nicht bestätigt werden.",
        );
      }
    })();
    return () => controller.abort();
  }, [token]);

  return (
    <AuthShell
      eyebrow="Registrierung"
      heading={
        confirmed ? "E-Mail-Adresse bestätigt" : "E-Mail-Adresse bestätigen"
      }
    >
      {!confirmed && !error ? (
        <p role="status">Bestätigung wird geprüft …</p>
      ) : null}
      {confirmed ? (
        <>
          <p role="status">
            Dein Konto ist aktiv und die Einladung wurde angenommen.
          </p>
          <Link className="primary-action button-link" to="/">
            Zum Freizeit-Cockpit
          </Link>
        </>
      ) : null}
      {error ? (
        <div className="error-message" role="alert">
          {error}
        </div>
      ) : null}
    </AuthShell>
  );
}

function invitationStatusText(status: number): string | null {
  switch (status) {
    case 1:
      return "Diese Einladung ist vorübergehend für eine laufende Registrierung reserviert.";
    case 2:
      return "Diese Einladung wurde bereits verwendet.";
    case 3:
      return "Diese Einladung wurde widerrufen.";
    case 4:
      return "Diese Einladung ist abgelaufen.";
    default:
      return null;
  }
}

function grantDescription(preview: InvitationPreview): string {
  const grant = preview.grant;
  if (grant.isSuperAdmin) return "Superadmin für das gesamte Freizeit-Cockpit";
  if (grant.newOrganization)
    return `Organisationsadmin für die neue Organisation ${grant.newOrganization.name}`;
  if (grant.organizationRole !== null)
    return `Organisationsadmin für ${preview.organizationName ?? "die Organisation"}`;
  const campRole =
    grant.campRole === 0
      ? "Campleitung"
      : grant.campRole === 1
        ? "Mitarbeit"
        : "Leserechte";
  return `${campRole} für ${preview.campName ?? "eine Freizeit"}`;
}

function isPreview(value: unknown): value is InvitationPreview {
  return (
    typeof value === "object" &&
    value !== null &&
    "grant" in value &&
    "status" in value &&
    typeof value.status === "number" &&
    "expiresAt" in value &&
    typeof value.expiresAt === "string"
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
