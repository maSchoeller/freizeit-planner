import { Navigate, Route, Routes } from "react-router-dom";
import { LoginPage } from "./LoginPage";
import { InvitationConfirmationPage, InvitationPage } from "./InvitationPage";
import { AccountPage } from "./AccountPage";
import { OrganizationMembersPage } from "./OrganizationMembersPage";
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
          element={
            <OnlineOnly>
              <OrganizationMembersPage />
            </OnlineOnly>
          }
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
