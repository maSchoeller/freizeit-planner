import { FormEvent, useEffect, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "react-router-dom";
import type { components } from "./api/schema";
import { SettingsLayout } from "./OrganizationMembersPage";

type Membership = components["schemas"]["AccountMembershipView"];
type CampSummary = components["schemas"]["CampSummary"];
type CampView = components["schemas"]["CampView"];

const periodLabels = ["Zukünftig", "Laufend", "Vergangen"] as const;

async function readProblem(response: Response, fallback: string) {
  const body = (await response.json().catch(() => null)) as {
    detail?: string;
  } | null;
  return body?.detail ?? fallback;
}

async function getAntiforgeryToken() {
  const response = await fetch("/api/v1/auth/antiforgery", {
    credentials: "same-origin",
  });
  if (!response.ok)
    throw new Error("Sicherheitsprüfung konnte nicht geladen werden.");
  const body = (await response.json()) as { token: string };
  return body.token;
}

async function loadMembership(organizationSlug: string, signal?: AbortSignal) {
  const response = await fetch("/api/v1/account/memberships", {
    credentials: "same-origin",
    signal,
  });
  if (!response.ok)
    throw new Error("Deine Organisationen konnten nicht geladen werden.");
  const memberships = (await response.json()) as Membership[];
  const membership = memberships.find(
    (item) => item.organizationSlug === organizationSlug,
  );
  if (!membership)
    throw new Error("Du hast keinen Zugriff auf diese Organisation.");
  return membership;
}

export function CampsPage() {
  const { organizationSlug = "" } = useParams();
  const [membership, setMembership] = useState<Membership | null>(null);
  const [camps, setCamps] = useState<CampSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    void (async () => {
      try {
        const currentMembership = await loadMembership(
          organizationSlug,
          controller.signal,
        );
        const response = await fetch(
          `/api/v1/organizations/${currentMembership.organizationId}/camps`,
          { credentials: "same-origin", signal: controller.signal },
        );
        if (!response.ok)
          throw new Error("Die Camps konnten nicht geladen werden.");
        setMembership(currentMembership);
        setCamps((await response.json()) as CampSummary[]);
      } catch (reason) {
        if (!controller.signal.aborted)
          setError(
            reason instanceof Error
              ? reason.message
              : "Die Camps konnten nicht geladen werden.",
          );
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    })();
    return () => controller.abort();
  }, [organizationSlug]);

  async function createCamp(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!membership) return;
    setBusy(true);
    setError("");
    setNotice("");
    const form = new FormData(event.currentTarget);
    try {
      const token = await getAntiforgeryToken();
      const response = await fetch(
        `/api/v1/organizations/${membership.organizationId}/camps`,
        {
          method: "POST",
          credentials: "same-origin",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token,
          },
          body: JSON.stringify({
            name: form.get("name"),
            slug: form.get("slug"),
            description: form.get("description") || null,
            startsOn: form.get("startsOn"),
            endsOn: form.get("endsOn"),
            timeZoneId: form.get("timeZoneId"),
            defaultPortions: Number(form.get("defaultPortions")),
          }),
        },
      );
      if (!response.ok)
        throw new Error(
          await readProblem(response, "Das Camp konnte nicht angelegt werden."),
        );
      const created = (await response.json()) as CampView;
      setCamps((current) => [...current, created]);
      setShowCreate(false);
      setNotice(`„${created.name}“ wurde angelegt.`);
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : "Das Camp konnte nicht angelegt werden.",
      );
    } finally {
      setBusy(false);
    }
  }

  const canCreate = membership?.role === 0 || membership?.role === 1;

  return (
    <SettingsLayout backTo="/konto">
      <p className="eyebrow">
        {membership?.organizationName ?? "Organisation"}
      </p>
      <h1>Camps</h1>
      <p>
        Plane laufende und kommende Freizeiten oder öffne vergangene Camps zum
        Lesen und Exportieren.
      </p>
      {loading ? <p role="status">Camps werden geladen …</p> : null}
      {error ? (
        <p role="alert" className="error-message">
          {error}
        </p>
      ) : null}
      <p role="status" aria-live="polite">
        {notice}
      </p>

      {canCreate ? (
        <div className="toolbar">
          <button
            className="primary-action"
            type="button"
            onClick={() => setShowCreate((current) => !current)}
          >
            Camp anlegen
          </button>
        </div>
      ) : null}

      {showCreate ? (
        <section
          className="settings-section"
          aria-labelledby="create-camp-heading"
        >
          <h2 id="create-camp-heading">Neues Camp</h2>
          <form onSubmit={(event) => void createCamp(event)}>
            <CampFields />
            <div className="toolbar">
              <button className="primary-action" disabled={busy} type="submit">
                Camp speichern
              </button>
              <button
                className="secondary-action"
                disabled={busy}
                type="button"
                onClick={() => setShowCreate(false)}
              >
                Abbrechen
              </button>
            </div>
          </form>
        </section>
      ) : null}

      {!loading && !error ? (
        camps.length === 0 ? (
          <p className="empty-state">Noch kein Camp vorhanden.</p>
        ) : (
          <div className="camp-periods">
            {periodLabels.map((label, period) => {
              const items = camps.filter((camp) => camp.period === period);
              if (items.length === 0) return null;
              return (
                <section key={label} aria-labelledby={`camp-period-${period}`}>
                  <h2 id={`camp-period-${period}`}>{label}</h2>
                  <ul className="camp-list">
                    {items.map((camp) => (
                      <li key={camp.id} className="camp-card">
                        <div>
                          <Link
                            to={`/o/${organizationSlug}/camps/${camp.slug}`}
                          >
                            <strong>{camp.name}</strong>
                          </Link>
                          <span>
                            {formatDate(camp.startsOn)}–
                            {formatDate(camp.endsOn)} · {camp.timeZoneId}
                          </span>
                        </div>
                        <span
                          className={
                            camp.status === 1 ? "status info" : "status"
                          }
                        >
                          {camp.status === 1 ? "Archiviert" : "Aktiv"}
                        </span>
                        <Link
                          className="secondary-action"
                          to={`/o/${organizationSlug}/camps/${camp.slug}/einstellungen`}
                        >
                          Einstellungen
                        </Link>
                      </li>
                    ))}
                  </ul>
                </section>
              );
            })}
          </div>
        )
      ) : null}
    </SettingsLayout>
  );
}

