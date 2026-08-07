import { FormEvent, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { api } from "./api/client";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";

export function InvitationPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const [displayName, setDisplayName] = useState("");
  const [busy, setBusy] = useState(false);
  const [accepted, setAccepted] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function accept(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const antiforgery = await getAntiforgeryToken();
      const { error: problem, response } = await api.POST(
        "/api/v1/invitations/accept",
        {
          headers: { "X-CSRF-TOKEN": antiforgery },
          body: { token, displayName },
        },
      );
      if (!response.ok) {
        throw new Error(
          readProblemDetail(
            problem,
            "Die Einladung ist ungültig oder abgelaufen.",
          ),
        );
      }
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

  return (
    <div className="login-layout">
      <main id="main" className="login-card">
        <div className="login-brand" aria-label="Freizeit-Cockpit">
          <span className="brand-mark" aria-hidden="true">
            F
          </span>
          <span>Freizeit-Cockpit</span>
        </div>
        <p className="eyebrow">Einladung</p>
        <h1>{accepted ? "Willkommen im Team" : "Einladung annehmen"}</h1>
        {accepted ? (
          <>
            <p role="status">
              Die Einladung wurde angenommen. Melde dich jetzt mit deiner
              eingeladenen E-Mail-Adresse an.
            </p>
            <Link className="primary-action button-link" to="/anmelden">
              Zur Anmeldung
            </Link>
          </>
        ) : token ? (
          <form onSubmit={(event) => void accept(event)} noValidate>
            <p>Gib den Namen ein, der deinem Planungsteam angezeigt wird.</p>
            <div className="field">
              <label htmlFor="invitation-name">Anzeigename</label>
              <input
                id="invitation-name"
                autoComplete="name"
                maxLength={160}
                required
                value={displayName}
                onChange={(event) => setDisplayName(event.target.value)}
              />
            </div>
            <button className="primary-action" disabled={busy} type="submit">
              {busy ? "Einladung wird geprüft …" : "Einladung annehmen"}
            </button>
          </form>
        ) : (
          <div className="error-message" role="alert">
            Der Einladungslink ist unvollständig. Öffne bitte den vollständigen
            Link aus deiner E-Mail.
          </div>
        )}
        {error ? (
          <div className="error-message" role="alert">
            {error}
          </div>
        ) : null}
        <p className="login-help">
          <a href="/hilfe/organisationen-camps-rollen">Hilfe zu Einladungen</a>
        </p>
      </main>
    </div>
  );
}
