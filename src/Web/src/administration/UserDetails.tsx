import type {
  AdministrationMode,
  Camp,
  Membership,
  MembershipStatus,
  PendingAction,
  User,
} from "./support";
import { active, organizationAdmin, suspended } from "./support";

export function UserDetails({
  user,
  displayName,
  mode,
  membership,
  camps,
  selectedOrganizationId,
  busy,
  setPendingAction,
  changeGlobalStatus,
  clearLoginLockout,
  changeSuperAdmin,
  changeMembership,
  changeCampAssignment,
}: {
  user: User;
  displayName: string;
  mode: AdministrationMode;
  membership: Membership | null | undefined;
  camps: Camp[];
  selectedOrganizationId: string;
  busy: string | null;
  setPendingAction: (action: PendingAction) => void;
  changeGlobalStatus: (user: User) => Promise<void>;
  clearLoginLockout: (user: User) => Promise<void>;
  changeSuperAdmin: (user: User) => Promise<void>;
  changeMembership: (user: User, status: MembershipStatus) => Promise<void>;
  changeCampAssignment: (
    user: User,
    campId: string,
    role: number | null,
  ) => Promise<void>;
}) {
  return (
    <div className="admin-user-details">
      {mode === "superadmin" ? (
        <>
          <section aria-labelledby={`account-${user.userId}`}>
            <h3 id={`account-${user.userId}`}>Konto</h3>
            <p>
              {user.accountStatus === active
                ? "Das Konto ist global aktiv."
                : "Das Konto ist global gesperrt; alle Sitzungen sind beendet."}
            </p>
            <button
              className={
                user.accountStatus === active
                  ? "danger-action"
                  : "secondary-action"
              }
              disabled={busy !== null}
              onClick={() => {
                if (user.accountStatus !== active) {
                  void changeGlobalStatus(user);
                  return;
                }
                setPendingAction({
                  title: "Konto global sperren?",
                  description: `${displayName} verliert sofort jeden Plattform- und Organisationszugriff. Alle Sitzungen werden beendet.`,
                  confirmLabel: "Konto global sperren",
                  run: () => changeGlobalStatus(user),
                });
              }}
              type="button"
            >
              {user.accountStatus === active
                ? "Global sperren"
                : "Global entsperren"}
            </button>
            {user.loginLockedUntil ? (
              <button
                className="secondary-action"
                disabled={busy !== null}
                onClick={() => void clearLoginLockout(user)}
                type="button"
              >
                Anmeldesperre aufheben
              </button>
            ) : null}
          </section>
          <section aria-labelledby={`platform-role-${user.userId}`}>
            <h3 id={`platform-role-${user.userId}`}>Plattformrolle</h3>
            <p>{user.isSuperAdmin ? "Superadmin" : "Keine Plattformrolle"}</p>
            <button
              className={
                user.isSuperAdmin ? "danger-action" : "secondary-action"
              }
              disabled={busy !== null}
              onClick={() => {
                if (!user.isSuperAdmin) {
                  void changeSuperAdmin(user);
                  return;
                }
                setPendingAction({
                  title: "Superadmin-Rolle entziehen?",
                  description: `${displayName} kann danach keine Plattformkonten oder Organisationen mehr verwalten.`,
                  confirmLabel: "Superadmin-Rolle entziehen",
                  run: () => changeSuperAdmin(user),
                });
              }}
              type="button"
            >
              {user.isSuperAdmin
                ? "Superadmin entziehen"
                : "Zum Superadmin machen"}
            </button>
          </section>
          <section aria-labelledby={`organizations-${user.userId}`}>
            <h3 id={`organizations-${user.userId}`}>Organisationen</h3>
            <p>
              Organisationsadmin-Zugang für die oben ausgewählte Organisation
              vergeben.
            </p>
            {selectedOrganizationId ? (
              <button
                className="secondary-action"
                disabled={busy !== null}
                onClick={() => void changeMembership(user, active)}
                type="button"
              >
                Als Organisationsadmin zuweisen
              </button>
            ) : (
              <p>Keine Organisation vorhanden.</p>
            )}
          </section>
        </>
      ) : (
        <section aria-labelledby={`organization-${user.userId}`}>
          <h3 id={`organization-${user.userId}`}>Organisation</h3>
          <p>
            {membership?.status === active
              ? membership.role === organizationAdmin
                ? "Organisationsadmin · aktiv"
                : "Mitglied · aktiv"
              : membership?.status === suspended
                ? "In dieser Organisation gesperrt"
                : "Entfernt"}
          </p>
          <div className="admin-actions">
            <button
              className="secondary-action"
              disabled={busy !== null}
              onClick={() => void changeMembership(user, active)}
              type="button"
            >
              Als Organisationsadmin aktivieren
            </button>
            <button
              className="danger-action"
              disabled={busy !== null || membership?.status !== active}
              onClick={() =>
                setPendingAction({
                  title: "In dieser Organisation sperren?",
                  description: `${displayName} verliert den Zugriff auf diese Organisation und ihre Freizeiten. Andere Zugänge bleiben bestehen.`,
                  confirmLabel: "In Organisation sperren",
                  run: () => changeMembership(user, suspended),
                })
              }
              type="button"
            >
              In Organisation sperren
            </button>
          </div>
        </section>
      )}
      {mode === "organization" && membership?.status === active ? (
        <section aria-labelledby={`camps-${user.userId}`}>
          <h3 id={`camps-${user.userId}`}>Freizeiten</h3>
          <div className="camp-role-grid">
            {camps.map((camp) => {
              const assignment = membership.camps.find(
                (item) => item.campId === camp.id,
              );
              return (
                <label key={camp.id}>
                  {camp.name}
                  <select
                    aria-label={`Freizeitrolle für ${user.firstName} ${user.lastName} in ${camp.name}`}
                    disabled={busy !== null}
                    onChange={(event) =>
                      void changeCampAssignment(
                        user,
                        camp.id,
                        event.target.value === ""
                          ? null
                          : Number(event.target.value),
                      )
                    }
                    value={assignment?.role ?? ""}
                  >
                    <option value="">Kein Zugriff</option>
                    <option value="0">Freizeit-Leitung</option>
                    <option value="1">Mitarbeit</option>
                    <option value="2">Lesender Zugriff</option>
                  </select>
                </label>
              );
            })}
          </div>
        </section>
      ) : null}
    </div>
  );
}
