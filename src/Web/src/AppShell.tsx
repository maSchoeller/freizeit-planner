import type { ReactNode } from "react";
import { Link } from "react-router-dom";

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
          <Link
            className="profile-button"
            aria-label={
              displayName
                ? `Konto von ${displayName} öffnen`
                : "Mein Konto öffnen"
            }
            to="/konto/profil"
          >
            {displayName ? accountInitials(displayName) : "Konto"}
          </Link>
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
