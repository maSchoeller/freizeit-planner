import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import type {
  Devotion,
  MaterialRequirementSummary,
  Meal,
  NotebookNote,
  NoteLinkCandidate,
  NoteLinkReference,
  NoteSummary,
  RecipeSummary,
  ScheduleEntry,
  ShoppingListSummary,
} from "./types";
import { getJson, mutateCateringJson } from "./api";
import { useCampQuery, useCampRuntime } from "./runtime";
import { nextLocalDate } from "./schedule";
import { PageHeading, QueryState } from "./ui";
import { OwnerAttachmentsPanel } from "./AttachmentsPanel";

export const noteLinkTypeLabels: Record<number, string> = {
  0: "Tagesplan",
  1: "Mahlzeit",
  2: "Rezept",
  3: "Material",
  4: "Einkaufsliste",
  5: "Andacht",
};

export function noteLinkKey(link: NoteLinkReference) {
  return `${link.type}:${link.targetId}`;
}

export function NoteLinkFields({
  candidates,
  selected,
  loading,
  error,
  onChange,
}: {
  candidates: NoteLinkCandidate[];
  selected: NoteLinkReference[];
  loading: boolean;
  error: Error | null;
  onChange: (links: NoteLinkReference[]) => void;
}) {
  const selectedKeys = new Set(selected.map(noteLinkKey));
  return (
    <fieldset className="responsibility-selector note-link-selector">
      <legend>Planungsobjekte verknüpfen</legend>
      <p className="form-hint">
        Optional. Die Verknüpfung verweist auf das gewählte Planungsobjekt,
        übernimmt aber keine Berechtigungen.
      </p>
      <QueryState loading={loading} error={error} />
      {!loading && !error && candidates.length === 0 ? (
        <p className="form-hint">Keine Planungsobjekte verfügbar.</p>
      ) : null}
      {candidates.map((candidate) => {
        const key = noteLinkKey(candidate);
        const label = noteLinkTypeLabels[candidate.type] ?? "Planung";
        return (
          <label className="checkbox-label" key={key}>
            <input
              type="checkbox"
              checked={selectedKeys.has(key)}
              onChange={(event) =>
                onChange(
                  event.target.checked
                    ? [
                        ...selected,
                        {
                          type: candidate.type,
                          targetId: candidate.targetId,
                        },
                      ]
                    : selected.filter((link) => noteLinkKey(link) !== key),
                )
              }
            />
            {label}: {candidate.targetTitle}
          </label>
        );
      })}
    </fieldset>
  );
}
export function NotesPage({ offline }: { offline: boolean }) {
  const { organizationId, campId, camp } = useCampRuntime();
  const queryClient = useQueryClient();
  const path = `/api/v1/organizations/${organizationId}/camps/${campId}/notes`;
  const query = useCampQuery<NoteSummary[]>("notes", path);
  const [creating, setCreating] = useState(false);
  const [selectedNoteId, setSelectedNoteId] = useState<string | null>(null);
  const [title, setTitle] = useState("");
  const [markdown, setMarkdown] = useState("");
  const [tagInput, setTagInput] = useState("");
  const [isPinned, setIsPinned] = useState(false);
  const [selectedLinks, setSelectedLinks] = useState<NoteLinkReference[]>([]);
  const [searchText, setSearchText] = useState("");
  const [notice, setNotice] = useState("");
  const [editing, setEditing] = useState(false);
  const [editTitle, setEditTitle] = useState("");
  const [editMarkdown, setEditMarkdown] = useState("");
  const [editTags, setEditTags] = useState("");
  const [editPinned, setEditPinned] = useState(false);
  const [editLinks, setEditLinks] = useState<NoteLinkReference[]>([]);
  const [confirmTrash, setConfirmTrash] = useState(false);
  const [trashConfirmed, setTrashConfirmed] = useState(false);
  const linkSelectionOpen = creating || editing;
  const scheduleCandidates = useQuery({
    queryKey: [organizationId, campId, "schedule"],
    queryFn: () =>
      getJson<ScheduleEntry[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/schedule?fromDate=${camp.startsOn}&toDateExclusive=${nextLocalDate(camp.endsOn)}`,
      ),
    enabled: linkSelectionOpen,
    retry: false,
  });
  const mealCandidates = useQuery({
    queryKey: [organizationId, campId, "meals"],
    queryFn: () =>
      getJson<Meal[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals`,
      ),
    enabled: linkSelectionOpen,
    retry: false,
  });
  const recipeCandidates = useQuery({
    queryKey: [organizationId, "catering", "recipes"],
    queryFn: () =>
      getJson<RecipeSummary[]>(
        `/api/v1/organizations/${organizationId}/catering/recipes`,
      ),
    enabled: linkSelectionOpen,
    retry: false,
  });
  const materialCandidates = useQuery({
    queryKey: [organizationId, campId, "material"],
    queryFn: () =>
      getJson<MaterialRequirementSummary[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/logistics/material`,
      ),
    enabled: linkSelectionOpen,
    retry: false,
  });
  const shoppingCandidates = useQuery({
    queryKey: [organizationId, campId, "shopping-lists"],
    queryFn: () =>
      getJson<ShoppingListSummary[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/logistics/shopping-lists`,
      ),
    enabled: linkSelectionOpen,
    retry: false,
  });
  const devotionCandidates = useQuery({
    queryKey: [organizationId, campId, "devotions"],
    queryFn: () =>
      getJson<Devotion[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/devotions`,
      ),
    enabled: linkSelectionOpen,
    retry: false,
  });
  const linkCandidates: NoteLinkCandidate[] = [
    ...(scheduleCandidates.data ?? []).map((entry) => ({
      type: 0,
      targetId: entry.id,
      targetTitle: entry.title,
    })),
    ...(mealCandidates.data ?? []).map((meal) => ({
      type: 1,
      targetId: meal.id,
      targetTitle: meal.name,
    })),
    ...(recipeCandidates.data ?? []).map((recipe) => ({
      type: 2,
      targetId: recipe.id,
      targetTitle: recipe.name,
    })),
    ...(materialCandidates.data ?? []).map((material) => ({
      type: 3,
      targetId: material.id,
      targetTitle: material.name,
    })),
    ...(shoppingCandidates.data ?? []).map((list) => ({
      type: 4,
      targetId: list.id,
      targetTitle: list.name,
    })),
    ...(devotionCandidates.data ?? []).map((devotion) => ({
      type: 5,
      targetId: devotion.id,
      targetTitle: devotion.topic,
    })),
  ];
  const linkCandidatesLoading =
    linkSelectionOpen &&
    [
      scheduleCandidates,
      mealCandidates,
      recipeCandidates,
      materialCandidates,
      shoppingCandidates,
      devotionCandidates,
    ].some((candidateQuery) => candidateQuery.isLoading);
  const linkCandidatesError =
    scheduleCandidates.error ??
    mealCandidates.error ??
    recipeCandidates.error ??
    materialCandidates.error ??
    shoppingCandidates.error ??
    devotionCandidates.error;
  const detail = useQuery({
    queryKey: [organizationId, campId, "note", selectedNoteId],
    queryFn: () => getJson<NotebookNote>(`${path}/${selectedNoteId}`),
    enabled: selectedNoteId !== null,
    retry: false,
    staleTime: Number.POSITIVE_INFINITY,
  });
  const createNote = useMutation({
    mutationFn: () =>
      mutateCateringJson<NotebookNote>(path, "POST", {
        title,
        markdown,
        tags: tagInput
          .split(",")
          .map((tag) => tag.trim())
          .filter(Boolean),
        isPinned,
        links: selectedLinks,
      }),
    onSuccess: (created) => {
      const excerpt = created.markdown
        .replace(/[#*_`[\]()]/g, " ")
        .replace(/\s+/g, " ")
        .trim()
        .slice(0, 180);
      queryClient.setQueryData<NoteSummary[]>(
        [organizationId, campId, "notes"],
        (current) => [
          {
            id: created.id,
            title: created.title,
            plainTextExcerpt: excerpt,
            tags: created.tags,
            isPinned: created.isPinned,
            linkCount: created.links.length,
            state: created.state,
            updatedAt: created.updatedAt,
            trashedAt: created.trashedAt,
            purgeAfter: created.purgeAfter,
            version: created.version,
          },
          ...(current ?? []),
        ],
      );
      queryClient.setQueryData(
        [organizationId, campId, "note", created.id],
        created,
      );
      setSelectedNoteId(created.id);
      setCreating(false);
      setSelectedLinks([]);
      setNotice(`${created.title} wurde angelegt.`);
    },
  });
  const reviseNote = useMutation({
    mutationFn: () => {
      const current = detail.data;
      if (!current) throw new Error("Die Notiz ist noch nicht geladen.");
      return mutateCateringJson<NotebookNote>(
        `${path}/${current.id}`,
        "PUT",
        {
          title: editTitle,
          markdown: editMarkdown,
          tags: editTags
            .split(",")
            .map((tag) => tag.trim())
            .filter(Boolean),
          isPinned: editPinned,
          links: editLinks,
        },
        current.version,
        "Die Notiz wurde zwischenzeitlich geändert. Schließe die Bearbeitung und öffne sie erneut.",
      );
    },
    onSuccess: (revised) => {
      queryClient.setQueryData(
        [organizationId, campId, "note", revised.id],
        revised,
      );
      queryClient.setQueryData<NoteSummary[]>(
        [organizationId, campId, "notes"],
        (current) =>
          current?.map((note) =>
            note.id === revised.id
              ? {
                  ...note,
                  title: revised.title,
                  plainTextExcerpt: revised.markdown
                    .replace(/[#*_`[\]()]/g, " ")
                    .replace(/\s+/g, " ")
                    .trim()
                    .slice(0, 180),
                  tags: revised.tags,
                  isPinned: revised.isPinned,
                  linkCount: revised.links.length,
                  updatedAt: revised.updatedAt,
                  version: revised.version,
                }
              : note,
          ),
      );
      setEditing(false);
      setNotice(`${revised.title} wurde gespeichert.`);
    },
  });
  const trashNote = useMutation({
    mutationFn: () => {
      const current = detail.data;
      if (!current) throw new Error("Die Notiz ist noch nicht geladen.");
      return mutateCateringJson<NotebookNote>(
        `${path}/${current.id}`,
        "DELETE",
        {},
        current.version,
        "Die Notiz wurde zwischenzeitlich geändert. Öffne den aktuellen Stand erneut.",
      );
    },
    onSuccess: (trashed) => {
      queryClient.setQueryData<NoteSummary[]>(
        [organizationId, campId, "notes"],
        (current) => current?.filter((note) => note.id !== trashed.id),
      );
      queryClient.removeQueries({
        queryKey: [organizationId, campId, "note", trashed.id],
      });
      setSelectedNoteId(null);
      setConfirmTrash(false);
      setTrashConfirmed(false);
      setNotice(`${trashed.title} wurde in den Papierkorb verschoben.`);
    },
  });
  const appendMarkdown = (value: string) => {
    setMarkdown((current) => `${current}${current ? "\n" : ""}${value}`);
  };
  const normalizedSearch = searchText.trim().toLocaleLowerCase("de-DE");
  const visibleNotes = query.data?.filter(
    (note) =>
      !normalizedSearch ||
      note.title.toLocaleLowerCase("de-DE").includes(normalizedSearch) ||
      note.plainTextExcerpt
        .toLocaleLowerCase("de-DE")
        .includes(normalizedSearch) ||
      note.tags.some((tag) =>
        tag.toLocaleLowerCase("de-DE").includes(normalizedSearch),
      ),
  );
  return (
    <>
      <PageHeading eyebrow="Gemeinsam festhalten" title="Notizbuch">
        <p>
          Notizen sind für das gesamte zugewiesene Team sichtbar. Roh-HTML,
          Tabellen und eingebettete Bilder sind gesperrt.
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
            setSelectedLinks([]);
            setNotice("");
          }}
        >
          Notiz anlegen
        </button>
        <label className="search-field">
          Notizen durchsuchen
          <input
            type="search"
            value={searchText}
            onChange={(event) => setSearchText(event.target.value)}
          />
        </label>
      </div>
      {creating ? (
        <form
          className="schedule-create-form note-form"
          aria-label="Notiz anlegen"
          onSubmit={(event) => {
            event.preventDefault();
            setNotice("");
            createNote.mutate();
          }}
        >
          <h2>Gemeinsame Notiz anlegen</h2>
          <p className="form-hint">
            Die Notiz ist für das gesamte zugewiesene Camp-Team sichtbar.
            Roh-HTML, Tabellen und eingebettete Bilder werden nicht akzeptiert.
          </p>
          <label>
            Titel
            <input
              required
              value={title}
              onChange={(event) => setTitle(event.target.value)}
            />
          </label>
          <div className="markdown-toolbar" aria-label="Markdown-Werkzeuge">
            <button
              type="button"
              className="secondary-action"
              onClick={() => appendMarkdown("## Überschrift")}
            >
              Überschrift einfügen
            </button>
            <button
              type="button"
              className="secondary-action"
              onClick={() => appendMarkdown("**fetter Text**")}
            >
              Fett einfügen
            </button>
            <button
              type="button"
              className="secondary-action"
              onClick={() => appendMarkdown("*kursiver Text*")}
            >
              Kursiv einfügen
            </button>
            <button
              type="button"
              className="secondary-action"
              onClick={() => appendMarkdown("- Listeneintrag")}
            >
              Liste einfügen
            </button>
            <button
              type="button"
              className="secondary-action"
              onClick={() => appendMarkdown("[Linktext](https://example.org)")}
            >
              Link einfügen
            </button>
          </div>
          <label>
            Markdown-Inhalt
            <textarea
              required
              value={markdown}
              onChange={(event) => setMarkdown(event.target.value)}
            />
          </label>
          <label>
            Tags
            <input
              value={tagInput}
              onChange={(event) => setTagInput(event.target.value)}
              placeholder="Team, Ablauf"
            />
          </label>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={isPinned}
              onChange={(event) => setIsPinned(event.target.checked)}
            />
            Notiz anheften
          </label>
          <NoteLinkFields
            candidates={linkCandidates}
            selected={selectedLinks}
            loading={linkCandidatesLoading}
            error={linkCandidatesError}
            onChange={setSelectedLinks}
          />
          {createNote.error ? (
            <p role="alert" className="error-message">
              {createNote.error.message}
            </p>
          ) : null}
          <div className="toolbar">
            <button
              type="submit"
              className="primary-action"
              disabled={createNote.isPending}
            >
              Notiz speichern
            </button>
            <button
              type="button"
              className="secondary-action"
              disabled={createNote.isPending}
              onClick={() => setCreating(false)}
            >
              Abbrechen
            </button>
          </div>
        </form>
      ) : null}
      <div className="card-grid">
        {visibleNotes?.map((note) => (
          <article className="card" key={note.id}>
            <p className="eyebrow">
              {note.isPinned ? "Angeheftet · " : ""}
              {note.tags.join(" · ") || "Ohne Tags"}
            </p>
            <h2>{note.title}</h2>
            <p>{note.plainTextExcerpt}</p>
            <button
              type="button"
              className="secondary-action"
              aria-label={`${note.title} öffnen`}
              onClick={() => {
                setSelectedNoteId(note.id);
                setEditing(false);
                setConfirmTrash(false);
                setTrashConfirmed(false);
                setNotice("");
              }}
            >
              Notiz öffnen
            </button>
          </article>
        ))}
      </div>
      {selectedNoteId ? (
        <section
          className="settings-section note-detail"
          aria-label="Geöffnete Notiz"
        >
          <QueryState loading={detail.isLoading} error={detail.error} />
          {detail.data ? (
            <>
              <div className="section-heading">
                <div>
                  <p className="eyebrow">
                    <span>
                      {detail.data.isPinned ? "Angeheftet" : "Gemeinsame Notiz"}
                    </span>{" "}
                    <span>{detail.data.tags.join(" · ") || "Ohne Tags"}</span>
                  </p>
                  <h2>{detail.data.title}</h2>
                </div>
                <div className="toolbar compact-toolbar">
                  {!offline ? (
                    <button
                      type="button"
                      className="secondary-action"
                      onClick={() => {
                        const current = detail.data;
                        setEditTitle(current.title);
                        setEditMarkdown(current.markdown);
                        setEditTags(current.tags.join(", "));
                        setEditPinned(current.isPinned);
                        setEditLinks(
                          current.links.map(({ type, targetId }) => ({
                            type,
                            targetId,
                          })),
                        );
                        setEditing(true);
                        setNotice("");
                      }}
                    >
                      Notiz bearbeiten
                    </button>
                  ) : null}
                  <button
                    type="button"
                    className="secondary-action"
                    onClick={() => {
                      setSelectedNoteId(null);
                      setEditing(false);
                      setConfirmTrash(false);
                    }}
                  >
                    Notiz schließen
                  </button>
                </div>
              </div>
              {editing ? (
                <form
                  className="schedule-create-form note-edit-form"
                  aria-label={`${detail.data.title} bearbeiten`}
                  onSubmit={(event) => {
                    event.preventDefault();
                    setNotice("");
                    reviseNote.mutate();
                  }}
                >
                  <label>
                    Titel bearbeiten
                    <input
                      required
                      value={editTitle}
                      onChange={(event) => setEditTitle(event.target.value)}
                    />
                  </label>
                  <label>
                    Markdown-Inhalt bearbeiten
                    <textarea
                      required
                      value={editMarkdown}
                      onChange={(event) => setEditMarkdown(event.target.value)}
                    />
                  </label>
                  <label>
                    Tags bearbeiten
                    <input
                      value={editTags}
                      onChange={(event) => setEditTags(event.target.value)}
                    />
                  </label>
                  <label className="checkbox-label">
                    <input
                      type="checkbox"
                      checked={editPinned}
                      onChange={(event) => setEditPinned(event.target.checked)}
                    />
                    Notiz anheften
                  </label>
                  <NoteLinkFields
                    candidates={linkCandidates}
                    selected={editLinks}
                    loading={linkCandidatesLoading}
                    error={linkCandidatesError}
                    onChange={setEditLinks}
                  />
                  {reviseNote.error ? (
                    <p role="alert" className="error-message">
                      {reviseNote.error.message}
                    </p>
                  ) : null}
                  <div className="toolbar">
                    <button
                      type="submit"
                      className="primary-action"
                      disabled={reviseNote.isPending}
                    >
                      Notizänderung speichern
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      disabled={reviseNote.isPending}
                      onClick={() => setEditing(false)}
                    >
                      Abbrechen
                    </button>
                  </div>
                </form>
              ) : null}
              <div
                className="rendered-markdown"
                dangerouslySetInnerHTML={{ __html: detail.data.renderedHtml }}
              />
              {detail.data.links.length > 0 ? (
                <section
                  className="linked-planning-objects"
                  aria-label="Verknüpfte Planungsobjekte"
                >
                  <h3>Verknüpfte Planung</h3>
                  <ul>
                    {detail.data.links.map((link) => (
                      <li key={noteLinkKey(link)}>
                        {noteLinkTypeLabels[link.type] ?? "Planung"} ·{" "}
                        {link.targetTitle}
                      </li>
                    ))}
                  </ul>
                </section>
              ) : null}
              <OwnerAttachmentsPanel
                organizationId={organizationId}
                campId={campId}
                ownerType="Note"
                ownerId={detail.data.id}
                ownerName={detail.data.title}
                ownerNoun="die Notiz"
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
                  Notiz in Papierkorb verschieben
                </button>
              ) : null}
              {confirmTrash ? (
                <section
                  className="delete-confirmation"
                  aria-label="Notiz in Papierkorb verschieben"
                >
                  <h3>{detail.data.title} wirklich verschieben?</h3>
                  <p>
                    Die Notiz bleibt 30 Tage im Camp-Papierkorb und kann dort
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
                    Ich möchte diese Notiz in den Papierkorb verschieben.
                  </label>
                  {trashNote.error ? (
                    <p role="alert" className="error-message">
                      {trashNote.error.message}
                    </p>
                  ) : null}
                  <div className="toolbar">
                    <button
                      type="button"
                      className="danger-action"
                      disabled={!trashConfirmed || trashNote.isPending}
                      onClick={() => trashNote.mutate()}
                    >
                      Verschieben bestätigen
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      disabled={trashNote.isPending}
                      onClick={() => setConfirmTrash(false)}
                    >
                      Abbrechen
                    </button>
                  </div>
                </section>
              ) : null}
            </>
          ) : null}
        </section>
      ) : null}
    </>
  );
}
