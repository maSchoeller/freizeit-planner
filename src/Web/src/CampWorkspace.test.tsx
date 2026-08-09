import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";

type CalendarMutationInfo = {
  event: {
    id: string;
    allDay: boolean;
    startStr: string;
    endStr: string;
  };
  revert: () => void;
};

const calendarMock = vi.hoisted(() => ({
  props: undefined as
    | {
        eventDrop?: (info: CalendarMutationInfo) => void;
        eventResize?: (info: CalendarMutationInfo) => void;
        initialDate?: string;
        timeZone?: string;
      }
    | undefined,
}));

vi.mock("@fullcalendar/react", () => ({
  default: (props: NonNullable<typeof calendarMock.props>) => {
    calendarMock.props = props;
    return <div data-testid="calendar" />;
  },
}));

function renderRoute(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  if (path.includes("/o/sonnenhoehe/camps/sommerfreizeit-2026")) {
    queryClient.setQueryData(
      ["camp-workspace", "sonnenhoehe", "sommerfreizeit-2026"],
      {
        organizationId: "20000000-0000-0000-0000-000000000001",
        organizationName: "Sonnenhöhe e. V.",
        organizationSlug: "sonnenhoehe",
        organizationRole: 1,
        campId: "30000000-0000-0000-0000-000000000001",
        campSlug: "sommerfreizeit-2026",
        campBase: "/o/sonnenhoehe/camps/sommerfreizeit-2026",
        camp: {
          id: "30000000-0000-0000-0000-000000000001",
          organizationId: "20000000-0000-0000-0000-000000000001",
          name: "Sommerfreizeit 2026",
          slug: "sommerfreizeit-2026",
          description: "Gemeinsame Woche am See",
          startsOn: "2026-08-01",
          endsOn: "2026-08-08",
          timeZoneId: "Europe/Berlin",
          defaultPortions: 42,
          status: 0,
          period: 1,
          version: 4,
        },
      },
    );
  }
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function requestPath(request: RequestInfo | URL) {
  if (typeof request === "string") return request;
  return request instanceof URL ? request.toString() : request.url;
}

describe("camp workspace", () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    localStorage.clear();
    calendarMock.props = undefined;
  });

  it("resolves a speaking camp route and keeps an archived camp read-only", async () => {
    const organizationId = "21000000-0000-0000-0000-000000000001";
    const campId = "31000000-0000-0000-0000-000000000001";
    const fetchMock = vi.fn((request: RequestInfo | URL) => {
      const path = requestPath(request);
      if (path === "/api/v1/account/memberships")
        return Promise.resolve(
          new Response(
            JSON.stringify([
              {
                organizationId,
                organizationName: "Nordlicht e. V.",
                organizationSlug: "nordlicht",
                role: 4,
              },
            ]),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      if (path.includes("/camps/by-slug/winterfreizeit"))
        return Promise.resolve(
          new Response(
            JSON.stringify({
              id: campId,
              organizationId,
              name: "Winterfreizeit",
              slug: "winterfreizeit",
              description: null,
              startsOn: "2027-01-02",
              endsOn: "2027-01-09",
              timeZoneId: "Europe/Berlin",
              defaultPortions: 24,
              status: 1,
              period: 0,
              version: 3,
            }),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      return Promise.resolve(
        new Response(JSON.stringify([]), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );
    });
    vi.stubGlobal("fetch", fetchMock);
    renderRoute("/o/nordlicht/camps/winterfreizeit/tagesplan");

    expect(await screen.findByText("Winterfreizeit")).toBeInTheDocument();
    expect(
      screen.getByText(/Archiviert · nur lesen\. Inhalte bleiben lesbar/),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Eintrag erstellen" }),
    ).toBeDisabled();
    expect(calendarMock.props).toMatchObject({
      initialDate: "2027-01-02",
      timeZone: "Europe/Berlin",
    });
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(
        `/organizations/${organizationId}/camps/${campId}/schedule?fromDate=2027-01-02&toDateExclusive=2027-01-10`,
      ),
      expect.objectContaining({ credentials: "same-origin" }),
    );
  });

  it("renders the next camp dashboard from live planning data", async () => {
    const organizationId = "21000000-0000-0000-0000-000000000001";
    const campId = "31000000-0000-0000-0000-000000000001";
    const userId = "11000000-0000-0000-0000-000000000001";
    const fetchMock = vi.fn((request: RequestInfo | URL) => {
      const path = requestPath(request);
      let body: unknown = [];
      if (path === "/api/v1/account/memberships")
        body = [
          {
            organizationId,
            organizationName: "Nordlicht e. V.",
            organizationSlug: "nordlicht",
            role: 3,
          },
        ];
      else if (path.includes("/camps/by-slug/winterfreizeit"))
        body = {
          id: campId,
          organizationId,
          name: "Winterfreizeit",
          slug: "winterfreizeit",
          description: null,
          startsOn: "2027-01-02",
          endsOn: "2027-01-09",
          timeZoneId: "Europe/Berlin",
          defaultPortions: 24,
          status: 0,
          period: 0,
          version: 3,
        };
      else if (path === "/api/v1/account")
        body = {
          id: userId,
          email: "lea@example.test",
          displayName: "Lea Beispiel",
          deletionScheduledAt: null,
          isPlatformAdmin: false,
        };
      else if (path.includes("/schedule?"))
        body = [
          {
            id: "32000000-0000-0000-0000-000000000001",
            title: "Ankommen und Zimmer beziehen",
            location: "Haupthaus",
            category: "Programm",
            status: 1,
            responsibleUserIds: [userId],
            overlapsAnotherEntry: false,
            timing: {
              isAllDay: false,
              startsAtUtc: "2027-01-02T14:00:00Z",
              endsAtUtc: "2027-01-02T15:00:00Z",
            },
            version: 1,
          },
          {
            id: "32000000-0000-0000-0000-000000000002",
            title: "Frühstück am Sonntag",
            location: "Speisesaal",
            category: "Essen",
            status: 0,
            responsibleUserIds: [],
            overlapsAnotherEntry: false,
            timing: {
              isAllDay: false,
              startsAtUtc: "2027-01-03T07:00:00Z",
              endsAtUtc: "2027-01-03T08:00:00Z",
            },
            version: 1,
          },
        ];
      else if (path.endsWith("/logistics/material"))
        body = [
          { id: "m1", name: "Namensschilder", status: 0 },
          { id: "m2", name: "Erste-Hilfe-Set", status: 2 },
        ];
      else if (path.endsWith("/logistics/shopping-lists"))
        body = [
          {
            id: "s1",
            name: "Anreise",
            openItemCount: 3,
            checkedItemCount: 1,
            version: 1,
            changeSequence: 1,
          },
        ];
      else if (path.includes("/activity?"))
        body = [
          {
            id: "a1",
            actorId: userId,
            kind: 0,
            objectType: "ScheduleEntry",
            title: "Ankommen und Zimmer beziehen",
            timestamp: "2026-12-20T10:00:00Z",
          },
        ];
      return Promise.resolve(
        new Response(JSON.stringify(body), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );
    });
    vi.stubGlobal("fetch", fetchMock);

    renderRoute("/o/nordlicht/camps/winterfreizeit");

    expect(
      await screen.findByRole("heading", { name: "Hallo, Lea Beispiel" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Nächster Tagesplan" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Samstag, 2. Januar 2027")).toBeInTheDocument();
    expect(
      screen.getByText("Ankommen und Zimmer beziehen"),
    ).toBeInTheDocument();
    expect(screen.queryByText("Frühstück am Sonntag")).not.toBeInTheDocument();

    const responsibilities = screen.getByRole("region", {
      name: "Meine Verantwortungen",
    });
    expect(await within(responsibilities).findByText("1")).toBeInTheDocument();
    const procurement = screen.getByRole("region", { name: "Beschaffung" });
    expect(await within(procurement).findByText("4")).toBeInTheDocument();
    expect(
      within(procurement).getByText("1 Material · 3 Einkauf"),
    ).toBeInTheDocument();
    expect(screen.queryByText("Geländespiel im Wald")).not.toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(
        `/organizations/${organizationId}/camps/${campId}/schedule?fromDate=2027-01-02&toDateExclusive=2027-01-10`,
      ),
      expect.objectContaining({ credentials: "same-origin" }),
    );
  });

  it("creates an organization recipe with an autocompleted decimal ingredient", async () => {
    const ingredientId = "41000000-0000-0000-0000-000000000001";
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/auth/antiforgery")
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (init?.method === "POST" && path.endsWith("/catering/recipes"))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                id: "42000000-0000-0000-0000-000000000001",
                organizationId: "20000000-0000-0000-0000-000000000001",
                currentVersion: {
                  number: 1,
                  name: "Kartoffelsuppe",
                  basePortions: 8,
                  ingredients: [],
                  dietaryTags: ["vegetarisch"],
                },
                version: 1,
              }),
              {
                status: 201,
                headers: { "Content-Type": "application/json", ETag: '"1"' },
              },
            ),
          );
        if (path.includes("/catering/ingredients?query="))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: ingredientId,
                  organizationId: "20000000-0000-0000-0000-000000000001",
                  name: "Kartoffeln",
                  isMerged: false,
                  mergedIntoIngredientId: null,
                  version: 2,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        return Promise.resolve(
          new Response(JSON.stringify([]), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          }),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/essen");

    await user.click(screen.getByRole("button", { name: "Rezept anlegen" }));
    expect(
      screen.getByRole("heading", { name: "Neues Rezept" }),
    ).toBeInTheDocument();

    await user.type(
      screen.getByRole("textbox", { name: "Rezeptname" }),
      "Kartoffelsuppe",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Beschreibung" }),
      "Wärmende Suppe",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Zubereitung" }),
      "Kartoffeln kochen und pürieren.",
    );
    await user.clear(
      screen.getByRole("spinbutton", { name: "Basisportionen" }),
    );
    await user.type(
      screen.getByRole("spinbutton", { name: "Basisportionen" }),
      "8",
    );
    await user.type(
      screen.getByRole("searchbox", { name: "Zutat suchen" }),
      "Kartoff",
    );
    await user.click(
      await screen.findByRole("button", { name: "Kartoffeln hinzufügen" }),
    );
    await user.clear(
      screen.getByRole("spinbutton", { name: "Menge für Kartoffeln" }),
    );
    await user.type(
      screen.getByRole("spinbutton", { name: "Menge für Kartoffeln" }),
      "1.5",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Einheit für Kartoffeln" }),
      "1",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Ernährungs-Tags" }),
      "vegetarisch",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Allergenhinweise" }),
      "Milch prüfen",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Küchenhinweise" }),
      "Pürierstab bereithalten",
    );
    await user.click(screen.getByRole("button", { name: "Rezept speichern" }));

    expect(
      await screen.findByText(
        "Kartoffelsuppe wurde als Rezeptversion 1 gespeichert.",
      ),
    ).toHaveAttribute("role", "status");
    const recipeCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("/catering/recipes") &&
        init?.method === "POST",
    );
    expect(recipeCall).toBeDefined();
    const init = recipeCall?.[1];
    expect(init?.headers).toMatchObject({ "X-CSRF-TOKEN": "csrf-token" });
    expect(typeof init?.body).toBe("string");
    const payload = JSON.parse(init?.body as string) as Record<string, unknown>;
    expect(payload).toMatchObject({
      name: "Kartoffelsuppe",
      description: "Wärmende Suppe",
      preparation: "Kartoffeln kochen und pürieren.",
      basePortions: 8,
      ingredients: [
        {
          ingredientId,
          quantity: { value: 1.5, unit: 1, countUnitName: null },
          note: null,
        },
      ],
      dietaryTags: ["vegetarisch"],
      allergenNotes: "Milch prüfen",
      kitchenNotes: "Pürierstab bereithalten",
    });
  });

  it("offers every planning area as a real route", () => {
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/logistik");
    expect(
      screen.getByRole("heading", { name: "Material & Einkaufslisten" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Andachten" })).toHaveAttribute(
      "href",
      "/o/sonnenhoehe/camps/sommerfreizeit-2026/andachten",
    );
    expect(
      screen.getByRole("checkbox", { name: "12 kg Kartoffeln" }),
    ).toBeEnabled();
  });

  it("marks offline mode and prevents planning writes", () => {
    vi.spyOn(window.navigator, "onLine", "get").mockReturnValue(false);
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/dateien");
    expect(screen.getByText(/^Offline/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /hochladen/ })).toBeDisabled();
  });

  it("searches the current camp and exposes real CSV downloads", async () => {
    const fetchMock = vi.fn((request: RequestInfo | URL) => {
      const path = requestPath(request);
      return Promise.resolve(
        new Response(
          JSON.stringify(
            path.endsWith("/trash")
              ? []
              : [
                  {
                    objectType: "Meal",
                    objectId: "40000000-0000-0000-0000-000000000001",
                    title: "Kartoffelsuppe aus der API",
                    metadata: { portions: "42" },
                    updatedAt: "2026-08-04T10:00:00Z",
                    version: 1,
                  },
                ],
          ),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      );
    });
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/suche");

    await user.type(
      screen.getByRole("searchbox", { name: "Camp durchsuchen" }),
      "Kartoffel",
    );

    expect(
      await screen.findByText("Kartoffelsuppe aus der API"),
    ).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining("/search?query=Kartoffel"),
      expect.objectContaining({ credentials: "same-origin" }),
    );
    expect(
      screen.getByRole("link", { name: "Zeitplan als CSV" }),
    ).toHaveAttribute("href", expect.stringContaining("/exports/schedule.csv"));
  });

  it("loads the camp trash and restores an item with antiforgery and version", async () => {
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/auth/antiforgery") {
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        }
        if (init?.method === "POST")
          return Promise.resolve(new Response(null, { status: 200 }));
        return Promise.resolve(
          new Response(
            JSON.stringify([
              {
                objectType: "Note",
                objectId: "41000000-0000-0000-0000-000000000001",
                title: "Packliste",
                deletedAt: "2026-08-09T10:00:00Z",
                purgeAt: "2026-09-08T10:00:00Z",
                version: 3,
                restorePath: "/api/v1/restore-note",
              },
            ]),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/suche");

    expect(await screen.findByText("Packliste")).toBeInTheDocument();
    await user.click(
      screen.getByRole("button", { name: "Packliste wiederherstellen" }),
    );

    const restoreCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request) === "/api/v1/restore-note" &&
        init?.method === "POST",
    );
    expect(restoreCall?.[1]?.headers).toEqual({
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"3"',
    });
  });

  it("requires an explicit linked-content choice before deleting a schedule entry", async () => {
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/auth/antiforgery") {
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        }
        if (init?.method === "DELETE")
          return Promise.resolve(new Response(null, { status: 204 }));
        return Promise.resolve(
          new Response(
            JSON.stringify([
              {
                id: "40000000-0000-0000-0000-000000000001",
                title: "Geländespiel",
                location: "Wald",
                category: "Programm",
                overlapsAnotherEntry: false,
                timing: {
                  isAllDay: true,
                  startDate: "2026-08-03",
                  endDateExclusive: "2026-08-04",
                },
                version: 7,
              },
            ]),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/tagesplan");

    await user.click(
      await screen.findByRole("button", { name: "Geländespiel löschen" }),
    );
    expect(
      screen.getByRole("group", { name: "Verknüpfte Inhalte" }),
    ).toBeInTheDocument();
    await user.click(
      screen.getByRole("radio", {
        name: /Mahlzeiten und Andachten vom Zeitplaneintrag lösen/,
      }),
    );
    await user.click(
      screen.getByRole("button", { name: "In den Papierkorb verschieben" }),
    );

    const deleteCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("?linkedBehavior=Unlink") &&
        init?.method === "DELETE",
    );
    expect(deleteCall?.[1]?.headers).toEqual({
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"7"',
    });
    expect(
      await screen.findByRole("status", { name: "Löschstatus" }),
    ).toHaveTextContent("Geländespiel“ wurde in den Papierkorb verschoben.");
  });

  it("creates a schedule entry and meal through one visible workflow", async () => {
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/auth/antiforgery") {
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        }
        if (init?.method === "POST")
          return Promise.resolve(
            new Response(JSON.stringify({}), {
              status: 201,
              headers: { "Content-Type": "application/json" },
            }),
          );
        return Promise.resolve(
          new Response(JSON.stringify([]), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          }),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/tagesplan");

    await user.click(screen.getByRole("button", { name: "Eintrag erstellen" }));
    await user.type(
      screen.getByRole("textbox", { name: "Titel des Zeitplaneintrags" }),
      "Mittagessen",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Gemeinsam anlegen" }),
      "Meal",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Name der Mahlzeit" }),
      "Kartoffelsuppe",
    );
    await user.click(
      screen.getByRole("button", { name: "Zeitplaneintrag anlegen" }),
    );

    const createCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("/schedule/with-meal") &&
        init?.method === "POST",
    );
    const serializedBody = createCall?.[1]?.body;
    if (typeof serializedBody !== "string")
      throw new Error("Der Request enthält keinen JSON-Text.");
    const payload = JSON.parse(serializedBody) as {
      schedule: { title: string };
      meal: { name: string };
    };
    expect(createCall?.[1]?.headers).toEqual({
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": "csrf-token",
    });
    expect(payload.schedule.title).toBe("Mittagessen");
    expect(payload.meal.name).toBe("Kartoffelsuppe");
  });

  it("creates a schedule entry and devotion through one visible workflow", async () => {
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/auth/antiforgery") {
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        }
        if (init?.method === "POST")
          return Promise.resolve(
            new Response(JSON.stringify({}), {
              status: 201,
              headers: { "Content-Type": "application/json" },
            }),
          );
        return Promise.resolve(
          new Response(JSON.stringify([]), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          }),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/tagesplan");

    await user.click(screen.getByRole("button", { name: "Eintrag erstellen" }));
    await user.type(
      screen.getByRole("textbox", { name: "Titel des Zeitplaneintrags" }),
      "Abendandacht",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Gemeinsam anlegen" }),
      "Devotion",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Thema der Andacht" }),
      "Vertrauen",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Bibelstelle" }),
      "Psalm 23",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Kernaussage" }),
      "Gott begleitet uns.",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Inhalt der Andacht" }),
      "## Impuls",
    );
    expect(
      screen.getByText("Du wirst zunächst als verantwortlich eingetragen."),
    ).toBeInTheDocument();
    await user.click(
      screen.getByRole("button", { name: "Zeitplaneintrag anlegen" }),
    );

    const createCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("/schedule/with-devotion") &&
        init?.method === "POST",
    );
    const serializedBody = createCall?.[1]?.body;
    if (typeof serializedBody !== "string")
      throw new Error("Der Request enthält keinen JSON-Text.");
    const payload = JSON.parse(serializedBody) as {
      schedule: { title: string };
      devotion: { topic: string; bibleReference: string };
    };
    expect(payload.schedule.title).toBe("Abendandacht");
    expect(payload.devotion.topic).toBe("Vertrauen");
    expect(payload.devotion.bibleReference).toBe("Psalm 23");
  });

  it("creates an all-day schedule entry through the accessible form", async () => {
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/auth/antiforgery") {
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        }
        if (init?.method === "POST")
          return Promise.resolve(
            new Response(JSON.stringify({}), { status: 201 }),
          );
        return Promise.resolve(
          new Response(JSON.stringify([]), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          }),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/tagesplan");

    await user.click(screen.getByRole("button", { name: "Eintrag erstellen" }));
    await user.type(
      screen.getByRole("textbox", { name: "Titel des Zeitplaneintrags" }),
      "Anreisetag",
    );
    await user.click(
      screen.getByRole("checkbox", { name: "Ganztägiger Eintrag" }),
    );
    await user.click(
      screen.getByRole("button", { name: "Zeitplaneintrag anlegen" }),
    );

    const createCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("/schedule") && init?.method === "POST",
    );
    const body = createCall?.[1]?.body;
    if (typeof body !== "string") throw new Error("Kein JSON-Text gesendet.");
    const payload = JSON.parse(body) as {
      timing: Record<string, unknown>;
    };
    expect(payload.timing).toEqual({
      isAllDay: true,
      localStart: null,
      localEnd: null,
      startDate: "2026-08-01",
      endDateExclusive: "2026-08-02",
      startChoice: 0,
      endChoice: 0,
    });
  });

  it("assigns readable camp members as schedule responsibilities", async () => {
    const userId = "10000000-0000-0000-0000-000000000001";
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/auth/antiforgery") {
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        }
        if (path.endsWith("/responsibility-candidates"))
          return Promise.resolve(
            new Response(
              JSON.stringify([{ userId, displayName: "Miriam Keller" }]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (init?.method === "POST")
          return Promise.resolve(
            new Response(JSON.stringify({}), { status: 201 }),
          );
        return Promise.resolve(
          new Response(JSON.stringify([]), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          }),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/tagesplan");

    await user.click(screen.getByRole("button", { name: "Eintrag erstellen" }));
    await user.type(
      screen.getByRole("textbox", { name: "Titel des Zeitplaneintrags" }),
      "Geländespiel",
    );
    await user.click(
      await screen.findByRole("checkbox", { name: "Miriam Keller" }),
    );
    await user.click(
      screen.getByRole("button", { name: "Zeitplaneintrag anlegen" }),
    );

    const createCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("/schedule") && init?.method === "POST",
    );
    const body = createCall?.[1]?.body;
    if (typeof body !== "string") throw new Error("Kein JSON-Text gesendet.");
    const payload = JSON.parse(body) as { responsibleUserIds: string[] };
    expect(payload.responsibleUserIds).toEqual([userId]);
  });

  it("edits a schedule entry through the accessible agenda form with antiforgery and version", async () => {
    const entry = {
      id: "40000000-0000-0000-0000-000000000001",
      title: "Geländespiel",
      description: "In Gruppen",
      location: "Wald",
      category: "Programm",
      status: 0,
      responsibleUserIds: [],
      audience: "Ab 12",
      overlapsAnotherEntry: false,
      timing: {
        isAllDay: false,
        startsAtUtc: "2026-08-03T08:00:00Z",
        endsAtUtc: "2026-08-03T09:30:00Z",
        timeZoneId: "Europe/Berlin",
      },
      version: 7,
    };
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/auth/antiforgery") {
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        }
        if (init?.method === "PUT") {
          return Promise.resolve(
            new Response(
              JSON.stringify({ ...entry, title: "Waldspiel", version: 8 }),
              {
                status: 200,
                headers: { "Content-Type": "application/json" },
              },
            ),
          );
        }
        return Promise.resolve(
          new Response(JSON.stringify([entry]), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          }),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/tagesplan");

    await user.click(
      await screen.findByRole("button", { name: "Geländespiel bearbeiten" }),
    );
    const title = screen.getByRole("textbox", { name: "Titel" });
    await user.clear(title);
    await user.type(title, "Waldspiel");
    await user.click(
      screen.getByRole("button", { name: "Änderungen speichern" }),
    );

    const updateCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/schedule/${entry.id}`) &&
        init?.method === "PUT",
    );
    expect(updateCall?.[1]?.headers).toEqual({
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"7"',
    });
    const body = updateCall?.[1]?.body;
    if (typeof body !== "string") throw new Error("Kein JSON-Text gesendet.");
    expect(JSON.parse(body)).toMatchObject({
      title: "Waldspiel",
      description: "In Gruppen",
      audience: "Ab 12",
      timing: {
        isAllDay: false,
        localStart: "2026-08-03T10:00:00",
        localEnd: "2026-08-03T11:30:00",
      },
    });
  });

  it("rolls an optimistic calendar drag back when the server rejects it", async () => {
    const revert = vi.fn();
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/auth/antiforgery") {
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        }
        if (init?.method === "PUT")
          return Promise.resolve(new Response(null, { status: 500 }));
        return Promise.resolve(
          new Response(
            JSON.stringify([
              {
                id: "40000000-0000-0000-0000-000000000001",
                title: "Geländespiel",
                description: null,
                location: "Wald",
                category: "Programm",
                status: 0,
                responsibleUserIds: [],
                audience: null,
                overlapsAnotherEntry: false,
                timing: {
                  isAllDay: false,
                  startsAtUtc: "2026-08-03T08:00:00Z",
                  endsAtUtc: "2026-08-03T09:30:00Z",
                  timeZoneId: "Europe/Berlin",
                },
                version: 7,
              },
            ]),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/tagesplan");
    expect(await screen.findByText("Geländespiel")).toBeInTheDocument();

    act(() =>
      calendarMock.props?.eventDrop?.({
        event: {
          id: "40000000-0000-0000-0000-000000000001",
          allDay: false,
          startStr: "2026-08-03T14:00:00+02:00",
          endStr: "2026-08-03T15:30:00+02:00",
        },
        revert,
      }),
    );

    await waitFor(() => expect(revert).toHaveBeenCalledOnce());
    expect(
      screen.getByRole("status", { name: "Änderungsstatus" }),
    ).toHaveTextContent("Änderung wurde zurückgesetzt");
  });

  it("explains a calendar resize version conflict and reloads current data", async () => {
    const revert = vi.fn();
    const fetchMock = vi.fn(
      (request: RequestInfo | URL, init?: RequestInit) => {
        const path = requestPath(request);
        if (path === "/api/v1/auth/antiforgery") {
          return Promise.resolve(
            new Response(JSON.stringify({ token: "csrf-token" }), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        }
        if (init?.method === "PUT")
          return Promise.resolve(
            new Response(
              JSON.stringify({ errorCode: "schedule_entry_version_conflict" }),
              {
                status: 409,
                headers: { "Content-Type": "application/problem+json" },
              },
            ),
          );
        return Promise.resolve(
          new Response(
            JSON.stringify([
              {
                id: "40000000-0000-0000-0000-000000000001",
                title: "Geländespiel",
                description: null,
                location: "Wald",
                category: "Programm",
                status: 0,
                responsibleUserIds: [],
                audience: null,
                overlapsAnotherEntry: false,
                timing: {
                  isAllDay: false,
                  startsAtUtc: "2026-08-03T08:00:00Z",
                  endsAtUtc: "2026-08-03T09:30:00Z",
                  timeZoneId: "Europe/Berlin",
                },
                version: 7,
              },
            ]),
            { status: 200, headers: { "Content-Type": "application/json" } },
          ),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/tagesplan");
    expect(await screen.findByText("Geländespiel")).toBeInTheDocument();

    act(() =>
      calendarMock.props?.eventResize?.({
        event: {
          id: "40000000-0000-0000-0000-000000000001",
          allDay: false,
          startStr: "2026-08-03T10:00:00+02:00",
          endStr: "2026-08-03T12:00:00+02:00",
        },
        revert,
      }),
    );

    await waitFor(() => expect(revert).toHaveBeenCalledOnce());
    expect(
      screen.getByRole("status", { name: "Änderungsstatus" }),
    ).toHaveTextContent("zwischenzeitlich geändert");
  });
});
