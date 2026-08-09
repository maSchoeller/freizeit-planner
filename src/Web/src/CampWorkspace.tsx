import FullCalendar from "@fullcalendar/react";
import dayGridPlugin from "@fullcalendar/daygrid";
import interactionPlugin from "@fullcalendar/interaction";
import timeGridPlugin from "@fullcalendar/timegrid";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  CalendarDays,
  ChefHat,
  Church,
  ClipboardList,
  FileText,
  NotebookPen,
  Search,
  ShoppingCart,
} from "lucide-react";
import { useEffect, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { Link, NavLink, Route, Routes, useLocation } from "react-router-dom";
import {
  clearOfflineSnapshot,
  loadOfflineSnapshot,
  saveOfflineSnapshot,
} from "./offlineSnapshot";
import { getAntiforgeryToken } from "./api/security";

const organizationId = "20000000-0000-0000-0000-000000000001";
const campId = "30000000-0000-0000-0000-000000000001";
const campBase = "/o/sonnenhoehe/camps/sommerfreizeit-2026";

const navigation = [
  { to: "", label: "Übersicht", icon: ClipboardList, end: true },
  { to: "tagesplan", label: "Tagesplan", icon: CalendarDays },
  { to: "essen", label: "Essen & Rezepte", icon: ChefHat },
  { to: "logistik", label: "Material & Einkauf", icon: ShoppingCart },
  { to: "andachten", label: "Andachten", icon: Church },
  { to: "notizen", label: "Notizbuch", icon: NotebookPen },
  { to: "dateien", label: "Dateien", icon: FileText },
  { to: "suche", label: "Suche & Papierkorb", icon: Search },
];

type ScheduleEntry = {
  id: string;
  title: string;
  location?: string;
  category: string;
  overlapsAnotherEntry: boolean;
  timing: {
    isAllDay: boolean;
    startsAtUtc?: string;
    endsAtUtc?: string;
    startDate?: string;
    endDateExclusive?: string;
  };
  version: number;
};

type Meal = {
  id: string;
  name: string;
  effectivePortions: number;
  recipeCount: number;
};
type Note = {
  id: string;
  title: string;
  plainTextExcerpt: string;
  tags: string[];
  isPinned: boolean;
};
type Devotion = {
  id: string;
  topic: string;
  bibleReference: string;
  hasBibleSnapshot: boolean;
};
type ActivityEvent = {
  id: string;
  actorId: string;
  kind: 0 | 1 | 2 | 3;
  objectType: string;
  title: string;
  timestamp: string;
};
type SearchResult = {
  objectType: string;
  objectId: string;
  title: string;
  metadata: Record<string, string>;
  updatedAt: string;
  version: number;
};
type CampTrashItem = {
  objectType:
    | "Note"
    | "Devotion"
    | "Attachment"
    | "MaterialRequirement"
    | "ShoppingList"
    | "ShoppingItem"
    | "ScheduleEntry"
    | "Meal";
  objectId: string;
  title: string;
  deletedAt: string;
  purgeAt: string;
  version: number;
  restorePath: string;
};

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, { credentials: "same-origin" });
  if (!response.ok)
    throw new Error(
      response.status === 401
        ? "Bitte melde dich erneut an."
        : "Daten konnten nicht geladen werden.",
    );
  return (await response.json()) as T;
}

function useCampQuery<T>(key: string, path: string) {
  return useQuery({
    queryKey: [organizationId, campId, key],
    queryFn: () => getJson<T>(path),
    retry: false,
  });
}

