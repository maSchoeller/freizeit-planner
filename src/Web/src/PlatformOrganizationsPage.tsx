import { FormEvent, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import type { components } from "./api/schema";
import { api } from "./api/client";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";
import { SettingsLayout } from "./OrganizationMembersPage";
import { PlatformAdministrationNavigation } from "./AdministrationNavigation";

type Organization = components["schemas"]["SuperAdminOrganizationView"];
type OrganizationStatus = components["schemas"]["OrganizationStatus"];

export function PlatformOrganizationsPage() {
  const [organizations, setOrganizations] = useState<Organization[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [organizationName, setOrganizationName] = useState("");
  const [organizationSlug, setOrganizationSlug] = useState("");
  const [copiedLink, setCopiedLink] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<Organization | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    const controller = new AbortController();
    void api
      .GET("/api/v1/superadmin/organizations", { signal: controller.signal })
      .then((result) => {
        if (result.response.status === 401) {
          void navigate("/anmelden", { replace: true });
          return;
        }
        if (!result.data)
          throw new Error(
            readProblemDetail(
              result.error,
              "Organisationen konnten nicht geladen werden.",
            ),
          );
        setOrganizations(result.data);
      })
      .catch((caught: unknown) => {
        if (caught instanceof DOMException && caught.name === "AbortError")
          return;
        setError(
          caught instanceof Error
            ? caught.message
            : "Organisationen konnten nicht geladen werden.",
        );
      })
      .finally(() => setLoading(false));
    return () => controller.abort();
  }, [navigate]);

  async function changeStatus(organization: Organization) {
    setBusy(organization.organizationId);
    setError(null);
    const status: OrganizationStatus = organization.status === 0 ? 1 : 0;
    try {
      const token = await getAntiforgeryToken();
      const result = await api.PATCH(
        "/api/v1/superadmin/organizations/{organizationId}/status",
        {
          params: { path: { organizationId: organization.organizationId } },
          headers: {
            "X-CSRF-TOKEN": token,
            "If-Match": `"${organization.version}"`,
          },
          body: { status },
        },
      );
      if (!result.data)
        throw new Error(
          readProblemDetail(
            result.error,
            "Der Status konnte nicht geändert werden.",
          ),
        );
      const changed = result.data;
      setOrganizations((current) =>
        current.map((item) =>
          item.organizationId === organization.organizationId
            ? {
                ...item,
                status: changed.status,
                version: changed.version,
              }
            : item,
        ),
      );
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Der Status konnte nicht geändert werden.",
      );
    } finally {
      setBusy(null);
    }
  }

  async function createOrganizationInvitation(event: FormEvent) {
    event.preventDefault();
    setBusy("new-organization");
    setError(null);
    try {
      const token = await getAntiforgeryToken();
      const result = await api.POST("/api/v1/invitations/links", {
        headers: { "X-CSRF-TOKEN": token },
        body: {
          isSuperAdmin: false,
          organizationId: null,
          organizationRole: 0,
          campId: null,
          campRole: null,
          newOrganization: {
            name: organizationName.trim(),
            slug: organizationSlug.trim(),
          },
        },
      });
      if (!result.data)
        throw new Error(
          readProblemDetail(
            result.error,
            "Der Einrichtungslink konnte nicht erstellt werden.",
          ),
        );
      const link = `${globalThis.location.origin}/einladung?token=${encodeURIComponent(result.data.token)}`;
      await globalThis.navigator.clipboard.writeText(link);
      setCopiedLink(link);
      setOrganizationName("");
      setOrganizationSlug("");
      setShowCreate(false);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Der Einrichtungslink konnte nicht erstellt werden.",
      );
    } finally {
      setBusy(null);
    }
  }

  return (
    <SettingsLayout backTo="/" isSuperAdmin>
      <p className="eyebrow">Plattformverwaltung</p>
      <div className="page-heading compact-heading">
        <div>
          <h1>Organisationen verwalten</h1>
          <p>Plattformzugänge und Organisationsstatus zentral verwalten.</p>
        </div>
        <button
          className="primary-action"
          type="button"
          onClick={() => setShowCreate(true)}
        >
          Organisation einrichten
        </button>
      </div>
      <PlatformAdministrationNavigation />
      <p>
        Hier werden ausschließlich Plattform-Metadaten angezeigt. Fachliche
        Inhalte der Organisationen sind für Superadmins ohne zusätzliche
        Organisationsadmin-Zuweisung nicht zugänglich.
      </p>
      {showCreate ? (
        <dialog
          open
          aria-labelledby="create-organization-heading"
          className="app-dialog"
        >
          <form
            className="organization-invitation-form"
            onSubmit={(event) => void createOrganizationInvitation(event)}
          >
            <h2 id="create-organization-heading">Organisation einrichten</h2>
            <p>Der Link ist einmalig sichtbar und wird direkt kopiert.</p>
            <label>
              Name
              <input
                onChange={(event) => setOrganizationName(event.target.value)}
                required
                value={organizationName}
              />
            </label>
            <label>
              Kurzname für die URL
              <input
                onChange={(event) => setOrganizationSlug(event.target.value)}
                pattern="[a-z0-9-]+"
                required
                value={organizationSlug}
              />
            </label>
            <div className="dialog-actions">
              <button
                className="primary-action"
                disabled={busy !== null}
                type="submit"
              >
                {busy === "new-organization"
                  ? "Link wird erstellt …"
                  : "Einrichtungslink erstellen & kopieren"}
              </button>
              <button
                className="secondary-action"
                disabled={busy !== null}
                type="button"
                onClick={() => setShowCreate(false)}
              >
                Abbrechen
              </button>
            </div>
          </form>
        </dialog>
      ) : null}
      {copiedLink ? (
        <p className="success-message" role="status">
          Einrichtungslink wurde kopiert:{" "}
          <span className="copy-value">{copiedLink}</span>
        </p>
      ) : null}
      {loading ? <p role="status">Organisationen werden geladen …</p> : null}
      {error ? (
        <div className="error-message" role="alert">
          {error}
        </div>
      ) : null}
      {!loading && !error && organizations.length === 0 ? (
        <p className="empty-state">Noch keine Organisation vorhanden.</p>
      ) : null}
      <ul className="management-list">
        {organizations.map((organization) => (
          <li key={organization.organizationId}>
            <div>
              <strong>{organization.name}</strong>
              <span>
                /{organization.slug} ·{" "}
                {organization.status === 0 ? "Aktiv" : "Gesperrt"}
              </span>
            </div>
            <button
              className={
                organization.status === 0 ? "danger-action" : "secondary-action"
              }
              disabled={busy !== null}
              onClick={() =>
                organization.status === 0
                  ? setPendingStatus(organization)
                  : void changeStatus(organization)
              }
              type="button"
            >
              {busy === organization.organizationId
                ? "Wird gespeichert …"
                : organization.status === 0
                  ? "Sperren"
                  : "Entsperren"}
            </button>
          </li>
        ))}
      </ul>
      {pendingStatus ? (
        <dialog
          open
          aria-labelledby="confirm-organization-status"
          className="app-dialog danger-dialog"
        >
          <h2 id="confirm-organization-status">Organisation sperren?</h2>
          <p>
            „{pendingStatus.name}“ kann danach nicht mehr fachlich arbeiten. Die
            Sperre lässt sich wieder aufheben.
          </p>
          <div className="dialog-actions">
            <button
              className="danger-action"
              type="button"
              onClick={() => {
                const target = pendingStatus;
                setPendingStatus(null);
                void changeStatus(target);
              }}
            >
              Organisation sperren
            </button>
            <button
              className="secondary-action"
              type="button"
              onClick={() => setPendingStatus(null)}
            >
              Abbrechen
            </button>
          </div>
        </dialog>
      ) : null}
    </SettingsLayout>
  );
}
