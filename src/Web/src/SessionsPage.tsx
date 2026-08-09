import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { clearOfflineSession } from "./offlineSnapshot";

interface SessionView {
  id: string;
  createdAt: string;
  expiresAt: string;
  ipAddress: string;
  isCurrent: boolean;
}

export function SessionsPage() {
  const [sessions, setSessions] = useState<SessionView[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busySession, setBusySession] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    const controller = new AbortController();
    void fetch("/api/v1/auth/sessions", { signal: controller.signal })
      .then(async (response) => {
        if (response.status === 401) {
          void navigate("/anmelden", { replace: true });
          return null;
        }
        if (!response.ok)
          throw new Error("Sitzungen konnten nicht geladen werden.");
        return readSessions(await response.json());
      })
      .then((result) => {
        if (result) setSessions(result);
      })
      .catch((caught: unknown) => {
        if (caught instanceof DOMException && caught.name === "AbortError")
          return;
        setError(
          "Sitzungen konnten nicht geladen werden. Bitte versuche es erneut.",
        );
      });
    return () => controller.abort();
  }, [navigate]);

  async function revoke(session: SessionView) {
    setBusySession(session.id);
    setError(null);
    try {
      const token = await getAntiforgeryToken();
      const response = await fetch(`/api/v1/auth/sessions/${session.id}`, {
        method: "DELETE",
        headers: { "X-CSRF-TOKEN": token },
      });
      if (!response.ok)
        throw new Error("Die Sitzung konnte nicht widerrufen werden.");
      if (session.isCurrent) {
        clearOfflineSession();
        void navigate("/anmelden", { replace: true });
      } else {
        setSessions(
          (current) =>
            current?.filter((item) => item.id !== session.id) ?? null,
        );
      }
    } catch {
      setError(
        "Die Sitzung konnte nicht widerrufen werden. Bitte versuche es erneut.",
      );
    } finally {
      setBusySession(null);
    }
  }

  async function revokeOthers() {
    setBusySession("others");
    setError(null);
    try {
      const token = await getAntiforgeryToken();
      const response = await fetch("/api/v1/auth/sessions/revoke-others", {
        method: "POST",
        headers: { "X-CSRF-TOKEN": token },
      });
      if (!response.ok)
        throw new Error("Andere Sitzungen konnten nicht widerrufen werden.");
      setSessions(
        (current) => current?.filter((item) => item.isCurrent) ?? null,
      );
    } catch {
      setError(
        "Andere Sitzungen konnten nicht widerrufen werden. Bitte versuche es erneut.",
      );
    } finally {
      setBusySession(null);
    }
  }

  return (
    <div className="account-layout">
      <header className="topbar">
        <Link className="brand" to="/o/sonnenhoehe/camps/sommerfreizeit-2026">
          <span className="brand-mark" aria-hidden="true">
            F
          </span>
          <span>Freizeit-Cockpit</span>
        </Link>
      </header>
      <main id="main" className="account-page">
        <p className="eyebrow">Konto & Sicherheit</p>
        <div className="section-heading account-heading">
          <div>
            <h1>Aktive Sitzungen</h1>
            <p>
              Hier siehst du, wo dein Konto angemeldet ist, und kannst Zugriffe
              sofort widerrufen.
            </p>
          </div>
          {sessions &&
          sessions.filter((session) => !session.isCurrent).length > 0 ? (
            <button
              className="secondary-action"
              disabled={busySession !== null}
              onClick={() => void revokeOthers()}
              type="button"
            >
              Alle anderen widerrufen
            </button>
          ) : null}
        </div>
        {error ? (
          <div className="error-message" role="alert">
            {error}
          </div>
        ) : null}
        {sessions === null && !error ? (
          <p role="status">Sitzungen werden geladen …</p>
        ) : null}
        {sessions?.length === 0 ? (
          <div className="empty-state">
            <h2>Keine aktive Sitzung gefunden</h2>
            <p>Melde dich erneut an, um das Freizeit-Cockpit zu verwenden.</p>
            <Link to="/anmelden">Zur Anmeldung</Link>
          </div>
        ) : null}
        {sessions && sessions.length > 0 ? (
          <ul className="session-list">
            {sessions.map((session) => (
              <li key={session.id}>
                <div>
                  <strong>
                    {session.isCurrent ? "Diese Sitzung" : "Weitere Sitzung"}
                  </strong>
                  <span>
                    Begonnen {formatDate(session.createdAt)} · IP{" "}
                    {session.ipAddress}
                  </span>
                  <span>Gültig bis {formatDate(session.expiresAt)}</span>
                </div>
                <button
                  className={
                    session.isCurrent ? "danger-action" : "secondary-action"
                  }
                  disabled={busySession !== null}
                  onClick={() => void revoke(session)}
                  type="button"
                >
                  {busySession === session.id
                    ? "Wird widerrufen …"
                    : "Sitzung widerrufen"}
                </button>
              </li>
            ))}
          </ul>
        ) : null}
      </main>
    </div>
  );
}

async function getAntiforgeryToken(): Promise<string> {
  const response = await fetch("/api/v1/auth/antiforgery");
  if (!response.ok)
    throw new Error("Sicherheits-Token konnte nicht geladen werden.");
  const value: unknown = await response.json();
  if (
    typeof value === "object" &&
    value !== null &&
    "token" in value &&
    typeof value.token === "string"
  ) {
    return value.token;
  }
  throw new Error("Sicherheits-Token fehlt.");
}

function readSessions(value: unknown): SessionView[] {
  if (!Array.isArray(value)) throw new Error("Ungültige Sitzungsantwort.");
  return value.filter(isSessionView);
}

function isSessionView(value: unknown): value is SessionView {
  return (
    typeof value === "object" &&
    value !== null &&
    "id" in value &&
    typeof value.id === "string" &&
    "createdAt" in value &&
    typeof value.createdAt === "string" &&
    "expiresAt" in value &&
    typeof value.expiresAt === "string" &&
    "ipAddress" in value &&
    typeof value.ipAddress === "string" &&
    "isCurrent" in value &&
    typeof value.isCurrent === "boolean"
  );
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat("de-DE", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}
