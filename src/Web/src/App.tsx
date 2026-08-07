import { Navigate, Route, Routes } from "react-router-dom";
import { LoginPage } from "./LoginPage";
import { SessionsPage } from "./SessionsPage";
import { InvitationPage } from "./InvitationPage";
import { AccountPage } from "./AccountPage";
import { OrganizationMembersPage } from "./OrganizationMembersPage";
import { PlatformOrganizationsPage } from "./PlatformOrganizationsPage";
import { CampWorkspace } from "./CampWorkspace";

export function App() {
  return (
    <Routes>
      <Route path="/anmelden" element={<LoginPage />} />
      <Route path="/einladung" element={<InvitationPage />} />
      <Route path="/konto" element={<AccountPage />} />
      <Route path="/konto/sitzungen" element={<SessionsPage />} />
      <Route
        path="/o/:organizationSlug/einstellungen/mitglieder"
        element={<OrganizationMembersPage />}
      />
      <Route
        path="/plattform/organisationen"
        element={<PlatformOrganizationsPage />}
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
  );
}
