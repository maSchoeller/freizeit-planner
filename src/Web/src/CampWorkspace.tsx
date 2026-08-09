import FullCalendar from "@fullcalendar/react";
import dayGridPlugin from "@fullcalendar/daygrid";
import interactionPlugin from "@fullcalendar/interaction";
import luxonPlugin from "@fullcalendar/luxon3";
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
  Settings,
  ShoppingCart,
} from "lucide-react";
import { createContext, useContext, useEffect, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import {
  Link,
  NavLink,
  Route,
  Routes,
  useLocation,
  useParams,
} from "react-router-dom";
import type { components } from "./api/schema";
import {
  clearOfflineSnapshot,
  loadOfflineSnapshot,
  saveOfflineSnapshot,
} from "./offlineSnapshot";
import { getAntiforgeryToken } from "./api/security";

type AccountMembership = components["schemas"]["AccountMembershipView"];
type Account = components["schemas"]["AccountView"];
type WorkspaceCamp = components["schemas"]["CampView"];

type CampRuntime = {
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  organizationRole: AccountMembership["role"];
  campId: string;
  campSlug: string;
  campBase: string;
  camp: WorkspaceCamp;
};

const CampRuntimeContext = createContext<CampRuntime | null>(null);

function useCampRuntime() {
  const runtime = useContext(CampRuntimeContext);
  if (!runtime) throw new Error("Camp-Kontext fehlt.");
  return runtime;
}

const navigation = [
  { to: "", label: "Übersicht", icon: ClipboardList, end: true },
  { to: "tagesplan", label: "Tagesplan", icon: CalendarDays },
  { to: "essen", label: "Essen & Rezepte", icon: ChefHat },
  { to: "logistik", label: "Material & Einkauf", icon: ShoppingCart },
  { to: "andachten", label: "Andachten", icon: Church },
  { to: "notizen", label: "Notizbuch", icon: NotebookPen },
  { to: "dateien", label: "Dateien", icon: FileText },
  { to: "suche", label: "Suche & Papierkorb", icon: Search },
  { to: "einstellungen", label: "Camp-Einstellungen", icon: Settings },
];

type ScheduleEntry = {
  id: string;
  title: string;
  description?: string;
  location?: string;
  category: string;
  status: number;
  responsibleUserIds: string[];
  audience?: string;
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

type ScheduleTimingBody = {
  isAllDay: boolean;
  localStart: string | null;
  localEnd: string | null;
  startDate: string | null;
  endDateExclusive: string | null;
  startChoice: number;
  endChoice: number;
};

type ScheduleEntryBody = {
  timing: ScheduleTimingBody;
  title: string;
  description: string | null;
  location: string | null;
  category: string;
  status: number;
  responsibleUserIds: string[];
  audience: string | null;
};

type ScheduleEditDraft = {
  isAllDay: boolean;
  startDate: string;
  endDate: string;
  startTime: string;
  endTime: string;
  title: string;
  description: string;
  location: string;
  category: string;
  status: string;
  audience: string;
  responsibleUserIds: string[];
};

type CampMemberSummary = { userId: string; displayName: string };

class ScheduleUpdateError extends Error {
  constructor(message: string) {
    super(message);
  }
}

function formatCampLocalDateTime(value: string | undefined, timeZone: string) {
  if (!value) return { date: "", time: "" };
  const parts = new Intl.DateTimeFormat("sv-SE", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(new Date(value));
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((item) => item.type === type)?.value ?? "";
  return {
    date: `${part("year")}-${part("month")}-${part("day")}`,
    time: `${part("hour")}:${part("minute")}`,
  };
}

function createScheduleEditDraft(
  entry: ScheduleEntry,
  timeZone: string,
): ScheduleEditDraft {
  const start = entry.timing.isAllDay
    ? { date: entry.timing.startDate ?? "", time: "" }
    : formatCampLocalDateTime(entry.timing.startsAtUtc, timeZone);
  const end = entry.timing.isAllDay
    ? { date: entry.timing.endDateExclusive ?? "", time: "" }
    : formatCampLocalDateTime(entry.timing.endsAtUtc, timeZone);
  return {
    isAllDay: entry.timing.isAllDay,
    startDate: start.date,
    endDate: end.date,
    startTime: start.time,
    endTime: end.time,
    title: entry.title,
    description: entry.description ?? "",
    location: entry.location ?? "",
    category: entry.category,
    status: String(entry.status ?? 0),
    audience: entry.audience ?? "",
    responsibleUserIds: entry.responsibleUserIds ?? [],
  };
}

function scheduleBodyFromDraft(
  entry: ScheduleEntry,
  draft: ScheduleEditDraft,
): ScheduleEntryBody {
  return {
    timing: draft.isAllDay
      ? {
          isAllDay: true,
          localStart: null,
          localEnd: null,
          startDate: draft.startDate,
          endDateExclusive: draft.endDate,
          startChoice: 0,
          endChoice: 0,
        }
      : {
          isAllDay: false,
          localStart: `${draft.startDate}T${draft.startTime}:00`,
          localEnd: `${draft.endDate}T${draft.endTime}:00`,
          startDate: null,
          endDateExclusive: null,
          startChoice: 0,
          endChoice: 0,
        },
    title: draft.title,
    description: draft.description || null,
    location: draft.location || null,
    category: draft.category,
    status: Number(draft.status),
    responsibleUserIds: draft.responsibleUserIds,
    audience: draft.audience || null,
  };
}

function optimisticEntryFromDraft(
  entry: ScheduleEntry,
  draft: ScheduleEditDraft,
): ScheduleEntry {
  return {
    ...entry,
    title: draft.title,
    description: draft.description || undefined,
    location: draft.location || undefined,
    category: draft.category,
    status: Number(draft.status),
    audience: draft.audience || undefined,
    responsibleUserIds: draft.responsibleUserIds,
    timing: draft.isAllDay
      ? {
          isAllDay: true,
          startDate: draft.startDate,
          endDateExclusive: draft.endDate,
        }
      : {
          isAllDay: false,
          startsAtUtc: `${draft.startDate}T${draft.startTime}:00`,
          endsAtUtc: `${draft.endDate}T${draft.endTime}:00`,
        },
  };
}

function localCalendarDateTime(value: string) {
  return value.slice(0, 19);
}

function nextLocalDate(value: string) {
  const date = new Date(`${value}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
}

function scheduleBodyFromCalendar(
  entry: ScheduleEntry,
  event: { allDay: boolean; startStr: string; endStr: string },
): ScheduleEntryBody {
  return {
    timing: event.allDay
      ? {
          isAllDay: true,
          localStart: null,
          localEnd: null,
          startDate: event.startStr.slice(0, 10),
          endDateExclusive: event.endStr.slice(0, 10),
          startChoice: 0,
          endChoice: 0,
        }
      : {
          isAllDay: false,
          localStart: localCalendarDateTime(event.startStr),
          localEnd: localCalendarDateTime(event.endStr),
          startDate: null,
          endDateExclusive: null,
          startChoice: 0,
          endChoice: 0,
        },
    title: entry.title,
    description: entry.description ?? null,
    location: entry.location ?? null,
    category: entry.category,
    status: entry.status ?? 0,
    responsibleUserIds: entry.responsibleUserIds ?? [],
    audience: entry.audience ?? null,
  };
}

function optimisticEntryFromCalendar(
  entry: ScheduleEntry,
  event: { allDay: boolean; startStr: string; endStr: string },
): ScheduleEntry {
  return {
    ...entry,
    timing: event.allDay
      ? {
          isAllDay: true,
          startDate: event.startStr.slice(0, 10),
          endDateExclusive: event.endStr.slice(0, 10),
        }
      : {
          isAllDay: false,
          startsAtUtc: event.startStr,
          endsAtUtc: event.endStr,
        },
  };
}

function scheduleTimingLabel(entry: ScheduleEntry, timeZone: string) {
  if (entry.timing.isAllDay)
    return `${entry.timing.startDate ?? ""} · ganztägig`;
  const formatter = new Intl.DateTimeFormat("de-DE", {
    timeZone,
    weekday: "short",
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
  const start = entry.timing.startsAtUtc
    ? formatter.format(new Date(entry.timing.startsAtUtc))
    : "";
  const end = entry.timing.endsAtUtc
    ? new Intl.DateTimeFormat("de-DE", {
        timeZone,
        hour: "2-digit",
        minute: "2-digit",
      }).format(new Date(entry.timing.endsAtUtc))
    : "";
  return `${start}–${end} Uhr`;
}

function accountInitials(displayName: string) {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);
  return (
    parts.length > 1
      ? `${parts[0][0]}${parts.at(-1)?.[0] ?? ""}`
      : parts[0]?.slice(0, 2) || "K"
  ).toLocaleUpperCase("de-DE");
}

function campLocalDate(timeZone: string) {
  return new Intl.DateTimeFormat("sv-SE", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
}

function scheduleEntryDate(entry: ScheduleEntry, timeZone: string) {
  return entry.timing.isAllDay
    ? entry.timing.startDate
    : formatCampLocalDateTime(entry.timing.startsAtUtc, timeZone).date;
}

function scheduleEntryDateTime(entry: ScheduleEntry) {
  return entry.timing.isAllDay
    ? entry.timing.startDate
    : entry.timing.startsAtUtc;
}

function scheduleEntryTime(entry: ScheduleEntry, timeZone: string) {
  if (entry.timing.isAllDay) return "Ganztägig";
  return formatCampLocalDateTime(entry.timing.startsAtUtc, timeZone).time;
}

function compareScheduleEntries(left: ScheduleEntry, right: ScheduleEntry) {
  const value = (entry: ScheduleEntry) =>
    entry.timing.isAllDay
      ? `${entry.timing.startDate ?? ""}T00:00:00`
      : (entry.timing.startsAtUtc ?? "");
  return value(left).localeCompare(value(right));
}

function formatDashboardDate(localDate: string) {
  return new Intl.DateTimeFormat("de-DE", {
    dateStyle: "full",
    timeZone: "UTC",
  }).format(new Date(`${localDate}T12:00:00Z`));
}

const scheduleStatusLabel: Record<number, string> = {
  0: "Geplant",
  1: "Bestätigt",
  2: "Abgesagt",
};

function ResponsibilityFields({
  candidates,
  selected,
  onChange,
}: {
  candidates: CampMemberSummary[];
  selected: string[];
  onChange: (userIds: string[]) => void;
}) {
  return (
    <fieldset className="responsibility-selector">
      <legend>Verantwortliche</legend>
      {candidates.length === 0 ? (
        <p className="form-hint">
          Keine auswählbaren Camp-Mitglieder gefunden.
        </p>
      ) : (
        candidates.map((candidate) => (
          <label className="checkbox-label" key={candidate.userId}>
            <input
              type="checkbox"
              checked={selected.includes(candidate.userId)}
              onChange={(event) =>
                onChange(
                  event.target.checked
                    ? [...selected, candidate.userId]
                    : selected.filter((userId) => userId !== candidate.userId),
                )
              }
            />
            {candidate.displayName}
          </label>
        ))
      )}
    </fieldset>
  );
}

type Meal = {
  id: string;
  name: string;
  effectivePortions: number;
  scheduleEntryId: string | null;
  recipeCount: number;
  version: number;
};
type MealRecipeSnapshot = {
  id: string;
  sourceRecipeId: string;
  sourceRecipeVersionNumber: number;
  latestRecipeVersionNumber: number;
  refreshAvailable: boolean;
  name: string;
  description: string;
  preparation: string;
  basePortions: number;
  ingredients: {
    id: string;
    ingredientId: string;
    ingredientName: string;
    baseQuantity: RecipeQuantity;
    scaledQuantity: RecipeQuantity;
    note: string | null;
  }[];
  dietaryTags: string[];
  allergenNotes: string | null;
  kitchenNotes: string | null;
  capturedAt: string;
};
type MealDetail = {
  id: string;
  organizationId: string;
  campId: string;
  name: string;
  campDefaultPortions: number;
  portionOverride: number | null;
  effectivePortions: number;
  scheduleEntryId: string | null;
  recipeSnapshots: MealRecipeSnapshot[];
  version: number;
};
type Ingredient = {
  id: string;
  organizationId: string;
  name: string;
  isMerged: boolean;
  mergedIntoIngredientId: string | null;
  version: number;
};
type RecipeSummary = {
  id: string;
  organizationId: string;
  name: string;
  basePortions: number;
  currentVersionNumber: number;
  version: number;
};
type RecipeCreateResult = {
  id: string;
  currentVersion: { number: number; name: string };
  version: number;
};
type RecipeIngredientDraft = {
  ingredient: { id: string; name: string };
  quantity: string;
  unit: string;
  countUnitName: string;
  note: string;
};
type RecipeQuantity = {
  value: number;
  unit: number;
  countUnitName: string | null;
};
type RecipeIngredient = {
  id: string;
  ingredientId: string;
  ingredientName: string;
  quantity: RecipeQuantity;
  note: string | null;
};
type RecipeDetail = {
  id: string;
  organizationId: string;
  currentVersion: {
    id: string;
    number: number;
    name: string;
    description: string;
    preparation: string;
    basePortions: number;
    ingredients: RecipeIngredient[];
    dietaryTags: string[];
    allergenNotes: string | null;
    kitchenNotes: string | null;
    createdAt: string;
  };
  version: number;
};
type RecipeAttachment = {
  id: string;
  originalFileName: string;
  mediaType: number;
  contentType: string;
  sizeBytes: number;
  version: number;
};
type RecipeAttachmentQuota = {
  limitBytes: number;
  usedBytes: number;
  pendingBytes: number;
  availableBytes: number;
};
type AttachmentReadGrant = {
  token: string;
  attachmentId: string;
  expiresAt: string;
  disposition: number;
};
type IngredientMergePreview = {
  source: Ingredient;
  target: Ingredient;
  affectedRecipes: RecipeSummary[];
};
type IngredientMergeResult = {
  target: Ingredient;
  revisedRecipeIds: string[];
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
type MaterialRequirementSummary = {
  id: string;
  name: string;
  status: number;
};
type ShoppingListSummary = {
  id: string;
  name: string;
  openItemCount: number;
  checkedItemCount: number;
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

async function mutateCateringJson<T>(
  path: string,
  method: "POST" | "PUT" | "DELETE",
  body: unknown,
  version?: number,
  conflictMessage?: string,
) {
  const token = await getAntiforgeryToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    "X-CSRF-TOKEN": token,
  };
  if (version !== undefined) headers["If-Match"] = `"${version}"`;
  const response = await fetch(path, {
    method,
    credentials: "same-origin",
    headers,
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string;
    } | null;
    throw new Error(
      problem?.detail ??
        (response.status === 412 ? conflictMessage : undefined) ??
        "Die Änderung konnte nicht gespeichert werden.",
    );
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

function useCampQuery<T>(key: string, path: string) {
  const { organizationId, campId } = useCampRuntime();
  return useQuery({
    queryKey: [organizationId, campId, key],
    queryFn: () => getJson<T>(path),
    retry: false,
  });
}

export function CampWorkspace() {
  const { organizationSlug = "", campSlug = "" } = useParams();
  const workspace = useQuery({
    queryKey: ["camp-workspace", organizationSlug, campSlug],
    queryFn: () => resolveCampRuntime(organizationSlug, campSlug),
    retry: false,
    staleTime: Number.POSITIVE_INFINITY,
  });

  if (workspace.isLoading)
    return (
      <div className="account-layout">
        <header className="topbar">
          <Link className="brand" to="/konto">
            <span className="brand-mark" aria-hidden="true">
              F
            </span>
            <span>Freizeit-Cockpit</span>
          </Link>
        </header>
        <main id="main" className="account-page">
          <p role="status">Camp wird geladen …</p>
        </main>
      </div>
    );
  if (workspace.error || !workspace.data)
    return (
      <div className="account-layout">
        <header className="topbar">
          <Link className="brand" to={`/o/${organizationSlug}/camps`}>
            <span className="brand-mark" aria-hidden="true">
              F
            </span>
            <span>Freizeit-Cockpit</span>
          </Link>
        </header>
        <main id="main" className="account-page">
          <h1>Camp nicht verfügbar</h1>
          <p role="alert" className="error-message">
            {workspace.error instanceof Error
              ? workspace.error.message
              : "Das Camp konnte nicht geladen werden."}
          </p>
        </main>
      </div>
    );

  return (
    <CampRuntimeContext.Provider value={workspace.data}>
      <CampWorkspaceShell />
    </CampRuntimeContext.Provider>
  );
}

async function resolveCampRuntime(
  organizationSlug: string,
  campSlug: string,
): Promise<CampRuntime> {
  const membershipsResponse = await fetch("/api/v1/account/memberships", {
    credentials: "same-origin",
  });
  if (!membershipsResponse.ok)
    throw new Error("Deine Organisationen konnten nicht geladen werden.");
  const memberships = (await membershipsResponse.json()) as AccountMembership[];
  const membership = memberships.find(
    (item) => item.organizationSlug === organizationSlug,
  );
  if (!membership)
    throw new Error("Du hast keinen Zugriff auf diese Organisation.");

  const campResponse = await fetch(
    `/api/v1/organizations/${membership.organizationId}/camps/by-slug/${encodeURIComponent(campSlug)}`,
    { credentials: "same-origin" },
  );
  if (!campResponse.ok)
    throw new Error("Das Camp wurde nicht gefunden oder ist nicht zugänglich.");
  const camp = (await campResponse.json()) as WorkspaceCamp;
  return {
    organizationId: membership.organizationId,
    organizationName: membership.organizationName,
    organizationSlug,
    organizationRole: membership.role,
    campId: camp.id,
    campSlug: camp.slug,
    campBase: `/o/${organizationSlug}/camps/${camp.slug}`,
    camp,
  };
}

function CampWorkspaceShell() {
  const runtime = useCampRuntime();
  const { campBase, camp } = runtime;
  const location = useLocation();
  const [offline, setOffline] = useState(!navigator.onLine);
  const account = useQuery({
    queryKey: ["account"],
    queryFn: () => getJson<Account>("/api/v1/account"),
    retry: false,
    staleTime: 5 * 60 * 1000,
  });
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
  const readOnly = offline || camp.status === 1;
  const accountDisplayName = account.data?.displayName?.trim();

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
            {offline
              ? "Offline · nur gespeicherter Stand"
              : camp.status === 1
                ? "Archiviert · nur lesen"
                : "Online"}
          </span>
          <Link
            className="profile-button"
            aria-label={
              accountDisplayName
                ? `Kontomenü von ${accountDisplayName} öffnen`
                : "Kontomenü öffnen"
            }
            to="/konto"
          >
            {accountDisplayName ? accountInitials(accountDisplayName) : "…"}
          </Link>
        </div>
      </header>
      <div className="workspace">
        <aside className="sidebar" aria-label="Camp-Navigation">
          <p className="eyebrow">{runtime.organizationName}</p>
          <p className="camp-name">{camp.name}</p>
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
          {camp.status === 1 ? (
            <p className="notice" role="status">
              Archiviert · nur lesen. Inhalte bleiben lesbar und exportierbar;
              Änderungen sind erst nach der Reaktivierung möglich.
            </p>
          ) : null}
          <Routes>
            <Route index element={<OverviewPage />} />
            <Route
              path="tagesplan"
              element={<SchedulePage offline={readOnly} />}
            />
            <Route path="essen" element={<MealsPage offline={readOnly} />} />
            <Route
              path="logistik"
              element={<LogisticsPage offline={readOnly} />}
            />
            <Route
              path="andachten"
              element={<DevotionsPage offline={readOnly} />}
            />
            <Route path="notizen" element={<NotesPage offline={readOnly} />} />
            <Route path="dateien" element={<FilesPage offline={readOnly} />} />
            <Route
              path="suche"
              element={<SearchTrashPage offline={readOnly} />}
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
  const { organizationId, campId, camp } = useCampRuntime();
  const account = useQuery({
    queryKey: ["account"],
    queryFn: () => getJson<Account>("/api/v1/account"),
    retry: false,
    staleTime: 5 * 60 * 1000,
  });
  const schedulePath = `/api/v1/organizations/${organizationId}/camps/${campId}/schedule?fromDate=${camp.startsOn}&toDateExclusive=${nextLocalDate(camp.endsOn)}`;
  const schedule = useCampQuery<ScheduleEntry[]>("schedule", schedulePath);
  const material = useCampQuery<MaterialRequirementSummary[]>(
    "material",
    `/api/v1/organizations/${organizationId}/camps/${campId}/logistics/material`,
  );
  const shopping = useCampQuery<ShoppingListSummary[]>(
    "shopping-lists",
    `/api/v1/organizations/${organizationId}/camps/${campId}/logistics/shopping-lists`,
  );
  const activity = useCampQuery<ActivityEvent[]>(
    "activity",
    `/api/v1/organizations/${organizationId}/camps/${campId}/activity?limit=5`,
  );
  const today = campLocalDate(camp.timeZoneId);
  const scheduleEntries = schedule.data ?? [];
  const availableDates = Array.from(
    new Set(
      scheduleEntries
        .map((entry) => scheduleEntryDate(entry, camp.timeZoneId))
        .filter((date): date is string => Boolean(date)),
    ),
  ).sort();
  const planDate =
    availableDates.find((date) => date >= today) ??
    availableDates.at(-1) ??
    (today < camp.startsOn
      ? camp.startsOn
      : today > camp.endsOn
        ? camp.endsOn
        : today);
  const planEntries = scheduleEntries
    .filter((entry) => scheduleEntryDate(entry, camp.timeZoneId) === planDate)
    .sort(compareScheduleEntries);
  const planHeading =
    planDate === today
      ? "Heute im Tagesplan"
      : planDate > today
        ? "Nächster Tagesplan"
        : "Letzter Tagesplan";
  const accountDisplayName = account.data?.displayName?.trim();
  const accountId = account.data?.id;
  const responsibilities = accountId
    ? scheduleEntries.filter(
        (entry) =>
          entry.status !== 2 && entry.responsibleUserIds.includes(accountId),
      )
    : [];
  const openMaterial = (material.data ?? []).filter(
    (requirement) => requirement.status === 0 || requirement.status === 1,
  ).length;
  const openShopping = (shopping.data ?? []).reduce(
    (sum, list) => sum + list.openItemCount,
    0,
  );
  return (
    <>
      <PageHeading
        eyebrow={formatDashboardDate(planDate)}
        title={
          accountDisplayName ? `Hallo, ${accountDisplayName}` : "Camp-Übersicht"
        }
      >
        <p>Hier siehst du, was als Nächstes für euer Team wichtig ist.</p>
      </PageHeading>
      <section aria-labelledby="today-heading">
        <div className="section-heading">
          <h2 id="today-heading">{planHeading}</h2>
          <Link to="tagesplan">Ganzen Plan öffnen</Link>
        </div>
        <QueryState loading={schedule.isLoading} error={schedule.error} />
        {planEntries.length ? (
          <ol className="timeline">
            {planEntries.map((entry) => (
              <li key={entry.id}>
                <time dateTime={scheduleEntryDateTime(entry)}>
                  {scheduleEntryTime(entry, camp.timeZoneId)}
                </time>
                <div>
                  <strong>{entry.title}</strong>
                  <span>
                    {[entry.location, entry.category]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                </div>
                <span
                  className={
                    entry.overlapsAnotherEntry ? "status info" : "status"
                  }
                >
                  {entry.overlapsAnotherEntry
                    ? "Parallel"
                    : scheduleStatusLabel[entry.status]}
                </span>
              </li>
            ))}
          </ol>
        ) : (
          !schedule.isLoading && (
            <p className="empty-state">
              Für diesen Tag sind noch keine Einträge geplant.
            </p>
          )
        )}
      </section>
      <div className="dashboard-grid">
        <SummaryCard
          title="Meine Verantwortungen"
          value={String(responsibilities.length)}
          text={
            responsibilities.length === 1
              ? "aktiver Zeitplaneintrag"
              : "aktive Zeitplaneinträge"
          }
        >
          <QueryState
            loading={account.isLoading || schedule.isLoading}
            error={account.error ?? schedule.error}
          />
        </SummaryCard>
        <SummaryCard
          title="Beschaffung"
          value={String(openMaterial + openShopping)}
          text="noch zu beschaffen"
        >
          <p className="metric-detail">
            {openMaterial} Material · {openShopping} Einkauf
          </p>
          <QueryState
            loading={material.isLoading || shopping.isLoading}
            error={material.error ?? shopping.error}
          />
        </SummaryCard>
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
  children,
}: {
  title: string;
  value: string;
  text: string;
  children?: ReactNode;
}) {
  const headingId = `summary-${title.toLocaleLowerCase("de-DE").replace(/[^a-z0-9]+/g, "-")}`;
  return (
    <section className="card" aria-labelledby={headingId}>
      <h2 id={headingId}>{title}</h2>
      <p className="metric">
        {value} <span>{text}</span>
      </p>
      {children}
    </section>
  );
}

function SchedulePage({ offline }: { offline: boolean }) {
  const { organizationId, campId, camp } = useCampRuntime();
  const toDateExclusive = nextLocalDate(camp.endsOn);
  const path = `/api/v1/organizations/${organizationId}/camps/${campId}/schedule?fromDate=${camp.startsOn}&toDateExclusive=${toDateExclusive}`;
  const query = useCampQuery<ScheduleEntry[]>("schedule", path);
  const candidatesQuery = useCampQuery<CampMemberSummary[]>(
    "responsibility-candidates",
    `/api/v1/organizations/${organizationId}/camps/${campId}/responsibility-candidates`,
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
          locale="de"
          firstDay={1}
          allDayText="Ganztägig"
          height="auto"
          events={events}
          editable={!offline && !update.isPending}
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
      <section className="settings-section" aria-labelledby="agenda-title">
        <div className="section-heading">
          <h2 id="agenda-title">Barrierearme Agenda</h2>
          <button
            className="primary-action"
            disabled={offline}
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
                disabled={offline || create.isPending}
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
                disabled={offline || update.isPending}
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
                  className="secondary-action"
                  disabled={offline || update.isPending}
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

function IngredientLibraryPanel({
  organizationId,
  onClose,
}: {
  organizationId: string;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const queryKey = [organizationId, "catering", "ingredient-management"];
  const ingredients = useQuery({
    queryKey,
    queryFn: () =>
      getJson<Ingredient[]>(
        `/api/v1/organizations/${organizationId}/catering/ingredients?query=&limit=100`,
      ),
    retry: false,
  });
  const [newIngredientName, setNewIngredientName] = useState("");
  const [renameIngredient, setRenameIngredient] = useState<Ingredient | null>(
    null,
  );
  const [renameName, setRenameName] = useState("");
  const [sourceIngredientId, setSourceIngredientId] = useState("");
  const [targetIngredientId, setTargetIngredientId] = useState("");
  const [mergeConfirmed, setMergeConfirmed] = useState(false);
  const [notice, setNotice] = useState("");
  const createIngredient = useMutation({
    mutationFn: () =>
      mutateCateringJson<Ingredient>(
        `/api/v1/organizations/${organizationId}/catering/ingredients`,
        "POST",
        { name: newIngredientName },
      ),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey });
      setNewIngredientName("");
      setNotice(`${created.name} wurde angelegt.`);
    },
  });
  const renameMutation = useMutation({
    mutationFn: () => {
      if (!renameIngredient) throw new Error("Wähle zuerst eine Zutat aus.");
      return mutateCateringJson<Ingredient>(
        `/api/v1/organizations/${organizationId}/catering/ingredients/${renameIngredient.id}`,
        "PUT",
        { name: renameName },
        renameIngredient.version,
      );
    },
    onSuccess: async (renamed) => {
      await queryClient.invalidateQueries({ queryKey });
      setRenameIngredient(null);
      setRenameName("");
      setNotice(`${renamed.name} wurde gespeichert.`);
    },
  });
  const previewMerge = useMutation({
    mutationFn: () =>
      mutateCateringJson<IngredientMergePreview>(
        `/api/v1/organizations/${organizationId}/catering/ingredients/merge-preview`,
        "POST",
        {
          sourceIngredientId,
          targetIngredientId,
          expectedSourceVersion: 0,
          expectedTargetVersion: 0,
        },
      ),
    onSuccess: () => {
      setMergeConfirmed(false);
      setNotice("");
    },
  });
  const mergeIngredients = useMutation({
    mutationFn: () => {
      const preview = previewMerge.data;
      if (!preview) throw new Error("Prüfe die Zusammenführung zuerst erneut.");
      return mutateCateringJson<IngredientMergeResult>(
        `/api/v1/organizations/${organizationId}/catering/ingredients/merge`,
        "POST",
        {
          sourceIngredientId: preview.source.id,
          targetIngredientId: preview.target.id,
          expectedSourceVersion: preview.source.version,
          expectedTargetVersion: preview.target.version,
        },
      );
    },
    onSuccess: async () => {
      const preview = previewMerge.data;
      await Promise.all([
        queryClient.invalidateQueries({ queryKey }),
        queryClient.invalidateQueries({
          queryKey: [organizationId, "catering", "recipes"],
        }),
      ]);
      setNotice(
        `${preview?.source.name ?? "Die Zutat"} wurde kontrolliert in ${preview?.target.name ?? "die Zielzutat"} zusammengeführt.`,
      );
      setSourceIngredientId("");
      setTargetIngredientId("");
      setMergeConfirmed(false);
      previewMerge.reset();
    },
  });
  const mutationError =
    createIngredient.error ??
    renameMutation.error ??
    previewMerge.error ??
    mergeIngredients.error;

  return (
    <section
      className="settings-section ingredient-management"
      aria-labelledby="ingredient-management-heading"
    >
      <div className="section-heading">
        <div>
          <p className="eyebrow">Organisationsbibliothek</p>
          <h2 id="ingredient-management-heading">
            Zutatenbibliothek verwalten
          </h2>
        </div>
        <button className="secondary-action" type="button" onClick={onClose}>
          Verwaltung schließen
        </button>
      </div>
      <p>
        Namen werden normalisiert und sind innerhalb der Organisation eindeutig.
        Eine Zusammenführung ändert aktuelle Bibliotheksrezepte, aber keine
        vorhandenen Mahlzeiten-Snapshots.
      </p>
      {notice ? (
        <p role="status" className="form-feedback">
          {notice}
        </p>
      ) : null}
      {mutationError ? (
        <p role="alert" className="error-message">
          {mutationError.message}
        </p>
      ) : null}
      <form
        className="toolbar"
        onSubmit={(event) => {
          event.preventDefault();
          setNotice("");
          createIngredient.mutate();
        }}
      >
        <label>
          Neue Zutat
          <input
            required
            value={newIngredientName}
            onChange={(event) => setNewIngredientName(event.target.value)}
          />
        </label>
        <button
          className="primary-action"
          type="submit"
          disabled={createIngredient.isPending}
        >
          {createIngredient.isPending
            ? "Zutat wird angelegt …"
            : "Zutat anlegen"}
        </button>
      </form>
      <QueryState loading={ingredients.isLoading} error={ingredients.error} />
      {ingredients.data?.length ? (
        <ul className="ingredient-list">
          {ingredients.data.map((ingredient) => (
            <li key={ingredient.id}>
              <span>
                <strong>{ingredient.name}</strong>
                <small>Version {ingredient.version}</small>
              </span>
              <button
                className="text-action"
                type="button"
                onClick={() => {
                  setRenameIngredient(ingredient);
                  setRenameName(ingredient.name);
                  setNotice("");
                }}
              >
                {ingredient.name} umbenennen
              </button>
            </li>
          ))}
        </ul>
      ) : (
        !ingredients.isLoading && (
          <p className="empty-state">Noch keine Zutat vorhanden.</p>
        )
      )}
      {renameIngredient ? (
        <form
          className="schedule-create-form compact-form"
          onSubmit={(event) => {
            event.preventDefault();
            setNotice("");
            renameMutation.mutate();
          }}
        >
          <h3>{renameIngredient.name} umbenennen</h3>
          <label>
            Neuer Name für {renameIngredient.name}
            <input
              required
              value={renameName}
              onChange={(event) => setRenameName(event.target.value)}
            />
          </label>
          <div className="toolbar">
            <button
              className="primary-action"
              type="submit"
              disabled={renameMutation.isPending}
            >
              Neuen Namen speichern
            </button>
            <button
              className="secondary-action"
              type="button"
              onClick={() => setRenameIngredient(null)}
            >
              Abbrechen
            </button>
          </div>
        </form>
      ) : null}
      <form
        className="schedule-create-form ingredient-merge-form"
        aria-labelledby="ingredient-merge-heading"
        onSubmit={(event) => {
          event.preventDefault();
          previewMerge.mutate();
        }}
      >
        <h3 id="ingredient-merge-heading">Doppelte Zutaten zusammenführen</h3>
        <p className="form-hint">
          Die doppelte Zutat wird nach der Bestätigung nicht mehr angeboten. Die
          Zielzutat bleibt erhalten.
        </p>
        <div className="schedule-create-grid schedule-all-day-grid">
          <label>
            Doppelte Zutat
            <select
              required
              value={sourceIngredientId}
              onChange={(event) => {
                setSourceIngredientId(event.target.value);
                previewMerge.reset();
                setMergeConfirmed(false);
              }}
            >
              <option value="">Bitte wählen</option>
              {ingredients.data?.map((ingredient) => (
                <option key={ingredient.id} value={ingredient.id}>
                  {ingredient.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Zielzutat
            <select
              required
              value={targetIngredientId}
              onChange={(event) => {
                setTargetIngredientId(event.target.value);
                previewMerge.reset();
                setMergeConfirmed(false);
              }}
            >
              <option value="">Bitte wählen</option>
              {ingredients.data?.map((ingredient) => (
                <option key={ingredient.id} value={ingredient.id}>
                  {ingredient.name}
                </option>
              ))}
            </select>
          </label>
        </div>
        <button
          className="secondary-action"
          type="submit"
          disabled={
            previewMerge.isPending ||
            !sourceIngredientId ||
            !targetIngredientId ||
            sourceIngredientId === targetIngredientId
          }
        >
          {previewMerge.isPending
            ? "Zusammenführung wird geprüft …"
            : "Zusammenführung prüfen"}
        </button>
        {previewMerge.data ? (
          <section
            className="merge-preview"
            aria-labelledby="merge-preview-heading"
          >
            <h4 id="merge-preview-heading">
              Auswirkung: {previewMerge.data.source.name} →{" "}
              {previewMerge.data.target.name}
            </h4>
            {previewMerge.data.affectedRecipes.length ? (
              <>
                <p>Folgende aktuelle Rezepte erhalten eine neue Version:</p>
                <ul>
                  {previewMerge.data.affectedRecipes.map((recipe) => (
                    <li key={recipe.id}>
                      {recipe.name} · Version {recipe.currentVersionNumber}
                    </li>
                  ))}
                </ul>
              </>
            ) : (
              <p>Kein aktuelles Rezept ist betroffen.</p>
            )}
            <p>
              Bereits gespeicherte Mahlzeiten-Snapshots bleiben unverändert.
            </p>
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={mergeConfirmed}
                onChange={(event) => setMergeConfirmed(event.target.checked)}
              />
              Ich habe die betroffenen Rezepte geprüft.
            </label>
            <button
              className="danger-action"
              type="button"
              disabled={!mergeConfirmed || mergeIngredients.isPending}
              onClick={() => mergeIngredients.mutate()}
            >
              {mergeIngredients.isPending
                ? "Zutaten werden zusammengeführt …"
                : "Zusammenführung bestätigen"}
            </button>
          </section>
        ) : null}
      </form>
    </section>
  );
}

const measurementUnitLabels = [
  "Gramm",
  "Kilogramm",
  "Milliliter",
  "Liter",
  "Stück",
] as const;

function formatRecipeQuantity(quantity: RecipeQuantity) {
  const value = new Intl.NumberFormat("de-DE", {
    maximumFractionDigits: 3,
  }).format(quantity.value);
  const unit =
    quantity.unit === 5
      ? quantity.countUnitName || "Zähleinheit"
      : measurementUnitLabels[quantity.unit] || "Einheit";
  return `${value} ${unit}`;
}

function formatFileSize(bytes: number) {
  const mebibytes = bytes / (1024 * 1024);
  if (mebibytes >= 1)
    return `${new Intl.NumberFormat("de-DE", { maximumFractionDigits: 1 }).format(mebibytes)} MiB`;
  return `${new Intl.NumberFormat("de-DE", { maximumFractionDigits: 0 }).format(bytes / 1024)} KiB`;
}

function RecipeAttachmentsPanel({
  organizationId,
  recipeId,
  recipeName,
  canUpload,
}: {
  organizationId: string;
  recipeId: string;
  recipeName: string;
  canUpload: boolean;
}) {
  const queryClient = useQueryClient();
  const basePath = `/api/v1/organizations/${organizationId}/recipe-files`;
  const attachmentQueryKey = [organizationId, "recipe-files", recipeId];
  const quotaQueryKey = [organizationId, "recipe-files", "quota"];
  const attachments = useQuery({
    queryKey: attachmentQueryKey,
    queryFn: () =>
      getJson<RecipeAttachment[]>(`${basePath}?ownerId=${recipeId}`),
    retry: false,
  });
  const quota = useQuery({
    queryKey: quotaQueryKey,
    queryFn: () => getJson<RecipeAttachmentQuota>(`${basePath}/quota`),
    retry: false,
  });
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [inputKey, setInputKey] = useState(0);
  const [notice, setNotice] = useState("");
  const uploadAttachment = useMutation({
    mutationFn: async () => {
      if (!selectedFile) throw new Error("Wähle zuerst eine Datei aus.");
      if (selectedFile.size > 10 * 1024 * 1024)
        throw new Error("Eine Datei darf höchstens zehn MiB groß sein.");
      const token = await getAntiforgeryToken();
      const body = new FormData();
      body.append("file", selectedFile);
      const response = await fetch(`${basePath}?ownerId=${recipeId}`, {
        method: "POST",
        credentials: "same-origin",
        headers: { "X-CSRF-TOKEN": token },
        body,
      });
      if (!response.ok) {
        const problem = (await response.json().catch(() => null)) as {
          detail?: string;
        } | null;
        throw new Error(
          problem?.detail ?? "Die Datei konnte nicht hochgeladen werden.",
        );
      }
      return (await response.json()) as RecipeAttachment;
    },
    onSuccess: async (uploaded) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: attachmentQueryKey }),
        queryClient.invalidateQueries({ queryKey: quotaQueryKey }),
      ]);
      setNotice(`${uploaded.originalFileName} wurde sicher hochgeladen.`);
      setSelectedFile(null);
      setInputKey((current) => current + 1);
    },
  });
  const openAttachment = useMutation({
    mutationFn: async (attachment: RecipeAttachment) => {
      const viewer = window.open("", "_blank", "noopener,noreferrer");
      try {
        if (!viewer)
          throw new Error(
            "Die Datei konnte nicht geöffnet werden. Erlaube Pop-ups für diese Seite und versuche es erneut.",
          );
        const token = await getAntiforgeryToken();
        const response = await fetch(
          `${basePath}/${attachment.id}/read-grant`,
          {
            method: "POST",
            credentials: "same-origin",
            headers: { "X-CSRF-TOKEN": token },
          },
        );
        if (!response.ok) {
          const problem = (await response.json().catch(() => null)) as {
            detail?: string;
          } | null;
          throw new Error(
            problem?.detail ?? "Die Datei konnte nicht geöffnet werden.",
          );
        }
        const grant = (await response.json()) as AttachmentReadGrant;
        viewer.location.href = `${basePath}/content?token=${encodeURIComponent(grant.token)}`;
        return attachment;
      } catch (error) {
        viewer?.close();
        throw error;
      }
    },
  });

  return (
    <section
      className="recipe-attachments"
      aria-label={`Dateien zu ${recipeName}`}
    >
      <div className="section-heading">
        <div>
          <h3>Dateien</h3>
          <p className="form-hint">
            PDF, JPEG, PNG oder WebP · höchstens 10 MiB pro Datei
          </p>
        </div>
        {quota.data ? (
          <p className="quota-usage">
            {formatFileSize(quota.data.usedBytes)} von{" "}
            {formatFileSize(quota.data.limitBytes)} belegt
          </p>
        ) : null}
      </div>
      <QueryState loading={attachments.isLoading} error={attachments.error} />
      {quota.error ? (
        <p role="alert" className="error-message">
          {quota.error.message}
        </p>
      ) : null}
      {attachments.data?.length ? (
        <ul className="recipe-attachment-list">
          {attachments.data.map((attachment) => (
            <li key={attachment.id}>
              <span>
                <strong>{attachment.originalFileName}</strong>
                <small>{formatFileSize(attachment.sizeBytes)}</small>
              </span>
              <button
                type="button"
                className="secondary-action"
                disabled={
                  openAttachment.isPending &&
                  openAttachment.variables?.id === attachment.id
                }
                onClick={() => openAttachment.mutate(attachment)}
              >
                {attachment.originalFileName} öffnen
              </button>
            </li>
          ))}
        </ul>
      ) : !attachments.isLoading && !attachments.error ? (
        <p className="empty-state">Noch keine Datei am Rezept.</p>
      ) : null}
      {openAttachment.error ? (
        <p role="alert" className="error-message">
          {openAttachment.error.message}
        </p>
      ) : null}
      {canUpload ? (
        <form
          className="recipe-attachment-upload"
          onSubmit={(event) => {
            event.preventDefault();
            setNotice("");
            uploadAttachment.mutate();
          }}
        >
          <label>
            Datei für das Rezept
            <input
              key={inputKey}
              type="file"
              accept="application/pdf,image/jpeg,image/png,image/webp"
              onChange={(event) => {
                setSelectedFile(event.target.files?.[0] ?? null);
                setNotice("");
                uploadAttachment.reset();
              }}
            />
          </label>
          <button
            type="submit"
            className="primary-action"
            disabled={!selectedFile || uploadAttachment.isPending}
          >
            {uploadAttachment.isPending
              ? `${selectedFile?.name ?? "Datei"} wird hochgeladen …`
              : `${selectedFile?.name ?? "Datei"} hochladen`}
          </button>
        </form>
      ) : null}
      {uploadAttachment.error ? (
        <p role="alert" className="error-message">
          {uploadAttachment.error.message}
        </p>
      ) : null}
      {notice ? (
        <p className="form-feedback" role="status">
          {notice}
        </p>
      ) : null}
      <p className="muted">
        Dateien bleiben privat und werden erst nach einer aktuellen
        Berechtigungsprüfung kurzzeitig ausgeliefert. Eine Malware-Prüfung ist
        nicht enthalten; lade nur vertrauenswürdige Dateien hoch.
      </p>
    </section>
  );
}

function RecipeDetailPanel({
  organizationId,
  recipeId,
  canManage,
  readOnly,
  onClose,
}: {
  organizationId: string;
  recipeId: string;
  canManage: boolean;
  readOnly: boolean;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const detailQueryKey = [organizationId, "catering", "recipes", recipeId];
  const recipe = useQuery({
    queryKey: detailQueryKey,
    queryFn: () =>
      getJson<RecipeDetail>(
        `/api/v1/organizations/${organizationId}/catering/recipes/${recipeId}`,
      ),
    retry: false,
  });
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [preparation, setPreparation] = useState("");
  const [basePortions, setBasePortions] = useState("4");
  const [dietaryTags, setDietaryTags] = useState("");
  const [allergenNotes, setAllergenNotes] = useState("");
  const [kitchenNotes, setKitchenNotes] = useState("");
  const [ingredientSearch, setIngredientSearch] = useState("");
  const [ingredients, setIngredients] = useState<RecipeIngredientDraft[]>([]);
  const [notice, setNotice] = useState("");
  const ingredientSuggestions = useQuery({
    queryKey: [
      organizationId,
      "catering",
      "ingredients",
      "recipe-edit",
      ingredientSearch.trim(),
    ],
    queryFn: () =>
      getJson<Ingredient[]>(
        `/api/v1/organizations/${organizationId}/catering/ingredients?query=${encodeURIComponent(ingredientSearch.trim())}&limit=10`,
      ),
    enabled: editing && ingredientSearch.trim().length >= 2,
    retry: false,
  });
  const reviseRecipe = useMutation({
    mutationFn: () => {
      const current = recipe.data;
      if (!current) throw new Error("Das Rezept ist noch nicht geladen.");
      return mutateCateringJson<RecipeDetail>(
        `/api/v1/organizations/${organizationId}/catering/recipes/${recipeId}`,
        "PUT",
        {
          name,
          description,
          preparation,
          basePortions: Number(basePortions),
          ingredients: ingredients.map((row) => ({
            ingredientId: row.ingredient.id,
            quantity: {
              value: Number(row.quantity),
              unit: Number(row.unit),
              countUnitName:
                row.unit === "5" ? row.countUnitName || null : null,
            },
            note: row.note || null,
          })),
          dietaryTags: Array.from(
            new Set(
              dietaryTags
                .split(/[,;\n]/)
                .map((tag) => tag.trim())
                .filter(Boolean),
            ),
          ),
          allergenNotes: allergenNotes || null,
          kitchenNotes: kitchenNotes || null,
        },
        current.version,
        "Das Rezept wurde zwischenzeitlich geändert. Schließe die Bearbeitung und öffne das Rezept erneut.",
      );
    },
    onSuccess: async (revised) => {
      queryClient.setQueryData(detailQueryKey, revised);
      await queryClient.invalidateQueries({
        queryKey: [organizationId, "catering", "recipes"],
      });
      setEditing(false);
      setIngredientSearch("");
      setNotice(
        `${revised.currentVersion.name} wurde als Rezeptversion ${revised.currentVersion.number} gespeichert.`,
      );
    },
  });
  const beginEditing = () => {
    const current = recipe.data?.currentVersion;
    if (!current) return;
    setName(current.name);
    setDescription(current.description);
    setPreparation(current.preparation);
    setBasePortions(String(current.basePortions));
    setDietaryTags(current.dietaryTags.join(", "));
    setAllergenNotes(current.allergenNotes ?? "");
    setKitchenNotes(current.kitchenNotes ?? "");
    setIngredients(
      current.ingredients.map((row) => ({
        ingredient: { id: row.ingredientId, name: row.ingredientName },
        quantity: String(row.quantity.value),
        unit: String(row.quantity.unit),
        countUnitName: row.quantity.countUnitName ?? "",
        note: row.note ?? "",
      })),
    );
    setIngredientSearch("");
    setNotice("");
    reviseRecipe.reset();
    setEditing(true);
  };
  const updateIngredient = (
    ingredientId: string,
    changes: Partial<RecipeIngredientDraft>,
  ) =>
    setIngredients((current) =>
      current.map((row) =>
        row.ingredient.id === ingredientId ? { ...row, ...changes } : row,
      ),
    );
  const current = recipe.data?.currentVersion;

  return (
    <section className="recipe-detail-panel" aria-label="Rezeptdetails">
      <div className="section-heading">
        <div>
          <p className="eyebrow">
            {current ? `Aktuelle Version ${current.number}` : "Rezept"}
          </p>
          <h2>
            {current ? `Rezeptdetails: ${current.name}` : "Rezept wird geladen"}
          </h2>
        </div>
        <button type="button" className="secondary-action" onClick={onClose}>
          Rezept schließen
        </button>
      </div>
      <QueryState loading={recipe.isLoading} error={recipe.error} />
      {notice ? (
        <p className="form-feedback" role="status">
          {notice}
        </p>
      ) : null}
      {current && !editing ? (
        <div className="recipe-detail-content">
          <div className="recipe-detail-grid">
            <section>
              <h3>Beschreibung</h3>
              <p>{current.description}</p>
            </section>
            <section>
              <h3>Zubereitung</h3>
              <p className="long-text">{current.preparation}</p>
            </section>
          </div>
          <section>
            <h3>Zutaten für {current.basePortions} Basisportionen</h3>
            <ul className="recipe-detail-ingredients">
              {current.ingredients.map((row) => (
                <li key={row.id}>
                  <span>
                    {formatRecipeQuantity(row.quantity)} {row.ingredientName}
                  </span>
                  {row.note ? <small>{row.note}</small> : null}
                </li>
              ))}
            </ul>
          </section>
          <div className="recipe-detail-grid">
            <section>
              <h3>Ernährungs-Tags</h3>
              <p>
                {current.dietaryTags.length
                  ? current.dietaryTags.join(", ")
                  : "Keine Tags hinterlegt"}
              </p>
            </section>
            <section>
              <h3>Allergenhinweise</h3>
              <p>{current.allergenNotes || "Keine Hinweise hinterlegt"}</p>
            </section>
            <section>
              <h3>Küchenhinweise</h3>
              <p>{current.kitchenNotes || "Keine Hinweise hinterlegt"}</p>
            </section>
          </div>
          <p className="form-hint">
            Gespeichert am{" "}
            {new Intl.DateTimeFormat("de-DE", {
              dateStyle: "medium",
              timeStyle: "short",
            }).format(new Date(current.createdAt))}
            . Bereits geplante Mahlzeiten behalten ihren unveränderten
            Rezept-Snapshot.
          </p>
          {canManage && !readOnly ? (
            <button
              type="button"
              className="primary-action"
              onClick={beginEditing}
            >
              Rezept bearbeiten
            </button>
          ) : null}
        </div>
      ) : null}
      {current && editing ? (
        <form
          className="schedule-create-form recipe-form recipe-edit-form"
          aria-label={`${current.name} bearbeiten`}
          onSubmit={(event) => {
            event.preventDefault();
            setNotice("");
            reviseRecipe.mutate();
          }}
        >
          <p className="form-hint">
            Änderungen erzeugen eine neue Version. Bestehende
            Mahlzeiten-Snapshots werden nicht still verändert.
          </p>
          <div className="camp-form-grid">
            <label>
              Rezeptname bearbeiten
              <input
                required
                value={name}
                onChange={(event) => setName(event.target.value)}
              />
            </label>
            <label>
              Basisportionen bearbeiten
              <input
                required
                type="number"
                min="1"
                step="1"
                value={basePortions}
                onChange={(event) => setBasePortions(event.target.value)}
              />
            </label>
            <label className="full-row">
              Beschreibung bearbeiten
              <textarea
                required
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </label>
            <label className="full-row">
              Zubereitung bearbeiten
              <textarea
                required
                value={preparation}
                onChange={(event) => setPreparation(event.target.value)}
              />
            </label>
          </div>
          <fieldset>
            <legend>Zutatenpositionen bearbeiten</legend>
            <label>
              Weitere Zutat suchen
              <input
                type="search"
                value={ingredientSearch}
                placeholder="Mindestens zwei Zeichen"
                onChange={(event) => setIngredientSearch(event.target.value)}
              />
            </label>
            {ingredientSuggestions.isLoading ? (
              <p role="status">Zutaten werden gesucht …</p>
            ) : null}
            {ingredientSuggestions.error ? (
              <p role="alert" className="error-message">
                {ingredientSuggestions.error.message}
              </p>
            ) : null}
            {ingredientSuggestions.data?.length ? (
              <ul className="autocomplete-results">
                {ingredientSuggestions.data
                  .filter(
                    (ingredient) =>
                      !ingredients.some(
                        (row) => row.ingredient.id === ingredient.id,
                      ),
                  )
                  .map((ingredient) => (
                    <li key={ingredient.id}>
                      <button
                        type="button"
                        className="secondary-action"
                        aria-label={`${ingredient.name} zum Rezept hinzufügen`}
                        onClick={() => {
                          setIngredients((rows) => [
                            ...rows,
                            {
                              ingredient,
                              quantity: "1",
                              unit: "0",
                              countUnitName: "",
                              note: "",
                            },
                          ]);
                          setIngredientSearch("");
                        }}
                      >
                        {ingredient.name}
                      </button>
                    </li>
                  ))}
              </ul>
            ) : null}
            <div className="recipe-ingredient-list">
              {ingredients.map((row) => (
                <section
                  className="recipe-ingredient-row"
                  aria-label={`${row.ingredient.name} bearbeiten`}
                  key={row.ingredient.id}
                >
                  <h3>{row.ingredient.name}</h3>
                  <label>
                    Menge für {row.ingredient.name} bearbeiten
                    <input
                      required
                      type="number"
                      min="0.001"
                      step="0.001"
                      value={row.quantity}
                      onChange={(event) =>
                        updateIngredient(row.ingredient.id, {
                          quantity: event.target.value,
                        })
                      }
                    />
                  </label>
                  <label>
                    Einheit für {row.ingredient.name} bearbeiten
                    <select
                      value={row.unit}
                      onChange={(event) =>
                        updateIngredient(row.ingredient.id, {
                          unit: event.target.value,
                        })
                      }
                    >
                      <option value="0">Gramm</option>
                      <option value="1">Kilogramm</option>
                      <option value="2">Milliliter</option>
                      <option value="3">Liter</option>
                      <option value="4">Stück</option>
                      <option value="5">Benannte Zähleinheit</option>
                    </select>
                  </label>
                  {row.unit === "5" ? (
                    <label>
                      Name der Zähleinheit für {row.ingredient.name} bearbeiten
                      <input
                        required
                        value={row.countUnitName}
                        onChange={(event) =>
                          updateIngredient(row.ingredient.id, {
                            countUnitName: event.target.value,
                          })
                        }
                      />
                    </label>
                  ) : null}
                  <label>
                    Hinweis für {row.ingredient.name} bearbeiten
                    <input
                      value={row.note}
                      onChange={(event) =>
                        updateIngredient(row.ingredient.id, {
                          note: event.target.value,
                        })
                      }
                    />
                  </label>
                  <button
                    type="button"
                    className="text-action"
                    onClick={() =>
                      setIngredients((rows) =>
                        rows.filter(
                          (item) => item.ingredient.id !== row.ingredient.id,
                        ),
                      )
                    }
                  >
                    {row.ingredient.name} entfernen
                  </button>
                </section>
              ))}
            </div>
          </fieldset>
          <div className="camp-form-grid">
            <label className="full-row">
              Ernährungs-Tags bearbeiten
              <input
                value={dietaryTags}
                onChange={(event) => setDietaryTags(event.target.value)}
              />
            </label>
            <label className="full-row">
              Allergenhinweise bearbeiten
              <textarea
                value={allergenNotes}
                onChange={(event) => setAllergenNotes(event.target.value)}
              />
            </label>
            <label className="full-row">
              Küchenhinweise bearbeiten
              <textarea
                value={kitchenNotes}
                onChange={(event) => setKitchenNotes(event.target.value)}
              />
            </label>
          </div>
          {reviseRecipe.error ? (
            <p role="alert" className="error-message">
              {reviseRecipe.error.message}
            </p>
          ) : null}
          <div className="toolbar">
            <button
              className="primary-action"
              type="submit"
              disabled={reviseRecipe.isPending || ingredients.length === 0}
            >
              {reviseRecipe.isPending
                ? "Neue Rezeptversion wird gespeichert …"
                : "Neue Rezeptversion speichern"}
            </button>
            <button
              className="secondary-action"
              type="button"
              disabled={reviseRecipe.isPending}
              onClick={() => {
                setEditing(false);
                reviseRecipe.reset();
              }}
            >
              Bearbeitung abbrechen
            </button>
          </div>
        </form>
      ) : null}
      {current ? (
        <RecipeAttachmentsPanel
          organizationId={organizationId}
          recipeId={recipeId}
          recipeName={current.name}
          canUpload={canManage && !readOnly}
        />
      ) : null}
    </section>
  );
}

function MealDetailPanel({
  organizationId,
  campId,
  mealId,
  readOnly,
  onClose,
  onDeleted,
}: {
  organizationId: string;
  campId: string;
  mealId: string;
  readOnly: boolean;
  onClose: () => void;
  onDeleted: (name: string) => void;
}) {
  const queryClient = useQueryClient();
  const detailQueryKey = [organizationId, campId, "meal", mealId];
  const meal = useQuery({
    queryKey: detailQueryKey,
    queryFn: () =>
      getJson<MealDetail>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}`,
      ),
    retry: false,
  });
  const [notice, setNotice] = useState("");
  const [editing, setEditing] = useState(false);
  const [editName, setEditName] = useState("");
  const [editOverride, setEditOverride] = useState(false);
  const [editPortions, setEditPortions] = useState("");
  const [editScheduleEntryId, setEditScheduleEntryId] = useState("");
  const [recipeToAdd, setRecipeToAdd] = useState("");
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleteConfirmed, setDeleteConfirmed] = useState(false);
  const recipes = useQuery({
    queryKey: [organizationId, "catering", "recipes"],
    queryFn: () =>
      getJson<RecipeSummary[]>(
        `/api/v1/organizations/${organizationId}/catering/recipes`,
      ),
    retry: false,
  });
  const updateMeal = useMutation({
    mutationFn: () => {
      if (!meal.data) throw new Error("Die Mahlzeit ist noch nicht geladen.");
      return mutateCateringJson<MealDetail>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}`,
        "PUT",
        {
          name: editName,
          portionOverride: editOverride ? Number(editPortions) : null,
          scheduleEntryId: editScheduleEntryId || null,
          recipeIds: [],
        },
        meal.data.version,
        "Die Mahlzeit wurde zwischenzeitlich geändert. Schließe die Bearbeitung und öffne sie erneut.",
      );
    },
    onSuccess: async (updated) => {
      queryClient.setQueryData(detailQueryKey, updated);
      await queryClient.invalidateQueries({
        queryKey: [organizationId, campId, "meals"],
      });
      setEditing(false);
      setNotice(`${updated.name} wurde gespeichert.`);
    },
  });
  const addSnapshot = useMutation({
    mutationFn: () => {
      if (!meal.data) throw new Error("Die Mahlzeit ist noch nicht geladen.");
      return mutateCateringJson<MealDetail>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}/recipes`,
        "POST",
        { recipeId: recipeToAdd },
        meal.data.version,
      );
    },
    onSuccess: (updated) => {
      const added = recipes.data?.find((recipe) => recipe.id === recipeToAdd);
      queryClient.setQueryData(detailQueryKey, updated);
      setRecipeToAdd("");
      setNotice(`${added?.name ?? "Rezept"} wurde hinzugefügt.`);
    },
  });
  const removeSnapshot = useMutation({
    mutationFn: (snapshot: MealRecipeSnapshot) => {
      if (!meal.data) throw new Error("Die Mahlzeit ist noch nicht geladen.");
      return mutateCateringJson<MealDetail>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}/recipes/${snapshot.id}`,
        "DELETE",
        {},
        meal.data.version,
      );
    },
    onSuccess: (updated, snapshot) => {
      queryClient.setQueryData(detailQueryKey, updated);
      setNotice(`${snapshot.name} wurde entfernt.`);
    },
  });
  const deleteMeal = useMutation({
    mutationFn: async () => {
      if (!meal.data) throw new Error("Die Mahlzeit ist noch nicht geladen.");
      const name = meal.data.name;
      await mutateCateringJson<void>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}`,
        "DELETE",
        {},
        meal.data.version,
      );
      return name;
    },
    onSuccess: async (name) => {
      await queryClient.invalidateQueries({
        queryKey: [organizationId, campId, "meals"],
      });
      onDeleted(name);
    },
  });
  const refreshSnapshot = useMutation({
    mutationFn: (snapshot: MealRecipeSnapshot) => {
      const current = meal.data;
      if (!current) throw new Error("Die Mahlzeit ist noch nicht geladen.");
      return mutateCateringJson<MealDetail>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}/recipes/${snapshot.id}/refresh`,
        "POST",
        {},
        current.version,
        "Die Mahlzeit wurde zwischenzeitlich geändert. Schließe die Details und öffne sie erneut.",
      );
    },
    onSuccess: async (revised, snapshot) => {
      queryClient.setQueryData(detailQueryKey, revised);
      await queryClient.invalidateQueries({
        queryKey: [organizationId, campId, "meals"],
      });
      setNotice(
        `${snapshot.name} wurde ausdrücklich auf Rezeptversion ${snapshot.latestRecipeVersionNumber} aktualisiert.`,
      );
    },
  });
  const current = meal.data;

  return (
    <section className="meal-detail-panel" aria-label="Mahlzeitdetails">
      <div className="section-heading">
        <div>
          <p className="eyebrow">
            {current
              ? `${current.effectivePortions} Personen`
              : "Mahlzeit wird geladen"}
          </p>
          <h2>
            {current ? `Mahlzeitdetails: ${current.name}` : "Mahlzeitdetails"}
          </h2>
        </div>
        <button type="button" className="secondary-action" onClick={onClose}>
          Mahlzeit schließen
        </button>
      </div>
      <QueryState loading={meal.isLoading} error={meal.error} />
      {notice ? (
        <p className="form-feedback" role="status">
          {notice}
        </p>
      ) : null}
      {current ? (
        <>
          <p className="form-hint">
            {current.portionOverride === null
              ? `Verwendet den Camp-Standard von ${current.campDefaultPortions} Personen.`
              : `Überschreibt den Camp-Standard von ${current.campDefaultPortions} mit ${current.portionOverride} Personen.`}
            {current.scheduleEntryId
              ? " Mit einem Zeitplaneintrag verknüpft."
              : " Ohne Zeitplaneintrag."}
          </p>
          {!readOnly && !editing ? (
            <button
              type="button"
              className="secondary-action"
              onClick={() => {
                setEditName(current.name);
                setEditOverride(current.portionOverride !== null);
                setEditPortions(
                  String(
                    current.portionOverride ?? current.campDefaultPortions,
                  ),
                );
                setEditScheduleEntryId(current.scheduleEntryId ?? "");
                setEditing(true);
                setNotice("");
              }}
            >
              Mahlzeit bearbeiten
            </button>
          ) : null}
          {editing ? (
            <form
              className="schedule-create-form meal-create-form"
              aria-label={`${current.name} bearbeiten`}
              onSubmit={(event) => {
                event.preventDefault();
                updateMeal.mutate();
              }}
            >
              <label>
                Name bearbeiten
                <input
                  required
                  value={editName}
                  onChange={(event) => setEditName(event.target.value)}
                />
              </label>
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={editOverride}
                  onChange={(event) => setEditOverride(event.target.checked)}
                />
                Personenzahl weiter überschreiben
              </label>
              {editOverride ? (
                <label>
                  Personenzahl bearbeiten
                  <input
                    required
                    type="number"
                    min="1"
                    step="1"
                    value={editPortions}
                    onChange={(event) => setEditPortions(event.target.value)}
                  />
                </label>
              ) : null}
              <label>
                Zeitplaneintrag-ID bearbeiten
                <input
                  value={editScheduleEntryId}
                  onChange={(event) =>
                    setEditScheduleEntryId(event.target.value)
                  }
                />
              </label>
              {updateMeal.error ? (
                <p role="alert" className="error-message">
                  {updateMeal.error.message}
                </p>
              ) : null}
              <div className="toolbar">
                <button
                  type="submit"
                  className="primary-action"
                  disabled={updateMeal.isPending}
                >
                  Änderungen speichern
                </button>
                <button
                  type="button"
                  className="secondary-action"
                  onClick={() => setEditing(false)}
                >
                  Abbrechen
                </button>
              </div>
            </form>
          ) : null}
          {!readOnly ? (
            <form
              className="meal-snapshot-add"
              onSubmit={(event) => {
                event.preventDefault();
                addSnapshot.mutate();
              }}
            >
              <label>
                Rezept-Snapshot hinzufügen
                <select
                  value={recipeToAdd}
                  onChange={(event) => setRecipeToAdd(event.target.value)}
                >
                  <option value="">Rezept auswählen</option>
                  {recipes.data
                    ?.filter(
                      (recipe) =>
                        !current.recipeSnapshots.some(
                          (snapshot) => snapshot.sourceRecipeId === recipe.id,
                        ),
                    )
                    .map((recipe) => (
                      <option key={recipe.id} value={recipe.id}>
                        {recipe.name}
                      </option>
                    ))}
                </select>
              </label>
              <button
                type="submit"
                className="secondary-action"
                disabled={!recipeToAdd || addSnapshot.isPending}
              >
                Snapshot hinzufügen
              </button>
            </form>
          ) : null}
          <div className="meal-snapshot-list">
            {current.recipeSnapshots.map((snapshot) => (
              <article className="meal-snapshot-card" key={snapshot.id}>
                <div className="section-heading">
                  <div>
                    <p className="eyebrow">
                      Rezeptversion {snapshot.sourceRecipeVersionNumber} von{" "}
                      {snapshot.latestRecipeVersionNumber}
                    </p>
                    <h3>{snapshot.name}</h3>
                  </div>
                  {snapshot.refreshAvailable ? (
                    <span className="status warn">Neue Version verfügbar</span>
                  ) : (
                    <span className="status done">Aktuell</span>
                  )}
                </div>
                <p>{snapshot.description}</p>
                <h4>Skalierte Zutaten</h4>
                <ul className="recipe-detail-ingredients">
                  {snapshot.ingredients.map((ingredient) => (
                    <li key={ingredient.id}>
                      <span>
                        {formatRecipeQuantity(ingredient.scaledQuantity)}{" "}
                        {ingredient.ingredientName}
                      </span>
                      {ingredient.note ? (
                        <small>{ingredient.note}</small>
                      ) : null}
                    </li>
                  ))}
                </ul>
                {snapshot.allergenNotes ? (
                  <p>
                    <strong>Allergenhinweis:</strong> {snapshot.allergenNotes}
                  </p>
                ) : null}
                {snapshot.refreshAvailable ? (
                  <button
                    type="button"
                    className="primary-action"
                    disabled={readOnly || refreshSnapshot.isPending}
                    onClick={() => {
                      setNotice("");
                      refreshSnapshot.mutate(snapshot);
                    }}
                  >
                    {snapshot.name} auf Version{" "}
                    {snapshot.latestRecipeVersionNumber} aktualisieren
                  </button>
                ) : null}
                {!readOnly ? (
                  <button
                    type="button"
                    className="text-action"
                    disabled={removeSnapshot.isPending}
                    onClick={() => removeSnapshot.mutate(snapshot)}
                  >
                    {snapshot.name} aus Mahlzeit entfernen
                  </button>
                ) : null}
              </article>
            ))}
            {current.recipeSnapshots.length === 0 ? (
              <p className="empty-state">
                Diese Mahlzeit enthält noch keinen Rezept-Snapshot.
              </p>
            ) : null}
          </div>
          {refreshSnapshot.error ? (
            <p role="alert" className="error-message">
              {refreshSnapshot.error.message}
            </p>
          ) : null}
          {addSnapshot.error || removeSnapshot.error ? (
            <p role="alert" className="error-message">
              {addSnapshot.error?.message ?? removeSnapshot.error?.message}
            </p>
          ) : null}
          {!readOnly && !confirmDelete ? (
            <button
              type="button"
              className="danger-action"
              onClick={() => {
                setConfirmDelete(true);
                setDeleteConfirmed(false);
              }}
            >
              Mahlzeit in Papierkorb verschieben
            </button>
          ) : null}
          {confirmDelete ? (
            <section
              className="confirmation-panel"
              aria-label="Mahlzeit löschen"
            >
              <p>
                Die Mahlzeit bleibt 30 Tage im Papierkorb und kann dort
                wiederhergestellt werden.
              </p>
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={deleteConfirmed}
                  onChange={(event) => setDeleteConfirmed(event.target.checked)}
                />
                Ich möchte diese Mahlzeit in den Papierkorb verschieben.
              </label>
              {deleteMeal.error ? (
                <p role="alert" className="error-message">
                  {deleteMeal.error.message}
                </p>
              ) : null}
              <div className="toolbar">
                <button
                  type="button"
                  className="danger-action"
                  disabled={!deleteConfirmed || deleteMeal.isPending}
                  onClick={() => deleteMeal.mutate()}
                >
                  Verschieben bestätigen
                </button>
                <button
                  type="button"
                  className="secondary-action"
                  onClick={() => setConfirmDelete(false)}
                >
                  Abbrechen
                </button>
              </div>
            </section>
          ) : null}
        </>
      ) : null}
    </section>
  );
}

function MealsPage({ offline }: { offline: boolean }) {
  const { organizationId, organizationRole, campId, camp } = useCampRuntime();
  const canManageLibrary = organizationRole === 0 || organizationRole === 1;
  const queryClient = useQueryClient();
  const path = `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals`;
  const query = useCampQuery<Meal[]>("meals", path);
  const recipes = useQuery({
    queryKey: [organizationId, "catering", "recipes"],
    queryFn: () =>
      getJson<RecipeSummary[]>(
        `/api/v1/organizations/${organizationId}/catering/recipes`,
      ),
    retry: false,
  });
  const [showRecipeForm, setShowRecipeForm] = useState(false);
  const [showIngredientLibrary, setShowIngredientLibrary] = useState(false);
  const [selectedRecipeId, setSelectedRecipeId] = useState<string | null>(null);
  const [showMealForm, setShowMealForm] = useState(false);
  const [selectedMealId, setSelectedMealId] = useState<string | null>(null);
  const [mealName, setMealName] = useState("");
  const [overridePortions, setOverridePortions] = useState(false);
  const [mealPortions, setMealPortions] = useState(
    String(camp.defaultPortions),
  );
  const [mealScheduleEntryId, setMealScheduleEntryId] = useState("");
  const [mealRecipeIds, setMealRecipeIds] = useState<string[]>([]);
  const [mealNotice, setMealNotice] = useState("");
  const [recipeName, setRecipeName] = useState("");
  const [recipeDescription, setRecipeDescription] = useState("");
  const [recipePreparation, setRecipePreparation] = useState("");
  const [recipeBasePortions, setRecipeBasePortions] = useState("4");
  const [recipeDietaryTags, setRecipeDietaryTags] = useState("");
  const [recipeAllergenNotes, setRecipeAllergenNotes] = useState("");
  const [recipeKitchenNotes, setRecipeKitchenNotes] = useState("");
  const [ingredientSearch, setIngredientSearch] = useState("");
  const [recipeIngredients, setRecipeIngredients] = useState<
    RecipeIngredientDraft[]
  >([]);
  const [recipeFilter, setRecipeFilter] = useState("");
  const [recipeNotice, setRecipeNotice] = useState("");
  const ingredientSuggestions = useQuery({
    queryKey: [
      organizationId,
      "catering",
      "ingredients",
      ingredientSearch.trim(),
    ],
    queryFn: () =>
      getJson<Ingredient[]>(
        `/api/v1/organizations/${organizationId}/catering/ingredients?query=${encodeURIComponent(ingredientSearch.trim())}&limit=10`,
      ),
    enabled: showRecipeForm && ingredientSearch.trim().length >= 2,
    retry: false,
  });
  const mealScheduleEntries = useQuery({
    queryKey: [organizationId, campId, "meal-schedule-candidates"],
    queryFn: () =>
      getJson<ScheduleEntry[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/schedule?fromDate=${camp.startsOn}&toDateExclusive=${nextLocalDate(camp.endsOn)}`,
      ),
    enabled: showMealForm,
    retry: false,
  });
  const createMeal = useMutation({
    mutationFn: () =>
      mutateCateringJson<MealDetail>(path, "POST", {
        name: mealName,
        portionOverride: overridePortions ? Number(mealPortions) : null,
        scheduleEntryId: mealScheduleEntryId || null,
        recipeIds: mealRecipeIds,
      }),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({
        queryKey: [organizationId, campId, "meals"],
      });
      setMealNotice(
        `${created.name} wurde mit ${created.effectivePortions} Personen und ${created.recipeSnapshots.length} Rezept-${created.recipeSnapshots.length === 1 ? "Snapshot" : "Snapshots"} angelegt.`,
      );
      setShowMealForm(false);
      setMealName("");
      setOverridePortions(false);
      setMealPortions(String(camp.defaultPortions));
      setMealScheduleEntryId("");
      setMealRecipeIds([]);
    },
  });
  const createRecipe = useMutation({
    mutationFn: async () => {
      const token = await getAntiforgeryToken();
      const response = await fetch(
        `/api/v1/organizations/${organizationId}/catering/recipes`,
        {
          method: "POST",
          credentials: "same-origin",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token,
          },
          body: JSON.stringify({
            name: recipeName,
            description: recipeDescription,
            preparation: recipePreparation,
            basePortions: Number(recipeBasePortions),
            ingredients: recipeIngredients.map((row) => ({
              ingredientId: row.ingredient.id,
              quantity: {
                value: Number(row.quantity),
                unit: Number(row.unit),
                countUnitName:
                  row.unit === "5" ? row.countUnitName || null : null,
              },
              note: row.note || null,
            })),
            dietaryTags: Array.from(
              new Set(
                recipeDietaryTags
                  .split(/[,;\n]/)
                  .map((tag) => tag.trim())
                  .filter(Boolean),
              ),
            ),
            allergenNotes: recipeAllergenNotes || null,
            kitchenNotes: recipeKitchenNotes || null,
          }),
        },
      );
      if (!response.ok) {
        const problem = (await response.json().catch(() => null)) as {
          detail?: string;
        } | null;
        throw new Error(
          problem?.detail ?? "Das Rezept konnte nicht gespeichert werden.",
        );
      }
      return (await response.json()) as RecipeCreateResult;
    },
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({
        queryKey: [organizationId, "catering", "recipes"],
      });
      setRecipeNotice(
        `${created.currentVersion.name} wurde als Rezeptversion ${created.currentVersion.number} gespeichert.`,
      );
      setShowRecipeForm(false);
      setRecipeName("");
      setRecipeDescription("");
      setRecipePreparation("");
      setRecipeBasePortions("4");
      setRecipeDietaryTags("");
      setRecipeAllergenNotes("");
      setRecipeKitchenNotes("");
      setIngredientSearch("");
      setRecipeIngredients([]);
    },
  });
  const meals =
    query.data ??
    (offline ? ((loadOfflineSnapshot()?.meals ?? []) as Meal[]) : []);
  const filteredRecipes = (recipes.data ?? []).filter((recipe) =>
    recipe.name
      .toLocaleLowerCase("de-DE")
      .includes(recipeFilter.trim().toLocaleLowerCase("de-DE")),
  );
  const updateRecipeIngredient = (
    ingredientId: string,
    changes: Partial<RecipeIngredientDraft>,
  ) =>
    setRecipeIngredients((current) =>
      current.map((row) =>
        row.ingredient.id === ingredientId ? { ...row, ...changes } : row,
      ),
    );
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
        <button
          type="button"
          className="primary-action"
          disabled={offline}
          aria-expanded={showMealForm}
          onClick={() => {
            setShowMealForm((current) => !current);
            setSelectedMealId(null);
            setSelectedRecipeId(null);
            setShowRecipeForm(false);
            setShowIngredientLibrary(false);
            setMealNotice("");
          }}
        >
          {showMealForm ? "Mahlzeitformular schließen" : "Mahlzeit planen"}
        </button>
        <button
          type="button"
          className="secondary-action"
          disabled={offline || !canManageLibrary}
          aria-expanded={showRecipeForm}
          title={
            canManageLibrary
              ? undefined
              : "Nur Owner und Organisations-Admins verwalten Rezepte."
          }
          onClick={() => {
            setShowRecipeForm((current) => !current);
            setShowIngredientLibrary(false);
            setSelectedRecipeId(null);
            setShowMealForm(false);
            setSelectedMealId(null);
            setRecipeNotice("");
          }}
        >
          {showRecipeForm ? "Rezeptformular schließen" : "Rezept anlegen"}
        </button>
        <button
          type="button"
          className="secondary-action"
          disabled={offline || !canManageLibrary}
          aria-expanded={showIngredientLibrary}
          title={
            canManageLibrary
              ? undefined
              : "Nur Owner und Organisations-Admins verwalten Zutaten."
          }
          onClick={() => {
            setShowIngredientLibrary((current) => !current);
            setShowRecipeForm(false);
            setSelectedRecipeId(null);
            setShowMealForm(false);
            setSelectedMealId(null);
            setRecipeNotice("");
          }}
        >
          {showIngredientLibrary
            ? "Zutatenverwaltung schließen"
            : "Zutaten verwalten"}
        </button>
        <label className="search-field">
          Rezepte suchen
          <input
            type="search"
            placeholder="z. B. Kartoffelsuppe"
            value={recipeFilter}
            onChange={(event) => setRecipeFilter(event.target.value)}
          />
        </label>
      </div>
      {mealNotice ? (
        <p className="form-feedback" role="status">
          {mealNotice}
        </p>
      ) : null}
      {showMealForm ? (
        <form
          className="schedule-create-form meal-create-form"
          aria-labelledby="new-meal-heading"
          onSubmit={(event) => {
            event.preventDefault();
            setMealNotice("");
            createMeal.mutate();
          }}
        >
          <h2 id="new-meal-heading">Neue Mahlzeit</h2>
          <p className="form-hint">
            Camp-Standard: {camp.defaultPortions} Personen
          </p>
          <div className="camp-form-grid">
            <label>
              Name der Mahlzeit
              <input
                required
                value={mealName}
                onChange={(event) => setMealName(event.target.value)}
              />
            </label>
            <label>
              Zeitplaneintrag
              <select
                value={mealScheduleEntryId}
                onChange={(event) => setMealScheduleEntryId(event.target.value)}
              >
                <option value="">Nicht mit dem Zeitplan verknüpfen</option>
                {mealScheduleEntries.data?.map((entry) => (
                  <option key={entry.id} value={entry.id}>
                    {entry.title}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={overridePortions}
              onChange={(event) => setOverridePortions(event.target.checked)}
            />
            Personenzahl überschreiben
          </label>
          {overridePortions ? (
            <label>
              Personenzahl
              <input
                required
                type="number"
                min="1"
                step="1"
                value={mealPortions}
                onChange={(event) => setMealPortions(event.target.value)}
              />
            </label>
          ) : null}
          <fieldset>
            <legend>Rezept-Snapshots</legend>
            <p className="form-hint">
              Ausgewählte Rezepte werden in ihrem aktuellen Stand kopiert und
              später nicht still verändert.
            </p>
            <div className="meal-recipe-options">
              {(recipes.data ?? []).map((recipe) => (
                <label className="checkbox-label" key={recipe.id}>
                  <input
                    type="checkbox"
                    checked={mealRecipeIds.includes(recipe.id)}
                    onChange={(event) =>
                      setMealRecipeIds((current) =>
                        event.target.checked
                          ? [...current, recipe.id]
                          : current.filter((id) => id !== recipe.id),
                      )
                    }
                  />
                  {recipe.name} als Snapshot hinzufügen
                </label>
              ))}
              {!recipes.isLoading && recipes.data?.length === 0 ? (
                <p className="empty-state">
                  Noch kein Bibliotheksrezept vorhanden. Die Mahlzeit kann
                  trotzdem ohne Rezept angelegt werden.
                </p>
              ) : null}
            </div>
          </fieldset>
          <QueryState
            loading={mealScheduleEntries.isLoading}
            error={mealScheduleEntries.error}
          />
          {createMeal.error ? (
            <p role="alert" className="error-message">
              {createMeal.error.message}
            </p>
          ) : null}
          <div className="toolbar">
            <button
              type="submit"
              className="primary-action"
              disabled={createMeal.isPending}
            >
              {createMeal.isPending
                ? "Mahlzeit wird gespeichert …"
                : "Mahlzeit speichern"}
            </button>
            <button
              type="button"
              className="secondary-action"
              disabled={createMeal.isPending}
              onClick={() => setShowMealForm(false)}
            >
              Abbrechen
            </button>
          </div>
        </form>
      ) : null}
      {showIngredientLibrary ? (
        <IngredientLibraryPanel
          organizationId={organizationId}
          onClose={() => setShowIngredientLibrary(false)}
        />
      ) : null}
      {selectedRecipeId ? (
        <RecipeDetailPanel
          organizationId={organizationId}
          recipeId={selectedRecipeId}
          canManage={canManageLibrary}
          readOnly={offline}
          onClose={() => setSelectedRecipeId(null)}
        />
      ) : null}
      {selectedMealId ? (
        <MealDetailPanel
          organizationId={organizationId}
          campId={campId}
          mealId={selectedMealId}
          readOnly={offline}
          onClose={() => setSelectedMealId(null)}
          onDeleted={(name) => {
            setSelectedMealId(null);
            setMealNotice(`${name} wurde in den Papierkorb verschoben.`);
          }}
        />
      ) : null}
      {recipeNotice ? (
        <p className="form-feedback" role="status">
          {recipeNotice}
        </p>
      ) : null}
      {showRecipeForm ? (
        <form
          className="schedule-create-form recipe-form"
          aria-labelledby="new-recipe-heading"
          onSubmit={(event) => {
            event.preventDefault();
            setRecipeNotice("");
            createRecipe.mutate();
          }}
        >
          <h2 id="new-recipe-heading">Neues Rezept</h2>
          <div className="camp-form-grid">
            <label>
              Rezeptname
              <input
                required
                value={recipeName}
                onChange={(event) => setRecipeName(event.target.value)}
              />
            </label>
            <label>
              Basisportionen
              <input
                required
                type="number"
                min="1"
                step="1"
                value={recipeBasePortions}
                onChange={(event) => setRecipeBasePortions(event.target.value)}
              />
            </label>
            <label className="full-row">
              Beschreibung
              <textarea
                required
                value={recipeDescription}
                onChange={(event) => setRecipeDescription(event.target.value)}
              />
            </label>
            <label className="full-row">
              Zubereitung
              <textarea
                required
                value={recipePreparation}
                onChange={(event) => setRecipePreparation(event.target.value)}
              />
            </label>
          </div>
          <fieldset>
            <legend>Zutatenpositionen</legend>
            <label>
              Zutat suchen
              <input
                type="search"
                value={ingredientSearch}
                placeholder="Mindestens zwei Zeichen"
                onChange={(event) => setIngredientSearch(event.target.value)}
              />
            </label>
            {ingredientSuggestions.isLoading ? (
              <p role="status">Zutaten werden gesucht …</p>
            ) : null}
            {ingredientSuggestions.error ? (
              <p role="alert" className="error-message">
                {ingredientSuggestions.error.message}
              </p>
            ) : null}
            {ingredientSearch.trim().length >= 2 &&
            ingredientSuggestions.data?.length === 0 ? (
              <p className="empty-state">Keine passende Zutat gefunden.</p>
            ) : null}
            {ingredientSuggestions.data?.length ? (
              <ul className="autocomplete-results">
                {ingredientSuggestions.data
                  .filter(
                    (ingredient) =>
                      !recipeIngredients.some(
                        (row) => row.ingredient.id === ingredient.id,
                      ),
                  )
                  .map((ingredient) => (
                    <li key={ingredient.id}>
                      <button
                        type="button"
                        className="secondary-action"
                        aria-label={`${ingredient.name} hinzufügen`}
                        onClick={() => {
                          setRecipeIngredients((current) => [
                            ...current,
                            {
                              ingredient,
                              quantity: "1",
                              unit: "0",
                              countUnitName: "",
                              note: "",
                            },
                          ]);
                          setIngredientSearch("");
                        }}
                      >
                        {ingredient.name}
                      </button>
                    </li>
                  ))}
              </ul>
            ) : null}
            {recipeIngredients.length === 0 ? (
              <p className="form-hint">
                Füge mindestens eine Zutat aus der Organisationsbibliothek
                hinzu.
              </p>
            ) : (
              <div className="recipe-ingredient-list">
                {recipeIngredients.map((row) => (
                  <section
                    className="recipe-ingredient-row"
                    aria-label={row.ingredient.name}
                    key={row.ingredient.id}
                  >
                    <h3>{row.ingredient.name}</h3>
                    <label>
                      Menge für {row.ingredient.name}
                      <input
                        required
                        type="number"
                        min="0.001"
                        step="0.001"
                        value={row.quantity}
                        onChange={(event) =>
                          updateRecipeIngredient(row.ingredient.id, {
                            quantity: event.target.value,
                          })
                        }
                      />
                    </label>
                    <label>
                      Einheit für {row.ingredient.name}
                      <select
                        value={row.unit}
                        onChange={(event) =>
                          updateRecipeIngredient(row.ingredient.id, {
                            unit: event.target.value,
                          })
                        }
                      >
                        <option value="0">Gramm</option>
                        <option value="1">Kilogramm</option>
                        <option value="2">Milliliter</option>
                        <option value="3">Liter</option>
                        <option value="4">Stück</option>
                        <option value="5">Benannte Zähleinheit</option>
                      </select>
                    </label>
                    {row.unit === "5" ? (
                      <label>
                        Name der Zähleinheit für {row.ingredient.name}
                        <input
                          required
                          value={row.countUnitName}
                          onChange={(event) =>
                            updateRecipeIngredient(row.ingredient.id, {
                              countUnitName: event.target.value,
                            })
                          }
                        />
                      </label>
                    ) : null}
                    <label>
                      Hinweis für {row.ingredient.name}
                      <input
                        value={row.note}
                        onChange={(event) =>
                          updateRecipeIngredient(row.ingredient.id, {
                            note: event.target.value,
                          })
                        }
                      />
                    </label>
                    <button
                      type="button"
                      className="text-action"
                      onClick={() =>
                        setRecipeIngredients((current) =>
                          current.filter(
                            (item) => item.ingredient.id !== row.ingredient.id,
                          ),
                        )
                      }
                    >
                      {row.ingredient.name} entfernen
                    </button>
                  </section>
                ))}
              </div>
            )}
          </fieldset>
          <div className="camp-form-grid">
            <label className="full-row">
              Ernährungs-Tags
              <input
                value={recipeDietaryTags}
                placeholder="z. B. vegetarisch, glutenfrei"
                onChange={(event) => setRecipeDietaryTags(event.target.value)}
              />
            </label>
            <label className="full-row">
              Allergenhinweise
              <textarea
                value={recipeAllergenNotes}
                onChange={(event) => setRecipeAllergenNotes(event.target.value)}
              />
            </label>
            <label className="full-row">
              Küchenhinweise
              <textarea
                value={recipeKitchenNotes}
                onChange={(event) => setRecipeKitchenNotes(event.target.value)}
              />
            </label>
          </div>
          {createRecipe.error ? (
            <p role="alert" className="error-message">
              {createRecipe.error.message}
            </p>
          ) : null}
          <div className="toolbar">
            <button
              className="primary-action"
              type="submit"
              disabled={
                createRecipe.isPending || recipeIngredients.length === 0
              }
            >
              {createRecipe.isPending
                ? "Rezept wird gespeichert …"
                : "Rezept speichern"}
            </button>
            <button
              className="secondary-action"
              type="button"
              disabled={createRecipe.isPending}
              onClick={() => setShowRecipeForm(false)}
            >
              Abbrechen
            </button>
          </div>
        </form>
      ) : null}
      <section aria-labelledby="recipe-library-heading">
        <div className="section-heading">
          <h2 id="recipe-library-heading">Rezeptbibliothek</h2>
        </div>
        <QueryState loading={recipes.isLoading} error={recipes.error} />
        <div className="card-grid">
          {filteredRecipes.map((recipe) => (
            <article className="card" key={recipe.id}>
              <p className="eyebrow">
                Version {recipe.currentVersionNumber} · {recipe.basePortions}{" "}
                Basisportionen
              </p>
              <h3>{recipe.name}</h3>
              <button
                className="secondary-action"
                type="button"
                disabled={offline}
                aria-label={`${recipe.name} öffnen`}
                aria-expanded={selectedRecipeId === recipe.id}
                onClick={() => {
                  setSelectedRecipeId(recipe.id);
                  setSelectedMealId(null);
                  setShowMealForm(false);
                  setShowRecipeForm(false);
                  setShowIngredientLibrary(false);
                  setRecipeNotice("");
                }}
              >
                Rezept öffnen
              </button>
            </article>
          ))}
          {!recipes.isLoading && filteredRecipes.length === 0 ? (
            <p className="empty-state">Noch kein passendes Rezept vorhanden.</p>
          ) : null}
        </div>
      </section>
      <section aria-labelledby="meal-list-heading">
        <div className="section-heading">
          <h2 id="meal-list-heading">Geplante Mahlzeiten</h2>
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
              <button
                type="button"
                className="secondary-action"
                aria-label={`${meal.name} öffnen`}
                aria-expanded={selectedMealId === meal.id}
                disabled={offline}
                onClick={() => {
                  setSelectedMealId(meal.id);
                  setSelectedRecipeId(null);
                  setShowMealForm(false);
                  setShowRecipeForm(false);
                  setShowIngredientLibrary(false);
                  setMealNotice("");
                }}
              >
                Mahlzeit öffnen
              </button>
            </article>
          ))}
          {meals.length === 0 && (
            <p className="empty-state">Noch keine Mahlzeit geplant.</p>
          )}
        </div>
      </section>
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
  const { organizationId, campId } = useCampRuntime();
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
  const { organizationId, campId } = useCampRuntime();
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
  const { organizationId, campId, camp } = useCampRuntime();
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
