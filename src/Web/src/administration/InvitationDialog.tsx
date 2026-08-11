import { ModalDialog } from "../ModalDialog";
import type { AdministrationMode, Camp, Organization } from "./support";

export function InvitationDialog({
  mode,
  camps,
  organizations,
  invitationKind,
  setInvitationKind,
  invitationCampId,
  setInvitationCampId,
  globalInvitationKind,
  setGlobalInvitationKind,
  selectedOrganizationId,
  setSelectedOrganizationId,
  busy,
  invitationUnavailable,
  createInvitation,
  setShowInvitation,
}: {
  mode: AdministrationMode;
  camps: Camp[];
  organizations: Organization[];
  invitationKind: "orgadmin" | "campLead" | "member" | "viewer";
  setInvitationKind: (
    kind: "orgadmin" | "campLead" | "member" | "viewer",
  ) => void;
  invitationCampId: string;
  setInvitationCampId: (campId: string) => void;
  globalInvitationKind: "superadmin" | "orgadmin";
  setGlobalInvitationKind: (kind: "superadmin" | "orgadmin") => void;
  selectedOrganizationId: string;
  setSelectedOrganizationId: (organizationId: string) => void;
  busy: string | null;
  invitationUnavailable: boolean;
  createInvitation: () => Promise<void>;
  setShowInvitation: (show: boolean) => void;
}) {
  return (
    <ModalDialog
      labelledBy="invite-person-heading"
      onClose={() => setShowInvitation(false)}
    >
      <h2 id="invite-person-heading">Person einladen</h2>
      <p>Wähle nur den Zugang, den die Person wirklich benötigt.</p>
      {mode === "organization" ? (
        <div className="invitation-options">
          <label>
            Rolle des nächsten Einladungslinks
            <select
              autoFocus
              onChange={(event) =>
                setInvitationKind(
                  event.target.value as
                    "orgadmin" | "campLead" | "member" | "viewer",
                )
              }
              value={invitationKind}
            >
              <option value="orgadmin">Organisationsadmin</option>
              <option value="campLead">Freizeit-Leitung</option>
              <option value="member">Freizeitmitarbeit</option>
              <option value="viewer">Lesender Freizeitzugriff</option>
            </select>
          </label>
          {invitationKind !== "orgadmin" ? (
            <label>
              Freizeit
              <select
                onChange={(event) => setInvitationCampId(event.target.value)}
                required
                value={invitationCampId}
              >
                {camps.map((camp) => (
                  <option key={camp.id} value={camp.id}>
                    {camp.name}
                  </option>
                ))}
              </select>
            </label>
          ) : null}
        </div>
      ) : (
        <div className="invitation-options">
          <label>
            Rolle des nächsten Einladungslinks
            <select
              autoFocus
              onChange={(event) =>
                setGlobalInvitationKind(
                  event.target.value as "superadmin" | "orgadmin",
                )
              }
              value={globalInvitationKind}
            >
              <option value="superadmin">Superadmin</option>
              <option value="orgadmin">Organisationsadmin</option>
            </select>
          </label>
          {globalInvitationKind === "orgadmin" ? (
            <label className="admin-organization-choice">
              Organisation für Organisationsadmin-Rechte
              <select
                onChange={(event) =>
                  setSelectedOrganizationId(event.target.value)
                }
                required
                value={selectedOrganizationId}
              >
                <option value="">Organisation auswählen</option>
                {organizations.map((organization) => (
                  <option
                    key={organization.organizationId}
                    value={organization.organizationId}
                  >
                    {organization.name}
                  </option>
                ))}
              </select>
            </label>
          ) : null}
        </div>
      )}
      <div className="dialog-actions">
        <button
          className="primary-action"
          disabled={busy !== null || invitationUnavailable}
          onClick={() => void createInvitation()}
          type="button"
        >
          {busy === "invitation"
            ? "Link wird erstellt …"
            : "Einladungslink erstellen & kopieren"}
        </button>
        <button
          className="secondary-action"
          disabled={busy !== null}
          onClick={() => setShowInvitation(false)}
          type="button"
        >
          Abbrechen
        </button>
      </div>
    </ModalDialog>
  );
}
