import { NavLink } from "react-router-dom";

export function PlatformAdministrationNavigation() {
  return (
    <nav
      className="section-navigation administration-navigation"
      aria-label="Plattformverwaltung"
    >
      <NavLink to="/superadmin/organisationen">Organisationen</NavLink>
      <NavLink to="/superadmin/benutzer">Benutzer</NavLink>
    </nav>
  );
}

export function OrganizationAdministrationNavigation({
  organizationSlug,
}: {
  organizationSlug: string;
}) {
  return (
    <nav
      className="section-navigation administration-navigation"
      aria-label="Organisationsverwaltung"
    >
      <NavLink to={`/o/${organizationSlug}/camps`}>Freizeiten</NavLink>
      <NavLink to={`/o/${organizationSlug}/verwaltung/team`}>
        Team &amp; Rechte
      </NavLink>
    </nav>
  );
}
