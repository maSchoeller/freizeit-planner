import { useState } from "react";
import type { ReactNode } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  authenticatedFetch as fetch,
  clearAuthentication,
} from "./api/authentication";
import { getAntiforgeryToken } from "./api/security";
import { clearOfflineSession } from "./offlineSnapshot";

export function AppHeader({
  homeTo,
  displayName,
  organizationName,
  organizationSlug,
  canManageOrganization = false,
  isSuperAdmin = false,
  searchTo,
  status,
  profileAvailable = true,
}: {
  homeTo: string;
  displayName?: string;
  organizationName?: string;
  organizationSlug?: string;
  canManageOrganization?: boolean;
  isSuperAdmin?: boolean;
  searchTo?: string;
  status?: ReactNode;
  profileAvailable?: boolean;
}) {
  const navigate = useNavigate();
  const [logoutBusy, setLogoutBusy] = useState(false);
  const [logoutError, setLogoutError] = useState("");

  async function logout() {
    setLogoutBusy(true);
    setLogoutError("");
    try {
      const response = await fetch("/api/v1/auth/logout", {
        method: "POST",
        credentials: "same-origin",
        headers: { "X-CSRF-TOKEN": await getAntiforgeryToken() },
      });
      if (!response.ok) throw new Error("Die Abmeldung ist fehlgeschlagen.");
      clearAuthentication();
      clearOfflineSession();
      void navigate("/anmelden", { replace: true });
    } catch (reason) {
      setLogoutError(
        reason instanceof Error
          ? reason.message
          : "Die Abmeldung ist fehlgeschlagen.",
      );
    } finally {
      setLogoutBusy(false);
    }
  }

  return (
    <header className="topbar">
      <Link
        className="brand"
        to={homeTo}
        aria-label="Freizeit-Cockpit Startseite"
      >
        <span className="brand-mark" aria-hidden="true">
          F
        </span>
        <span>Freizeit-Cockpit</span>
      </Link>
      <nav className="global-navigation" aria-label="Globale Navigation">
        {organizationSlug ? (
          <Link to={`/o/${organizationSlug}/camps`}>
            {organizationName ?? "Freizeiten"}
          </Link>
        ) : null}
        {searchTo ? <Link to={searchTo}>Suche</Link> : null}
        {canManageOrganization && organizationSlug ? (
          <Link to={`/o/${organizationSlug}/verwaltung/team`}>
            Organisation verwalten
          </Link>
        ) : null}
        {isSuperAdmin ? (
          <Link to="/superadmin/organisationen">Plattform verwalten</Link>
        ) : null}
        <a href="/hilfe/">Hilfe</a>
      </nav>
      <div className="topbar-actions">
        {status}
        {profileAvailable ? (
          <details className="profile-menu">
            <summary
              className="profile-button"
              aria-label={
                displayName
                  ? `Kontomenü von ${displayName} öffnen`
                  : "Kontomenü öffnen"
              }
            >
              {displayName ? accountInitials(displayName) : "Konto"}
            </summary>
            <div className="profile-menu-panel">
              <Link to="/konto/profil">Mein Profil</Link>
              <Link to="/konto/sicherheit">Sicherheit &amp; Sitzungen</Link>
              <Link to="/konto/organisationen">Meine Organisationen</Link>
              {canManageOrganization && organizationSlug ? (
                <Link to={`/o/${organizationSlug}/verwaltung/team`}>
                  Organisation verwalten
                </Link>
              ) : null}
              {isSuperAdmin ? (
                <Link to="/superadmin/organisationen">Plattform verwalten</Link>
              ) : null}
              <button
                className="text-action"
                disabled={logoutBusy}
                onClick={() => void logout()}
                type="button"
              >
                {logoutBusy ? "Wird abgemeldet …" : "Abmelden"}
              </button>
              {logoutError ? <span role="alert">{logoutError}</span> : null}
            </div>
          </details>
        ) : (
          <span
            className="profile-button"
            aria-label="Kontomenü ist offline nicht verfügbar"
          >
            …
          </span>
        )}
      </div>
    </header>
  );
}

function accountInitials(displayName: string): string {
  return displayName
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((name) => name[0]?.toLocaleUpperCase("de-DE") ?? "")
    .join("");
}
