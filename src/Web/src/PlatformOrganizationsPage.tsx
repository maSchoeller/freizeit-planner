import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import type { components } from "./api/schema";
import { api } from "./api/client";
import { getAntiforgeryToken, readProblemDetail } from "./api/security";
import { SettingsLayout } from "./OrganizationMembersPage";

type Organization = components["schemas"]["PlatformOrganizationView"];
type OrganizationStatus = components["schemas"]["OrganizationStatus"];

export function PlatformOrganizationsPage() {
  const [organizations, setOrganizations] = useState<Organization[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    const controller = new AbortController();
    void api
      .GET("/api/v1/platform/organizations", { signal: controller.signal })
      .then((result) => {
        if (result.response.status === 401) {
          void navigate("/anmelden", { replace: true });
          return;
        }
        if (!result.data)
          throw new Error(
            readProblemDetail(
              result.error,
              "Organizations konnten nicht geladen werden.",
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
            : "Organizations konnten nicht geladen werden.",
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
        "/api/v1/platform/organizations/{organizationId}/status",
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

  return (
    <SettingsLayout backTo="/konto">
      <p className="eyebrow">Plattformverwaltung</p>
      <h1>Organizations</h1>
      <p>
        Hier werden ausschließlich Plattform-Metadaten angezeigt. Fachliche
        Inhalte der Mandanten sind für Platform Admins nicht zugänglich.
      </p>
      {loading ? <p role="status">Organizations werden geladen …</p> : null}
      {error ? (
        <div className="error-message" role="alert">
          {error}
        </div>
      ) : null}
      {!loading && !error && organizations.length === 0 ? (
        <p className="empty-state">Noch keine Organization vorhanden.</p>
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
              onClick={() => void changeStatus(organization)}
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
    </SettingsLayout>
  );
}
