import { FormEvent, useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { api } from "./api/client";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";
import { SettingsLayout } from "./OrganizationMembersPage";
import {
  OrganizationAdministrationNavigation,
  PlatformAdministrationNavigation,
} from "./AdministrationNavigation";
import { ConfirmActionDialog } from "./administration/ConfirmActionDialog";
import { InvitationDialog } from "./administration/InvitationDialog";
import { UserDetails } from "./administration/UserDetails";
import type {
  Camp,
  CampRole,
  Membership,
  MembershipStatus,
  Organization,
  Page,
  PendingAction,
  User,
} from "./administration/support";
import {
  active,
  membershipFor,
  message,
  organizationAdmin,
  removed,
  suspended,
  versionHeaders,
} from "./administration/support";

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
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(
    null,
  );
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
        <InvitationDialog
          mode={mode}
          camps={camps}
          organizations={organizations}
          invitationKind={invitationKind}
          setInvitationKind={setInvitationKind}
          invitationCampId={invitationCampId}
          setInvitationCampId={setInvitationCampId}
          globalInvitationKind={globalInvitationKind}
          setGlobalInvitationKind={setGlobalInvitationKind}
          selectedOrganizationId={selectedOrganizationId}
          setSelectedOrganizationId={setSelectedOrganizationId}
          busy={busy}
          invitationUnavailable={invitationUnavailable}
          createInvitation={createInvitation}
          setShowInvitation={setShowInvitation}
        />
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
                <UserDetails
                  user={user}
                  displayName={displayName}
                  mode={mode}
                  membership={membership}
                  camps={camps}
                  selectedOrganizationId={selectedOrganizationId}
                  busy={busy}
                  setPendingAction={setPendingAction}
                  changeGlobalStatus={changeGlobalStatus}
                  clearLoginLockout={clearLoginLockout}
                  changeSuperAdmin={changeSuperAdmin}
                  changeMembership={changeMembership}
                  changeCampAssignment={changeCampAssignment}
                />
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
        <ConfirmActionDialog
          pendingAction={pendingAction}
          busy={busy}
          setPendingAction={setPendingAction}
        />
      ) : null}
    </SettingsLayout>
  );
}
