import { FormEvent, useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import type { components } from "./api/schema";
import { api } from "./api/client";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";
import { SettingsLayout } from "./OrganizationMembersPage";

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
          throw new Error("Die Organization wurde nicht gefunden.");
        setOrganizationId(organization.organizationId);
      })
      .catch((caught: unknown) => {
        if (caught instanceof DOMException && caught.name === "AbortError")
          return;
        setError(
          message(caught, "Die Organization konnte nicht geladen werden."),
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
            "Die Camp-Rolle konnte nicht geändert werden.",
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
  const invitationLabel =
    mode === "superadmin"
      ? globalInvitationKind === "superadmin"
        ? "Superadmin-Link kopieren"
        : "Orgadmin-Link kopieren"
      : invitationKind === "orgadmin"
        ? "Orgadmin-Link kopieren"
        : "Camp-Link kopieren";

  return (
    <SettingsLayout
      backTo={mode === "superadmin" ? "/konto" : `/o/${organizationSlug}/camps`}
    >
      <p className="eyebrow">
        {mode === "superadmin"
          ? "Superadmin-Verwaltung"
          : "Organization-Verwaltung"}
      </p>
      <h1>{title}</h1>
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
          onClick={() => void createInvitation()}
          type="button"
        >
          {busy === "invitation" ? "Link wird erstellt …" : invitationLabel}
        </button>
      </div>
      {mode === "organization" ? (
        <div className="invitation-options">
          <label>
            Rolle des nächsten Einladungslinks
            <select
              onChange={(event) =>
                setInvitationKind(
                  event.target.value as
                    "orgadmin" | "campLead" | "member" | "viewer",
                )
              }
              value={invitationKind}
            >
              <option value="orgadmin">Orgadmin</option>
              <option value="campLead">Camp-Leitung</option>
              <option value="member">Campmitarbeit</option>
              <option value="viewer">Lesender Campzugriff</option>
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
      ) : null}
      {mode === "superadmin" && organizations.length > 0 ? (
        <div className="invitation-options">
          <label>
            Rolle des nächsten Einladungslinks
            <select
              onChange={(event) =>
                setGlobalInvitationKind(
                  event.target.value as "superadmin" | "orgadmin",
                )
              }
              value={globalInvitationKind}
            >
              <option value="superadmin">Superadmin</option>
              <option value="orgadmin">Orgadmin</option>
            </select>
          </label>
          <label className="admin-organization-choice">
            Organization für Orgadmin-Rechte
            <select
              disabled={globalInvitationKind !== "orgadmin"}
              onChange={(event) =>
                setSelectedOrganizationId(event.target.value)
              }
              value={selectedOrganizationId}
            >
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
          <Link to="/superadmin/organisationen">Organizations verwalten</Link>
        </div>
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
          return (
            <li key={user.userId}>
              <div className="admin-user-heading">
                <div>
                  <strong>
                    {`${user.firstName} ${user.lastName}`.trim() ||
                      "Ohne Namen"}
                  </strong>
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
              </div>
              {mode === "superadmin" ? (
                <div className="admin-actions">
                  <button
                    className="secondary-action"
                    disabled={busy !== null}
                    onClick={() => void changeSuperAdmin(user)}
                    type="button"
                  >
                    {user.isSuperAdmin
                      ? "Superadmin entziehen"
                      : "Zum Superadmin machen"}
                  </button>
                  <button
                    className={
                      user.accountStatus === active
                        ? "danger-action"
                        : "secondary-action"
                    }
                    disabled={busy !== null}
                    onClick={() => void changeGlobalStatus(user)}
                    type="button"
                  >
                    {user.accountStatus === active
                      ? "Global sperren"
                      : "Entsperren"}
                  </button>
                  {selectedOrganizationId ? (
                    <button
                      className="secondary-action"
                      disabled={busy !== null}
                      onClick={() => void changeMembership(user, active)}
                      type="button"
                    >
                      Als Orgadmin zuweisen
                    </button>
                  ) : null}
                </div>
              ) : (
                <div className="admin-actions">
                  <span>
                    {membership?.status === active
                      ? membership.role === organizationAdmin
                        ? "Orgadmin · aktiv"
                        : "Mitglied · aktiv"
                      : membership?.status === suspended
                        ? "In dieser Organization gesperrt"
                        : "Entfernt"}
                  </span>
                  <button
                    className="secondary-action"
                    disabled={busy !== null}
                    onClick={() => void changeMembership(user, active)}
                    type="button"
                  >
                    Als Orgadmin aktivieren
                  </button>
                  <button
                    className="danger-action"
                    disabled={busy !== null || membership?.status !== active}
                    onClick={() => void changeMembership(user, suspended)}
                    type="button"
                  >
                    In Organization sperren
                  </button>
                </div>
              )}
              {mode === "organization" && membership?.status === active ? (
                <div className="camp-role-grid">
                  {camps.map((camp) => {
                    const assignment = membership.camps.find(
                      (item) => item.campId === camp.id,
                    );
                    return (
                      <label key={camp.id}>
                        {camp.name}
                        <select
                          aria-label={`Camp-Rolle für ${user.firstName} ${user.lastName} in ${camp.name}`}
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
                          <option value="0">Camp-Leitung</option>
                          <option value="1">Mitarbeit</option>
                          <option value="2">Lesender Zugriff</option>
                        </select>
                      </label>
                    );
                  })}
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
      {mode === "superadmin" ? (
        <p>
          <Link to="/superadmin/organisationen">Organizations verwalten</Link>
        </p>
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