export function CampSettingsPage() {
  const { organizationSlug = "", campSlug = "" } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [membership, setMembership] = useState<Membership | null>(null);
  const [camp, setCamp] = useState<CampView | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    void (async () => {
      try {
        const currentMembership = await loadMembership(
          organizationSlug,
          controller.signal,
        );
        const response = await fetch(
          `/api/v1/organizations/${currentMembership.organizationId}/camps/by-slug/${encodeURIComponent(campSlug)}`,
          { credentials: "same-origin", signal: controller.signal },
        );
        if (!response.ok)
          throw new Error(
            "Die Camp-Einstellungen konnten nicht geladen werden.",
          );
        setMembership(currentMembership);
        setCamp((await response.json()) as CampView);
      } catch (reason) {
        if (!controller.signal.aborted)
          setError(
            reason instanceof Error
              ? reason.message
              : "Die Camp-Einstellungen konnten nicht geladen werden.",
          );
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    })();
    return () => controller.abort();
  }, [campSlug, organizationSlug]);

  async function updateCamp(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!membership || !camp) return;
    const form = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const token = await getAntiforgeryToken();
      const response = await fetch(
        `/api/v1/organizations/${membership.organizationId}/camps/${camp.id}`,
        {
          method: "PUT",
          credentials: "same-origin",
          headers: versionedHeaders(token, camp.version),
          body: JSON.stringify({
            name: form.get("name"),
            slug: form.get("slug"),
            description: form.get("description") || null,
            startsOn: form.get("startsOn"),
            endsOn: form.get("endsOn"),
            timeZoneId: form.get("timeZoneId"),
            defaultPortions: Number(form.get("defaultPortions")),
          }),
        },
      );
      if (!response.ok)
        throw new Error(
          await readProblem(
            response,
            "Das Camp konnte nicht gespeichert werden.",
          ),
        );
      const updated = (await response.json()) as CampView;
      setCamp(updated);
      await queryClient.invalidateQueries({
        queryKey: ["camp-workspace", organizationSlug],
      });
      setNotice("Camp-Einstellungen wurden gespeichert.");
      if (updated.slug !== campSlug)
        void navigate(
          `/o/${organizationSlug}/camps/${updated.slug}/einstellungen`,
          { replace: true },
        );
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : "Das Camp konnte nicht gespeichert werden.",
      );
    } finally {
      setBusy(false);
    }
  }

  async function changeStatus(status: number) {
    if (!membership || !camp) return;
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const token = await getAntiforgeryToken();
      const response = await fetch(
        `/api/v1/organizations/${membership.organizationId}/camps/${camp.id}/status`,
        {
          method: "PATCH",
          credentials: "same-origin",
          headers: versionedHeaders(token, camp.version),
          body: JSON.stringify({ status }),
        },
      );
      if (!response.ok)
        throw new Error(
          await readProblem(
            response,
            "Der Camp-Status konnte nicht geändert werden.",
          ),
        );
      const updated = (await response.json()) as CampView;
      setCamp(updated);
      await queryClient.invalidateQueries({
        queryKey: ["camp-workspace", organizationSlug],
      });
      setNotice(
        status === 1
          ? "Das Camp wurde archiviert."
          : "Das Camp wurde reaktiviert.",
      );
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : "Der Camp-Status konnte nicht geändert werden.",
      );
    } finally {
      setBusy(false);
    }
  }

  return (
    <SettingsLayout backTo={`/o/${organizationSlug}/camps/${campSlug}`}>
      <p className="eyebrow">Camp verwalten</p>
      <h1>Camp-Einstellungen</h1>
      {loading ? (
        <p role="status">Camp-Einstellungen werden geladen …</p>
      ) : null}
      {error ? (
        <p role="alert" className="error-message">
          {error}
        </p>
      ) : null}
      <p role="status" aria-live="polite">
        {notice}
      </p>

      {camp ? (
        <>
          {camp.status === 1 ? (
            <p className="notice">
              Dieses archivierte Camp ist schreibgeschützt. Lesen und
              Exportieren bleiben möglich; reaktiviere es für Änderungen.
            </p>
          ) : null}
          <section
            className="settings-section"
            aria-labelledby="camp-details-heading"
          >
            <h2 id="camp-details-heading">Stammdaten</h2>
            <form onSubmit={(event) => void updateCamp(event)}>
              <CampFields camp={camp} disabled={camp.status === 1 || busy} />
              <button
                className="primary-action"
                disabled={camp.status === 1 || busy}
                type="submit"
              >
                Änderungen speichern
              </button>
            </form>
          </section>
          <section
            className="settings-section"
            aria-labelledby="camp-status-heading"
          >
            <h2 id="camp-status-heading">Archiv</h2>
            <p>
              Archivierte Camps bleiben vollständig lesbar und exportierbar,
              sind aber gegen Änderungen geschützt.
            </p>
            <button
              className={camp.status === 1 ? "primary-action" : "danger-action"}
              disabled={busy}
              type="button"
              onClick={() => void changeStatus(camp.status === 1 ? 0 : 1)}
            >
              {camp.status === 1 ? "Camp reaktivieren" : "Camp archivieren"}
            </button>
          </section>
          <Link to={`/o/${organizationSlug}/camps`}>Alle Camps anzeigen</Link>
        </>
      ) : null}
    </SettingsLayout>
  );
}