export function CampWorkspace() {
  const location = useLocation();
  const [offline, setOffline] = useState(!navigator.onLine);
  useEffect(() => {
    const online = () => setOffline(false);
    const offlineHandler = () => setOffline(true);
    window.addEventListener("online", online);
    window.addEventListener("offline", offlineHandler);
    return () => {
      window.removeEventListener("online", online);
      window.removeEventListener("offline", offlineHandler);
    };
  }, []);
  useEffect(() => {
    if (!location.pathname.startsWith(campBase)) clearOfflineSnapshot();
  }, [location.pathname]);

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main">
        Zum Inhalt springen
      </a>
      <header className="topbar">
        <Link
          className="brand"
          to={campBase}
          aria-label="Freizeit-Cockpit Startseite"
        >
          <span className="brand-mark" aria-hidden="true">
            F
          </span>
          <span>Freizeit-Cockpit</span>
        </Link>
        <div className="topbar-actions">
          <span
            className={offline ? "connection offline" : "connection"}
            role="status"
          >
            {offline ? "Offline · nur gespeicherter Stand" : "Online"}
          </span>
          <Link
            className="profile-button"
            aria-label="Kontomenü von Miriam öffnen"
            to="/konto"
          >
            MK
          </Link>
        </div>
      </header>
      <div className="workspace">
        <aside className="sidebar" aria-label="Camp-Navigation">
          <p className="eyebrow">Sonnenhöhe e. V.</p>
          <p className="camp-name">Sommerfreizeit 2026</p>
          <nav aria-label="Camp-Navigation">
            <ul>
              {navigation.map(({ to, label, icon: Icon, end }) => (
                <li key={label}>
                  <NavLink to={to ? `${campBase}/${to}` : campBase} end={end}>
                    <Icon aria-hidden="true" size={20} />
                    <span>{label}</span>
                  </NavLink>
                </li>
              ))}
            </ul>
          </nav>
          <a className="help-link" href="/hilfe/">
            Hilfe & Anleitung
          </a>
        </aside>
        <main id="main" tabIndex={-1}>
          <Routes>
            <Route index element={<OverviewPage />} />
            <Route
              path="tagesplan"
              element={<SchedulePage offline={offline} />}
            />
            <Route path="essen" element={<MealsPage offline={offline} />} />
            <Route
              path="logistik"
              element={<LogisticsPage offline={offline} />}
            />
            <Route
              path="andachten"
              element={<DevotionsPage offline={offline} />}
            />
            <Route path="notizen" element={<NotesPage offline={offline} />} />
            <Route path="dateien" element={<FilesPage offline={offline} />} />
            <Route
              path="suche"
              element={<SearchTrashPage offline={offline} />}
            />
          </Routes>
        </main>
      </div>
    </div>
  );
}

function PageHeading({
  eyebrow,
  title,
  children,
}: {
  eyebrow: string;
  title: string;
  children?: ReactNode;
}) {
  return (
    <div className="page-heading">
      <div>
        <p className="eyebrow">{eyebrow}</p>
        <h1>{title}</h1>
        {children}
      </div>
    </div>
  );
}

function QueryState({
  loading,
  error,
}: {
  loading: boolean;
  error: Error | null;
}) {
  if (loading)
    return (
      <p role="status" className="notice">
        Daten werden geladen …
      </p>
    );
  if (error)
    return (
      <p role="alert" className="error-message">
        {error.message}
      </p>
    );
  return null;
}

function OverviewPage() {
  const activity = useCampQuery<ActivityEvent[]>(
    "activity",
    `/api/v1/organizations/${organizationId}/camps/${campId}/activity?limit=5`,
  );
  return (
    <>
      <PageHeading eyebrow="Dienstag, 4. August" title="Guten Morgen, Miriam">
        <p>Hier siehst du, was heute für euer Team wichtig ist.</p>
      </PageHeading>
      <section aria-labelledby="today-heading">
        <div className="section-heading">
          <h2 id="today-heading">Heute im Tagesplan</h2>
          <Link to="tagesplan">Ganzen Plan öffnen</Link>
        </div>
        <ol className="timeline">
          <li>
            <time dateTime="2026-08-04T08:00">08:00</time>
            <div>
              <strong>Frühstück</strong>
              <span>Speisesaal · Küchenteam</span>
            </div>
            <span className="status">Geplant</span>
          </li>
          <li>
            <time dateTime="2026-08-04T09:30">09:30</time>
            <div>
              <strong>Geländespiel im Wald</strong>
              <span>Treffpunkt Haupthaus · Miriam, Jonas</span>
            </div>
            <span className="status info">Parallel</span>
          </li>
          <li>
            <time dateTime="2026-08-04T19:30">19:30</time>
            <div>
              <strong>Abendandacht</strong>
              <span>Feuerstelle · Samuel</span>
            </div>
            <span className="status">Vorbereitet</span>
          </li>
        </ol>
      </section>
      <div className="dashboard-grid">
        <SummaryCard
          title="Meine Verantwortungen"
          value="4"
          text="offene Punkte"
        />
        <SummaryCard title="Beschaffung" value="12" text="noch einzukaufen" />
        <section className="card activity-card">
          <h2>Jüngste Aktivitäten</h2>
          <QueryState loading={activity.isLoading} error={activity.error} />
          {activity.data?.length ? (
            <ul>
              {activity.data.map((event) => (
                <li key={event.id}>
                  <span>
                    {activityKindLabel[event.kind]}: „{event.title}“
                  </span>
                  <time dateTime={event.timestamp}>
                    {new Intl.DateTimeFormat("de-DE", {
                      dateStyle: "short",
                      timeStyle: "short",
                    }).format(new Date(event.timestamp))}
                  </time>
                </li>
              ))}
            </ul>
          ) : (
            !activity.isLoading && (
              <p className="empty-state">Noch keine Aktivität vorhanden.</p>
            )
          )}
        </section>
      </div>
    </>
  );
}

