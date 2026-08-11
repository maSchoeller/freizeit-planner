import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { getAntiforgeryToken } from "../api/security";
import { authenticatedFetch as fetch } from "../api/authentication";
import type { CampTrashItem, SearchResult } from "./types";
import { getJson } from "./api";
import { useCampQuery, useCampRuntime } from "./runtime";
import { nextLocalDate } from "./schedule";
import { PageHeading, QueryState } from "./ui";

export function SearchTrashPage({ offline }: { offline: boolean }) {
  const { organizationId, campId, camp } = useCampRuntime();
  const [query, setQuery] = useState("");
  const [objectType, setObjectType] = useState("");
  const [metadataFilter, setMetadataFilter] = useState("");
  const [restoreNotice, setRestoreNotice] = useState("");
  const queryClient = useQueryClient();
  const normalizedQuery = query.trim();
  const search = useQuery({
    queryKey: [
      organizationId,
      campId,
      "search",
      normalizedQuery,
      objectType,
      metadataFilter,
    ],
    queryFn: () =>
      getJson<SearchResult[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/search?query=${encodeURIComponent(normalizedQuery)}${objectType ? `&objectTypes=${encodeURIComponent(objectType)}` : ""}${metadataFilter ? `&metadata=${encodeURIComponent(metadataFilter)}` : ""}`,
      ),
    enabled: normalizedQuery.length >= 2,
    retry: false,
  });
  const trash = useCampQuery<CampTrashItem[]>(
    "trash",
    `/api/v1/organizations/${organizationId}/camps/${campId}/trash`,
  );
  const restore = useMutation({
    mutationFn: async (item: CampTrashItem) => {
      const token = await getAntiforgeryToken();
      const response = await fetch(item.restorePath, {
        method: "POST",
        credentials: "same-origin",
        headers: {
          "X-CSRF-TOKEN": token,
          "If-Match": `"${item.version}"`,
        },
      });
      if (!response.ok)
        throw new Error("Der Inhalt konnte nicht wiederhergestellt werden.");
      return item;
    },
    onSuccess: async (item) => {
      queryClient.setQueryData<CampTrashItem[]>(
        [organizationId, campId, "trash"],
        (current) =>
          current?.filter(
            (candidate) =>
              candidate.objectType !== item.objectType ||
              candidate.objectId !== item.objectId,
          ),
      );
      setRestoreNotice(`${item.title} wurde wiederhergestellt.`);
      const invalidations = [
        queryClient.invalidateQueries({
          queryKey: [organizationId, campId, "trash"],
        }),
        queryClient.invalidateQueries({
          queryKey: [organizationId, campId, "search"],
        }),
        queryClient.invalidateQueries({
          queryKey: [organizationId, campId, "activity"],
        }),
      ];
      const restoredScope = restoredObjectQueryScope[item.objectType];
      if (restoredScope)
        invalidations.push(
          queryClient.invalidateQueries({
            queryKey: [organizationId, campId, restoredScope],
          }),
        );
      await Promise.all(invalidations);
    },
  });
  const exportBase = `/api/v1/organizations/${organizationId}/camps/${campId}/exports`;
  return (
    <>
      <PageHeading eyebrow="Finden und wiederherstellen" title="Suche">
        <p>
          Die Suche bleibt auf dieses Camp begrenzt. Gelöschte Inhalte werden
          nach 30 Tagen endgültig entfernt.
        </p>
      </PageHeading>
      <nav className="section-navigation" aria-label="Suche und Papierkorb">
        <a href="#suchergebnisse">Suche</a>
        <a href="#papierkorb">Papierkorb</a>
      </nav>
      {restoreNotice ? (
        <p className="form-feedback" role="status">
          {restoreNotice}
        </p>
      ) : null}
      <div className="toolbar search-toolbar">
        <label className="search-field search-wide">
          Camp durchsuchen
          <input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Titel oder Text"
          />
        </label>
        <label>
          Inhaltstyp
          <select
            value={objectType}
            onChange={(event) => {
              setObjectType(event.target.value);
              setMetadataFilter("");
            }}
          >
            <option value="">Alle Inhalte</option>
            <option value="ScheduleEntry">Zeitplan</option>
            <option value="Meal">Mahlzeiten</option>
            <option value="MaterialRequirement">Material</option>
            <option value="ShoppingList">Einkaufslisten</option>
            <option value="ShoppingItem">Einkaufspositionen</option>
            <option value="Devotion">Andachten</option>
            <option value="Note">Notizen</option>
            <option value="Attachment">Dateien</option>
          </select>
        </label>
        {searchMetadataFilters[objectType]?.length ? (
          <label>
            Merkmal
            <select
              value={metadataFilter}
              onChange={(event) => setMetadataFilter(event.target.value)}
            >
              <option value="">Alle Merkmale</option>
              {searchMetadataFilters[objectType].map((filter) => (
                <option key={filter.value} value={filter.value}>
                  {filter.label}
                </option>
              ))}
            </select>
          </label>
        ) : null}
      </div>
      <section id="suchergebnisse" className="settings-section">
        <h2>Suchergebnisse</h2>
        <QueryState loading={search.isLoading} error={search.error} />
        {search.data?.length ? (
          <ul className="detail-list">
            {search.data.map((result) => (
              <li key={`${result.objectType}-${result.objectId}`}>
                <strong>{result.title}</strong>
                <span>
                  {searchTypeLabel[result.objectType] ?? result.objectType} ·
                  zuletzt aktualisiert{" "}
                  {new Intl.DateTimeFormat("de-DE").format(
                    new Date(result.updatedAt),
                  )}
                </span>
              </li>
            ))}
          </ul>
        ) : normalizedQuery.length < 2 ? (
          <p className="empty-state">Gib mindestens zwei Zeichen ein.</p>
        ) : (
          !search.isLoading && (
            <p className="empty-state">Keine passenden Inhalte gefunden.</p>
          )
        )}
      </section>
      <section id="papierkorb" className="settings-section">
        <h2>Papierkorb</h2>
        <QueryState
          loading={trash.isLoading}
          error={trash.error ?? restore.error}
        />
        {trash.data?.length ? (
          <ul className="detail-list">
            {trash.data.map((item) => (
              <li key={`${item.objectType}-${item.objectId}`}>
                <strong>{item.title}</strong>
                <span>
                  {searchTypeLabel[item.objectType] ?? item.objectType} ·
                  endgültige Löschung am{" "}
                  {new Intl.DateTimeFormat("de-DE").format(
                    new Date(item.purgeAt),
                  )}
                </span>
                <button
                  className="secondary-action"
                  disabled={offline || restore.isPending}
                  onClick={() => restore.mutate(item)}
                  aria-label={`${item.title} wiederherstellen`}
                >
                  Wiederherstellen
                </button>
              </li>
            ))}
          </ul>
        ) : (
          !trash.isLoading && (
            <p className="empty-state">Der Papierkorb ist leer.</p>
          )
        )}
      </section>
      <div className="toolbar">
        <a
          className="secondary-action"
          href={`${exportBase}/schedule.csv?fromDate=${camp.startsOn}&toDateExclusive=${nextLocalDate(camp.endsOn)}`}
        >
          Zeitplan als CSV
        </a>
        <a className="secondary-action" href={`${exportBase}/meals.csv`}>
          Mahlzeiten als CSV
        </a>
        <a className="secondary-action" href={`${exportBase}/material.csv`}>
          Material als CSV
        </a>
        <a className="secondary-action" href={`${exportBase}/shopping.csv`}>
          Einkauf als CSV
        </a>
      </div>
    </>
  );
}

export const searchTypeLabel: Record<string, string> = {
  Camp: "Camp",
  ScheduleEntry: "Zeitplan",
  Meal: "Mahlzeit",
  MaterialRequirement: "Material",
  ShoppingList: "Einkaufsliste",
  ShoppingItem: "Einkaufsposition",
  Devotion: "Andacht",
  Note: "Notiz",
  Attachment: "Datei",
};

export const searchMetadataFilters: Record<
  string,
  { value: string; label: string }[]
> = {
  ScheduleEntry: [
    { value: "status:Planned", label: "Status: geplant" },
    { value: "status:Confirmed", label: "Status: bestätigt" },
    { value: "status:Cancelled", label: "Status: abgesagt" },
  ],
  Meal: [
    { value: "linked:True", label: "Mit Zeitplan" },
    { value: "linked:False", label: "Ohne Zeitplan" },
  ],
  MaterialRequirement: [
    { value: "status:Open", label: "Status: offen" },
    { value: "status:Planned", label: "Status: geplant" },
    { value: "status:Procured", label: "Status: beschafft" },
    { value: "status:NotRequired", label: "Status: nicht benötigt" },
    { value: "linked:True", label: "Mit Zeitplan" },
    { value: "linked:False", label: "Ohne Zeitplan" },
  ],
  ShoppingList: [
    { value: "open:0", label: "Keine offenen Positionen" },
    { value: "checked:0", label: "Keine erledigten Positionen" },
  ],
  ShoppingItem: [
    { value: "checked:False", label: "Offen" },
    { value: "checked:True", label: "Erledigt" },
    { value: "source:Spontaneous", label: "Quelle: spontan" },
    { value: "source:Catering", label: "Quelle: Mahlzeit" },
    { value: "source:MaterialRequirement", label: "Quelle: Material" },
  ],
  Devotion: [
    { value: "hasSnapshot:True", label: "Mit Bibeltext-Snapshot" },
    { value: "hasSnapshot:False", label: "Ohne Bibeltext-Snapshot" },
    { value: "translation:Schlachter1951", label: "Schlachter 1951" },
    { value: "translation:Luther1912", label: "Luther 1912" },
    {
      value: "translation:ElberfelderUnrevised",
      label: "Elberfelder (unrevidiert)",
    },
    { value: "translation:Textbibel", label: "Textbibel" },
  ],
  Note: [
    { value: "pinned:True", label: "Angeheftet" },
    { value: "pinned:False", label: "Nicht angeheftet" },
  ],
  Attachment: [
    { value: "mediaType:Pdf", label: "Dateityp: PDF" },
    { value: "mediaType:Jpeg", label: "Dateityp: JPEG" },
    { value: "mediaType:Png", label: "Dateityp: PNG" },
    { value: "mediaType:WebP", label: "Dateityp: WebP" },
    { value: "ownerType:ScheduleEntry", label: "Zu Zeitplaneinträgen" },
    { value: "ownerType:Meal", label: "Zu Mahlzeiten" },
    { value: "ownerType:MaterialRequirement", label: "Zu Material" },
    { value: "ownerType:Devotion", label: "Zu Andachten" },
    { value: "ownerType:Note", label: "Zu Notizen" },
  ],
};

export const restoredObjectQueryScope: Record<string, string> = {
  ScheduleEntry: "schedule",
  Meal: "meals",
  MaterialRequirement: "material",
  ShoppingList: "shopping-lists",
  ShoppingItem: "shopping-lists",
  Devotion: "devotions",
  Note: "notes",
  Attachment: "files",
};