function CampFields({
  camp,
  disabled = false,
}: {
  camp?: CampView;
  disabled?: boolean;
}) {
  return (
    <div className="camp-form-grid">
      <label>
        Name
        <input
          name="name"
          required
          defaultValue={camp?.name}
          disabled={disabled}
        />
      </label>
      <label>
        Slug
        <input
          name="slug"
          required
          pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
          defaultValue={camp?.slug}
          disabled={disabled}
        />
      </label>
      <label className="full-width">
        Beschreibung
        <textarea
          name="description"
          defaultValue={camp?.description ?? ""}
          disabled={disabled}
        />
      </label>
      <label>
        Startdatum
        <input
          name="startsOn"
          type="date"
          required
          defaultValue={camp?.startsOn ?? "2026-08-10"}
          disabled={disabled}
        />
      </label>
      <label>
        Enddatum
        <input
          name="endsOn"
          type="date"
          required
          defaultValue={camp?.endsOn ?? "2026-08-17"}
          disabled={disabled}
        />
      </label>
      <label>
        Zeitzone
        <input
          name="timeZoneId"
          required
          defaultValue={camp?.timeZoneId ?? "Europe/Berlin"}
          disabled={disabled}
        />
      </label>
      <label>
        Standardportionen
        <input
          name="defaultPortions"
          type="number"
          min="1"
          step="1"
          required
          defaultValue={camp?.defaultPortions ?? 30}
          disabled={disabled}
        />
      </label>
    </div>
  );
}

function versionedHeaders(token: string, version: number | string) {
  return {
    "Content-Type": "application/json",
    "X-CSRF-TOKEN": token,
    "If-Match": `"${version}"`,
  };
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("de-DE", { dateStyle: "medium" }).format(
    new Date(`${value}T12:00:00`),
  );
}