const activityKindLabel: Record<ActivityEvent["kind"], string> = {
  0: "Erstellt",
  1: "Geändert",
  2: "In den Papierkorb verschoben",
  3: "Wiederhergestellt",
};

function SummaryCard({
  title,
  value,
  text,
}: {
  title: string;
  value: string;
  text: string;
}) {
  return (
    <section className="card">
      <h2>{title}</h2>
      <p className="metric">
        {value} <span>{text}</span>
      </p>
    </section>
  );
}

function SchedulePage({ offline }: { offline: boolean }) {
  const path = `/api/v1/organizations/${organizationId}/camps/${campId}/schedule?fromDate=2026-08-01&toDateExclusive=2026-08-09`;
  const query = useCampQuery<ScheduleEntry[]>("schedule", path);
  const queryClient = useQueryClient();
  const [deleteCandidate, setDeleteCandidate] = useState<ScheduleEntry | null>(
    null,
  );
  const [linkedBehavior, setLinkedBehavior] = useState<
    "" | "Unlink" | "MoveLinkedToTrash"
  >("");
  const [deleteStatus, setDeleteStatus] = useState("");
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
  const stored = (loadOfflineSnapshot()?.schedule ?? []) as ScheduleEntry[];
  const entries = query.data ?? (offline ? stored : []);
  useEffect(() => {
    if (query.data) saveOfflineSnapshot({ schedule: query.data });
  }, [query.data]);
  const events = entries.map((entry) => ({
    id: entry.id,
    title: entry.title,
    start: entry.timing.startsAtUtc ?? entry.timing.startDate,
    end: entry.timing.endsAtUtc ?? entry.timing.endDateExclusive,
    allDay: entry.timing.isAllDay,
  }));
  return (
    <>
      <PageHeading eyebrow="Planung" title="Tages- und Wochenplan">
        <p>
          Überlappungen sind erlaubt und werden informativ markiert. Alle Zeiten
          gelten für Europe/Berlin.
        </p>
      </PageHeading>
      <QueryState
        loading={query.isLoading && !offline}
        error={query.error ?? remove.error}
      />
      <p
        className="visually-hidden"
        role="status"
        aria-label="Löschstatus"
        aria-live="polite"
      >
        {deleteStatus}
      </p>
      <section className="calendar-card" aria-label="Kalenderansicht">
        <FullCalendar
          plugins={[timeGridPlugin, dayGridPlugin, interactionPlugin]}
          initialView="timeGridWeek"
          locale="de"
          firstDay={1}
          allDayText="Ganztägig"
          height="auto"
          events={events}
          editable={!offline}
        />
      </section>
      <section className="settings-section" aria-labelledby="agenda-title">
        <div className="section-heading">
          <h2 id="agenda-title">Barrierearme Agenda</h2>
          <button className="primary-action" disabled={offline}>
            Eintrag erstellen
          </button>
        </div>
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
                </div>
                {entry.overlapsAnotherEntry && (
                  <span className="status info">Überschneidung</span>
                )}
                <button className="secondary-action" disabled={offline}>
                  Bearbeiten
                </button>
                <button
                  className="danger-action"
                  disabled={offline || remove.isPending}
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

function MealsPage({ offline }: { offline: boolean }) {
  const path = `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals`;
  const query = useCampQuery<Meal[]>("meals", path);
  const meals =
    query.data ??
    (offline ? ((loadOfflineSnapshot()?.meals ?? []) as Meal[]) : []);
  useEffect(() => {
    if (query.data) saveOfflineSnapshot({ meals: query.data });
  }, [query.data]);
  return (
    <>
      <PageHeading eyebrow="Küche" title="Essen & Rezepte">
        <p>
          Mengen werden dezimal und nur innerhalb kompatibler Einheiten
          skaliert. Allergenhinweise sind keine medizinische Garantie.
        </p>
      </PageHeading>
      <QueryState loading={query.isLoading && !offline} error={query.error} />
      <div className="toolbar">
        <button className="primary-action" disabled={offline}>
          Mahlzeit planen
        </button>
        <button className="secondary-action" disabled={offline}>
          Rezept anlegen
        </button>
        <label className="search-field">
          Rezepte und Zutaten suchen
          <input type="search" placeholder="z. B. Tomaten" />
        </label>
      </div>
      <div className="card-grid">
        {meals.map((meal) => (
          <article className="card" key={meal.id}>
            <p className="eyebrow">{meal.effectivePortions} Portionen</p>
            <h2>{meal.name}</h2>
            <p>
              {meal.recipeCount} Rezept-Snapshots · Änderungen an
              Bibliotheksrezepten werden nicht still übernommen.
            </p>
            <button className="secondary-action" disabled={offline}>
              Öffnen
            </button>
          </article>
        ))}
        {meals.length === 0 && (
          <p className="empty-state">Noch keine Mahlzeit geplant.</p>
        )}
      </div>
    </>
  );
}

function LogisticsPage({ offline }: { offline: boolean }) {
  const [checked, setChecked] = useState(() => new Set<string>());
  const items = [
    "12 kg Kartoffeln",
    "6 Kisten Mineralwasser",
    "20 Holzlatten",
    "4 Rollen Gewebeband",
  ];
  useEffect(() => {
    const refresh = () => {
      /* focus-triggered polling refreshes server data in the connected view */
    };
    window.addEventListener("focus", refresh);
    const timer = window.setInterval(refresh, 15_000);
    return () => {
      window.removeEventListener("focus", refresh);
      window.clearInterval(timer);
    };
  }, []);
  return (
    <>
      <PageHeading eyebrow="Logistik" title="Material & Einkaufslisten">
        <p>
          Lebensmittel, Material und spontane Positionen stehen in gemeinsamen,
          nachvollziehbaren Listen.
        </p>
      </PageHeading>
      <div className="split-view">
        <section className="settings-section">
          <div className="section-heading">
            <h2>Materialbedarf</h2>
            <button className="primary-action" disabled={offline}>
              Material hinzufügen
            </button>
          </div>
          <ul className="detail-list">
            <li>
              <strong>Beamer und Leinwand</strong>
              <span>Andachtsraum · Jonas · Vorhanden</span>
            </li>
            <li>
              <strong>Holzlatten</strong>
              <span>Geländespiel · Miriam · Einkaufen</span>
            </li>
          </ul>
        </section>
        <section className="settings-section">
          <div className="section-heading">
            <h2>Einkauf „Großeinkauf Dienstag“</h2>
            <span className="status">{items.length - checked.size} offen</span>
          </div>
          <ul className="check-list">
            {items.map((item) => (
              <li key={item}>
                <label>
                  <input
                    type="checkbox"
                    checked={checked.has(item)}
                    disabled={offline}
                    onChange={() =>
                      setChecked((previous) => {
                        const next = new Set(previous);
                        if (next.has(item)) next.delete(item);
                        else next.add(item);
                        return next;
                      })
                    }
                  />
                  <span>{item}</span>
                </label>
                <small>
                  {checked.has(item)
                    ? "Abgehakt von Miriam · gerade eben"
                    : "Quelle nachvollziehbar"}
                </small>
              </li>
            ))}
          </ul>
        </section>
      </div>
    </>
  );
}

function DevotionsPage({ offline }: { offline: boolean }) {
  const path = `/api/v1/organizations/${organizationId}/camps/${campId}/devotions`;
  const query = useCampQuery<Devotion[]>("devotions", path);
  return (
    <>
      <PageHeading eyebrow="Geistliche Planung" title="Andachten">
        <p>
          Schlachter 1951 ist die Standardübersetzung. Gespeicherte Bibeltexte
          bleiben unveränderte Snapshots.
        </p>
      </PageHeading>
      <QueryState loading={query.isLoading} error={query.error} />
      <div className="toolbar">
        <button className="primary-action" disabled={offline}>
          Andacht entwerfen
        </button>
        <label>
          Übersetzung
          <select defaultValue="Schlachter1951" disabled={offline}>
            <option value="Schlachter1951">Schlachter 1951</option>
            <option value="Luther1912">Luther 1912</option>
            <option value="ElberfelderUnrevised">
              Unrevidierte Elberfelder
            </option>
            <option value="Textbibel">Textbibel</option>
          </select>
        </label>
      </div>
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
            <button className="secondary-action" disabled={offline}>
              Bearbeiten
            </button>
          </article>
        ))}
      </div>
    </>
  );
}

