import { useEffect, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import type { components } from "./api/schema";
import { api } from "./api/client";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";
import { AppHeader } from "./AppShell";

type Member = components["schemas"]["OrganizationMemberView"];
type TenantRole = components["schemas"]["TenantRole"];

const roles: ReadonlyArray<{ value: TenantRole; label: string }> = [
  { value: 1, label: "Organisationsadmin" },
  { value: 2, label: "Freizeit-Leitung" },
  { value: 3, label: "Mitglied" },
  { value: 4, label: "Lesender Zugriff" },
];

export function OrganizationMembersPage() {
  const { organizationSlug = "" } = useParams();
  const [searchParams] = useSearchParams();
  const organizationId = searchParams.get("organizationId") ?? "";
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    if (!organizationId) {
      setError("Die Organisations-ID fehlt im Link.");
      setLoading(false);
      return;
    }
    const controller = new AbortController();
    void api
      .GET("/api/v1/organizations/{organizationId}/members", {
        params: { path: { organizationId } },
        signal: controller.signal,
      })
      .then((result) => {
        if (result.response.status === 401) {
          void navigate("/anmelden", { replace: true });
          return;
        }
        if (!result.data)
          throw new Error(
            readProblemDetail(
              result.error,
              "Mitglieder konnten nicht geladen werden.",
            ),
          );
        setMembers(result.data);
      })
      .catch((caught: unknown) => {
        if (caught instanceof DOMException && caught.name === "AbortError")
          return;
        setError(
          caught instanceof Error
            ? caught.message
            : "Mitglieder konnten nicht geladen werden.",
        );
      })
      .finally(() => setLoading(false));
    return () => controller.abort();
  }, [navigate, organizationId]);

  async function changeRole(member: Member, role: TenantRole) {
    await mutate(member.userId, async (token) => {
      const result = await api.PATCH(
        "/api/v1/organizations/{organizationId}/members/{userId}/role",
        {
          params: { path: { organizationId, userId: member.userId } },
          headers: {
            "X-CSRF-TOKEN": token,
            "If-Match": `"${member.version}"`,
          },
          body: { role },
        },
      );
      if (!result.data)
        throw new Error(
          readProblemDetail(
            result.error,
            "Die Rolle konnte nicht geändert werden.",
          ),
        );
      replaceMember(result.data);
    });
  }

  async function removeMember(member: Member) {
    await mutate(member.userId, async (token) => {
      const result = await api.DELETE(
        "/api/v1/organizations/{organizationId}/members/{userId}",
        {
          params: { path: { organizationId, userId: member.userId } },
          headers: {
            "X-CSRF-TOKEN": token,
            "If-Match": `"${member.version}"`,
          },
        },
      );
      if (!result.response.ok)
        throw new Error(
          readProblemDetail(
            result.error,
            "Das Mitglied konnte nicht entfernt werden.",
          ),
        );
      setMembers((current) =>
        current.filter((item) => item.userId !== member.userId),
      );
    });
  }

  function replaceMember(member: Member) {
    setMembers((current) =>
      current.map((item) => (item.userId === member.userId ? member : item)),
    );
  }

  async function mutate(key: string, action: (token: string) => Promise<void>) {
    setBusy(key);
    setError(null);
    try {
      await action(await getAntiforgeryToken());
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Die Änderung konnte nicht gespeichert werden.",
      );
    } finally {
      setBusy(null);
    }
  }

  return (
    <SettingsLayout backTo="/konto">
      <p className="eyebrow">Organisationseinstellungen</p>
      <h1>Mitglieder verwalten</h1>
      <p>
        Rollen gelten serverseitig. Eine Organisation darf bewusst ohne
        Organisationsadmin bestehen.
      </p>
      {loading ? <p role="status">Mitglieder werden geladen …</p> : null}
      {error ? (
        <div className="error-message" role="alert">
          {error}
        </div>
      ) : null}
      {!loading && !error && members.length === 0 ? (
        <p className="empty-state">Keine Mitglieder gefunden.</p>
      ) : null}
      <ul className="management-list">
        {members.map((member) => (
          <li key={member.userId}>
            <div>
              <strong>{member.displayName ?? "Ohne Anzeigenamen"}</strong>
              <span>{member.email ?? "E-Mail nicht verfügbar"}</span>
            </div>
            <label>
              <span className="visually-hidden">
                Rolle für {member.displayName}
              </span>
              <select
                aria-label={`Rolle für ${member.displayName ?? member.email ?? "Mitglied"}`}
                disabled={busy !== null}
                value={member.role}
                onChange={(event) =>
                  void changeRole(member, Number(event.target.value))
                }
              >
                {roles.map((role) => (
                  <option key={role.value} value={role.value}>
                    {role.label}
                  </option>
                ))}
              </select>
            </label>
            <button
              className="danger-action"
              disabled={busy !== null}
              onClick={() => void removeMember(member)}
              type="button"
            >
              {busy === member.userId ? "Wird gespeichert …" : "Entfernen"}
            </button>
          </li>
        ))}
      </ul>
      <p className="muted">Organisation: {organizationSlug}</p>
    </SettingsLayout>
  );
}

export function SettingsLayout({
  backTo,
  children,
  organizationName,
  organizationSlug,
  canManageOrganization = false,
  isSuperAdmin = false,
}: {
  backTo: string;
  children: React.ReactNode;
  organizationName?: string;
  organizationSlug?: string;
  canManageOrganization?: boolean;
  isSuperAdmin?: boolean;
}) {
  return (
    <div className="account-layout">
      <a className="skip-link" href="#main">
        Zum Inhalt springen
      </a>
      <AppHeader
        homeTo={backTo}
        organizationName={organizationName}
        organizationSlug={organizationSlug}
        canManageOrganization={canManageOrganization}
        isSuperAdmin={isSuperAdmin}
      />
      <main id="main" className="account-page">
        {children}
      </main>
    </div>
  );
}
