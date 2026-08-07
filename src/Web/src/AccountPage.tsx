import { FormEvent, useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import type { components } from "./api/schema";
import { api } from "./api/client";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";

type Account = components["schemas"]["AccountView"];
type Membership = components["schemas"]["AccountMembershipView"];

export function AccountPage() {
  const [account, setAccount] = useState<Account | null>(null);
  const [memberships, setMemberships] = useState<Membership[]>([]);
  const [displayName, setDisplayName] = useState("");
  const [newEmail, setNewEmail] = useState("");
  const [emailCode, setEmailCode] = useState("");
  const [emailCodeRequested, setEmailCodeRequested] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [confirmDeletion, setConfirmDeletion] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    const controller = new AbortController();
    void Promise.all([
      api.GET("/api/v1/account", { signal: controller.signal }),
      api.GET("/api/v1/account/memberships", { signal: controller.signal }),
    ])
      .then(([accountResult, membershipResult]) => {
        if (accountResult.response.status === 401) {
          void navigate("/anmelden", { replace: true });
          return;
        }
        if (!accountResult.data || !membershipResult.data) {
          throw new Error("Kontodaten konnten nicht geladen werden.");
        }
        setAccount(accountResult.data);
        setDisplayName(accountResult.data.displayName);
        setMemberships(membershipResult.data);
      })
      .catch((caught: unknown) => {
        if (caught instanceof DOMException && caught.name === "AbortError")
          return;
        setError(
          "Kontodaten konnten nicht geladen werden. Bitte versuche es erneut.",
        );
      })
      .finally(() => setLoading(false));
    return () => controller.abort();
  }, [navigate]);

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await mutate("profile", async (token) => {
      const result = await api.PATCH("/api/v1/account/profile", {
        headers: { "X-CSRF-TOKEN": token },
        body: { displayName },
      });
      if (!result.data)
        throw new Error(
          readProblemDetail(
            result.error,
            "Der Name konnte nicht gespeichert werden.",
          ),
        );
      setAccount(result.data);
    });
  }

  async function scheduleDeletion() {
    await mutate("deletion", async (token) => {
      const result = await api.POST("/api/v1/account/deletion", {
        headers: { "X-CSRF-TOKEN": token },
      });
      if (!result.data)
        throw new Error(
          readProblemDetail(
            result.error,
            "Das Konto konnte nicht vorgemerkt werden.",
          ),
        );
      setAccount((current) =>
        current
          ? { ...current, deletionScheduledAt: result.data?.scheduledAt }
          : current,
      );
      setConfirmDeletion(false);
    });
  }

  async function requestEmailChange(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await mutate("email", async (token) => {
      const result = await api.POST("/api/v1/account/email-change", {
        headers: { "X-CSRF-TOKEN": token },
        body: { email: newEmail },
      });
      if (!result.response.ok) {
        throw new Error(
          readProblemDetail(
            result.error,
            "Der Einmalcode konnte nicht angefordert werden.",
          ),
        );
      }
      setEmailCodeRequested(true);
    });
  }

  async function confirmEmailChange(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await mutate("email", async (token) => {
      const result = await api.POST("/api/v1/account/email-change/confirm", {
        headers: { "X-CSRF-TOKEN": token },
        body: { email: newEmail, code: emailCode },
      });
      if (!result.data || result.data.outcome !== 0 || !result.data.email) {
        throw new Error(
          readProblemDetail(result.error, "Der Einmalcode ist nicht gültig."),
        );
      }
      const changedEmail = result.data.email;
      setAccount((current) =>
        current ? { ...current, email: changedEmail } : current,
      );
      setNewEmail("");
      setEmailCode("");
      setEmailCodeRequested(false);
    });
  }

  async function cancelDeletion() {
    await mutate("deletion", async (token) => {
      const result = await api.DELETE("/api/v1/account/deletion", {
        headers: { "X-CSRF-TOKEN": token },
      });
      if (!result.response.ok)
        throw new Error("Die Löschvormerkung konnte nicht aufgehoben werden.");
      setAccount((current) =>
        current ? { ...current, deletionScheduledAt: null } : current,
      );
    });
  }

  async function leave(membership: Membership) {
    await mutate(membership.organizationId, async (token) => {
      const result = await api.POST(
        "/api/v1/account/organizations/{organizationId}/leave",
        {
          params: { path: { organizationId: membership.organizationId } },
          headers: { "X-CSRF-TOKEN": token },
        },
      );
      if (!result.response.ok)
        throw new Error(
          readProblemDetail(
            result.error,
            "Die Organisation konnte nicht verlassen werden.",
          ),
        );
      setMemberships((current) =>
        current.filter(
          (item) => item.organizationId !== membership.organizationId,
        ),
      );
    });
  }

  async function mutate(key: string, action: (token: string) => Promise<void>) {
    setBusy(key);
    setError(null);
    try {
      await action(await getAntiforgeryToken());
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Die Änderung konnte nicht gespeichert werden.",
      );
    } finally {
      setBusy(null);
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
        <p className="eyebrow">Konto</p>
        <h1>Mein Konto</h1>
        {loading ? <p role="status">Kontodaten werden geladen …</p> : null}
        {error ? (
          <div className="error-message" role="alert">
            {error}
          </div>
        ) : null}
        {account ? (
          <>
            <section
              className="settings-section"
              aria-labelledby="profile-heading"
            >
              <h2 id="profile-heading">Profil</h2>
              <p className="muted">Anmeldung: {account.email}</p>
              <form onSubmit={(event) => void saveProfile(event)}>
                <div className="field">
                  <label htmlFor="account-name">Anzeigename</label>
                  <input
                    id="account-name"
                    required
                    maxLength={160}
                    value={displayName}
                    onChange={(event) => setDisplayName(event.target.value)}
                  />
                </div>
                <button
                  className="primary-action"
                  disabled={busy !== null}
                  type="submit"
                >
                  {busy === "profile"
                    ? "Wird gespeichert …"
                    : "Anzeigename speichern"}
                </button>
              </form>
              <p>
                <Link to="/konto/sitzungen">Aktive Sitzungen verwalten</Link>
              </p>
            </section>
            <section
              className="settings-section"
              aria-labelledby="email-heading"
            >
              <h2 id="email-heading">E-Mail-Adresse ändern</h2>
              <p>
                Die neue Adresse wird erst nach Eingabe eines sechsstelligen
                Einmalcodes übernommen.
              </p>
              {!emailCodeRequested ? (
                <form onSubmit={(event) => void requestEmailChange(event)}>
                  <div className="field">
                    <label htmlFor="new-email">Neue E-Mail-Adresse</label>
                    <input
                      id="new-email"
                      autoComplete="email"
                      required
                      type="email"
                      value={newEmail}
                      onChange={(event) => setNewEmail(event.target.value)}
                    />
                  </div>
                  <button
                    className="primary-action"
                    disabled={busy !== null}
                    type="submit"
                  >
                    {busy === "email"
                      ? "Code wird angefordert …"
                      : "Einmalcode anfordern"}
                  </button>
                </form>
              ) : (
                <form onSubmit={(event) => void confirmEmailChange(event)}>
                  <p role="status">
                    Ein Einmalcode wurde an {newEmail} gesendet.
                  </p>
                  <div className="field">
                    <label htmlFor="email-code">Einmalcode</label>
                    <input
                      id="email-code"
                      autoComplete="one-time-code"
                      inputMode="numeric"
                      maxLength={6}
                      minLength={6}
                      pattern="[0-9]{6}"
                      required
                      value={emailCode}
                      onChange={(event) =>
                        setEmailCode(event.target.value.replace(/\D/g, ""))
                      }
                    />
                  </div>
                  <div className="login-actions">
                    <button
                      className="primary-action"
                      disabled={busy !== null}
                      type="submit"
                    >
                      {busy === "email"
                        ? "Wird bestätigt …"
                        : "Adresse bestätigen"}
                    </button>
                    <button
                      className="secondary-action"
                      disabled={busy !== null}
                      onClick={() => {
                        setEmailCodeRequested(false);
                        setEmailCode("");
                      }}
                      type="button"
                    >
                      Andere Adresse
                    </button>
                  </div>
                </form>
              )}
            </section>
            <section
              className="settings-section"
              aria-labelledby="memberships-heading"
            >
              <h2 id="memberships-heading">Organisationen</h2>
              {memberships.length === 0 ? (
                <p>Keine aktive Mitgliedschaft.</p>
              ) : (
                <ul className="membership-list">
                  {memberships.map((membership) => (
                    <li key={membership.organizationId}>
                      <div>
                        <strong>{membership.organizationName}</strong>
                        <span>{roleLabel(membership.role)}</span>
                      </div>
                      <button
                        className="secondary-action"
                        disabled={busy !== null}
                        onClick={() => void leave(membership)}
                        type="button"
                      >
                        {busy === membership.organizationId
                          ? "Wird verlassen …"
                          : "Organisation verlassen"}
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </section>
            <section
              className="settings-section danger-zone"
              aria-labelledby="delete-account-heading"
            >
              <h2 id="delete-account-heading">Konto löschen</h2>
              {account.deletionScheduledAt ? (
                <>
                  <p role="status">
                    Das Konto ist seit {formatDate(account.deletionScheduledAt)}{" "}
                    zur Löschung vorgemerkt. Die endgültige Löschung erfolgt
                    nach 30 Tagen.
                  </p>
                  <button
                    className="secondary-action"
                    disabled={busy !== null}
                    onClick={() => void cancelDeletion()}
                    type="button"
                  >
                    Löschung abbrechen
                  </button>
                </>
              ) : confirmDeletion ? (
                <div className="confirmation-panel">
                  <p>
                    Dein Konto wird 30 Tage vorgemerkt. Als letzter Owner ist
                    die Vormerkung nicht möglich.
                  </p>
                  <div className="login-actions">
                    <button
                      className="danger-action"
                      disabled={busy !== null}
                      onClick={() => void scheduleDeletion()}
                      type="button"
                    >
                      Konto vormerken
                    </button>
                    <button
                      className="secondary-action"
                      onClick={() => setConfirmDeletion(false)}
                      type="button"
                    >
                      Abbrechen
                    </button>
                  </div>
                </div>
              ) : (
                <button
                  className="danger-action"
                  onClick={() => setConfirmDeletion(true)}
                  type="button"
                >
                  Konto zur Löschung vormerken
                </button>
              )}
            </section>
          </>
        ) : null}
      </main>
    </div>
  );
}

function roleLabel(role: Membership["role"]): string {
  return (
    (
      {
        0: "Owner",
        1: "Organisations-Admin",
        2: "Camp-Leitung",
        3: "Mitglied",
        4: "Lesender Zugriff",
      } satisfies Record<number, string>
    )[role] ?? "Unbekannte Rolle"
  );
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat("de-DE", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}