function NotesPage({ offline }: { offline: boolean }) {
  const path = `/api/v1/organizations/${organizationId}/camps/${campId}/notes`;
  const query = useCampQuery<Note[]>("notes", path);
  return (
    <>
      <PageHeading eyebrow="Gemeinsam festhalten" title="Notizbuch">
        <p>
          Notizen sind für das gesamte zugewiesene Team sichtbar. Roh-HTML,
          Tabellen und eingebettete Bilder sind gesperrt.
        </p>
      </PageHeading>
      <QueryState loading={query.isLoading} error={query.error} />
      <div className="toolbar">
        <button className="primary-action" disabled={offline}>
          Notiz anlegen
        </button>
        <label className="search-field">
          Notizen durchsuchen
          <input type="search" />
        </label>
      </div>
      <div className="card-grid">
        {query.data?.map((note) => (
          <article className="card" key={note.id}>
            <p className="eyebrow">
              {note.isPinned
                ? "Angeheftet"
                : note.tags.join(" · ") || "Ohne Tags"}
            </p>
            <h2>{note.title}</h2>
            <p>{note.plainTextExcerpt}</p>
            <button className="secondary-action" disabled={offline}>
              Öffnen
            </button>
          </article>
        ))}
      </div>
    </>
  );
}

function FilesPage({ offline }: { offline: boolean }) {
  const [name, setName] = useState("");
  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
  };
  return (
    <>
      <PageHeading eyebrow="Anhänge" title="Dateien">
        <p>
          Erlaubt sind PDF, JPEG, PNG und WebP bis zehn MiB. PDFs werden
          heruntergeladen, Bilder sicher angezeigt.
        </p>
      </PageHeading>
      <section className="settings-section">
        <h2>Datei hochladen</h2>
        <form onSubmit={onSubmit}>
          <label className="field">
            Datei
            <input
              type="file"
              accept="application/pdf,image/jpeg,image/png,image/webp"
              disabled={offline}
              onChange={(event) => setName(event.target.files?.[0]?.name ?? "")}
            />
          </label>
          <label className="field">
            Gehört zu
            <select disabled={offline}>
              <option>Zeitplaneintrag</option>
              <option>Mahlzeit oder Rezept</option>
              <option>Material</option>
              <option>Andacht</option>
              <option>Notiz</option>
            </select>
          </label>
          <button className="primary-action" disabled={offline || !name}>
            „{name || "Datei"}“ hochladen
          </button>
        </form>
        <p className="muted">
          Malware-Prüfung ist eine bewusste Produktgrenze der v1. Lade nur
          vertrauenswürdige Dateien hoch.
        </p>
      </section>
    </>
  );
}

