import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import type {
  BibleSnapshotRefreshResult,
  BibleTranslationView,
  CampMemberSummary,
  Devotion,
  DevotionDetail,
  ScheduleEntry,
} from "./types";
import { getJson, mutateCateringJson } from "./api";
import { useCampQuery, useCampRuntime } from "./runtime";
import { nextLocalDate } from "./schedule";
import { PageHeading, QueryState, ResponsibilityFields } from "./ui";
import { OwnerAttachmentsPanel } from "./AttachmentsPanel";

export function DevotionsPage({ offline }: { offline: boolean }) {
  const { organizationId, campId, camp } = useCampRuntime();
  const queryClient = useQueryClient();
  const path = `/api/v1/organizations/${organizationId}/camps/${campId}/devotions`;
  const query = useCampQuery<Devotion[]>("devotions", path);
  const translations = useCampQuery<BibleTranslationView[]>(
    "devotion-translations",
    `${path}/translations`,
  );
  const scheduleEntries = useQuery({
    queryKey: [organizationId, campId, "devotion-schedule-candidates"],
    queryFn: () =>
      getJson<ScheduleEntry[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/schedule?fromDate=${camp.startsOn}&toDateExclusive=${nextLocalDate(camp.endsOn)}`,
      ),
    retry: false,
  });
  const members = useCampQuery<CampMemberSummary[]>(
    "responsibility-candidates",
    `/api/v1/organizations/${organizationId}/camps/${campId}/responsibility-candidates`,
  );
  const [selectedDevotionId, setSelectedDevotionId] = useState<string | null>(
    null,
  );
  const [creating, setCreating] = useState(false);
  const [topic, setTopic] = useState("");
  const [bibleReference, setBibleReference] = useState("");
  const [translation, setTranslation] = useState("0");
  const [coreMessage, setCoreMessage] = useState("");
  const [markdownContent, setMarkdownContent] = useState("");
  const [materialNotes, setMaterialNotes] = useState("");
  const [responsibleUserIds, setResponsibleUserIds] = useState<string[]>([]);
  const [scheduleEntryId, setScheduleEntryId] = useState("");
  const [manualSnapshotOpen, setManualSnapshotOpen] = useState(false);
  const [manualText, setManualText] = useState("");
  const [editing, setEditing] = useState(false);
  const [editTopic, setEditTopic] = useState("");
  const [editBibleReference, setEditBibleReference] = useState("");
  const [editTranslation, setEditTranslation] = useState("0");
  const [editCoreMessage, setEditCoreMessage] = useState("");
  const [editMarkdownContent, setEditMarkdownContent] = useState("");
  const [editMaterialNotes, setEditMaterialNotes] = useState("");
  const [editResponsibleUserIds, setEditResponsibleUserIds] = useState<
    string[]
  >([]);
  const [editScheduleEntryId, setEditScheduleEntryId] = useState("");
  const [confirmTrash, setConfirmTrash] = useState(false);
  const [trashConfirmed, setTrashConfirmed] = useState(false);
  const [notice, setNotice] = useState("");
  const detail = useQuery({
    queryKey: [organizationId, campId, "devotion", selectedDevotionId],
    queryFn: () => getJson<DevotionDetail>(`${path}/${selectedDevotionId}`),
    enabled: selectedDevotionId !== null,
    retry: false,
    staleTime: Number.POSITIVE_INFINITY,
  });
  const createDevotion = useMutation({
    mutationFn: () =>
      mutateCateringJson<DevotionDetail>(path, "POST", {
        topic,
        bibleReference,
        translation: Number(translation),
        coreMessage,
        markdownContent,
        responsibleUserIds,
        materialNotes,
        scheduleEntryId: scheduleEntryId || null,
      }),
    onSuccess: (created) => {
      queryClient.setQueryData<Devotion[]>(
        [organizationId, campId, "devotions"],
        (current) => [
          ...(current ?? []),
          {
            id: created.id,
            organizationId: created.organizationId,
            campId: created.campId,
            topic: created.topic,
            bibleReference: created.bibleReference,
            translation: created.translation,
            responsibleUserIds: created.responsibleUserIds,
            scheduleEntryId: created.scheduleEntryId,
            hasBibleSnapshot: created.bibleSnapshot !== null,
            version: created.version,
          },
        ],
      );
      queryClient.setQueryData(
        [organizationId, campId, "devotion", created.id],
        created,
      );
      setSelectedDevotionId(created.id);
      setCreating(false);
      setNotice(`${created.topic} wurde angelegt.`);
    },
  });
  const saveManualSnapshot = useMutation({
    mutationFn: () => {
      const current = detail.data;
      if (!current) throw new Error("Öffne zuerst eine Andacht.");
      return mutateCateringJson<DevotionDetail>(
        `${path}/${current.id}/bible/manual`,
        "PUT",
        {
          reference: current.bibleReference,
          translation: current.translation,
          textExcerpt: manualText,
        },
        current.version,
        "Die Andacht wurde zwischenzeitlich geändert. Öffne den aktuellen Stand erneut.",
      );
    },
    onSuccess: (updated) => {
      queryClient.setQueryData(
        [organizationId, campId, "devotion", updated.id],
        updated,
      );
      queryClient.setQueryData<Devotion[]>(
        [organizationId, campId, "devotions"],
        (current) =>
          current?.map((devotion) =>
            devotion.id === updated.id
              ? {
                  ...devotion,
                  bibleReference: updated.bibleReference,
                  translation: updated.translation,
                  hasBibleSnapshot: updated.bibleSnapshot !== null,
                  version: updated.version,
                }
              : devotion,
          ),
      );
      setManualSnapshotOpen(false);
      setManualText("");
      setNotice(
        "Der manuelle Bibeltext wurde als unveränderter Snapshot gespeichert.",
      );
    },
  });
  const updateDevotion = useMutation({
    mutationFn: () => {
      const current = detail.data;
      if (!current) throw new Error("Die Andacht ist noch nicht geladen.");
      return mutateCateringJson<DevotionDetail>(
        `${path}/${current.id}`,
        "PUT",
        {
          topic: editTopic,
          bibleReference: editBibleReference,
          translation: Number(editTranslation),
          coreMessage: editCoreMessage,
          markdownContent: editMarkdownContent,
          responsibleUserIds: editResponsibleUserIds,
          materialNotes: editMaterialNotes,
          scheduleEntryId: editScheduleEntryId || null,
        },
        current.version,
        "Die Andacht wurde zwischenzeitlich geändert. Schließe die Bearbeitung und öffne sie erneut.",
      );
    },
    onSuccess: (updated) => {
      queryClient.setQueryData(
        [organizationId, campId, "devotion", updated.id],
        updated,
      );
      queryClient.setQueryData<Devotion[]>(
        [organizationId, campId, "devotions"],
        (current) =>
          current?.map((devotion) =>
            devotion.id === updated.id
              ? {
                  ...devotion,
                  topic: updated.topic,
                  bibleReference: updated.bibleReference,
                  translation: updated.translation,
                  responsibleUserIds: updated.responsibleUserIds,
                  scheduleEntryId: updated.scheduleEntryId,
                  hasBibleSnapshot: updated.bibleSnapshot !== null,
                  version: updated.version,
                }
              : devotion,
          ),
      );
      setEditing(false);
      setNotice(`${updated.topic} wurde gespeichert.`);
    },
  });
  const trashDevotion = useMutation({
    mutationFn: async () => {
      const current = detail.data;
      if (!current) throw new Error("Die Andacht ist noch nicht geladen.");
      await mutateCateringJson<void>(
        `${path}/${current.id}`,
        "DELETE",
        {},
        current.version,
        "Die Andacht wurde zwischenzeitlich geändert. Öffne den aktuellen Stand erneut.",
      );
      return { id: current.id, topic: current.topic };
    },
    onSuccess: ({ id, topic: deletedTopic }) => {
      queryClient.setQueryData<Devotion[]>(
        [organizationId, campId, "devotions"],
        (current) => current?.filter((devotion) => devotion.id !== id),
      );
      queryClient.removeQueries({
        queryKey: [organizationId, campId, "devotion", id],
      });
      setSelectedDevotionId(null);
      setConfirmTrash(false);
      setTrashConfirmed(false);
      setNotice(`${deletedTopic} wurde in den Papierkorb verschoben.`);
    },
  });
  const refreshSnapshot = useMutation({
    mutationFn: () => {
      const current = detail.data;
      if (!current) throw new Error("Öffne zuerst eine Andacht.");
      return mutateCateringJson<BibleSnapshotRefreshResult>(
        `${path}/${current.id}/bible/refresh`,
        "POST",
        {},
        current.version,
        "Die Andacht wurde zwischenzeitlich geändert. Öffne den aktuellen Stand erneut.",
      );
    },
    onSuccess: (result) => {
      queryClient.setQueryData(
        [organizationId, campId, "devotion", result.devotion.id],
        result.devotion,
      );
      queryClient.setQueryData<Devotion[]>(
        [organizationId, campId, "devotions"],
        (current) =>
          current?.map((devotion) =>
            devotion.id === result.devotion.id
              ? {
                  ...devotion,
                  bibleReference: result.devotion.bibleReference,
                  translation: result.devotion.translation,
                  hasBibleSnapshot: result.devotion.bibleSnapshot !== null,
                  version: result.devotion.version,
                }
              : devotion,
          ),
      );
      const messages: Record<number, string> = {
        0: "Bibeltext wurde als neuer Snapshot gespeichert.",
        1: "Die Bibelstelle wurde beim Provider nicht gefunden. Der bisherige Snapshot bleibt erhalten.",
        2: "Der Bibel-Provider ist nicht erreichbar. Der bisherige Snapshot bleibt erhalten.",
        3: "Der Bibel-Provider hat nicht rechtzeitig geantwortet. Der bisherige Snapshot bleibt erhalten.",
      };
      setNotice(messages[result.status] ?? "Der Bibeltext wurde geprüft.");
    },
  });
  return (
    <>
      <PageHeading eyebrow="Geistliche Planung" title="Andachten">
        <p>
          Schlachter 1951 ist die Standardübersetzung. Gespeicherte Bibeltexte
          bleiben unveränderte Snapshots.
        </p>
      </PageHeading>
      {notice ? (
        <p className="form-feedback" role="status">
          {notice}
        </p>
      ) : null}
      <QueryState loading={query.isLoading} error={query.error} />
      <div className="toolbar">
        <button
          type="button"
          className="primary-action"
          disabled={offline}
          onClick={() => {
            setCreating(true);
            setNotice("");
          }}
        >
          Andacht entwerfen
        </button>
      </div>
      {creating ? (
        <form
          className="schedule-create-form devotion-form"
          aria-label="Andacht anlegen"
          onSubmit={(event) => {
            event.preventDefault();
            setNotice("");
            createDevotion.mutate();
          }}
        >
          <h2>Neue Andacht planen</h2>
          <p className="form-hint">
            Der Andachtsentwurf bleibt unabhängig vom Bibel-Provider
            bearbeitbar. Ein Bibeltext wird erst nach einer ausdrücklichen
            Aktion gespeichert.
          </p>
          <div className="camp-form-grid">
            <label>
              Thema
              <input
                required
                value={topic}
                onChange={(event) => setTopic(event.target.value)}
              />
            </label>
            <label>
              Bibelstelle
              <input
                required
                value={bibleReference}
                onChange={(event) => setBibleReference(event.target.value)}
              />
            </label>
            <label>
              Bibelübersetzung
              <select
                value={translation}
                onChange={(event) => setTranslation(event.target.value)}
              >
                {translations.data?.map((item) => (
                  <option
                    key={item.translation}
                    value={String(item.translation)}
                  >
                    {item.displayName}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Verknüpfung zum Tagesplan
              <select
                value={scheduleEntryId}
                onChange={(event) => setScheduleEntryId(event.target.value)}
              >
                <option value="">Keine Verknüpfung</option>
                {scheduleEntries.data?.map((entry) => (
                  <option key={entry.id} value={entry.id}>
                    {entry.title}
                  </option>
                ))}
              </select>
            </label>
            <label className="full-width">
              Ziel oder Kerngedanke
              <textarea
                required
                value={coreMessage}
                onChange={(event) => setCoreMessage(event.target.value)}
              />
            </label>
            <label className="full-width">
              Markdown-Inhalt oder Gliederung
              <textarea
                required
                value={markdownContent}
                onChange={(event) => setMarkdownContent(event.target.value)}
              />
            </label>
            <label className="full-width">
              Materialhinweise
              <textarea
                value={materialNotes}
                onChange={(event) => setMaterialNotes(event.target.value)}
              />
            </label>
          </div>
          <ResponsibilityFields
            candidates={members.data ?? []}
            selected={responsibleUserIds}
            onChange={setResponsibleUserIds}
          />
          {createDevotion.error ? (
            <p role="alert" className="error-message">
              {createDevotion.error.message}
            </p>
          ) : null}
          <div className="toolbar">
            <button
              type="submit"
              className="primary-action"
              disabled={createDevotion.isPending}
            >
              Andacht speichern
            </button>
            <button
              type="button"
              className="secondary-action"
              disabled={createDevotion.isPending}
              onClick={() => setCreating(false)}
            >
              Abbrechen
            </button>
          </div>
        </form>
      ) : null}
      <div className="card-grid">
        {query.data?.map((devotion) => (
          <article className="card" key={devotion.id}>
            <p className="eyebrow">{devotion.bibleReference}</p>
            <h2>{devotion.topic}</h2>
            <p>
              {devotion.hasBibleSnapshot
                ? "Bibeltext als lizenzierter Snapshot gespeichert"
                : "Referenz kann auch bei Provider-Ausfall manuell bearbeitet werden"}
            </p>
            <button
              type="button"
              className="secondary-action"
              aria-label={`${devotion.topic} öffnen`}
              aria-expanded={selectedDevotionId === devotion.id}
              onClick={() => {
                setSelectedDevotionId(devotion.id);
                setEditing(false);
                setManualSnapshotOpen(false);
                setConfirmTrash(false);
                setTrashConfirmed(false);
                setNotice("");
              }}
            >
              Andacht öffnen
            </button>
          </article>
        ))}
      </div>
      {selectedDevotionId ? (
        <section
          className="settings-section devotion-detail"
          aria-label="Geöffnete Andacht"
        >
          <QueryState loading={detail.isLoading} error={detail.error} />
          {detail.data ? (
            <>
              <div className="section-heading">
                <div>
                  <p className="eyebrow">{detail.data.bibleReference}</p>
                  <h2>{detail.data.topic}</h2>
                </div>
                <div className="toolbar compact-toolbar">
                  {!offline ? (
                    <>
                      <button
                        type="button"
                        className="secondary-action"
                        onClick={() => {
                          const current = detail.data;
                          setEditTopic(current.topic);
                          setEditBibleReference(current.bibleReference);
                          setEditTranslation(String(current.translation));
                          setEditCoreMessage(current.coreMessage);
                          setEditMarkdownContent(current.markdownContent);
                          setEditMaterialNotes(current.materialNotes);
                          setEditResponsibleUserIds(current.responsibleUserIds);
                          setEditScheduleEntryId(current.scheduleEntryId ?? "");
                          setEditing(true);
                          setNotice("");
                        }}
                      >
                        Andacht bearbeiten
                      </button>
                      <button
                        type="button"
                        className="primary-action"
                        disabled={refreshSnapshot.isPending}
                        onClick={() => {
                          setNotice("");
                          refreshSnapshot.mutate();
                        }}
                      >
                        Bibeltext ausdrücklich aktualisieren
                      </button>
                      <button
                        type="button"
                        className="secondary-action"
                        onClick={() => {
                          setManualSnapshotOpen(true);
                          setNotice("");
                        }}
                      >
                        Bibeltext manuell speichern
                      </button>
                    </>
                  ) : null}
                  <button
                    type="button"
                    className="secondary-action"
                    onClick={() => {
                      setSelectedDevotionId(null);
                      setEditing(false);
                      setManualSnapshotOpen(false);
                      setConfirmTrash(false);
                    }}
                  >
                    Andacht schließen
                  </button>
                </div>
              </div>
              {editing ? (
                <form
                  className="schedule-create-form devotion-edit-form"
                  aria-label={`${detail.data.topic} bearbeiten`}
                  onSubmit={(event) => {
                    event.preventDefault();
                    setNotice("");
                    updateDevotion.mutate();
                  }}
                >
                  <h3>Andacht bearbeiten</h3>
                  <div className="camp-form-grid">
                    <label>
                      Thema bearbeiten
                      <input
                        required
                        value={editTopic}
                        onChange={(event) => setEditTopic(event.target.value)}
                      />
                    </label>
                    <label>
                      Bibelstelle bearbeiten
                      <input
                        required
                        value={editBibleReference}
                        onChange={(event) =>
                          setEditBibleReference(event.target.value)
                        }
                      />
                    </label>
                    <label>
                      Bibelübersetzung bearbeiten
                      <select
                        value={editTranslation}
                        onChange={(event) =>
                          setEditTranslation(event.target.value)
                        }
                      >
                        {translations.data?.map((item) => (
                          <option
                            key={item.translation}
                            value={String(item.translation)}
                          >
                            {item.displayName}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Tagesplan-Verknüpfung bearbeiten
                      <select
                        value={editScheduleEntryId}
                        onChange={(event) =>
                          setEditScheduleEntryId(event.target.value)
                        }
                      >
                        <option value="">Keine Verknüpfung</option>
                        {scheduleEntries.data?.map((entry) => (
                          <option key={entry.id} value={entry.id}>
                            {entry.title}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="full-width">
                      Ziel oder Kerngedanke bearbeiten
                      <textarea
                        required
                        value={editCoreMessage}
                        onChange={(event) =>
                          setEditCoreMessage(event.target.value)
                        }
                      />
                    </label>
                    <label className="full-width">
                      Markdown-Inhalt oder Gliederung bearbeiten
                      <textarea
                        required
                        value={editMarkdownContent}
                        onChange={(event) =>
                          setEditMarkdownContent(event.target.value)
                        }
                      />
                    </label>
                    <label className="full-width">
                      Materialhinweise bearbeiten
                      <textarea
                        value={editMaterialNotes}
                        onChange={(event) =>
                          setEditMaterialNotes(event.target.value)
                        }
                      />
                    </label>
                  </div>
                  <ResponsibilityFields
                    candidates={members.data ?? []}
                    selected={editResponsibleUserIds}
                    onChange={setEditResponsibleUserIds}
                  />
                  {updateDevotion.error ? (
                    <p role="alert" className="error-message">
                      {updateDevotion.error.message}
                    </p>
                  ) : null}
                  <div className="toolbar">
                    <button
                      type="submit"
                      className="primary-action"
                      disabled={updateDevotion.isPending}
                    >
                      Andachtsänderung speichern
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      disabled={updateDevotion.isPending}
                      onClick={() => setEditing(false)}
                    >
                      Abbrechen
                    </button>
                  </div>
                </form>
              ) : null}
              <h3>Kerngedanke</h3>
              <p>{detail.data.coreMessage}</p>
              <h3>Entwurf</h3>
              <pre className="markdown-source">
                {detail.data.markdownContent}
              </pre>
              {detail.data.materialNotes ? (
                <p>
                  <strong>Materialhinweise:</strong> {detail.data.materialNotes}
                </p>
              ) : null}
              {detail.data.bibleSnapshot ? (
                <section
                  className="bible-snapshot"
                  aria-label="Gespeicherter Bibeltext"
                >
                  <div className="section-heading">
                    <div>
                      <p className="eyebrow">Unveränderter Snapshot</p>
                      <h3>{detail.data.bibleSnapshot.reference}</h3>
                    </div>
                    <p className="status">
                      Snapshot vom{" "}
                      {new Intl.DateTimeFormat("de-DE", {
                        day: "2-digit",
                        month: "2-digit",
                        year: "numeric",
                      }).format(
                        new Date(detail.data.bibleSnapshot.retrievedAt),
                      )}
                    </p>
                  </div>
                  <blockquote>
                    {detail.data.bibleSnapshot.textExcerpt}
                  </blockquote>
                  <dl className="definition-grid">
                    <div>
                      <dt>Übersetzung</dt>
                      <dd>
                        {detail.data.bibleSnapshot.translationDisplayName} ·{" "}
                        {detail.data.bibleSnapshot.technicalTranslationId}
                      </dd>
                    </div>
                    <div>
                      <dt>Lizenz</dt>
                      <dd>{detail.data.bibleSnapshot.license}</dd>
                    </div>
                    <div>
                      <dt>Attribution</dt>
                      <dd>{detail.data.bibleSnapshot.attribution}</dd>
                    </div>
                    <div>
                      <dt>Herkunft</dt>
                      <dd>
                        {detail.data.bibleSnapshot.origin === 0
                          ? "Bibel-Provider"
                          : "Manuell gespeichert"}
                      </dd>
                    </div>
                  </dl>
                </section>
              ) : (
                <p className="empty-state">
                  Noch kein Bibeltext gespeichert. Referenz und Andachtsentwurf
                  bleiben auch ohne Provider bearbeitbar.
                </p>
              )}
              {manualSnapshotOpen ? (
                <form
                  className="schedule-create-form manual-bible-form"
                  aria-label="Bibeltext manuell speichern"
                  onSubmit={(event) => {
                    event.preventDefault();
                    setNotice("");
                    saveManualSnapshot.mutate();
                  }}
                >
                  <h3>Manuellen Bibel-Snapshot speichern</h3>
                  <p className="form-hint">
                    Speichere nur einen Text, dessen Lizenz die Verwendung im
                    Freizeit-Cockpit erlaubt. Übersetzung und Bibelstelle werden
                    aus der Andacht übernommen.
                  </p>
                  <label>
                    Manueller Bibeltext
                    <textarea
                      required
                      value={manualText}
                      onChange={(event) => setManualText(event.target.value)}
                    />
                  </label>
                  {saveManualSnapshot.error ? (
                    <p role="alert" className="error-message">
                      {saveManualSnapshot.error.message}
                    </p>
                  ) : null}
                  <div className="toolbar">
                    <button
                      type="submit"
                      className="primary-action"
                      disabled={saveManualSnapshot.isPending}
                    >
                      Manuellen Snapshot speichern
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      disabled={saveManualSnapshot.isPending}
                      onClick={() => setManualSnapshotOpen(false)}
                    >
                      Abbrechen
                    </button>
                  </div>
                </form>
              ) : null}
              <OwnerAttachmentsPanel
                organizationId={organizationId}
                campId={campId}
                ownerType="Devotion"
                ownerId={detail.data.id}
                ownerName={detail.data.topic}
                ownerNoun="die Andacht"
                canUpload={!offline}
                canDelete={!offline}
              />
              {!offline && !confirmTrash ? (
                <button
                  type="button"
                  className="danger-action"
                  onClick={() => {
                    setConfirmTrash(true);
                    setTrashConfirmed(false);
                    setNotice("");
                  }}
                >
                  Andacht in Papierkorb verschieben
                </button>
              ) : null}
              {confirmTrash ? (
                <section
                  className="delete-confirmation"
                  aria-label="Andacht in Papierkorb verschieben"
                >
                  <h3>{detail.data.topic} wirklich verschieben?</h3>
                  <p>
                    Die Andacht bleibt 30 Tage im Camp-Papierkorb und kann dort
                    wiederhergestellt werden.
                  </p>
                  <label className="checkbox-label">
                    <input
                      type="checkbox"
                      checked={trashConfirmed}
                      onChange={(event) =>
                        setTrashConfirmed(event.target.checked)
                      }
                    />
                    Ich möchte diese Andacht in den Papierkorb verschieben.
                  </label>
                  {trashDevotion.error ? (
                    <p role="alert" className="error-message">
                      {trashDevotion.error.message}
                    </p>
                  ) : null}
                  <div className="toolbar">
                    <button
                      type="button"
                      className="danger-action"
                      disabled={!trashConfirmed || trashDevotion.isPending}
                      onClick={() => trashDevotion.mutate()}
                    >
                      Verschieben bestätigen
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      disabled={trashDevotion.isPending}
                      onClick={() => setConfirmTrash(false)}
                    >
                      Abbrechen
                    </button>
                  </div>
                </section>
              ) : null}
              {refreshSnapshot.error ? (
                <p role="alert" className="error-message">
                  {refreshSnapshot.error.message}
                </p>
              ) : null}
            </>
          ) : null}
        </section>
      ) : null}
    </>
  );
}
