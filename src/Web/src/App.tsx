import { Navigate, Route, Routes } from "react-router-dom";
import { LoginPage } from "./LoginPage";
import { SessionsPage } from "./SessionsPage";
import { InvitationPage } from "./InvitationPage";
import { AccountPage } from "./AccountPage";
import { OrganizationMembersPage } from "./OrganizationMembersPage";
import { PlatformOrganizationsPage } from "./PlatformOrganizationsPage";
import { CampWorkspace } from "./CampWorkspace";
import { CampsPage, CampSettingsPage } from "./CampsPage";
import { PwaUpdatePrompt } from "./PwaUpdatePrompt";
import { OnlineOnly } from "./OnlineOnly";

export function App() {
  return (
    <>
      <PwaUpdatePrompt />
      <Routes>
        <Route
          path="/anmelden"
          element={
            <OnlineOnly>
              <LoginPage />
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
          path="/konto"
          element={
            <OnlineOnly>
              <AccountPage />
            </OnlineOnly>
          }
        />
        <Route
          path="/konto/sitzungen"
          element={
            <OnlineOnly>
              <SessionsPage />
            </OnlineOnly>
          }
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
          path="/plattform/organisationen"
          element={
            <OnlineOnly>
              <PlatformOrganizationsPage />
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
        <Route
          path="*"
          element={
            <Navigate replace to="/o/sonnenhoehe/camps/sommerfreizeit-2026" />
          }
        />
      </Routes>
    </>
  );
}
