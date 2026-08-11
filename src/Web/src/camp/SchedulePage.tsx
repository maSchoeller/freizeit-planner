import FullCalendar from "@fullcalendar/react";
import deLocale from "@fullcalendar/core/locales/de";
import dayGridPlugin from "@fullcalendar/daygrid";
import interactionPlugin from "@fullcalendar/interaction";
import luxonPlugin from "@fullcalendar/luxon3";
import timeGridPlugin from "@fullcalendar/timegrid";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { loadOfflineSnapshot, saveOfflineSnapshot } from "../offlineSnapshot";
import { getAntiforgeryToken } from "../api/security";
import { authenticatedFetch as fetch } from "../api/authentication";
import type {
  CampMemberSummary,
  ScheduleEditDraft,
  ScheduleEntry,
  ScheduleEntryBody,
} from "./types";
import { useCampQuery, useCampRuntime } from "./runtime";
import {
  createScheduleEditDraft,
  nextLocalDate,
  optimisticEntryFromCalendar,
  optimisticEntryFromDraft,
  scheduleBodyFromCalendar,
  scheduleBodyFromDraft,
  scheduleTimingLabel,
  ScheduleUpdateError,
} from "./schedule";
import {
  PageHeading,
  PrintButton,
  QueryState,
  ResponsibilityFields,
} from "./ui";
import { OwnerAttachmentsPanel } from "./AttachmentsPanel";

