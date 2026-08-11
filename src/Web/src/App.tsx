import { useEffect, useRef } from "react";
import {
  Navigate,
  Route,
  Routes,
  useLocation,
  useParams,
} from "react-router-dom";
import { LoginPage } from "./LoginPage";
import { InvitationConfirmationPage, InvitationPage } from "./InvitationPage";
import { AccountPage } from "./AccountPage";
import { PlatformOrganizationsPage } from "./PlatformOrganizationsPage";
import { CampWorkspace } from "./CampWorkspace";
import { CampsPage, CampSettingsPage } from "./CampsPage";
import { PwaUpdatePrompt } from "./PwaUpdatePrompt";
import { OnlineOnly } from "./OnlineOnly";
import { FirstLoginPage } from "./FirstLoginPage";
import { PasswordResetPage } from "./PasswordResetPage";
import {
  OrganizationUsersPage,
  SuperAdminUsersPage,
} from "./UserAdministrationPage";
import { LandingPage } from "./LandingPage";

export function App() {
  return (
    <>
      <PwaUpdatePrompt />
      <RouteEffects />
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route
          path="/anmelden"
          element={
            <OnlineOnly>
              <LoginPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/erste-einrichtung"
          element={
            <OnlineOnly>
              <FirstLoginPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/passwort-vergessen"
          element={
            <OnlineOnly>
              <PasswordResetPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/passwort-zuruecksetzen"
          element={
            <OnlineOnly>
              <PasswordResetPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/einladung"
          element={
            <OnlineOnly>
              <InvitationPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/einladung-bestaetigen"
          element={
            <OnlineOnly>
              <InvitationConfirmationPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/konto"
          element={<Navigate replace to="/konto/profil" />}
        />
        {(
          ["profil", "sicherheit", "organisationen", "datenschutz"] as const
        ).map((section) => (
          <Route
            key={section}
            path={`/konto/${section}`}
            element={
              <OnlineOnly>
                <AccountPage section={section} />
              </OnlineOnly>
            }
          />
        ))}
        <Route
          path="/konto/sitzungen"
          element={<Navigate replace to="/konto/sicherheit#sitzungen" />}
        />
        <Route
          path="/o/:organizationSlug/einstellungen/mitglieder"
          element={<OrganizationAdministrationRedirect />}
        />
        <Route
          path="/superadmin/organisationen"
          element={
            <OnlineOnly>
              <PlatformOrganizationsPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/superadmin/benutzer"
          element={
            <OnlineOnly>
              <SuperAdminUsersPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/o/:organizationSlug/verwaltung/benutzer"
          element={<OrganizationAdministrationRedirect />}
        />
        <Route
          path="/o/:organizationSlug/verwaltung/team"
          element={
            <OnlineOnly>
              <OrganizationUsersPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/o/:organizationSlug/camps"
          element={
            <OnlineOnly>
              <CampsPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/o/:organizationSlug/camps/:campSlug/einstellungen"
          element={
            <OnlineOnly>
              <CampSettingsPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/o/:organizationSlug/camps/:campSlug/*"
          element={<CampWorkspace />}
        />
        <Route path="*" element={<Navigate replace to="/" />} />
      </Routes>
    </>
  );
}

function RouteEffects() {
  const { pathname } = useLocation();
  const previousPath = useRef<string | null>(null);

  useEffect(() => {
    document.title = `${routeTitle(pathname)} | Freizeit-Cockpit`;
    if (previousPath.current && previousPath.current !== pathname) {
      const heading = document.querySelector<HTMLElement>("main h1");
      if (heading) {
        heading.tabIndex = -1;
        heading.focus();
      }
    }
    previousPath.current = pathname;
  }, [pathname]);

  return null;
}

function routeTitle(pathname: string) {
  if (pathname === "/anmelden") return "Anmelden";
  if (pathname === "/erste-einrichtung") return "Erste Einrichtung";
  if (pathname.startsWith("/passwort-")) return "Passwort zurücksetzen";
  if (pathname.startsWith("/einladung")) return "Einladung";
  if (pathname.startsWith("/konto/sicherheit")) return "Sicherheit";
  if (pathname.startsWith("/konto/organisationen"))
    return "Meine Organisationen";
  if (pathname.startsWith("/konto/datenschutz")) return "Datenschutz";
  if (pathname.startsWith("/konto")) return "Mein Profil";
  if (pathname === "/superadmin/organisationen")
    return "Plattformverwaltung – Organisationen";
  if (pathname === "/superadmin/benutzer")
    return "Plattformverwaltung – Benutzer";
  if (pathname.includes("/verwaltung/")) return "Organisationsverwaltung";
  if (pathname.endsWith("/einstellungen")) return "Einstellungen";
  if (pathname.endsWith("/tagesplan")) return "Tagesplan";
  if (pathname.endsWith("/essen")) return "Verpflegung";
  if (pathname.endsWith("/logistik")) return "Material & Einkauf";
  if (pathname.endsWith("/andachten")) return "Andachten";
  if (pathname.endsWith("/notizen")) return "Notizbuch";
  if (pathname.endsWith("/dateien")) return "Dateien";
  if (pathname.endsWith("/suche")) return "Suche";
  if (pathname.endsWith("/camps")) return "Freizeiten";
  if (pathname.includes("/camps/")) return "Freizeit";
  return "Startseite";
}

function OrganizationAdministrationRedirect() {
  const { organizationSlug = "" } = useParams();
  return <Navigate replace to={`/o/${organizationSlug}/verwaltung/team`} />;
}