function SearchTrashPage({ offline }: { offline: boolean }) {
  const [query, setQuery] = useState("");
  const [objectType, setObjectType] = useState("");
  const queryClient = useQueryClient();
  const normalizedQuery = query.trim();
  const search = useQuery({
    queryKey: [organizationId, campId, "search", normalizedQuery, objectType],
    queryFn: () =>
      getJson<SearchResult[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/search?query=${encodeURIComponent(normalizedQuery)}${objectType ? `&objectTypes=${encodeURIComponent(objectType)}` : ""}`,
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
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: [organizationId, campId, "trash"],
      });
    },
  });
  const exportBase = `/api/v1/organizations/${organizationId}/camps/${campId}/exports`;
  return (
    <>
      <PageHeading
        eyebrow="Finden und wiederherstellen"
        title="Suche & Papierkorb"
      >
        <p>
          Die Suche bleibt auf dieses Camp begrenzt. Gelöschte Inhalte werden
          nach 30 Tagen endgültig entfernt.
        </p>
      </PageHeading>
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
            onChange={(event) => setObjectType(event.target.value)}
          >
            <option value="">Alle Inhalte</option>
            <option value="ScheduleEntry">Zeitplan</option>
            <option value="Meal">Mahlzeiten</option>
            <option value="MaterialRequirement">Material</option>
            <option value="ShoppingList">Einkaufslisten</option>
            <option value="Devotion">Andachten</option>
            <option value="Note">Notizen</option>
            <option value="Attachment">Dateien</option>
          </select>
        </label>
      </div>
      <section className="settings-section">
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
      <section className="settings-section">
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
          href={`${exportBase}/schedule.csv?fromDate=2026-08-01&toDateExclusive=2026-08-09`}
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
        <button className="secondary-action" onClick={() => window.print()}>
          Druckansicht
        </button>
      </div>
    </>
  );
}

const searchTypeLabel: Record<string, string> = {
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
