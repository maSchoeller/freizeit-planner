import { FormEvent, useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import type { components } from "./api/schema";
import { api } from "./api/client";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";
import { SettingsLayout } from "./OrganizationMembersPage";
import {
  OrganizationAdministrationNavigation,
  PlatformAdministrationNavigation,
} from "./AdministrationNavigation";
import { ModalDialog } from "./ModalDialog";

type User = components["schemas"]["UserAdministrationView"];
type Page = components["schemas"]["AdministrationPageOfUserAdministrationView"];
type Membership = components["schemas"]["OrganizationAdministrationView"];
type MembershipStatus = components["schemas"]["MembershipStatus"];
type Organization = components["schemas"]["SuperAdminOrganizationView"];
type Camp = components["schemas"]["CampSummary"];
type CampRole = components["schemas"]["CampRole"];

const active = 0;
const suspended = 1;
const removed = 2;
const organizationAdmin = 0;

export function SuperAdminUsersPage() {
  return <UserAdministrationPage mode="superadmin" />;
}

export function OrganizationUsersPage() {
  return <UserAdministrationPage mode="organization" />;
}

function UserAdministrationPage({
  mode,
}: {
  mode: "superadmin" | "organization";
}) {
  const { organizationSlug = "" } = useParams();
  const navigate = useNavigate();
  const [organizationId, setOrganizationId] = useState<string | null>(null);
  const [organizationName, setOrganizationName] = useState("");
  const [organizations, setOrganizations] = useState<Organization[]>([]);
  const [selectedOrganizationId, setSelectedOrganizationId] = useState("");
  const [camps, setCamps] = useState<Camp[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copiedLink, setCopiedLink] = useState<string | null>(null);
  const [showInvitation, setShowInvitation] = useState(false);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);
  const [pendingAction, setPendingAction] = useState<{
    title: string;
    description: string;
    confirmLabel: string;
    run: () => Promise<void>;
  } | null>(null);
  const [globalInvitationKind, setGlobalInvitationKind] = useState<
    "superadmin" | "orgadmin"
  >("superadmin");
  const [invitationKind, setInvitationKind] = useState<
    "orgadmin" | "campLead" | "member" | "viewer"
  >("orgadmin");
  const [invitationCampId, setInvitationCampId] = useState("");

  useEffect(() => {
    if (mode === "superadmin") {
      void api.GET("/api/v1/superadmin/organizations").then((result) => {
        if (!result.data) return;
        setOrganizations(result.data);
        setSelectedOrganizationId(result.data[0]?.organizationId ?? "");
      });
      return;
    }
    const controller = new AbortController();
    void api
      .GET("/api/v1/account/memberships", { signal: controller.signal })
      .then((result) => {
        if (result.response.status === 401) {
          void navigate("/anmelden", { replace: true });
          return;
        }
        const organization = result.data?.find(
          (item) => item.organizationSlug === organizationSlug,
        );
        if (!organization)
          throw new Error("Die Organisation wurde nicht gefunden.");
        setOrganizationId(organization.organizationId);
        setOrganizationName(organization.organizationName);
      })
      .catch((caught: unknown) => {
        if (caught instanceof DOMException && caught.name === "AbortError")
          return;
        setError(
          message(caught, "Die Organisation konnte nicht geladen werden."),
        );
        setLoading(false);
      });
    return () => controller.abort();
  }, [mode, navigate, organizationSlug]);

  useEffect(() => {
    if (mode !== "organization" || !organizationId) return;
    const controller = new AbortController();
    void api
      .GET("/api/v1/organizations/{organizationId}/camps", {
        params: { path: { organizationId } },
        signal: controller.signal,
      })
      .then((result) => {
        if (result.data) {
          setCamps(result.data);
          setInvitationCampId(result.data[0]?.id ?? "");
        }
      });
    return () => controller.abort();
  }, [mode, organizationId]);

  const load = useCallback(async () => {
    if (mode === "organization" && !organizationId) return;
    setLoading(true);
    setError(null);
    try {
      let data: Page | undefined;
      let response: Response;
      if (mode === "superadmin") {
        const result = await api.GET("/api/v1/superadmin/users", {
          params: {
            query: { search: search || undefined, page, pageSize: 25 },
          },
        });
        data = result.data;
        response = result.response;
        if (!data)
          throw new Error(
            readProblemDetail(
              result.error,
              "Benutzer konnten nicht geladen werden.",
            ),
          );
      } else {
        const result = await api.GET(
          "/api/v1/organizations/{organizationId}/administration/users",
          {
            params: {
              path: { organizationId: organizationId! },
              query: { search: search || undefined, page, pageSize: 25 },
            },
          },
        );
        data = result.data;
        response = result.response;
        if (!data)
          throw new Error(
            readProblemDetail(
              result.error,
              "Benutzer konnten nicht geladen werden.",
            ),
          );
      }
      if (response.status === 401) {
        void navigate("/anmelden", { replace: true });
        return;
      }
      setUsers(data.items);
      setTotalCount(Number(data.totalCount));
    } catch (caught) {
      setError(message(caught, "Benutzer konnten nicht geladen werden."));
    } finally {
      setLoading(false);
    }
  }, [mode, navigate, organizationId, page, search]);

  useEffect(() => {
    void load();
  }, [load]);

  function submitSearch(event: FormEvent) {
    event.preventDefault();
    setPage(1);
    void load();
  }

  async function changeGlobalStatus(user: User) {
    await mutate(user.userId, async (token) => {
      const result = await api.PATCH(
        "/api/v1/superadmin/users/{userId}/status",
        {
          params: { path: { userId: user.userId } },
          headers: versionHeaders(token, user.version),
          body: { status: user.accountStatus === active ? suspended : active },
        },
      );
      if (!result.data)
        throw new Error(
          readProblemDetail(
            result.error,
            "Der Kontostatus konnte nicht geändert werden.",
          ),
        );
      replaceUser(result.data);
    });
  }

  async function changeSuperAdmin(user: User) {
    await mutate(user.userId, async (token) => {
      const result = await api.PATCH(
        "/api/v1/superadmin/users/{userId}/superadmin",
        {
          params: { path: { userId: user.userId } },
          headers: versionHeaders(token, user.version),
          body: { isSuperAdmin: !user.isSuperAdmin },
        },
      );
      if (!result.data)
        throw new Error(
          readProblemDetail(
            result.error,
            "Das Superadmin-Recht konnte nicht geändert werden.",
          ),
        );
      replaceUser(result.data);
    });
  }

  async function clearLoginLockout(user: User) {
    await mutate(user.userId, async (token) => {
      const result = await api.POST(
        "/api/v1/superadmin/users/{userId}/unlock",
        {
          params: { path: { userId: user.userId } },
          headers: versionHeaders(token, user.version),
        },
      );
      if (!result.data)
        throw new Error(
          readProblemDetail(
            result.error,
            "Die Anmeldesperre konnte nicht aufgehoben werden.",
          ),
        );
      replaceUser(result.data);
    });
  }

  async function changeMembership(user: User, status: MembershipStatus) {
    const targetOrganizationId =
      mode === "superadmin" ? selectedOrganizationId : organizationId;
    if (!targetOrganizationId) return;
    const membership = membershipFor(user, targetOrganizationId);
    await mutate(user.userId, async (token) => {
      const request = {
        params: {
          path: {
            organizationId: targetOrganizationId,
            userId: user.userId,
          },
        },
        headers: versionHeaders(token, membership?.version ?? 0),
        body: {
          status,
          role: status === removed ? null : organizationAdmin,
        },
      } as const;
      const result =
        mode === "superadmin"
          ? await api.PUT(
              "/api/v1/superadmin/users/{userId}/organizations/{organizationId}",
              request,
            )
          : await api.PUT(
              "/api/v1/organizations/{organizationId}/administration/users/{userId}/membership",
              request,
            );
      if (!result.data)
        throw new Error(
          readProblemDetail(
            result.error,
            "Die Org-Berechtigung konnte nicht geändert werden.",
          ),
        );
      replaceMembership(user, result.data);
    });
  }

  async function changeCampAssignment(
    user: User,
    campId: string,
    role: CampRole | null,
  ) {
    if (!organizationId) return;
    const membership = membershipFor(user, organizationId);
    const assignment = membership?.camps.find((item) => item.campId === campId);
    if (!membership) return;
    await mutate(`${user.userId}:${campId}`, async (token) => {
      const result = await api.PUT(
        "/api/v1/organizations/{organizationId}/administration/users/{userId}/camps/{campId}",
        {
          params: { path: { organizationId, userId: user.userId, campId } },
          headers: versionHeaders(token, assignment?.version ?? 0),
          body: { role },
        },
      );
      if (role !== null && !result.data)
        throw new Error(
          readProblemDetail(
            result.error,
            "Die Freizeitrolle konnte nicht geändert werden.",
          ),
        );
      replaceMembership(user, {
        ...membership,
        camps:
          role === null
            ? membership.camps.filter((item) => item.campId !== campId)
            : [
                ...membership.camps.filter((item) => item.campId !== campId),
                result.data!,
              ],
      });
    });
  }

  async function createInvitation() {
    setBusy("invitation");
    setError(null);
    try {
      const token = await getAntiforgeryToken();
      const result = await api.POST("/api/v1/invitations/links", {
        headers: { "X-CSRF-TOKEN": token },
        body:
          mode === "superadmin"
            ? globalInvitationKind === "superadmin"
              ? {
                  isSuperAdmin: true,
                  organizationId: null,
                  organizationRole: null,
                  campId: null,
                  campRole: null,
                }
              : {
                  isSuperAdmin: false,
                  organizationId: selectedOrganizationId,
                  organizationRole: organizationAdmin,
                  campId: null,
                  campRole: null,
                }
            : {
                isSuperAdmin: false,
                organizationId,
                organizationRole:
                  invitationKind === "orgadmin" ? organizationAdmin : null,
                campId: invitationKind === "orgadmin" ? null : invitationCampId,
                campRole:
                  invitationKind === "campLead"
                    ? 0
                    : invitationKind === "member"
                      ? 1
                      : invitationKind === "viewer"
                        ? 2
                        : null,
              },
      });
      if (!result.data)
        throw new Error(
          readProblemDetail(
            result.error,
            "Der Einladungslink konnte nicht erstellt werden.",
          ),
        );
      const link = `${globalThis.location.origin}/einladung?token=${encodeURIComponent(result.data.token)}`;
      await globalThis.navigator.clipboard.writeText(link);
      setCopiedLink(link);
      setShowInvitation(false);
    } catch (caught) {
      setError(
        message(caught, "Der Einladungslink konnte nicht erstellt werden."),
      );
    } finally {
      setBusy(null);
    }
  }

  async function mutate(key: string, action: (token: string) => Promise<void>) {
    setBusy(key);
    setError(null);
    try {
      await action(await getAntiforgeryToken());
    } catch (caught) {
      setError(
        message(caught, "Die Änderung konnte nicht gespeichert werden."),
      );
    } finally {
      setBusy(null);
    }
  }

  function replaceUser(changed: User) {
    setUsers((current) =>
      current.map((item) => (item.userId === changed.userId ? changed : item)),
    );
  }

  function replaceMembership(user: User, changed: Membership) {
    replaceUser({
      ...user,
      organizations: [
        ...user.organizations.filter(
          (item) => item.organizationId !== changed.organizationId,
        ),
        changed,
      ],
    });
  }

  const title = mode === "superadmin" ? "Benutzer verwalten" : "Team verwalten";
  const invitationUnavailable =
    (mode === "organization" &&
      invitationKind !== "orgadmin" &&
      !invitationCampId) ||
    (mode === "superadmin" &&
      globalInvitationKind === "orgadmin" &&
      !selectedOrganizationId);

  return (
    <SettingsLayout
      backTo={mode === "superadmin" ? "/" : `/o/${organizationSlug}/camps`}
      organizationName={organizationName || undefined}
      organizationSlug={mode === "organization" ? organizationSlug : undefined}
      canManageOrganization={mode === "organization"}
      isSuperAdmin={mode === "superadmin"}
    >
      <p className="eyebrow">
        {mode === "superadmin"
          ? "Plattformverwaltung"
          : "Organisationsverwaltung"}
      </p>
      <h1>{title}</h1>
      {mode === "superadmin" ? (
        <PlatformAdministrationNavigation />
      ) : (
        <OrganizationAdministrationNavigation
          organizationSlug={organizationSlug}
        />
      )}
      <p>
        Suche, Rollen und Sperren werden serverseitig geprüft. Änderungen gelten
        sofort für neue und bestehende Sitzungen.
      </p>
      <div className="admin-toolbar">
        <form onSubmit={submitSearch} role="search">
          <label htmlFor="user-search">Name oder E-Mail</label>
          <div>
            <input
              id="user-search"
              onChange={(event) => setSearch(event.target.value)}
              type="search"
              value={search}
            />
            <button className="secondary-action" type="submit">
              Suchen
            </button>
          </div>
        </form>
        <button
          className="primary-action"
          disabled={
            busy !== null || (mode === "organization" && !organizationId)
          }
          onClick={() => setShowInvitation(true)}
          type="button"
        >
          Person einladen
        </button>
      </div>
      {showInvitation ? (
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
                    onChange={(event) =>
                      setInvitationCampId(event.target.value)
                    }
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
      ) : null}
      {copiedLink ? (
        <p className="success-message" role="status">
          Einladungslink wurde kopiert. Er wird aus Sicherheitsgründen nur jetzt
          angezeigt: <span className="copy-value">{copiedLink}</span>
        </p>
      ) : null}
      {loading ? <p role="status">Benutzer werden geladen …</p> : null}
      {error ? (
        <div className="error-message" role="alert">
          {error}
        </div>
      ) : null}
      {!loading && !error && users.length === 0 ? (
        <p className="empty-state">Keine passenden Benutzer gefunden.</p>
      ) : null}
      <ul className="admin-user-list">
        {users.map((user) => {
          const membership = organizationId
            ? membershipFor(user, organizationId)
            : null;
          const displayName =
            `${user.firstName} ${user.lastName}`.trim() || "Ohne Namen";
          const selected = selectedUserId === user.userId;
          return (
            <li key={user.userId} className={selected ? "selected" : undefined}>
              <button
                className="admin-user-heading admin-user-select"
                type="button"
                aria-expanded={selected}
                aria-label={`${displayName} auswählen`}
                onClick={() => setSelectedUserId(selected ? null : user.userId)}
              >
                <div>
                  <strong>{displayName}</strong>
                  <span>{user.email}</span>
                </div>
                <span
                  className={
                    user.accountStatus === active
                      ? "status-badge"
                      : "status-badge warning"
                  }
                >
                  {user.accountStatus === active ? "Aktiv" : "Global gesperrt"}
                </span>
              </button>
              {selected ? (
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
                        <h3 id={`platform-role-${user.userId}`}>
                          Plattformrolle
                        </h3>
                        <p>
                          {user.isSuperAdmin
                            ? "Superadmin"
                            : "Keine Plattformrolle"}
                        </p>
                        <button
                          className={
                            user.isSuperAdmin
                              ? "danger-action"
                              : "secondary-action"
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
                        <h3 id={`organizations-${user.userId}`}>
                          Organisationen
                        </h3>
                        <p>
                          Organisationsadmin-Zugang für die oben ausgewählte
                          Organisation vergeben.
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
                          disabled={
                            busy !== null || membership?.status !== active
                          }
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
              ) : null}
            </li>
          );
        })}
      </ul>
      {totalCount > 25 ? (
        <nav className="pagination" aria-label="Seitennavigation">
          <button
            className="secondary-action"
            disabled={page === 1 || loading}
            onClick={() => setPage((current) => Math.max(1, current - 1))}
            type="button"
          >
            Zurück
          </button>
          <span>Seite {page}</span>
          <button
            className="secondary-action"
            disabled={page * 25 >= totalCount || loading}
            onClick={() => setPage((current) => current + 1)}
            type="button"
          >
            Weiter
          </button>
        </nav>
      ) : null}
      {pendingAction ? (
        <ModalDialog
          labelledBy="confirm-admin-action"
          className="danger-dialog"
          onClose={() => setPendingAction(null)}
        >
          <h2 id="confirm-admin-action">{pendingAction.title}</h2>
          <p>{pendingAction.description}</p>
          <div className="dialog-actions">
            <button
              className="danger-action"
              disabled={busy !== null}
              onClick={() => {
                const action = pendingAction;
                setPendingAction(null);
                void action.run();
              }}
              type="button"
            >
              {pendingAction.confirmLabel}
            </button>
            <button
              autoFocus
              className="secondary-action"
              disabled={busy !== null}
              onClick={() => setPendingAction(null)}
              type="button"
            >
              Abbrechen
            </button>
          </div>
        </ModalDialog>
      ) : null}
    </SettingsLayout>
  );
}

function membershipFor(user: User, organizationId: string) {
  return user.organizations.find(
    (membership) => membership.organizationId === organizationId,
  );
}

function versionHeaders(token: string, version: number | string) {
  return {
    "X-CSRF-TOKEN": token,
    "If-Match": `"${version}"`,
  };
}

function message(caught: unknown, fallback: string) {
  return caught instanceof Error ? caught.message : fallback;
}