export function SchedulePage({
  offline,
  readOnly,
}: {
  offline: boolean;
  readOnly: boolean;
}) {
  const runtime = useCampRuntime();
  const { organizationId, campId, camp } = runtime;
  const toDateExclusive = nextLocalDate(camp.endsOn);
  const path = `/api/v1/organizations/${organizationId}/camps/${campId}/schedule?fromDate=${camp.startsOn}&toDateExclusive=${toDateExclusive}`;
  const query = useCampQuery<ScheduleEntry[]>("schedule", path, !offline);
  const candidatesQuery = useCampQuery<CampMemberSummary[]>(
    "responsibility-candidates",
    `/api/v1/organizations/${organizationId}/camps/${campId}/responsibility-candidates`,
    !offline,
  );
  const queryClient = useQueryClient();
  const scheduleQueryKey = [organizationId, campId, "schedule"] as const;
  const [deleteCandidate, setDeleteCandidate] = useState<ScheduleEntry | null>(
    null,
  );
  const [linkedBehavior, setLinkedBehavior] = useState<
    "" | "Unlink" | "MoveLinkedToTrash"
  >("");
  const [deleteStatus, setDeleteStatus] = useState("");
  const [filesEntryId, setFilesEntryId] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [createType, setCreateType] = useState<"" | "Meal" | "Devotion">("");
  const [scheduleTitle, setScheduleTitle] = useState("");
  const [scheduleDescription, setScheduleDescription] = useState("");
  const [scheduleAllDay, setScheduleAllDay] = useState(false);
  const [scheduleDate, setScheduleDate] = useState(camp.startsOn);
  const [scheduleEndDate, setScheduleEndDate] = useState(
    nextLocalDate(camp.startsOn),
  );
  const [scheduleStart, setScheduleStart] = useState("12:00");
  const [scheduleEnd, setScheduleEnd] = useState("13:00");
  const [scheduleLocation, setScheduleLocation] = useState("");
  const [scheduleCategory, setScheduleCategory] = useState("Programm");
  const [scheduleStatus, setScheduleStatus] = useState("0");
  const [scheduleAudience, setScheduleAudience] = useState("");
  const [scheduleResponsibleUserIds, setScheduleResponsibleUserIds] = useState<
    string[]
  >([]);
  const [mealName, setMealName] = useState("");
  const [devotionTopic, setDevotionTopic] = useState("");
  const [devotionBibleReference, setDevotionBibleReference] = useState("");
  const [devotionCoreMessage, setDevotionCoreMessage] = useState("");
  const [devotionContent, setDevotionContent] = useState("");
  const [devotionMaterialNotes, setDevotionMaterialNotes] = useState("");
  const [createStatus, setCreateStatus] = useState("");
  const [editCandidate, setEditCandidate] = useState<ScheduleEntry | null>(
    null,
  );
  const [editDraft, setEditDraft] = useState<ScheduleEditDraft | null>(null);
  const [updateStatus, setUpdateStatus] = useState("");
  const create = useMutation({
    mutationFn: async () => {
      const token = await getAntiforgeryToken();
      const schedule = {
        timing: scheduleAllDay
          ? {
              isAllDay: true,
              localStart: null,
              localEnd: null,
              startDate: scheduleDate,
              endDateExclusive: scheduleEndDate,
              startChoice: 0,
              endChoice: 0,
            }
          : {
              isAllDay: false,
              localStart: `${scheduleDate}T${scheduleStart}:00`,
              localEnd: `${scheduleDate}T${scheduleEnd}:00`,
              startDate: null,
              endDateExclusive: null,
              startChoice: 0,
              endChoice: 0,
            },
        title: scheduleTitle,
        description: scheduleDescription || null,
        location: scheduleLocation || null,
        category: scheduleCategory,
        status: Number(scheduleStatus),
        responsibleUserIds: scheduleResponsibleUserIds,
        audience: scheduleAudience || null,
      };
      const path =
        createType === "Meal"
          ? "/schedule/with-meal"
          : createType === "Devotion"
            ? "/schedule/with-devotion"
            : "/schedule";
      const body =
        createType === "Meal"
          ? {
              schedule,
              meal: { name: mealName, portionOverride: null, recipeIds: [] },
            }
          : createType === "Devotion"
            ? {
                schedule,
                devotion: {
                  topic: devotionTopic,
                  bibleReference: devotionBibleReference,
                  translation: 0,
                  coreMessage: devotionCoreMessage,
                  markdownContent: devotionContent,
                  responsibleUserIds: [],
                  materialNotes: devotionMaterialNotes,
                },
              }
            : schedule;
      const response = await fetch(
        `/api/v1/organizations/${organizationId}/camps/${campId}${path}`,
        {
          method: "POST",
          credentials: "same-origin",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token,
          },
          body: JSON.stringify(body),
        },
      );
      if (!response.ok)
        throw new Error("Der Zeitplaneintrag konnte nicht angelegt werden.");
    },
    onSuccess: async () => {
      setCreateStatus(`„${scheduleTitle}“ wurde angelegt.`);
      setCreateOpen(false);
      setCreateType("");
      setScheduleTitle("");
      setScheduleDescription("");
      setScheduleAllDay(false);
      setScheduleLocation("");
      setScheduleStatus("0");
      setScheduleAudience("");
      setScheduleResponsibleUserIds([]);
      setMealName("");
      setDevotionTopic("");
      setDevotionBibleReference("");
      setDevotionCoreMessage("");
      setDevotionContent("");
      setDevotionMaterialNotes("");
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: [organizationId, campId, "schedule"],
        }),
        queryClient.invalidateQueries({
          queryKey: [organizationId, campId, "meals"],
        }),
        queryClient.invalidateQueries({
          queryKey: [organizationId, campId, "devotions"],
        }),
      ]);
    },
  });
  const remove = useMutation({
    mutationFn: async ({
      entry,
      behavior,
    }: {
      entry: ScheduleEntry;
      behavior: "Unlink" | "MoveLinkedToTrash";
    }) => {
      const token = await getAntiforgeryToken();
      const response = await fetch(
        `/api/v1/organizations/${organizationId}/camps/${campId}/schedule/${entry.id}?linkedBehavior=${behavior}`,
        {
          method: "DELETE",
          credentials: "same-origin",
          headers: {
            "X-CSRF-TOKEN": token,
            "If-Match": `"${entry.version}"`,
          },
        },
      );
      if (!response.ok)
        throw new Error(
          "Der Zeitplaneintrag konnte nicht in den Papierkorb verschoben werden.",
        );
    },
    onSuccess: async (_, variables) => {
      setDeleteStatus(
        `„${variables.entry.title}“ wurde in den Papierkorb verschoben.`,
      );
      setDeleteCandidate(null);
      setLinkedBehavior("");
      if (filesEntryId === variables.entry.id) setFilesEntryId(null);
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: [organizationId, campId, "schedule"],
        }),
        queryClient.invalidateQueries({
          queryKey: [organizationId, campId, "meals"],
        }),
        queryClient.invalidateQueries({
          queryKey: [organizationId, campId, "devotions"],
        }),
        queryClient.invalidateQueries({
          queryKey: [organizationId, campId, "trash"],
        }),
      ]);
    },
  });
  const update = useMutation({
    mutationFn: async ({
      entry,
      body,
    }: {
      entry: ScheduleEntry;
      body: ScheduleEntryBody;
      optimisticEntry: ScheduleEntry;
      source: "agenda" | "calendar";
      revert?: () => void;
    }) => {
      const token = await getAntiforgeryToken();
      const response = await fetch(
        `/api/v1/organizations/${organizationId}/camps/${campId}/schedule/${entry.id}`,
        {
          method: "PUT",
          credentials: "same-origin",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token,
            "If-Match": `"${entry.version}"`,
          },
          body: JSON.stringify(body),
        },
      );
      if (!response.ok)
        throw new ScheduleUpdateError(
          response.status === 409 || response.status === 412
            ? "Der Zeitplaneintrag wurde zwischenzeitlich geändert. Der aktuelle Stand wurde neu geladen."
            : "Die Änderung konnte nicht gespeichert werden. Die Änderung wurde zurückgesetzt.",
        );
      return (await response.json()) as ScheduleEntry;
    },
    onMutate: async (variables) => {
      setUpdateStatus("");
      await queryClient.cancelQueries({ queryKey: scheduleQueryKey });
      const previous =
        queryClient.getQueryData<ScheduleEntry[]>(scheduleQueryKey);
      queryClient.setQueryData<ScheduleEntry[]>(scheduleQueryKey, (current) =>
        current?.map((entry) =>
          entry.id === variables.entry.id ? variables.optimisticEntry : entry,
        ),
      );
      return { previous };
    },
    onSuccess: (result, variables) => {
      queryClient.setQueryData<ScheduleEntry[]>(scheduleQueryKey, (current) =>
        current?.map((entry) => (entry.id === result.id ? result : entry)),
      );
      setUpdateStatus(`„${result.title}“ wurde gespeichert.`);
      if (variables.source === "agenda") {
        setEditCandidate(null);
        setEditDraft(null);
      }
    },
    onError: (error, variables, context) => {
      variables.revert?.();
      if (context?.previous)
        queryClient.setQueryData(scheduleQueryKey, context.previous);
      setUpdateStatus(
        error instanceof ScheduleUpdateError
          ? error.message
          : "Die Änderung konnte nicht gespeichert werden. Die Änderung wurde zurückgesetzt.",
      );
    },
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: scheduleQueryKey });
    },
  });
  const stored = (loadOfflineSnapshot({ organizationId, campId })?.schedule ??
    []) as ScheduleEntry[];
  const entries = query.data ?? (offline ? stored : []);
  const filesEntry = entries.find((entry) => entry.id === filesEntryId);
  useEffect(() => {
    if (query.data) saveOfflineSnapshot(runtime, { schedule: query.data });
  }, [query.data]);
  const events = entries.map((entry) => ({
    id: entry.id,
    title: entry.title,
    start: entry.timing.startsAtUtc ?? entry.timing.startDate,
    end: entry.timing.endsAtUtc ?? entry.timing.endDateExclusive,
    allDay: entry.timing.isAllDay,
  }));
  const responsibilityCandidates = (candidatesQuery.data ?? []).filter(
    (candidate) => candidate.userId && candidate.displayName,
  );
  return (
    <>
      <PageHeading eyebrow="Planung" title="Tages- und Wochenplan">
        <p>
          Überlappungen sind erlaubt und werden informativ markiert. Alle Zeiten
          gelten für {camp.timeZoneId}.
        </p>
      </PageHeading>
      <div className="toolbar print-actions">
        <PrintButton scope="schedule">Zeitplan drucken</PrintButton>
      </div>
      <QueryState
        loading={query.isLoading && !offline}
        error={
          query.error ?? candidatesQuery.error ?? create.error ?? remove.error
        }
      />
      <p
        className="visually-hidden"
        role="status"
        aria-label="Anlegestatus"
        aria-live="polite"
      >
        {createStatus}
      </p>
      <p
        className="visually-hidden"
        role="status"
        aria-label="Löschstatus"
        aria-live="polite"
      >
        {deleteStatus}
      </p>
      <p
        className={updateStatus ? "form-feedback" : "visually-hidden"}
        role="status"
        aria-label="Änderungsstatus"
        aria-live="polite"
      >
        {updateStatus}
      </p>
      <section className="calendar-card" aria-label="Kalenderansicht">
        <FullCalendar
          plugins={[
            timeGridPlugin,
            dayGridPlugin,
            interactionPlugin,
            luxonPlugin,
          ]}
          initialView="timeGridWeek"
          initialDate={camp.startsOn}
          timeZone={camp.timeZoneId}
          locale={deLocale}
          buttonIcons={false}
          firstDay={1}
          allDayText="Ganztägig"
          height="auto"
          events={events}
          editable={!readOnly && !update.isPending}
          eventDrop={(info) => {
            const entry = entries.find((item) => item.id === info.event.id);
            if (!entry) {
              info.revert();
              return;
            }
            const event = {
              allDay: info.event.allDay,
              startStr: info.event.startStr,
              endStr: info.event.endStr,
            };
            update.mutate({
              entry,
              body: scheduleBodyFromCalendar(entry, event),
              optimisticEntry: optimisticEntryFromCalendar(entry, event),
              source: "calendar",
              revert: info.revert,
            });
          }}
          eventResize={(info) => {
            const entry = entries.find((item) => item.id === info.event.id);
            if (!entry) {
              info.revert();
              return;
            }
            const event = {
              allDay: info.event.allDay,
              startStr: info.event.startStr,
              endStr: info.event.endStr,
            };
            update.mutate({
              entry,
              body: scheduleBodyFromCalendar(entry, event),
              optimisticEntry: optimisticEntryFromCalendar(entry, event),
              source: "calendar",
              revert: info.revert,
            });
          }}
        />
      </section>
      <section
        className="settings-section"
        aria-labelledby="agenda-title"
        data-print-section="schedule"
      >
        <div className="section-heading">
          <h2 id="agenda-title">Barrierearme Agenda</h2>
          <button
            className="primary-action"
            disabled={readOnly}
            onClick={() => {
              setCreateStatus("");
              setCreateOpen(true);
            }}
          >
            Eintrag erstellen
          </button>
        </div>
        {createOpen && (
          <form
            className="schedule-create-form"
            onSubmit={(event) => {
              event.preventDefault();
              create.mutate();
            }}
          >
            <fieldset>
              <legend>Zeitplaneintrag</legend>
              <label>
                Titel des Zeitplaneintrags
                <input
                  required
                  value={scheduleTitle}
                  onChange={(event) => setScheduleTitle(event.target.value)}
                />
              </label>
              <label>
                Beschreibung
                <textarea
                  value={scheduleDescription}
                  onChange={(event) =>
                    setScheduleDescription(event.target.value)
                  }
                />
              </label>
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={scheduleAllDay}
                  onChange={(event) => {
                    setScheduleAllDay(event.target.checked);
                    if (event.target.checked)
                      setScheduleEndDate(nextLocalDate(scheduleDate));
                  }}
                />
                Ganztägiger Eintrag
              </label>
              <div
                className={`schedule-create-grid${
                  scheduleAllDay ? " schedule-all-day-grid" : ""
                }`}
              >
                <label>
                  {scheduleAllDay ? "Startdatum" : "Datum"}
                  <input
                    type="date"
                    required
                    value={scheduleDate}
                    onChange={(event) => {
                      setScheduleDate(event.target.value);
                      if (scheduleAllDay)
                        setScheduleEndDate(nextLocalDate(event.target.value));
                    }}
                  />
                </label>
                {scheduleAllDay ? (
                  <label>
                    Bis (exklusiv)
                    <input
                      type="date"
                      required
                      value={scheduleEndDate}
                      onChange={(event) =>
                        setScheduleEndDate(event.target.value)
                      }
                    />
                  </label>
                ) : (
                  <>
                    <label>
                      Beginn
                      <input
                        type="time"
                        required
                        value={scheduleStart}
                        onChange={(event) =>
                          setScheduleStart(event.target.value)
                        }
                      />
                    </label>
                    <label>
                      Ende
                      <input
                        type="time"
                        required
                        value={scheduleEnd}
                        onChange={(event) => setScheduleEnd(event.target.value)}
                      />
                    </label>
                  </>
                )}
              </div>
              <label>
                Ort
                <input
                  value={scheduleLocation}
                  onChange={(event) => setScheduleLocation(event.target.value)}
                />
              </label>
              <label>
                Kategorie
                <input
                  required
                  value={scheduleCategory}
                  onChange={(event) => setScheduleCategory(event.target.value)}
                />
              </label>
              <label>
                Status
                <select
                  value={scheduleStatus}
                  onChange={(event) => setScheduleStatus(event.target.value)}
                >
                  <option value="0">Geplant</option>
                  <option value="1">Bestätigt</option>
                  <option value="2">Abgesagt</option>
                </select>
              </label>
              <label>
                Zielgruppe
                <input
                  value={scheduleAudience}
                  onChange={(event) => setScheduleAudience(event.target.value)}
                />
              </label>
              <label>
                Gemeinsam anlegen
                <select
                  value={createType}
                  onChange={(event) =>
                    setCreateType(
                      event.target.value as "" | "Meal" | "Devotion",
                    )
                  }
                >
                  <option value="">Keine Verknüpfung</option>
                  <option value="Meal">Mahlzeit</option>
                  <option value="Devotion">Andacht</option>
                </select>
              </label>
            </fieldset>
            <ResponsibilityFields
              candidates={responsibilityCandidates}
              selected={scheduleResponsibleUserIds}
              onChange={setScheduleResponsibleUserIds}
            />
            {createType === "Meal" && (
              <fieldset>
                <legend>Mahlzeit</legend>
                <label>
                  Name der Mahlzeit
                  <input
                    required
                    value={mealName}
                    onChange={(event) => setMealName(event.target.value)}
                  />
                </label>
              </fieldset>
            )}
            {createType === "Devotion" && (
              <fieldset>
                <legend>Andacht</legend>
                <p className="form-hint">
                  Du wirst zunächst als verantwortlich eingetragen.
                </p>
                <label>
                  Thema der Andacht
                  <input
                    required
                    value={devotionTopic}
                    onChange={(event) => setDevotionTopic(event.target.value)}
                  />
                </label>
                <label>
                  Bibelstelle
                  <input
                    required
                    value={devotionBibleReference}
                    onChange={(event) =>
                      setDevotionBibleReference(event.target.value)
                    }
                  />
                </label>
                <label>
                  Kernaussage
                  <input
                    required
                    value={devotionCoreMessage}
                    onChange={(event) =>
                      setDevotionCoreMessage(event.target.value)
                    }
                  />
                </label>
                <label>
                  Inhalt der Andacht
                  <textarea
                    required
                    value={devotionContent}
                    onChange={(event) => setDevotionContent(event.target.value)}
                  />
                </label>
                <label>
                  Materialhinweise
                  <textarea
                    value={devotionMaterialNotes}
                    onChange={(event) =>
                      setDevotionMaterialNotes(event.target.value)
                    }
                  />
                </label>
              </fieldset>
            )}
            <div className="toolbar">
              <button
                className="primary-action"
                type="submit"
                disabled={readOnly || create.isPending}
              >
                Zeitplaneintrag anlegen
              </button>
              <button
                className="secondary-action"
                type="button"
                disabled={create.isPending}
                onClick={() => setCreateOpen(false)}
              >
                Abbrechen
              </button>
            </div>
          </form>
        )}
        {editCandidate && editDraft && (
          <form
            className="schedule-create-form"
            aria-label={`${editCandidate.title} bearbeiten`}
            onSubmit={(event) => {
              event.preventDefault();
              update.mutate({
                entry: editCandidate,
                body: scheduleBodyFromDraft(editCandidate, editDraft),
                optimisticEntry: optimisticEntryFromDraft(
                  editCandidate,
                  editDraft,
                ),
                source: "agenda",
              });
            }}
          >
            <fieldset>
              <legend>Zeitplaneintrag bearbeiten</legend>
              <label>
                Titel
                <input
                  autoFocus
                  required
                  value={editDraft.title}
                  onChange={(event) =>
                    setEditDraft({ ...editDraft, title: event.target.value })
                  }
                />
              </label>
              <label>
                Beschreibung
                <textarea
                  value={editDraft.description}
                  onChange={(event) =>
                    setEditDraft({
                      ...editDraft,
                      description: event.target.value,
                    })
                  }
                />
              </label>
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={editDraft.isAllDay}
                  onChange={(event) =>
                    setEditDraft({
                      ...editDraft,
                      isAllDay: event.target.checked,
                    })
                  }
                />
                Ganztägiger Eintrag
              </label>
              <div
                className={`schedule-create-grid ${
                  editDraft.isAllDay
                    ? "schedule-all-day-grid"
                    : "schedule-time-grid"
                }`}
              >
                <label>
                  Startdatum
                  <input
                    type="date"
                    required
                    value={editDraft.startDate}
                    onChange={(event) =>
                      setEditDraft({
                        ...editDraft,
                        startDate: event.target.value,
                      })
                    }
                  />
                </label>
                {!editDraft.isAllDay && (
                  <label>
                    Beginn
                    <input
                      type="time"
                      required
                      value={editDraft.startTime}
                      onChange={(event) =>
                        setEditDraft({
                          ...editDraft,
                          startTime: event.target.value,
                        })
                      }
                    />
                  </label>
                )}
                <label>
                  {editDraft.isAllDay ? "Bis (exklusiv)" : "Enddatum"}
                  <input
                    type="date"
                    required
                    value={editDraft.endDate}
                    onChange={(event) =>
                      setEditDraft({
                        ...editDraft,
                        endDate: event.target.value,
                      })
                    }
                  />
                </label>
                {!editDraft.isAllDay && (
                  <label>
                    Ende
                    <input
                      type="time"
                      required
                      value={editDraft.endTime}
                      onChange={(event) =>
                        setEditDraft({
                          ...editDraft,
                          endTime: event.target.value,
                        })
                      }
                    />
                  </label>
                )}
              </div>
              <label>
                Ort
                <input
                  value={editDraft.location}
                  onChange={(event) =>
                    setEditDraft({ ...editDraft, location: event.target.value })
                  }
                />
              </label>
              <label>
                Kategorie
                <input
                  required
                  value={editDraft.category}
                  onChange={(event) =>
                    setEditDraft({ ...editDraft, category: event.target.value })
                  }
                />
              </label>
              <label>
                Status
                <select
                  value={editDraft.status}
                  onChange={(event) =>
                    setEditDraft({ ...editDraft, status: event.target.value })
                  }
                >
                  <option value="0">Geplant</option>
                  <option value="1">Bestätigt</option>
                  <option value="2">Abgesagt</option>
                </select>
              </label>
              <label>
                Zielgruppe
                <input
                  value={editDraft.audience}
                  onChange={(event) =>
                    setEditDraft({ ...editDraft, audience: event.target.value })
                  }
                />
              </label>
            </fieldset>
            <ResponsibilityFields
              candidates={responsibilityCandidates}
              selected={editDraft.responsibleUserIds}
              onChange={(responsibleUserIds) =>
                setEditDraft({ ...editDraft, responsibleUserIds })
              }
            />
            <p className="form-hint">
              Verantwortlichkeit dient der Übersicht und ändert keine Rechte.
            </p>
            <div className="toolbar">
              <button
                className="primary-action"
                type="submit"
                disabled={readOnly || update.isPending}
              >
                Änderungen speichern
              </button>
              <button
                className="secondary-action"
                type="button"
                disabled={update.isPending}
                onClick={() => {
                  setEditCandidate(null);
                  setEditDraft(null);
                }}
              >
                Abbrechen
              </button>
            </div>
          </form>
        )}
        {entries.length === 0 ? (
          <p className="empty-state">
            Für diesen Zeitraum gibt es noch keine Einträge.
          </p>
        ) : (
          <ol className="agenda-list">
            {entries.map((entry) => (
              <li key={entry.id}>
                <div>
                  <strong>{entry.title}</strong>
                  <span>
                    {entry.location ?? "Kein Ort"} · {entry.category}
                  </span>
                  <span>{scheduleTimingLabel(entry, camp.timeZoneId)}</span>
                </div>
                {entry.overlapsAnotherEntry && (
                  <span className="status info">Überschneidung</span>
                )}
                <button
                  type="button"
                  className="secondary-action"
                  aria-expanded={filesEntryId === entry.id}
                  aria-label={`Dateien zu ${entry.title} ${
                    filesEntryId === entry.id ? "schließen" : "öffnen"
                  }`}
                  onClick={() =>
                    setFilesEntryId((current) =>
                      current === entry.id ? null : entry.id,
                    )
                  }
                >
                  Dateien
                </button>
                <button
                  className="secondary-action"
                  disabled={readOnly || update.isPending}
                  aria-label={`${entry.title} bearbeiten`}
                  onClick={() => {
                    setUpdateStatus("");
                    setEditCandidate(entry);
                    setEditDraft(
                      createScheduleEditDraft(entry, camp.timeZoneId),
                    );
                  }}
                >
                  Bearbeiten
                </button>
                <button
                  className="danger-action"
                  disabled={readOnly || remove.isPending}
                  aria-label={`${entry.title} löschen`}
                  onClick={() => {
                    setDeleteStatus("");
                    setDeleteCandidate(entry);
                    setLinkedBehavior("");
                  }}
                >
                  Löschen
                </button>
              </li>
            ))}
          </ol>
        )}
        {filesEntry ? (
          <OwnerAttachmentsPanel
            organizationId={organizationId}
            campId={campId}
            ownerType="ScheduleEntry"
            ownerId={filesEntry.id}
            ownerName={filesEntry.title}
            ownerNoun="den Zeitplaneintrag"
            canUpload={!readOnly}
            canDelete={!readOnly}
          />
        ) : null}
        {deleteCandidate && (
          <form
            className="delete-choice"
            onSubmit={(event) => {
              event.preventDefault();
              if (linkedBehavior)
                remove.mutate({
                  entry: deleteCandidate,
                  behavior: linkedBehavior,
                });
            }}
          >
            <h3>„{deleteCandidate.title}“ löschen?</h3>
            <p>
              Entscheide ausdrücklich, was mit verknüpften Mahlzeiten und
              Andachten geschehen soll.
            </p>
            <fieldset>
              <legend>Verknüpfte Inhalte</legend>
              <label>
                <input
                  type="radio"
                  name="linked-delete-behavior"
                  value="Unlink"
                  checked={linkedBehavior === "Unlink"}
                  onChange={() => setLinkedBehavior("Unlink")}
                />
                Mahlzeiten und Andachten vom Zeitplaneintrag lösen
              </label>
              <label>
                <input
                  type="radio"
                  name="linked-delete-behavior"
                  value="MoveLinkedToTrash"
                  checked={linkedBehavior === "MoveLinkedToTrash"}
                  onChange={() => setLinkedBehavior("MoveLinkedToTrash")}
                />
                Zeitplaneintrag, Mahlzeiten und Andachten gemeinsam in den
                Papierkorb verschieben
              </label>
            </fieldset>
            <div className="toolbar">
              <button
                type="submit"
                className="danger-action"
                disabled={!linkedBehavior || remove.isPending}
              >
                In den Papierkorb verschieben
              </button>
              <button
                type="button"
                className="secondary-action"
                disabled={remove.isPending}
                onClick={() => {
                  setDeleteCandidate(null);
                  setLinkedBehavior("");
                }}
              >
                Abbrechen
              </button>
            </div>
          </form>
        )}
      </section>
    </>
  );
}
