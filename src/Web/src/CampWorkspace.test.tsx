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
            actorDisplayName: "Lea Beispiel",
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
    expect(screen.getByText("Lea Beispiel · Zeitplan")).toBeInTheDocument();
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
  }, 10_000);

  it("opens a recipe and saves edits as a version-safe revision", async () => {
    const recipeId = "42000000-0000-0000-0000-000000000001";
    const ingredientId = "41000000-0000-0000-0000-000000000001";
    const attachmentId = "45000000-0000-0000-0000-000000000001";
    const readWindow = {
      location: { href: "" },
      close: vi.fn(),
    } as unknown as Window;
    const openWindow = vi.spyOn(window, "open").mockReturnValue(readWindow);
    const recipe = {
      id: recipeId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      currentVersion: {
        id: "43000000-0000-0000-0000-000000000001",
        number: 1,
        name: "Kartoffelsuppe",
        description: "Wärmende Suppe",
        preparation: "Kartoffeln kochen und pürieren.",
        basePortions: 8,
        ingredients: [
          {
            id: "44000000-0000-0000-0000-000000000001",
            ingredientId,
            ingredientName: "Kartoffeln",
            quantity: { value: 1.5, unit: 1, countUnitName: null },
            note: "mehligkochend",
          },
        ],
        dietaryTags: ["vegetarisch"],
        allergenNotes: "Milch prüfen",
        kitchenNotes: "Pürierstab bereithalten",
        createdAt: "2026-08-09T10:00:00Z",
      },
      version: 7,
    };
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
        if (path.endsWith(`/recipe-files/${attachmentId}/read-grant`))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                token: "single-use-token",
                attachmentId,
                expiresAt: "2026-08-09T10:01:00Z",
                disposition: 1,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (init?.method === "POST" && path.includes("/recipe-files?ownerId="))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                id: "45000000-0000-0000-0000-000000000002",
                originalFileName: "Küchenplan.png",
                mediaType: 2,
                contentType: "image/png",
                sizeBytes: 8,
                state: 1,
                version: 1,
              }),
              { status: 201, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/recipe-files/quota"))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                scope: 1,
                limitBytes: 104857600,
                usedBytes: 1048576,
                pendingBytes: 0,
                availableBytes: 103809024,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.includes(`/recipe-files?ownerId=${recipeId}`))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: attachmentId,
                  organizationId: recipe.organizationId,
                  campId: null,
                  owner: { type: 2, id: recipeId },
                  originalFileName: "Ablauf.pdf",
                  mediaType: 0,
                  contentType: "application/pdf",
                  sizeBytes: 204800,
                  state: 1,
                  createdBy: "10000000-0000-0000-0000-000000000001",
                  createdAt: "2026-08-09T10:00:00Z",
                  deletedAt: null,
                  purgeAt: null,
                  version: 2,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (init?.method === "PUT" && path.endsWith(`/recipes/${recipeId}`))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...recipe,
                currentVersion: {
                  ...recipe.currentVersion,
                  number: 2,
                  name: "Kartoffeleintopf",
                  ingredients: [
                    {
                      ...recipe.currentVersion.ingredients[0],
                      quantity: { value: 2, unit: 1, countUnitName: null },
                    },
                  ],
                },
                version: 8,
              }),
              {
                status: 200,
                headers: { "Content-Type": "application/json", ETag: '"8"' },
              },
            ),
          );
        if (path.endsWith(`/catering/recipes/${recipeId}`))
          return Promise.resolve(
            new Response(JSON.stringify(recipe), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"7"' },
            }),
          );
        if (path.endsWith("/catering/recipes"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: recipeId,
                  organizationId: recipe.organizationId,
                  name: "Kartoffelsuppe",
                  basePortions: 8,
                  currentVersionNumber: 1,
                  version: 7,
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

    await user.click(
      await screen.findByRole("button", {
        name: "Kartoffelsuppe öffnen",
      }),
    );
    expect(
      await screen.findByRole("heading", {
        name: "Rezeptdetails: Kartoffelsuppe",
      }),
    ).toBeInTheDocument();
    expect(screen.getByText("1,5 Kilogramm Kartoffeln")).toBeInTheDocument();
    expect(screen.getByText("mehligkochend")).toBeInTheDocument();
    expect(screen.getByText("Milch prüfen")).toBeInTheDocument();
    const attachments = screen.getByRole("region", {
      name: "Dateien zu Kartoffelsuppe",
    });
    expect(
      await within(attachments).findByText("Ablauf.pdf"),
    ).toBeInTheDocument();
    expect(
      within(attachments).getByText("1 MiB von 100 MiB belegt"),
    ).toBeInTheDocument();

    const upload = new File(
      [new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])],
      "Küchenplan.png",
      { type: "image/png" },
    );
    await user.upload(
      within(attachments).getByLabelText("Datei für das Rezept"),
      upload,
    );
    await user.click(
      within(attachments).getByRole("button", {
        name: "Küchenplan.png hochladen",
      }),
    );
    expect(
      await within(attachments).findByText(
        "Küchenplan.png wurde sicher hochgeladen.",
      ),
    ).toHaveAttribute("role", "status");
    await user.click(
      within(attachments).getByRole("button", { name: "Ablauf.pdf öffnen" }),
    );
    expect(openWindow).toHaveBeenCalledWith(
      "",
      "_blank",
      "noopener,noreferrer",
    );
    expect(readWindow.location.href).toContain(
      "/recipe-files/content?token=single-use-token",
    );

    const readGrantCall = fetchMock.mock.calls.find(([request]) =>
      requestPath(request).endsWith(`/recipe-files/${attachmentId}/read-grant`),
    );
    expect(readGrantCall?.[1]?.headers).toEqual({
      "X-CSRF-TOKEN": "csrf-token",
    });
    const uploadCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).includes(`/recipe-files?ownerId=${recipeId}`) &&
        init?.method === "POST",
    );
    expect(uploadCall?.[1]?.headers).toEqual({
      "X-CSRF-TOKEN": "csrf-token",
    });
    expect(uploadCall?.[1]?.body).toBeInstanceOf(FormData);

    await user.click(screen.getByRole("button", { name: "Rezept bearbeiten" }));
    const name = screen.getByRole("textbox", { name: "Rezeptname bearbeiten" });
    await user.clear(name);
    await user.type(name, "Kartoffeleintopf");
    const quantity = screen.getByRole("spinbutton", {
      name: "Menge für Kartoffeln bearbeiten",
    });
    await user.clear(quantity);
    await user.type(quantity, "2");
    await user.click(
      screen.getByRole("button", { name: "Neue Rezeptversion speichern" }),
    );

    expect(
      await screen.findByText(
        "Kartoffeleintopf wurde als Rezeptversion 2 gespeichert.",
      ),
    ).toHaveAttribute("role", "status");
    const reviseCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/recipes/${recipeId}`) &&
        init?.method === "PUT",
    );
    expect(reviseCall?.[1]?.headers).toMatchObject({
      "If-Match": '"7"',
      "X-CSRF-TOKEN": "csrf-token",
    });
    expect(JSON.parse(reviseCall?.[1]?.body as string)).toMatchObject({
      name: "Kartoffeleintopf",
      description: "Wärmende Suppe",
      preparation: "Kartoffeln kochen und pürieren.",
      basePortions: 8,
      ingredients: [
        {
          ingredientId,
          quantity: { value: 2, unit: 1, countUnitName: null },
          note: "mehligkochend",
        },
      ],
      dietaryTags: ["vegetarisch"],
      allergenNotes: "Milch prüfen",
      kitchenNotes: "Pürierstab bereithalten",
    });
  });

  it("explains a recipe revision conflict without losing the edit form", async () => {
    const recipeId = "42000000-0000-0000-0000-000000000001";
    const recipe = {
      id: recipeId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      currentVersion: {
        id: "43000000-0000-0000-0000-000000000001",
        number: 3,
        name: "Gemüsereis",
        description: "Einfaches Lageressen",
        preparation: "Reis und Gemüse garen.",
        basePortions: 6,
        ingredients: [
          {
            id: "44000000-0000-0000-0000-000000000001",
            ingredientId: "41000000-0000-0000-0000-000000000001",
            ingredientName: "Reis",
            quantity: { value: 500, unit: 0, countUnitName: null },
            note: null,
          },
        ],
        dietaryTags: [],
        allergenNotes: null,
        kitchenNotes: null,
        createdAt: "2026-08-09T10:00:00Z",
      },
      version: 9,
    };
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
        if (init?.method === "PUT")
          return Promise.resolve(new Response(null, { status: 412 }));
        if (path.endsWith(`/catering/recipes/${recipeId}`))
          return Promise.resolve(
            new Response(JSON.stringify(recipe), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"9"' },
            }),
          );
        if (path.endsWith("/catering/recipes"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: recipeId,
                  organizationId: recipe.organizationId,
                  name: recipe.currentVersion.name,
                  basePortions: 6,
                  currentVersionNumber: 3,
                  version: 9,
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

    await user.click(
      await screen.findByRole("button", { name: "Gemüsereis öffnen" }),
    );
    await user.click(
      await screen.findByRole("button", { name: "Rezept bearbeiten" }),
    );
    await user.click(
      screen.getByRole("button", { name: "Neue Rezeptversion speichern" }),
    );

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Das Rezept wurde zwischenzeitlich geändert. Schließe die Bearbeitung und öffne das Rezept erneut.",
    );
    expect(
      screen.getByRole("form", { name: "Gemüsereis bearbeiten" }),
    ).toBeInTheDocument();
  });

  it("manages ingredients through previewed version-safe mutations", async () => {
    const sourceId = "41000000-0000-0000-0000-000000000001";
    const targetId = "41000000-0000-0000-0000-000000000002";
    const source = {
      id: sourceId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      name: "Tomatenstücke",
      isMerged: false,
      mergedIntoIngredientId: null,
      version: 3,
    };
    const target = {
      ...source,
      id: targetId,
      name: "Tomaten",
      version: 5,
    };
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
        if (path.includes("/catering/ingredients?query=&limit=100"))
          return Promise.resolve(
            new Response(JSON.stringify([source, target]), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (init?.method === "POST" && path.endsWith("/catering/ingredients"))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...source,
                id: "41000000-0000-0000-0000-000000000003",
                name: "Paprika",
                version: 1,
              }),
              { status: 201, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (init?.method === "PUT" && path.endsWith(`/ingredients/${sourceId}`))
          return Promise.resolve(
            new Response(
              JSON.stringify({ ...source, name: "Tomatenwürfel", version: 4 }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/ingredients/merge-preview"))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                source,
                target,
                affectedRecipes: [
                  {
                    id: "42000000-0000-0000-0000-000000000001",
                    organizationId: source.organizationId,
                    name: "Tomatensuppe",
                    basePortions: 8,
                    currentVersionNumber: 2,
                    version: 2,
                  },
                ],
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/ingredients/merge"))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                target: { ...target, version: 6 },
                revisedRecipeIds: [],
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
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/essen");

    await user.click(screen.getByRole("button", { name: "Zutaten verwalten" }));
    expect(
      await screen.findByRole("heading", {
        name: "Zutatenbibliothek verwalten",
      }),
    ).toBeInTheDocument();

    await user.type(
      screen.getByRole("textbox", { name: "Neue Zutat" }),
      "Paprika",
    );
    await user.click(screen.getByRole("button", { name: "Zutat anlegen" }));
    expect(await screen.findByText("Paprika wurde angelegt.")).toHaveAttribute(
      "role",
      "status",
    );

    await user.click(
      screen.getByRole("button", { name: "Tomatenstücke umbenennen" }),
    );
    const rename = screen.getByRole("textbox", {
      name: "Neuer Name für Tomatenstücke",
    });
    await user.clear(rename);
    await user.type(rename, "Tomatenwürfel");
    await user.click(
      screen.getByRole("button", { name: "Neuen Namen speichern" }),
    );
    expect(
      await screen.findByText("Tomatenwürfel wurde gespeichert."),
    ).toBeInTheDocument();

    await user.selectOptions(
      screen.getByRole("combobox", { name: "Doppelte Zutat" }),
      sourceId,
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Zielzutat" }),
      targetId,
    );
    await user.click(
      screen.getByRole("button", { name: "Zusammenführung prüfen" }),
    );
    expect(
      await screen.findByText("Tomatensuppe · Version 2"),
    ).toBeInTheDocument();
    await user.click(
      screen.getByRole("checkbox", {
        name: "Ich habe die betroffenen Rezepte geprüft.",
      }),
    );
    await user.click(
      screen.getByRole("button", { name: "Zusammenführung bestätigen" }),
    );
    expect(
      await screen.findByText(
        "Tomatenstücke wurde kontrolliert in Tomaten zusammengeführt.",
      ),
    ).toBeInTheDocument();

    const renameCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/ingredients/${sourceId}`) &&
        init?.method === "PUT",
    );
    expect(renameCall?.[1]?.headers).toMatchObject({
      "If-Match": '"3"',
      "X-CSRF-TOKEN": "csrf-token",
    });
    const mergeCall = fetchMock.mock.calls.find(([request]) =>
      requestPath(request).endsWith("/ingredients/merge"),
    );
    expect(typeof mergeCall?.[1]?.body).toBe("string");
    expect(JSON.parse(mergeCall?.[1]?.body as string)).toMatchObject({
      sourceIngredientId: sourceId,
      targetIngredientId: targetId,
      expectedSourceVersion: 3,
      expectedTargetVersion: 5,
    });
  });

  it("creates a linked meal and explicitly refreshes an outdated recipe snapshot", async () => {
    const mealId = "46000000-0000-0000-0000-000000000001";
    const recipeId = "42000000-0000-0000-0000-000000000001";
    const snapshotId = "47000000-0000-0000-0000-000000000001";
    const scheduleEntryId = "40000000-0000-0000-0000-000000000001";
    const snapshot = {
      id: snapshotId,
      sourceRecipeId: recipeId,
      sourceRecipeVersionNumber: 1,
      latestRecipeVersionNumber: 2,
      refreshAvailable: true,
      name: "Kartoffelsuppe",
      description: "Wärmende Suppe",
      preparation: "Kochen und pürieren.",
      basePortions: 8,
      ingredients: [
        {
          id: "48000000-0000-0000-0000-000000000001",
          ingredientId: "41000000-0000-0000-0000-000000000001",
          ingredientName: "Kartoffeln",
          baseQuantity: { value: 1.5, unit: 1, countUnitName: null },
          scaledQuantity: { value: 7.5, unit: 1, countUnitName: null },
          note: null,
        },
      ],
      dietaryTags: ["vegetarisch"],
      allergenNotes: "Milch prüfen",
      kitchenNotes: null,
      capturedAt: "2026-08-09T10:00:00Z",
    };
    const meal = {
      id: mealId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      name: "Mittagessen",
      campDefaultPortions: 42,
      portionOverride: 40,
      effectivePortions: 40,
      scheduleEntryId,
      recipeSnapshots: [snapshot],
      version: 4,
    };
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
        if (
          init?.method === "POST" &&
          path.endsWith(`/meals/${mealId}/recipes/${snapshotId}/refresh`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...meal,
                recipeSnapshots: [
                  {
                    ...snapshot,
                    sourceRecipeVersionNumber: 2,
                    latestRecipeVersionNumber: 2,
                    refreshAvailable: false,
                  },
                ],
                version: 5,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (init?.method === "POST" && path.endsWith("/catering/meals"))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...meal,
                id: "46000000-0000-0000-0000-000000000002",
                name: "Abendessen",
                portionOverride: 30,
                effectivePortions: 30,
                version: 1,
              }),
              { status: 201, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith(`/catering/meals/${mealId}`))
          return Promise.resolve(
            new Response(JSON.stringify(meal), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"4"' },
            }),
          );
        if (path.endsWith("/catering/meals"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: mealId,
                  organizationId: meal.organizationId,
                  campId: meal.campId,
                  name: meal.name,
                  effectivePortions: 40,
                  scheduleEntryId,
                  recipeCount: 1,
                  version: 4,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/catering/recipes"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: recipeId,
                  organizationId: meal.organizationId,
                  name: "Kartoffelsuppe",
                  basePortions: 8,
                  currentVersionNumber: 2,
                  version: 2,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.includes("/schedule?"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: scheduleEntryId,
                  title: "Abendessen im Speisesaal",
                  description: "",
                  location: "Speisesaal",
                  category: "Mahlzeit",
                  status: 0,
                  responsibleUserIds: [],
                  audience: null,
                  overlapsAnotherEntry: false,
                  timing: {
                    isAllDay: false,
                    startsAtUtc: "2026-08-03T16:00:00Z",
                    endsAtUtc: "2026-08-03T17:00:00Z",
                  },
                  version: 3,
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

    await user.click(screen.getByRole("button", { name: "Mahlzeit planen" }));
    expect(
      screen.getByRole("heading", { name: "Neue Mahlzeit" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Camp-Standard: 42 Personen")).toBeInTheDocument();
    await user.type(
      screen.getByRole("textbox", { name: "Name der Mahlzeit" }),
      "Abendessen",
    );
    await user.click(
      screen.getByRole("checkbox", { name: "Personenzahl überschreiben" }),
    );
    await user.clear(screen.getByRole("spinbutton", { name: "Personenzahl" }));
    await user.type(
      screen.getByRole("spinbutton", { name: "Personenzahl" }),
      "30",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Zeitplaneintrag" }),
      scheduleEntryId,
    );
    await user.click(
      screen.getByRole("checkbox", {
        name: "Kartoffelsuppe als Snapshot hinzufügen",
      }),
    );
    await user.click(
      screen.getByRole("button", { name: "Mahlzeit speichern" }),
    );
    expect(
      await screen.findByText(
        "Abendessen wurde mit 30 Personen und 1 Rezept-Snapshot angelegt.",
      ),
    ).toHaveAttribute("role", "status");

    const createCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("/catering/meals") &&
        init?.method === "POST",
    );
    expect(createCall?.[1]?.headers).toMatchObject({
      "X-CSRF-TOKEN": "csrf-token",
    });
    expect(JSON.parse(createCall?.[1]?.body as string)).toMatchObject({
      name: "Abendessen",
      portionOverride: 30,
      scheduleEntryId,
      recipeIds: [recipeId],
    });

    await user.click(
      await screen.findByRole("button", { name: "Mittagessen öffnen" }),
    );
    expect(
      await screen.findByRole("heading", {
        name: "Mahlzeitdetails: Mittagessen",
      }),
    ).toBeInTheDocument();
    expect(screen.getByText("7,5 Kilogramm Kartoffeln")).toBeInTheDocument();
    expect(screen.getByText("Rezeptversion 1 von 2")).toBeInTheDocument();
    await user.click(
      screen.getByRole("button", {
        name: "Kartoffelsuppe auf Version 2 aktualisieren",
      }),
    );
    expect(
      await screen.findByText(
        "Kartoffelsuppe wurde ausdrücklich auf Rezeptversion 2 aktualisiert.",
      ),
    ).toHaveAttribute("role", "status");
    const refreshCall = fetchMock.mock.calls.find(([request]) =>
      requestPath(request).endsWith(
        `/meals/${mealId}/recipes/${snapshotId}/refresh`,
      ),
    );
    expect(refreshCall?.[1]?.headers).toMatchObject({
      "If-Match": '"4"',
      "X-CSRF-TOKEN": "csrf-token",
    });
  });

  it("manages an existing meal through version-safe mutations and trash", async () => {
    const mealId = "46000000-0000-0000-0000-000000000001";
    const recipeId = "42000000-0000-0000-0000-000000000001";
    const addedRecipeId = "42000000-0000-0000-0000-000000000002";
    const snapshotId = "47000000-0000-0000-0000-000000000001";
    const snapshot = {
      id: snapshotId,
      sourceRecipeId: recipeId,
      sourceRecipeVersionNumber: 2,
      latestRecipeVersionNumber: 2,
      refreshAvailable: false,
      name: "Kartoffelsuppe",
      description: "Suppe",
      preparation: "Kochen.",
      basePortions: 8,
      ingredients: [],
      dietaryTags: [],
      allergenNotes: null,
      kitchenNotes: null,
      capturedAt: "2026-08-09T10:00:00Z",
    };
    const addedSnapshot = {
      ...snapshot,
      id: "47000000-0000-0000-0000-000000000002",
      sourceRecipeId: addedRecipeId,
      name: "Gemüsereis",
    };
    const meal = {
      id: mealId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      name: "Mittagessen",
      campDefaultPortions: 42,
      portionOverride: 40,
      effectivePortions: 40,
      scheduleEntryId: null,
      recipeSnapshots: [snapshot],
      version: 4,
    };
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
        if (init?.method === "PUT" && path.endsWith(`/meals/${mealId}`))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...meal,
                name: "Brunch",
                portionOverride: 44,
                effectivePortions: 44,
                version: 5,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (
          init?.method === "POST" &&
          path.endsWith(`/meals/${mealId}/recipes`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...meal,
                name: "Brunch",
                portionOverride: 44,
                effectivePortions: 44,
                recipeSnapshots: [snapshot, addedSnapshot],
                version: 6,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (
          init?.method === "DELETE" &&
          path.endsWith(`/meals/${mealId}/recipes/${snapshotId}`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...meal,
                name: "Brunch",
                portionOverride: 44,
                effectivePortions: 44,
                recipeSnapshots: [addedSnapshot],
                version: 7,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (init?.method === "DELETE" && path.endsWith(`/meals/${mealId}`))
          return Promise.resolve(new Response(null, { status: 204 }));
        if (path.endsWith(`/catering/meals/${mealId}`))
          return Promise.resolve(
            new Response(JSON.stringify(meal), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"4"' },
            }),
          );
        if (path.endsWith("/catering/meals"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: mealId,
                  name: meal.name,
                  effectivePortions: 40,
                  scheduleEntryId: null,
                  recipeCount: 1,
                  version: 4,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/catering/recipes"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: recipeId,
                  name: "Kartoffelsuppe",
                  basePortions: 8,
                  currentVersionNumber: 2,
                  version: 2,
                },
                {
                  id: addedRecipeId,
                  name: "Gemüsereis",
                  basePortions: 8,
                  currentVersionNumber: 2,
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

    await user.click(
      await screen.findByRole("button", { name: "Mittagessen öffnen" }),
    );
    await user.click(
      screen.getByRole("button", { name: "Mahlzeit bearbeiten" }),
    );
    const name = screen.getByRole("textbox", { name: "Name bearbeiten" });
    await user.clear(name);
    await user.type(name, "Brunch");
    const portions = screen.getByRole("spinbutton", {
      name: "Personenzahl bearbeiten",
    });
    await user.clear(portions);
    await user.type(portions, "44");
    await user.click(
      screen.getByRole("button", { name: "Änderungen speichern" }),
    );
    expect(
      await screen.findByText("Brunch wurde gespeichert."),
    ).toHaveAttribute("role", "status");

    await user.selectOptions(
      screen.getByRole("combobox", { name: "Rezept-Snapshot hinzufügen" }),
      addedRecipeId,
    );
    await user.click(
      screen.getByRole("button", { name: "Snapshot hinzufügen" }),
    );
    expect(
      await screen.findByText("Gemüsereis wurde hinzugefügt."),
    ).toBeInTheDocument();
    await user.click(
      screen.getByRole("button", {
        name: "Kartoffelsuppe aus Mahlzeit entfernen",
      }),
    );
    expect(
      await screen.findByText("Kartoffelsuppe wurde entfernt."),
    ).toBeInTheDocument();

    await user.click(
      screen.getByRole("button", {
        name: "Mahlzeit in Papierkorb verschieben",
      }),
    );
    await user.click(
      screen.getByRole("checkbox", {
        name: "Ich möchte diese Mahlzeit in den Papierkorb verschieben.",
      }),
    );
    await user.click(
      screen.getByRole("button", { name: "Verschieben bestätigen" }),
    );
    expect(
      await screen.findByText("Brunch wurde in den Papierkorb verschoben."),
    ).toHaveAttribute("role", "status");

    const versionedCalls = fetchMock.mock.calls.filter(([, init]) =>
      ["PUT", "POST", "DELETE"].includes(init?.method ?? ""),
    );
    expect(versionedCalls.map(([, init]) => init?.headers)).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ "If-Match": '"4"' }),
        expect.objectContaining({ "If-Match": '"5"' }),
        expect.objectContaining({ "If-Match": '"6"' }),
        expect.objectContaining({ "If-Match": '"7"' }),
      ]),
    );
  });

  it("uploads and moves a private meal attachment to the camp trash", async () => {
    const mealId = "46000000-0000-0000-0000-000000000003";
    const attachmentId = "4f000000-0000-0000-0000-000000000008";
    const meal = {
      id: mealId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      name: "Abendessen",
      campDefaultPortions: 42,
      portionOverride: null,
      effectivePortions: 42,
      scheduleEntryId: null,
      recipeSnapshots: [],
      version: 3,
    };
    const attachment = {
      id: attachmentId,
      originalFileName: "Aufbauplan.pdf",
      mediaType: 0,
      contentType: "application/pdf",
      sizeBytes: 204800,
      version: 2,
    };
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
        if (
          init?.method === "POST" &&
          path.includes(`/files?ownerType=Meal&ownerId=${mealId}`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...attachment,
                id: "4f000000-0000-0000-0000-000000000009",
                originalFileName: "Sitzordnung.png",
                mediaType: 2,
                contentType: "image/png",
                sizeBytes: 8,
                version: 1,
              }),
              { status: 201, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (
          init?.method === "DELETE" &&
          path.endsWith(`/files/${attachmentId}`)
        )
          return Promise.resolve(new Response(null, { status: 204 }));
        if (path.endsWith("/files/quota"))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                limitBytes: 104857600,
                usedBytes: 1048576,
                pendingBytes: 0,
                availableBytes: 103809024,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.includes(`/files?ownerType=Meal&ownerId=${mealId}`))
          return Promise.resolve(
            new Response(JSON.stringify([attachment]), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (path.endsWith(`/catering/meals/${mealId}`))
          return Promise.resolve(
            new Response(JSON.stringify(meal), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"3"' },
            }),
          );
        if (path.endsWith("/catering/meals"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: mealId,
                  name: meal.name,
                  effectivePortions: 42,
                  scheduleEntryId: null,
                  recipeCount: 0,
                  version: 3,
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

    await user.click(
      await screen.findByRole("button", { name: "Abendessen öffnen" }),
    );
    const files = await screen.findByRole("region", {
      name: "Dateien zu Abendessen",
    });
    expect(await within(files).findByText("Aufbauplan.pdf")).toBeVisible();
    const upload = new File(
      [new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])],
      "Sitzordnung.png",
      { type: "image/png" },
    );
    await user.upload(
      within(files).getByLabelText("Datei für die Mahlzeit"),
      upload,
    );
    await user.click(
      within(files).getByRole("button", { name: "Sitzordnung.png hochladen" }),
    );
    expect(
      await within(files).findByText(
        "Sitzordnung.png wurde sicher hochgeladen.",
      ),
    ).toHaveAttribute("role", "status");

    await user.click(
      within(files).getByRole("button", { name: "Aufbauplan.pdf löschen" }),
    );
    await user.click(
      within(files).getByRole("checkbox", {
        name: "Aufbauplan.pdf wirklich in den Papierkorb verschieben",
      }),
    );
    await user.click(
      within(files).getByRole("button", {
        name: "Datei in Papierkorb verschieben",
      }),
    );
    expect(
      await within(files).findByText(
        "Aufbauplan.pdf wurde in den Papierkorb verschoben.",
      ),
    ).toHaveAttribute("role", "status");
    const deleteCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/files/${attachmentId}`) &&
        init?.method === "DELETE",
    );
    expect(deleteCall?.[1]?.headers).toMatchObject({ "If-Match": '"2"' });
  });

  it("reviews meal quantities before transferring them to a chosen shopping list", async () => {
    const mealId = "46000000-0000-0000-0000-000000000001";
    const snapshotId = "47000000-0000-0000-0000-000000000001";
    const snapshotIngredientId = "48000000-0000-0000-0000-000000000001";
    const recipeId = "42000000-0000-0000-0000-000000000001";
    const targetListId = "49000000-0000-0000-0000-000000000002";
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
        if (
          init?.method === "POST" &&
          path.endsWith(
            `/logistics/shopping-lists/${targetListId}/transfer/meal/${mealId}`,
          )
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                shoppingListId: targetListId,
                listVersion: 10,
                changeSequence: 18,
                items: [],
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith(`/catering/meals/${mealId}/shopping-draft`))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                mealId,
                mealName: "Mittagessen",
                effectivePortions: 40,
                mealVersion: 4,
                lines: [
                  {
                    recipeSnapshotId: snapshotId,
                    snapshotIngredientId,
                    sourceRecipeId: recipeId,
                    sourceRecipeVersionNumber: 2,
                    sourceLabel: "Kartoffelsuppe · Kartoffeln",
                    ingredientName: "Kartoffeln",
                    suggestedQuantity: {
                      value: 7.5,
                      unit: 1,
                      countUnitName: null,
                    },
                    dimension: 0,
                    compatibleUnits: [0, 1],
                  },
                ],
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/logistics/shopping-lists"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: "49000000-0000-0000-0000-000000000001",
                  name: "Kleiner Einkauf",
                  openItemCount: 1,
                  checkedItemCount: 0,
                  version: 3,
                  changeSequence: 4,
                },
                {
                  id: targetListId,
                  name: "Großeinkauf Dienstag",
                  openItemCount: 4,
                  checkedItemCount: 1,
                  version: 9,
                  changeSequence: 17,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith(`/catering/meals/${mealId}`))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                id: mealId,
                organizationId: "20000000-0000-0000-0000-000000000001",
                campId: "30000000-0000-0000-0000-000000000001",
                name: "Mittagessen",
                campDefaultPortions: 42,
                portionOverride: 40,
                effectivePortions: 40,
                scheduleEntryId: null,
                recipeSnapshots: [],
                version: 4,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/catering/meals"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: mealId,
                  name: "Mittagessen",
                  effectivePortions: 40,
                  scheduleEntryId: null,
                  recipeCount: 1,
                  version: 4,
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

    await user.click(
      await screen.findByRole("button", { name: "Mittagessen öffnen" }),
    );
    await user.click(
      screen.getByRole("button", { name: "In Einkaufsliste übernehmen" }),
    );
    await user.selectOptions(
      await screen.findByRole("combobox", { name: "Ziel-Einkaufsliste" }),
      targetListId,
    );
    const quantity = screen.getByRole("spinbutton", {
      name: "Menge für Kartoffeln",
    });
    await user.clear(quantity);
    await user.type(quantity, "8.25");
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Einheit für Kartoffeln" }),
      "0",
    );
    expect(
      within(
        screen.getByRole("combobox", { name: "Einheit für Kartoffeln" }),
      ).queryByRole("option", { name: "Liter" }),
    ).not.toBeInTheDocument();
    await user.click(
      screen.getByRole("button", { name: "1 Position übernehmen" }),
    );

    expect(
      await screen.findByRole("status", { name: "Einkaufsübernahme" }),
    ).toHaveTextContent(
      "1 Position aus Mittagessen wurde in Großeinkauf Dienstag übernommen.",
    );
    const transferCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(
          `/logistics/shopping-lists/${targetListId}/transfer/meal/${mealId}`,
        ) && init?.method === "POST",
    );
    expect(transferCall?.[1]?.headers).toEqual({
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"9"',
    });
    expect(JSON.parse(transferCall?.[1]?.body as string)).toEqual({
      expectedListVersion: 9,
      lines: [
        {
          recipeSnapshotId: snapshotId,
          snapshotIngredientId,
          content: {
            name: "Kartoffeln",
            quantity: { value: 8.25, unit: 0, customUnitName: null },
            responsibleUserIds: [],
            store: null,
            note: null,
          },
        },
      ],
    });
  });

  it("reviews a material requirement before transferring it to a chosen shopping list", async () => {
    const materialId = "48000000-0000-0000-0000-000000000001";
    const listId = "49000000-0000-0000-0000-000000000001";
    const memberId = "10000000-0000-0000-0000-000000000001";
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
        if (
          init?.method === "POST" &&
          path.endsWith(
            `/shopping-lists/${listId}/transfer/material/${materialId}`,
          )
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                shoppingListId: listId,
                listVersion: 6,
                changeSequence: 12,
                items: [
                  {
                    id: "4a000000-0000-0000-0000-000000000001",
                    shoppingListId: listId,
                    name: "Turnierbälle",
                    quantity: { value: 6, unit: 4, customUnitName: null },
                    responsibleUserIds: [memberId],
                    store: "Sportgeschäft",
                    note: "Größe 5",
                    source: {
                      kind: 2,
                      label: "Material · Turnierbälle",
                    },
                    isChecked: false,
                    checkedByUserId: null,
                    checkedAt: null,
                    version: 1,
                  },
                ],
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith(`/logistics/material/${materialId}`))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                id: materialId,
                organizationId: "20000000-0000-0000-0000-000000000001",
                campId: "30000000-0000-0000-0000-000000000001",
                name: "Turnierbälle",
                description: "Für das Geländeturnier",
                quantity: { value: 4, unit: 4, customUnitName: null },
                responsibleUserIds: [memberId],
                procurementSource: "Sportgeschäft",
                note: "Größe 5",
                status: 1,
                scheduleEntryId: null,
                version: 3,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/logistics/material"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: materialId,
                  name: "Turnierbälle",
                  quantity: { value: 4, unit: 4, customUnitName: null },
                  status: 1,
                  scheduleEntryId: null,
                  version: 3,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/responsibility-candidates"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                { userId: memberId, displayName: "Miriam Muster" },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/logistics/shopping-lists"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: listId,
                  name: "Großeinkauf Dienstag",
                  openItemCount: 0,
                  checkedItemCount: 0,
                  version: 5,
                  changeSequence: 11,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/logistik");

    await user.click(
      await screen.findByRole("button", { name: "Turnierbälle öffnen" }),
    );
    expect(await screen.findByText("Für das Geländeturnier")).toBeVisible();
    expect(screen.getByText("Miriam Muster")).toBeVisible();
    await user.click(
      screen.getByRole("button", { name: "Turnierbälle einkaufen" }),
    );
    expect(
      screen.getByText(/^Menge und Einheit können vor der Übernahme/),
    ).toBeVisible();
    const quantity = screen.getByRole("spinbutton", {
      name: "Menge für die Einkaufsposition",
    });
    await user.clear(quantity);
    await user.type(quantity, "6");
    await user.click(
      screen.getByRole("button", { name: "Material übernehmen" }),
    );
    expect(
      await screen.findByText(
        "Turnierbälle wurde in Großeinkauf Dienstag übernommen.",
      ),
    ).toHaveAttribute("role", "status");

    const transferCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(
          `/shopping-lists/${listId}/transfer/material/${materialId}`,
        ) && init?.method === "POST",
    );
    expect(transferCall?.[1]?.headers).toMatchObject({
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"5"',
    });
    expect(JSON.parse(transferCall?.[1]?.body as string)).toEqual({
      expectedListVersion: 5,
      expectedRequirementVersion: 3,
      content: {
        name: "Turnierbälle",
        quantity: { value: 6, unit: 4, customUnitName: null },
        responsibleUserIds: [memberId],
        store: "Sportgeschäft",
        note: "Größe 5",
      },
    });
  });

  it("creates, edits and moves schedule-linked material to the trash with current versions", async () => {
    const materialId = "48000000-0000-0000-0000-000000000001";
    const scheduleEntryId = "32000000-0000-0000-0000-000000000001";
    const memberId = "10000000-0000-0000-0000-000000000001";
    const createdMaterial = {
      id: materialId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      name: "Turnierbälle",
      description: "Für das Geländeturnier",
      quantity: { value: 4, unit: 4, customUnitName: null },
      responsibleUserIds: [memberId],
      procurementSource: "Sportgeschäft",
      note: "Größe 5",
      status: 1,
      scheduleEntryId,
      version: 1,
    };
    let materialRows: unknown[] = [];
    let currentMaterial = createdMaterial;
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
        if (init?.method === "POST" && path.endsWith("/logistics/material")) {
          materialRows = [createdMaterial];
          return Promise.resolve(
            new Response(JSON.stringify(createdMaterial), {
              status: 201,
              headers: { "Content-Type": "application/json", ETag: '"1"' },
            }),
          );
        }
        if (
          init?.method === "PUT" &&
          path.endsWith(`/logistics/material/${materialId}`)
        ) {
          currentMaterial = {
            ...createdMaterial,
            quantity: { value: 6, unit: 4, customUnitName: null },
            status: 2,
            version: 2,
          };
          materialRows = [currentMaterial];
          return Promise.resolve(
            new Response(JSON.stringify(currentMaterial), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"2"' },
            }),
          );
        }
        if (
          init?.method === "DELETE" &&
          path.endsWith(`/logistics/material/${materialId}`)
        ) {
          materialRows = [];
          return Promise.resolve(new Response(null, { status: 204 }));
        }
        if (path.includes("/schedule?"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: scheduleEntryId,
                  title: "Geländeturnier",
                  category: "Programm",
                  status: 1,
                  responsibleUserIds: [],
                  overlapsAnotherEntry: false,
                  timing: {
                    isAllDay: false,
                    startsAtUtc: "2026-08-05T12:00:00Z",
                    endsAtUtc: "2026-08-05T14:00:00Z",
                  },
                  version: 1,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/responsibility-candidates"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                { userId: memberId, displayName: "Miriam Muster" },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith(`/logistics/material/${materialId}`))
          return Promise.resolve(
            new Response(JSON.stringify(currentMaterial), {
              status: 200,
              headers: {
                "Content-Type": "application/json",
                ETag: `"${currentMaterial.version}"`,
              },
            }),
          );
        if (path.endsWith("/logistics/material"))
          return Promise.resolve(
            new Response(JSON.stringify(materialRows), {
              status: 200,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/logistik");

    await user.click(
      await screen.findByRole("button", { name: "Materialbedarf anlegen" }),
    );
    await user.type(
      screen.getByRole("textbox", { name: "Bezeichnung des Materials" }),
      "Turnierbälle",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Beschreibung des Materials" }),
      "Für das Geländeturnier",
    );
    const createQuantity = screen.getByRole("spinbutton", {
      name: "Menge des Materials",
    });
    await user.clear(createQuantity);
    await user.type(createQuantity, "4");
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Einheit des Materials" }),
      "4",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Beschaffungsstatus" }),
      "1",
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Verknüpfung zum Tagesplan" }),
      scheduleEntryId,
    );
    await user.type(
      screen.getByRole("textbox", { name: "Beschaffungsquelle" }),
      "Sportgeschäft",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Materialnotiz" }),
      "Größe 5",
    );
    await user.click(screen.getByRole("checkbox", { name: "Miriam Muster" }));
    await user.click(
      screen.getByRole("button", { name: "Materialbedarf speichern" }),
    );
    expect(
      await screen.findByText("Turnierbälle wurde angelegt."),
    ).toHaveAttribute("role", "status");
    expect(await screen.findByText("Tagesplan: Geländeturnier")).toBeVisible();

    await user.click(
      screen.getByRole("button", { name: "Turnierbälle bearbeiten" }),
    );
    const editQuantity = screen.getByRole("spinbutton", {
      name: "Menge des Materials",
    });
    await user.clear(editQuantity);
    await user.type(editQuantity, "6");
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Beschaffungsstatus" }),
      "2",
    );
    await user.click(
      screen.getByRole("button", { name: "Materialänderung speichern" }),
    );
    expect(
      await screen.findByText("Turnierbälle wurde gespeichert."),
    ).toHaveAttribute("role", "status");
    expect(screen.getByText("Beschafft")).toBeVisible();

    await user.click(
      screen.getByRole("button", { name: "Turnierbälle löschen" }),
    );
    await user.click(
      screen.getByRole("checkbox", {
        name: "Turnierbälle wirklich in den Papierkorb verschieben",
      }),
    );
    await user.click(
      screen.getByRole("button", {
        name: "Material in Papierkorb verschieben",
      }),
    );
    expect(
      await screen.findByText(
        "Turnierbälle wurde in den Papierkorb verschoben.",
      ),
    ).toHaveAttribute("role", "status");

    const createCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("/logistics/material") &&
        init?.method === "POST",
    );
    expect(JSON.parse(createCall?.[1]?.body as string)).toMatchObject({
      name: "Turnierbälle",
      quantity: { value: 4, unit: 4, customUnitName: null },
      responsibleUserIds: [memberId],
      status: 1,
      scheduleEntryId,
    });
    const updateCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/logistics/material/${materialId}`) &&
        init?.method === "PUT",
    );
    expect(updateCall?.[1]?.headers).toMatchObject({ "If-Match": '"1"' });
    const deleteCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/logistics/material/${materialId}`) &&
        init?.method === "DELETE",
    );
    expect(deleteCall?.[1]?.headers).toMatchObject({ "If-Match": '"2"' });
  });

  it("uploads and moves a material attachment to the camp trash", async () => {
    const materialId = "48000000-0000-0000-0000-000000000001";
    const attachmentId = "4f000000-0000-0000-0000-000000000001";
    const material = {
      id: materialId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      name: "Turnierbälle",
      description: null,
      quantity: { value: 4, unit: 4, customUnitName: null },
      responsibleUserIds: [],
      procurementSource: null,
      note: null,
      status: 0,
      scheduleEntryId: null,
      version: 1,
    };
    const attachment = {
      id: attachmentId,
      originalFileName: "Aufbauplan.pdf",
      mediaType: 0,
      contentType: "application/pdf",
      sizeBytes: 204800,
      version: 2,
    };
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
        if (
          init?.method === "POST" &&
          path.includes(
            `/files?ownerType=MaterialRequirement&ownerId=${materialId}`,
          )
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...attachment,
                id: "4f000000-0000-0000-0000-000000000002",
                originalFileName: "Materialliste.png",
                mediaType: 3,
                contentType: "image/png",
                sizeBytes: 8,
                version: 1,
              }),
              { status: 201, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (
          init?.method === "DELETE" &&
          path.endsWith(`/files/${attachmentId}`)
        )
          return Promise.resolve(new Response(null, { status: 204 }));
        if (path.endsWith("/files/quota"))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                limitBytes: 1073741824,
                usedBytes: 1048576,
                pendingBytes: 0,
                availableBytes: 1072693248,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (
          path.includes(
            `/files?ownerType=MaterialRequirement&ownerId=${materialId}`,
          )
        )
          return Promise.resolve(
            new Response(JSON.stringify([attachment]), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (path.endsWith(`/logistics/material/${materialId}`))
          return Promise.resolve(
            new Response(JSON.stringify(material), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (path.endsWith("/logistics/material"))
          return Promise.resolve(
            new Response(JSON.stringify([material]), {
              status: 200,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/logistik");

    await user.click(
      await screen.findByRole("button", { name: "Turnierbälle öffnen" }),
    );
    const files = await screen.findByRole("region", {
      name: "Dateien zu Turnierbälle",
    });
    expect(await within(files).findByText("Aufbauplan.pdf")).toBeVisible();
    expect(within(files).getByText("1 MiB von 1.024 MiB belegt")).toBeVisible();
    const upload = new File(
      [new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])],
      "Materialliste.png",
      { type: "image/png" },
    );
    await user.upload(
      within(files).getByLabelText("Datei für das Material"),
      upload,
    );
    await user.click(
      within(files).getByRole("button", {
        name: "Materialliste.png hochladen",
      }),
    );
    expect(
      await within(files).findByText(
        "Materialliste.png wurde sicher hochgeladen.",
      ),
    ).toHaveAttribute("role", "status");

    await user.click(
      within(files).getByRole("button", { name: "Aufbauplan.pdf löschen" }),
    );
    await user.click(
      within(files).getByRole("checkbox", {
        name: "Aufbauplan.pdf wirklich in den Papierkorb verschieben",
      }),
    );
    await user.click(
      within(files).getByRole("button", {
        name: "Datei in Papierkorb verschieben",
      }),
    );
    expect(
      await within(files).findByText(
        "Aufbauplan.pdf wurde in den Papierkorb verschoben.",
      ),
    ).toHaveAttribute("role", "status");
    const deleteCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/files/${attachmentId}`) &&
        init?.method === "DELETE",
    );
    expect(deleteCall?.[1]?.headers).toMatchObject({ "If-Match": '"2"' });
  });

  it("adds and checks live shopping items with independent versions", async () => {
    const listId = "49000000-0000-0000-0000-000000000001";
    const potatoId = "4a000000-0000-0000-0000-000000000001";
    const breadId = "4a000000-0000-0000-0000-000000000002";
    const potato = {
      id: potatoId,
      shoppingListId: listId,
      name: "Kartoffeln",
      quantity: { value: 12, unit: 1, customUnitName: null },
      responsibleUserIds: [],
      store: "Großmarkt",
      note: null,
      source: { kind: 1, label: "Mittagessen · Kartoffelsuppe" },
      isChecked: false,
      checkedByUserId: null,
      checkedAt: null,
      version: 2,
    };
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
        if (
          init?.method === "POST" &&
          path.endsWith(`/shopping-lists/${listId}/items`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                shoppingListId: listId,
                listVersion: 6,
                changeSequence: 12,
                item: {
                  ...potato,
                  id: breadId,
                  name: "Fladenbrot",
                  quantity: { value: 2.5, unit: 4, customUnitName: null },
                  store: null,
                  source: { kind: 0, label: "Spontan" },
                  version: 1,
                },
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (
          init?.method === "PATCH" &&
          path.endsWith(`/shopping-lists/${listId}/items/${potatoId}/checked`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                shoppingListId: listId,
                listVersion: 7,
                changeSequence: 13,
                item: {
                  ...potato,
                  isChecked: true,
                  checkedByUserId: "10000000-0000-0000-0000-000000000001",
                  checkedAt: "2026-08-09T18:40:00Z",
                  version: 3,
                },
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith(`/logistics/shopping-lists/${listId}`))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                id: listId,
                organizationId: "20000000-0000-0000-0000-000000000001",
                campId: "30000000-0000-0000-0000-000000000001",
                name: "Großeinkauf Dienstag",
                items: [potato],
                version: 5,
                changeSequence: 11,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/logistics/shopping-lists"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: listId,
                  name: "Großeinkauf Dienstag",
                  openItemCount: 1,
                  checkedItemCount: 0,
                  version: 5,
                  changeSequence: 11,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/logistik");

    await user.click(
      await screen.findByRole("button", {
        name: "Großeinkauf Dienstag öffnen",
      }),
    );
    expect(
      await screen.findByText("Quelle: Mittagessen · Kartoffelsuppe"),
    ).toBeInTheDocument();
    await user.type(
      screen.getByRole("textbox", {
        name: "Bezeichnung der spontanen Position",
      }),
      "Fladenbrot",
    );
    await user.clear(
      screen.getByRole("spinbutton", { name: "Menge der spontanen Position" }),
    );
    await user.type(
      screen.getByRole("spinbutton", { name: "Menge der spontanen Position" }),
      "2.5",
    );
    await user.selectOptions(
      screen.getByRole("combobox", {
        name: "Einheit der spontanen Position",
      }),
      "4",
    );
    await user.click(
      screen.getByRole("button", { name: "Spontane Position hinzufügen" }),
    );
    expect(
      await screen.findByText("Fladenbrot wurde hinzugefügt."),
    ).toHaveAttribute("role", "status");
    await user.click(
      screen.getByRole("checkbox", { name: "Kartoffeln abhaken" }),
    );
    expect(
      await screen.findByRole("checkbox", { name: "Kartoffeln wieder öffnen" }),
    ).toBeChecked();

    const addCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/shopping-lists/${listId}/items`) &&
        init?.method === "POST",
    );
    expect(addCall?.[1]?.headers).toEqual({
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"5"',
    });
    expect(JSON.parse(addCall?.[1]?.body as string)).toMatchObject({
      name: "Fladenbrot",
      quantity: { value: 2.5, unit: 4, customUnitName: null },
      responsibleUserIds: [],
    });
    const checkCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(
          `/shopping-lists/${listId}/items/${potatoId}/checked`,
        ) && init?.method === "PATCH",
    );
    expect(checkCall?.[1]?.headers).toEqual({
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"2"',
    });
    expect(JSON.parse(checkCall?.[1]?.body as string)).toEqual({
      isChecked: true,
    });
  });

  it("edits a sourced shopping item and moves it to the trash with chained versions", async () => {
    const listId = "49000000-0000-0000-0000-000000000001";
    const itemId = "4a000000-0000-0000-0000-000000000001";
    const memberId = "10000000-0000-0000-0000-000000000001";
    const item = {
      id: itemId,
      shoppingListId: listId,
      name: "Kartoffeln",
      quantity: { value: 12, unit: 1, customUnitName: null },
      responsibleUserIds: [],
      store: "Großmarkt",
      note: null,
      source: { kind: 1, label: "Mittagessen · Kartoffelsuppe" },
      isChecked: false,
      checkedByUserId: null,
      checkedAt: null,
      version: 2,
    };
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
        if (init?.method === "PUT" && path.endsWith(`/items/${itemId}`))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                shoppingListId: listId,
                listVersion: 5,
                changeSequence: 12,
                item: {
                  ...item,
                  quantity: { value: 13.5, unit: 0, customUnitName: null },
                  responsibleUserIds: [memberId],
                  note: "Festkochend",
                  version: 3,
                },
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (init?.method === "DELETE" && path.endsWith(`/items/${itemId}`))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                shoppingListId: listId,
                listVersion: 6,
                changeSequence: 13,
                item: null,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (
          init?.method === "PUT" &&
          path.endsWith(`/logistics/shopping-lists/${listId}`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                id: listId,
                organizationId: "20000000-0000-0000-0000-000000000001",
                campId: "30000000-0000-0000-0000-000000000001",
                name: "Wocheneinkauf",
                items: [],
                version: 7,
                changeSequence: 14,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (
          init?.method === "DELETE" &&
          path.endsWith(`/logistics/shopping-lists/${listId}`)
        )
          return Promise.resolve(new Response(null, { status: 204 }));
        if (path.endsWith("/responsibility-candidates"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                { userId: memberId, displayName: "Miriam Muster" },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith(`/logistics/shopping-lists/${listId}`))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                id: listId,
                organizationId: "20000000-0000-0000-0000-000000000001",
                campId: "30000000-0000-0000-0000-000000000001",
                name: "Großeinkauf Dienstag",
                items: [item],
                version: 5,
                changeSequence: 11,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/logistics/shopping-lists"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: listId,
                  name: "Großeinkauf Dienstag",
                  openItemCount: 1,
                  checkedItemCount: 0,
                  version: 5,
                  changeSequence: 11,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/logistik");

    await user.click(
      await screen.findByRole("button", {
        name: "Großeinkauf Dienstag öffnen",
      }),
    );
    await user.click(
      await screen.findByRole("button", { name: "Kartoffeln bearbeiten" }),
    );
    const quantity = screen.getByRole("spinbutton", {
      name: "Menge für Kartoffeln bearbeiten",
    });
    await user.clear(quantity);
    await user.type(quantity, "13.5");
    await user.selectOptions(
      screen.getByRole("combobox", {
        name: "Einheit für Kartoffeln bearbeiten",
      }),
      "0",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Notiz für Kartoffeln bearbeiten" }),
      "Festkochend",
    );
    await user.click(screen.getByRole("checkbox", { name: "Miriam Muster" }));
    await user.click(
      screen.getByRole("button", { name: "Position speichern" }),
    );
    expect(
      await screen.findByText("Kartoffeln wurde gespeichert."),
    ).toHaveAttribute("role", "status");
    await user.click(
      screen.getByRole("button", { name: "Kartoffeln löschen" }),
    );
    await user.click(
      screen.getByRole("checkbox", {
        name: "Kartoffeln wirklich in den Papierkorb verschieben",
      }),
    );
    await user.click(
      screen.getByRole("button", {
        name: "Position in Papierkorb verschieben",
      }),
    );
    expect(
      await screen.findByText("Kartoffeln wurde in den Papierkorb verschoben."),
    ).toHaveAttribute("role", "status");
    expect(screen.queryByText("13,5 Gramm Kartoffeln")).not.toBeInTheDocument();

    const editCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/items/${itemId}`) &&
        init?.method === "PUT",
    );
    expect(editCall?.[1]?.headers).toMatchObject({
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"2"',
    });
    expect(JSON.parse(editCall?.[1]?.body as string)).toMatchObject({
      quantity: { value: 13.5, unit: 0, customUnitName: null },
      responsibleUserIds: [memberId],
      note: "Festkochend",
    });
    const deleteCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/items/${itemId}`) &&
        init?.method === "DELETE",
    );
    expect(deleteCall?.[1]?.headers).toMatchObject({
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"3"',
    });

    await user.click(
      screen.getByRole("button", { name: "Großeinkauf Dienstag umbenennen" }),
    );
    const listName = screen.getByRole("textbox", {
      name: "Listenname bearbeiten",
    });
    await user.clear(listName);
    await user.type(listName, "Wocheneinkauf");
    await user.click(
      screen.getByRole("button", { name: "Listennamen speichern" }),
    );
    expect(
      await screen.findByText("Wocheneinkauf wurde umbenannt."),
    ).toHaveAttribute("role", "status");
    await user.click(
      screen.getByRole("button", { name: "Wocheneinkauf löschen" }),
    );
    await user.click(
      screen.getByRole("checkbox", {
        name: "Wocheneinkauf wirklich in den Papierkorb verschieben",
      }),
    );
    await user.click(
      screen.getByRole("button", {
        name: "Einkaufsliste in Papierkorb verschieben",
      }),
    );
    expect(
      await screen.findByText(
        "Wocheneinkauf wurde in den Papierkorb verschoben.",
      ),
    ).toHaveAttribute("role", "status");
    const renameCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/shopping-lists/${listId}`) &&
        init?.method === "PUT",
    );
    expect(renameCall?.[1]?.headers).toMatchObject({ "If-Match": '"6"' });
    const deleteListCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/shopping-lists/${listId}`) &&
        init?.method === "DELETE",
    );
    expect(deleteListCall?.[1]?.headers).toMatchObject({ "If-Match": '"7"' });
  });

  it("opens an attributed Bible snapshot and refreshes it only explicitly", async () => {
    const devotionId = "51000000-0000-0000-0000-000000000001";
    const existingSnapshot = {
      reference: "Johannes 3,16",
      textExcerpt: "Denn also hat Gott die Welt geliebt.",
      technicalTranslationId: "deu1951",
      translationDisplayName: "Schlachter 1951",
      license: "CC BY 4.0",
      attribution: "Genfer Bibelgesellschaft",
      retrievedAt: "2026-08-08T10:00:00Z",
      origin: 0,
    };
    const devotion = {
      id: devotionId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      topic: "Gottes Liebe",
      bibleReference: "Johannes 3,16",
      translation: 0,
      coreMessage: "Gottes Liebe gilt allen.",
      markdownContent: "## Einstieg\nEine kurze Geschichte.",
      responsibleUserIds: [],
      materialNotes: "Kerze",
      scheduleEntryId: null,
      bibleSnapshot: existingSnapshot,
      createdAt: "2026-08-08T09:00:00Z",
      updatedAt: "2026-08-08T10:00:00Z",
      deletedAt: null,
      version: 3,
    };
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
        if (
          init?.method === "POST" &&
          path.endsWith(`/devotions/${devotionId}/bible/refresh`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                status: 0,
                devotion: {
                  ...devotion,
                  bibleSnapshot: {
                    ...existingSnapshot,
                    textExcerpt: "Denn so sehr hat Gott die Welt geliebt.",
                    retrievedAt: "2026-08-09T20:00:00Z",
                  },
                  updatedAt: "2026-08-09T20:00:00Z",
                  version: 4,
                },
              }),
              {
                status: 200,
                headers: { "Content-Type": "application/json", ETag: '"4"' },
              },
            ),
          );
        if (path.endsWith(`/devotions/${devotionId}`))
          return Promise.resolve(
            new Response(JSON.stringify(devotion), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"3"' },
            }),
          );
        if (path.endsWith("/devotions/translations"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  translation: 0,
                  technicalId: "deu1951",
                  displayName: "Schlachter 1951",
                  license: "CC BY 4.0",
                  attribution: "Genfer Bibelgesellschaft",
                  isDefault: true,
                },
                {
                  translation: 1,
                  technicalId: "deu1912",
                  displayName: "Luther 1912",
                  license: "Public Domain",
                  attribution: "Public Domain",
                  isDefault: false,
                },
                {
                  translation: 2,
                  technicalId: "deuelo",
                  displayName: "Unrevidierte Elberfelder",
                  license: "Public Domain",
                  attribution: "Public Domain",
                  isDefault: false,
                },
                {
                  translation: 3,
                  technicalId: "deutkw",
                  displayName: "Textbibel",
                  license: "Public Domain",
                  attribution: "Public Domain",
                  isDefault: false,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/devotions"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: devotionId,
                  topic: "Gottes Liebe",
                  bibleReference: "Johannes 3,16",
                  translation: 0,
                  responsibleUserIds: [],
                  scheduleEntryId: null,
                  hasBibleSnapshot: true,
                  version: 3,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/andachten");

    await user.click(
      await screen.findByRole("button", { name: "Gottes Liebe öffnen" }),
    );
    expect(await screen.findByText(existingSnapshot.textExcerpt)).toBeVisible();
    expect(screen.getByText("CC BY 4.0")).toBeVisible();
    expect(screen.getByText("Genfer Bibelgesellschaft")).toBeVisible();
    expect(screen.getByText("Snapshot vom 08.08.2026")).toBeVisible();
    await user.click(
      screen.getByRole("button", {
        name: "Bibeltext ausdrücklich aktualisieren",
      }),
    );
    expect(
      await screen.findByText("Denn so sehr hat Gott die Welt geliebt."),
    ).toBeVisible();
    expect(
      screen.getByText("Bibeltext wurde als neuer Snapshot gespeichert."),
    ).toHaveAttribute("role", "status");
    const refreshCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(
          `/devotions/${devotionId}/bible/refresh`,
        ) && init?.method === "POST",
    );
    expect(refreshCall?.[1]?.headers).toMatchObject({
      "X-CSRF-TOKEN": "csrf-token",
      "If-Match": '"3"',
    });
  });

  it.each([
    [
      1,
      "Die Bibelstelle wurde beim Provider nicht gefunden. Der bisherige Snapshot bleibt erhalten.",
    ],
    [
      2,
      "Der Bibel-Provider ist nicht erreichbar. Der bisherige Snapshot bleibt erhalten.",
    ],
    [
      3,
      "Der Bibel-Provider hat nicht rechtzeitig geantwortet. Der bisherige Snapshot bleibt erhalten.",
    ],
  ])(
    "keeps the existing Bible snapshot for provider refresh status %s",
    async (status, expectedMessage) => {
      const devotionId = "51000000-0000-0000-0000-000000000003";
      const snapshotText = "Der Gott der Hoffnung erfülle euch mit Freude.";
      const devotion = {
        id: devotionId,
        organizationId: "20000000-0000-0000-0000-000000000001",
        campId: "30000000-0000-0000-0000-000000000001",
        topic: "Hoffnung bewahren",
        bibleReference: "Römer 15,13",
        translation: 0,
        coreMessage: "Gott bleibt bei uns.",
        markdownContent: "## Austausch",
        responsibleUserIds: [],
        materialNotes: "",
        scheduleEntryId: null,
        bibleSnapshot: {
          reference: "Römer 15,13",
          textExcerpt: snapshotText,
          technicalTranslationId: "deu1951",
          translationDisplayName: "Schlachter 1951",
          license: "CC BY 4.0",
          attribution: "Genfer Bibelgesellschaft",
          retrievedAt: "2026-08-08T12:00:00Z",
          origin: 0,
        },
        createdAt: "2026-08-08T10:00:00Z",
        updatedAt: "2026-08-08T12:00:00Z",
        deletedAt: null,
        version: 3,
      };
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
          if (
            init?.method === "POST" &&
            path.endsWith(`/devotions/${devotionId}/bible/refresh`)
          )
            return Promise.resolve(
              new Response(JSON.stringify({ status, devotion }), {
                status: 200,
                headers: { "Content-Type": "application/json" },
              }),
            );
          if (path.endsWith(`/devotions/${devotionId}`))
            return Promise.resolve(
              new Response(JSON.stringify(devotion), {
                status: 200,
                headers: { "Content-Type": "application/json" },
              }),
            );
          if (path.endsWith("/devotions"))
            return Promise.resolve(
              new Response(
                JSON.stringify([
                  {
                    id: devotionId,
                    topic: devotion.topic,
                    bibleReference: devotion.bibleReference,
                    translation: devotion.translation,
                    responsibleUserIds: [],
                    scheduleEntryId: null,
                    hasBibleSnapshot: true,
                    version: 3,
                  },
                ]),
                {
                  status: 200,
                  headers: { "Content-Type": "application/json" },
                },
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
      renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/andachten");

      await user.click(
        await screen.findByRole("button", {
          name: "Hoffnung bewahren öffnen",
        }),
      );
      expect(await screen.findByText(snapshotText)).toBeVisible();
      await user.click(
        screen.getByRole("button", {
          name: "Bibeltext ausdrücklich aktualisieren",
        }),
      );
      expect(await screen.findByText(expectedMessage)).toHaveAttribute(
        "role",
        "status",
      );
      expect(screen.getByText(snapshotText)).toBeVisible();
      expect(screen.getByText("Snapshot vom 08.08.2026")).toBeVisible();
    },
  );

  it("creates a schedule-linked devotion and stores a manual Bible snapshot", async () => {
    const devotionId = "51000000-0000-0000-0000-000000000001";
    const scheduleEntryId = "32000000-0000-0000-0000-000000000001";
    const memberId = "10000000-0000-0000-0000-000000000001";
    const created = {
      id: devotionId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      topic: "Mut zum Vertrauen",
      bibleReference: "Psalm 23,1",
      translation: 1,
      coreMessage: "Gott begleitet uns.",
      markdownContent: "## Einstieg\nGemeinsamer Rückblick.",
      responsibleUserIds: [memberId],
      materialNotes: "Tücher",
      scheduleEntryId,
      bibleSnapshot: null,
      createdAt: "2026-08-09T20:00:00Z",
      updatedAt: "2026-08-09T20:00:00Z",
      deletedAt: null,
      version: 1,
    };
    let summaries: unknown[] = [];
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
        if (init?.method === "POST" && path.endsWith("/devotions")) {
          summaries = [
            {
              id: devotionId,
              topic: created.topic,
              bibleReference: created.bibleReference,
              translation: created.translation,
              responsibleUserIds: [memberId],
              scheduleEntryId,
              hasBibleSnapshot: false,
              version: 1,
            },
          ];
          return Promise.resolve(
            new Response(JSON.stringify(created), {
              status: 201,
              headers: { "Content-Type": "application/json", ETag: '"1"' },
            }),
          );
        }
        if (
          init?.method === "PUT" &&
          path.endsWith(`/devotions/${devotionId}/bible/manual`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...created,
                bibleSnapshot: {
                  reference: "Psalm 23,1",
                  textExcerpt:
                    "Der HERR ist mein Hirte; mir wird nichts mangeln.",
                  technicalTranslationId: "deu1912",
                  translationDisplayName: "Luther 1912",
                  license: "Public Domain",
                  attribution: "Manuell eingetragen · Luther 1912",
                  retrievedAt: "2026-08-09T20:05:00Z",
                  origin: 1,
                },
                updatedAt: "2026-08-09T20:05:00Z",
                version: 2,
              }),
              {
                status: 200,
                headers: { "Content-Type": "application/json", ETag: '"2"' },
              },
            ),
          );
        if (path.endsWith("/devotions/translations"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  translation: 0,
                  technicalId: "deu1951",
                  displayName: "Schlachter 1951",
                  license: "CC BY 4.0",
                  attribution: "Genfer Bibelgesellschaft",
                  isDefault: true,
                },
                {
                  translation: 1,
                  technicalId: "deu1912",
                  displayName: "Luther 1912",
                  license: "Public Domain",
                  attribution: "Public Domain",
                  isDefault: false,
                },
                {
                  translation: 2,
                  technicalId: "deuelo",
                  displayName: "Unrevidierte Elberfelder",
                  license: "Public Domain",
                  attribution: "Public Domain",
                  isDefault: false,
                },
                {
                  translation: 3,
                  technicalId: "deutkw",
                  displayName: "Textbibel",
                  license: "Public Domain",
                  attribution: "Public Domain",
                  isDefault: false,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.includes("/schedule?"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: scheduleEntryId,
                  title: "Abendandacht",
                  category: "Andacht",
                  status: 1,
                  responsibleUserIds: [],
                  overlapsAnotherEntry: false,
                  timing: {
                    isAllDay: false,
                    startsAtUtc: "2026-08-05T18:00:00Z",
                    endsAtUtc: "2026-08-05T19:00:00Z",
                  },
                  version: 1,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/responsibility-candidates"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                { userId: memberId, displayName: "Miriam Muster" },
              ]),
              {
                status: 200,
                headers: { "Content-Type": "application/json" },
              },
            ),
          );
        if (path.endsWith("/devotions"))
          return Promise.resolve(
            new Response(JSON.stringify(summaries), {
              status: 200,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/andachten");

    await user.click(
      await screen.findByRole("button", { name: "Andacht entwerfen" }),
    );
    await user.type(
      screen.getByRole("textbox", { name: "Thema" }),
      created.topic,
    );
    await user.type(
      screen.getByRole("textbox", { name: "Bibelstelle" }),
      created.bibleReference,
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Bibelübersetzung" }),
      "1",
    );
    await user.type(
      screen.getByRole("textbox", { name: "Ziel oder Kerngedanke" }),
      created.coreMessage,
    );
    await user.type(
      screen.getByRole("textbox", { name: "Markdown-Inhalt oder Gliederung" }),
      created.markdownContent,
    );
    await user.type(
      screen.getByRole("textbox", { name: "Materialhinweise" }),
      created.materialNotes,
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Verknüpfung zum Tagesplan" }),
      scheduleEntryId,
    );
    await user.click(screen.getByRole("checkbox", { name: "Miriam Muster" }));
    await user.click(screen.getByRole("button", { name: "Andacht speichern" }));
    expect(
      await screen.findByText("Mut zum Vertrauen wurde angelegt."),
    ).toHaveAttribute("role", "status");
    expect(
      await screen.findByText(/Noch kein Bibeltext gespeichert/),
    ).toBeVisible();

    await user.click(
      screen.getByRole("button", { name: "Bibeltext manuell speichern" }),
    );
    await user.type(
      screen.getByRole("textbox", { name: "Manueller Bibeltext" }),
      "Der HERR ist mein Hirte; mir wird nichts mangeln.",
    );
    await user.click(
      screen.getByRole("button", { name: "Manuellen Snapshot speichern" }),
    );
    expect(await screen.findByText("Manuell gespeichert")).toBeVisible();
    expect(screen.getByText("Public Domain")).toBeVisible();

    const createCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("/devotions") && init?.method === "POST",
    );
    expect(JSON.parse(createCall?.[1]?.body as string)).toMatchObject({
      topic: created.topic,
      bibleReference: created.bibleReference,
      translation: 1,
      responsibleUserIds: [memberId],
      scheduleEntryId,
    });
    const manualCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(
          `/devotions/${devotionId}/bible/manual`,
        ) && init?.method === "PUT",
    );
    expect(manualCall?.[1]?.headers).toMatchObject({ "If-Match": '"1"' });
    expect(JSON.parse(manualCall?.[1]?.body as string)).toEqual({
      reference: "Psalm 23,1",
      translation: 1,
      textExcerpt: "Der HERR ist mein Hirte; mir wird nichts mangeln.",
    });
  });

  it("edits a devotion and moves the current version to the trash", async () => {
    const devotionId = "51000000-0000-0000-0000-000000000002";
    const existing = {
      id: devotionId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      topic: "Hoffnung",
      bibleReference: "Römer 15,13",
      translation: 0,
      coreMessage: "Gott schenkt Hoffnung.",
      markdownContent: "## Gespräch",
      responsibleUserIds: [],
      materialNotes: "",
      scheduleEntryId: null,
      bibleSnapshot: null,
      createdAt: "2026-08-09T18:00:00Z",
      updatedAt: "2026-08-09T18:00:00Z",
      deletedAt: null,
      version: 2,
    };
    const updated = {
      ...existing,
      topic: "Hoffnung trägt",
      coreMessage: "Gott trägt durch schwere Zeiten.",
      updatedAt: "2026-08-09T19:00:00Z",
      version: 3,
    };
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
        if (init?.method === "PUT" && path.endsWith(`/devotions/${devotionId}`))
          return Promise.resolve(
            new Response(JSON.stringify(updated), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"3"' },
            }),
          );
        if (
          init?.method === "DELETE" &&
          path.endsWith(`/devotions/${devotionId}`)
        )
          return Promise.resolve(new Response(null, { status: 204 }));
        if (path.endsWith(`/devotions/${devotionId}`))
          return Promise.resolve(
            new Response(JSON.stringify(existing), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"2"' },
            }),
          );
        if (path.endsWith("/devotions/translations"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  translation: 0,
                  technicalId: "deu1951",
                  displayName: "Schlachter 1951",
                  license: "CC BY 4.0",
                  attribution: "Genfer Bibelgesellschaft",
                  isDefault: true,
                },
              ]),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith("/devotions"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: devotionId,
                  topic: existing.topic,
                  bibleReference: existing.bibleReference,
                  translation: 0,
                  responsibleUserIds: [],
                  scheduleEntryId: null,
                  hasBibleSnapshot: false,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/andachten");

    await user.click(
      await screen.findByRole("button", { name: "Hoffnung öffnen" }),
    );
    await user.click(
      await screen.findByRole("button", { name: "Andacht bearbeiten" }),
    );
    const topic = screen.getByRole("textbox", { name: "Thema bearbeiten" });
    await user.clear(topic);
    await user.type(topic, updated.topic);
    const coreMessage = screen.getByRole("textbox", {
      name: "Ziel oder Kerngedanke bearbeiten",
    });
    await user.clear(coreMessage);
    await user.type(coreMessage, updated.coreMessage);
    await user.click(
      screen.getByRole("button", { name: "Andachtsänderung speichern" }),
    );
    expect(
      await screen.findByText("Hoffnung trägt wurde gespeichert."),
    ).toHaveAttribute("role", "status");

    await user.click(
      screen.getByRole("button", {
        name: "Andacht in Papierkorb verschieben",
      }),
    );
    await user.click(
      screen.getByRole("checkbox", {
        name: "Ich möchte diese Andacht in den Papierkorb verschieben.",
      }),
    );
    await user.click(
      screen.getByRole("button", { name: "Verschieben bestätigen" }),
    );
    expect(
      await screen.findByText(
        "Hoffnung trägt wurde in den Papierkorb verschoben.",
      ),
    ).toHaveAttribute("role", "status");
    expect(screen.queryByText("Hoffnung trägt", { selector: "h2" })).toBeNull();

    const updateCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/devotions/${devotionId}`) &&
        init?.method === "PUT",
    );
    expect(updateCall?.[1]?.headers).toMatchObject({ "If-Match": '"2"' });
    expect(JSON.parse(updateCall?.[1]?.body as string)).toMatchObject({
      topic: updated.topic,
      coreMessage: updated.coreMessage,
    });
    const deleteCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/devotions/${devotionId}`) &&
        init?.method === "DELETE",
    );
    expect(deleteCall?.[1]?.headers).toMatchObject({ "If-Match": '"3"' });
  });

  it("uploads and moves a devotion attachment to the camp trash", async () => {
    const devotionId = "51000000-0000-0000-0000-000000000004";
    const attachmentId = "4f000000-0000-0000-0000-000000000004";
    const devotion = {
      id: devotionId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      topic: "Licht der Welt",
      bibleReference: "Matthäus 5,14",
      translation: 0,
      coreMessage: "Wir bringen Licht in die Welt.",
      markdownContent: "## Einstieg",
      responsibleUserIds: [],
      materialNotes: "Kerzen",
      scheduleEntryId: null,
      bibleSnapshot: null,
      createdAt: "2026-08-09T18:00:00Z",
      updatedAt: "2026-08-09T18:00:00Z",
      deletedAt: null,
      version: 1,
    };
    const attachment = {
      id: attachmentId,
      originalFileName: "Impulsfragen.pdf",
      mediaType: 0,
      contentType: "application/pdf",
      sizeBytes: 102400,
      version: 2,
    };
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
        if (
          init?.method === "POST" &&
          path.includes(`/files?ownerType=Devotion&ownerId=${devotionId}`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...attachment,
                id: "4f000000-0000-0000-0000-000000000005",
                originalFileName: "Kerzenbild.png",
                mediaType: 2,
                contentType: "image/png",
                sizeBytes: 8,
                version: 1,
              }),
              { status: 201, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (
          init?.method === "DELETE" &&
          path.endsWith(`/files/${attachmentId}`)
        )
          return Promise.resolve(new Response(null, { status: 204 }));
        if (path.endsWith("/files/quota"))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                limitBytes: 1073741824,
                usedBytes: 1048576,
                pendingBytes: 0,
                availableBytes: 1072693248,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.includes(`/files?ownerType=Devotion&ownerId=${devotionId}`))
          return Promise.resolve(
            new Response(JSON.stringify([attachment]), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (path.endsWith(`/devotions/${devotionId}`))
          return Promise.resolve(
            new Response(JSON.stringify(devotion), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (path.endsWith("/devotions"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: devotionId,
                  topic: devotion.topic,
                  bibleReference: devotion.bibleReference,
                  translation: 0,
                  responsibleUserIds: [],
                  scheduleEntryId: null,
                  hasBibleSnapshot: false,
                  version: 1,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/andachten");

    await user.click(
      await screen.findByRole("button", { name: "Licht der Welt öffnen" }),
    );
    const files = await screen.findByRole("region", {
      name: "Dateien zu Licht der Welt",
    });
    expect(await within(files).findByText("Impulsfragen.pdf")).toBeVisible();
    const upload = new File(
      [new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])],
      "Kerzenbild.png",
      { type: "image/png" },
    );
    await user.upload(
      within(files).getByLabelText("Datei für die Andacht"),
      upload,
    );
    await user.click(
      within(files).getByRole("button", { name: "Kerzenbild.png hochladen" }),
    );
    expect(
      await within(files).findByText(
        "Kerzenbild.png wurde sicher hochgeladen.",
      ),
    ).toHaveAttribute("role", "status");

    await user.click(
      within(files).getByRole("button", { name: "Impulsfragen.pdf löschen" }),
    );
    await user.click(
      within(files).getByRole("checkbox", {
        name: "Impulsfragen.pdf wirklich in den Papierkorb verschieben",
      }),
    );
    await user.click(
      within(files).getByRole("button", {
        name: "Datei in Papierkorb verschieben",
      }),
    );
    expect(
      await within(files).findByText(
        "Impulsfragen.pdf wurde in den Papierkorb verschoben.",
      ),
    ).toHaveAttribute("role", "status");
    const deleteCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/files/${attachmentId}`) &&
        init?.method === "DELETE",
    );
    expect(deleteCall?.[1]?.headers).toMatchObject({ "If-Match": '"2"' });
  });

  it("creates and opens a shared pinned Markdown note", async () => {
    const noteId = "52000000-0000-0000-0000-000000000001";
    const created = {
      id: noteId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      title: "Erste Schritte",
      markdown:
        "## Treffpunkt\n**Wichtig:** Alle bringen ihr Namensschild mit.",
      renderedHtml:
        "<h2>Treffpunkt</h2><p><strong>Wichtig:</strong> Alle bringen ihr Namensschild mit.</p>",
      tags: ["Team", "Ablauf"],
      isPinned: true,
      links: [],
      state: 0,
      createdAt: "2026-08-09T20:00:00Z",
      createdBy: "10000000-0000-0000-0000-000000000001",
      updatedAt: "2026-08-09T20:00:00Z",
      updatedBy: "10000000-0000-0000-0000-000000000001",
      trashedAt: null,
      trashedBy: null,
      purgeAfter: null,
      version: 1,
    };
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
        if (init?.method === "POST" && path.endsWith("/notes"))
          return Promise.resolve(
            new Response(JSON.stringify(created), {
              status: 201,
              headers: { "Content-Type": "application/json", ETag: '"1"' },
            }),
          );
        if (path.endsWith("/notes"))
          return Promise.resolve(
            new Response(JSON.stringify([]), {
              status: 200,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/notizen");

    await user.click(
      await screen.findByRole("button", { name: "Notiz anlegen" }),
    );
    await user.type(
      screen.getByRole("textbox", { name: "Titel" }),
      created.title,
    );
    await user.type(
      screen.getByRole("textbox", { name: "Markdown-Inhalt" }),
      created.markdown,
    );
    await user.type(
      screen.getByRole("textbox", { name: "Tags" }),
      "Team, Ablauf",
    );
    await user.click(screen.getByRole("checkbox", { name: "Notiz anheften" }));
    await user.click(screen.getByRole("button", { name: "Notiz speichern" }));

    expect(
      await screen.findByText("Erste Schritte wurde angelegt."),
    ).toHaveAttribute("role", "status");
    expect(
      await screen.findByRole("heading", { name: "Treffpunkt" }),
    ).toBeVisible();
    expect(screen.getByText("Wichtig:", { selector: "strong" })).toBeVisible();
    expect(screen.getByText("Team · Ablauf")).toBeVisible();
    const createCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("/notes") && init?.method === "POST",
    );
    expect(JSON.parse(createCall?.[1]?.body as string)).toEqual({
      title: created.title,
      markdown: created.markdown,
      tags: ["Team", "Ablauf"],
      isPinned: true,
      links: [],
    });
  });

  it("revises a shared note and moves the current version to the trash", async () => {
    const noteId = "52000000-0000-0000-0000-000000000002";
    const existing = {
      id: noteId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      title: "Packliste",
      markdown: "## Kleidung\nWarme Jacke",
      renderedHtml: "<h2>Kleidung</h2><p>Warme Jacke</p>",
      tags: ["Vorbereitung"],
      isPinned: false,
      links: [],
      state: 0,
      createdAt: "2026-08-09T18:00:00Z",
      createdBy: "10000000-0000-0000-0000-000000000001",
      updatedAt: "2026-08-09T18:00:00Z",
      updatedBy: "10000000-0000-0000-0000-000000000001",
      trashedAt: null,
      trashedBy: null,
      purgeAfter: null,
      version: 1,
    };
    const revised = {
      ...existing,
      title: "Packliste Team",
      markdown: "## Kleidung\nWarme Jacke und Regenzeug",
      renderedHtml: "<h2>Kleidung</h2><p>Warme Jacke und Regenzeug</p>",
      tags: ["Team", "Vorbereitung"],
      isPinned: true,
      updatedAt: "2026-08-09T19:00:00Z",
      version: 2,
    };
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
        if (init?.method === "PUT" && path.endsWith(`/notes/${noteId}`))
          return Promise.resolve(
            new Response(JSON.stringify(revised), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"2"' },
            }),
          );
        if (init?.method === "DELETE" && path.endsWith(`/notes/${noteId}`))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...revised,
                state: 1,
                trashedAt: "2026-08-09T20:00:00Z",
                purgeAfter: "2026-09-08T20:00:00Z",
                version: 3,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.endsWith(`/notes/${noteId}`))
          return Promise.resolve(
            new Response(JSON.stringify(existing), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"1"' },
            }),
          );
        if (path.endsWith("/notes"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: noteId,
                  title: existing.title,
                  plainTextExcerpt: "Kleidung Warme Jacke",
                  tags: existing.tags,
                  isPinned: false,
                  linkCount: 0,
                  state: 0,
                  updatedAt: existing.updatedAt,
                  trashedAt: null,
                  purgeAfter: null,
                  version: 1,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/notizen");

    await user.click(
      await screen.findByRole("button", { name: "Packliste öffnen" }),
    );
    await user.click(
      await screen.findByRole("button", { name: "Notiz bearbeiten" }),
    );
    const title = screen.getByRole("textbox", { name: "Titel bearbeiten" });
    await user.clear(title);
    await user.type(title, revised.title);
    const markdown = screen.getByRole("textbox", {
      name: "Markdown-Inhalt bearbeiten",
    });
    await user.clear(markdown);
    await user.type(markdown, revised.markdown);
    const tags = screen.getByRole("textbox", { name: "Tags bearbeiten" });
    await user.clear(tags);
    await user.type(tags, "Team, Vorbereitung");
    await user.click(screen.getByRole("checkbox", { name: "Notiz anheften" }));
    await user.click(
      screen.getByRole("button", { name: "Notizänderung speichern" }),
    );
    expect(
      await screen.findByText("Packliste Team wurde gespeichert."),
    ).toHaveAttribute("role", "status");

    await user.click(
      screen.getByRole("button", { name: "Notiz in Papierkorb verschieben" }),
    );
    await user.click(
      screen.getByRole("checkbox", {
        name: "Ich möchte diese Notiz in den Papierkorb verschieben.",
      }),
    );
    await user.click(
      screen.getByRole("button", { name: "Verschieben bestätigen" }),
    );
    expect(
      await screen.findByText(
        "Packliste Team wurde in den Papierkorb verschoben.",
      ),
    ).toHaveAttribute("role", "status");
    const updateCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/notes/${noteId}`) &&
        init?.method === "PUT",
    );
    expect(updateCall?.[1]?.headers).toMatchObject({ "If-Match": '"1"' });
    expect(JSON.parse(updateCall?.[1]?.body as string)).toMatchObject({
      title: revised.title,
      markdown: revised.markdown,
      tags: revised.tags,
      isPinned: true,
      links: [],
    });
    const deleteCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/notes/${noteId}`) &&
        init?.method === "DELETE",
    );
    expect(deleteCall?.[1]?.headers).toMatchObject({ "If-Match": '"2"' });
  });

  it("links a shared note to typed planning objects", async () => {
    const noteId = "52000000-0000-0000-0000-000000000003";
    const scheduleEntryId = "32000000-0000-0000-0000-000000000003";
    const mealId = "42000000-0000-0000-0000-000000000003";
    const created = {
      id: noteId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      title: "Teamabsprachen",
      markdown: "## Sonntag\nDetails stehen in der Planung.",
      renderedHtml: "<h2>Sonntag</h2><p>Details stehen in der Planung.</p>",
      tags: ["Team"],
      isPinned: false,
      links: [
        { type: 0, targetId: scheduleEntryId, targetTitle: "Morgenandacht" },
        { type: 1, targetId: mealId, targetTitle: "Abendessen" },
      ],
      state: 0,
      createdAt: "2026-08-09T20:00:00Z",
      createdBy: "10000000-0000-0000-0000-000000000001",
      updatedAt: "2026-08-09T20:00:00Z",
      updatedBy: "10000000-0000-0000-0000-000000000001",
      trashedAt: null,
      trashedBy: null,
      purgeAfter: null,
      version: 1,
    };
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
        if (init?.method === "POST" && path.endsWith("/notes"))
          return Promise.resolve(
            new Response(JSON.stringify(created), {
              status: 201,
              headers: { "Content-Type": "application/json", ETag: '"1"' },
            }),
          );
        let body: unknown = [];
        if (path.includes("/schedule?"))
          body = [
            {
              id: scheduleEntryId,
              title: "Morgenandacht",
              category: "Andacht",
              status: 0,
              responsibleUserIds: [],
              overlapsAnotherEntry: false,
              timing: {
                isAllDay: false,
                startsAtUtc: "2026-08-02T07:00:00Z",
                endsAtUtc: "2026-08-02T08:00:00Z",
              },
              version: 1,
            },
          ];
        else if (path.endsWith("/catering/meals"))
          body = [
            {
              id: mealId,
              name: "Abendessen",
              effectivePortions: 42,
              scheduleEntryId: null,
              recipeCount: 0,
              version: 1,
            },
          ];
        else if (path.endsWith("/catering/recipes")) body = [];
        else if (path.endsWith("/logistics/material")) body = [];
        else if (path.endsWith("/logistics/shopping-lists")) body = [];
        else if (path.endsWith("/devotions")) body = [];
        return Promise.resolve(
          new Response(JSON.stringify(body), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          }),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/notizen");

    await user.click(
      await screen.findByRole("button", { name: "Notiz anlegen" }),
    );
    await user.type(
      screen.getByRole("textbox", { name: "Titel" }),
      created.title,
    );
    await user.type(
      screen.getByRole("textbox", { name: "Markdown-Inhalt" }),
      created.markdown,
    );
    await user.click(
      await screen.findByRole("checkbox", {
        name: "Tagesplan: Morgenandacht",
      }),
    );
    await user.click(
      screen.getByRole("checkbox", { name: "Mahlzeit: Abendessen" }),
    );
    await user.click(screen.getByRole("button", { name: "Notiz speichern" }));

    expect(await screen.findByText("Tagesplan · Morgenandacht")).toBeVisible();
    expect(screen.getByText("Mahlzeit · Abendessen")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "Notiz bearbeiten" }));
    expect(
      await screen.findByRole("checkbox", {
        name: "Tagesplan: Morgenandacht",
      }),
    ).toBeChecked();
    expect(
      screen.getByRole("checkbox", { name: "Mahlzeit: Abendessen" }),
    ).toBeChecked();
    const createCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith("/notes") && init?.method === "POST",
    );
    expect(JSON.parse(createCall?.[1]?.body as string)).toMatchObject({
      links: [
        { type: 0, targetId: scheduleEntryId },
        { type: 1, targetId: mealId },
      ],
    });
  });

  it("uploads and moves a private note attachment to the camp trash", async () => {
    const noteId = "52000000-0000-0000-0000-000000000004";
    const attachmentId = "4f000000-0000-0000-0000-000000000006";
    const note = {
      id: noteId,
      organizationId: "20000000-0000-0000-0000-000000000001",
      campId: "30000000-0000-0000-0000-000000000001",
      title: "Teamabsprachen",
      markdown: "## Treffpunkt\nBitte pünktlich sein.",
      renderedHtml: "<h2>Treffpunkt</h2><p>Bitte pünktlich sein.</p>",
      tags: ["Team"],
      isPinned: false,
      links: [],
      state: 0,
      createdAt: "2026-08-09T20:00:00Z",
      createdBy: "10000000-0000-0000-0000-000000000001",
      updatedAt: "2026-08-09T20:00:00Z",
      updatedBy: "10000000-0000-0000-0000-000000000001",
      trashedAt: null,
      trashedBy: null,
      purgeAfter: null,
      version: 1,
    };
    const attachment = {
      id: attachmentId,
      originalFileName: "Treffpunkt.pdf",
      mediaType: 0,
      contentType: "application/pdf",
      sizeBytes: 204800,
      version: 2,
    };
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
        if (
          init?.method === "POST" &&
          path.includes(`/files?ownerType=Note&ownerId=${noteId}`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...attachment,
                id: "4f000000-0000-0000-0000-000000000007",
                originalFileName: "Lageplan.png",
                mediaType: 2,
                contentType: "image/png",
                sizeBytes: 8,
                version: 1,
              }),
              { status: 201, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (
          init?.method === "DELETE" &&
          path.endsWith(`/files/${attachmentId}`)
        )
          return Promise.resolve(new Response(null, { status: 204 }));
        if (path.endsWith("/files/quota"))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                limitBytes: 104857600,
                usedBytes: 1048576,
                pendingBytes: 0,
                availableBytes: 103809024,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.includes(`/files?ownerType=Note&ownerId=${noteId}`))
          return Promise.resolve(
            new Response(JSON.stringify([attachment]), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (path.endsWith(`/notes/${noteId}`))
          return Promise.resolve(
            new Response(JSON.stringify(note), {
              status: 200,
              headers: { "Content-Type": "application/json", ETag: '"1"' },
            }),
          );
        if (path.endsWith("/notes"))
          return Promise.resolve(
            new Response(
              JSON.stringify([
                {
                  id: noteId,
                  title: note.title,
                  plainTextExcerpt: "Treffpunkt Bitte pünktlich sein.",
                  tags: note.tags,
                  isPinned: false,
                  linkCount: 0,
                  state: 0,
                  updatedAt: note.updatedAt,
                  trashedAt: null,
                  purgeAfter: null,
                  version: 1,
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
    renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/notizen");

    await user.click(
      await screen.findByRole("button", { name: "Teamabsprachen öffnen" }),
    );
    const files = await screen.findByRole("region", {
      name: "Dateien zu Teamabsprachen",
    });
    expect(await within(files).findByText("Treffpunkt.pdf")).toBeVisible();
    const upload = new File(
      [new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])],
      "Lageplan.png",
      { type: "image/png" },
    );
    await user.upload(
      within(files).getByLabelText("Datei für die Notiz"),
      upload,
    );
    await user.click(
      within(files).getByRole("button", { name: "Lageplan.png hochladen" }),
    );
    expect(
      await within(files).findByText("Lageplan.png wurde sicher hochgeladen."),
    ).toHaveAttribute("role", "status");

    await user.click(
      within(files).getByRole("button", { name: "Treffpunkt.pdf löschen" }),
    );
    await user.click(
      within(files).getByRole("checkbox", {
        name: "Treffpunkt.pdf wirklich in den Papierkorb verschieben",
      }),
    );
    await user.click(
      within(files).getByRole("button", {
        name: "Datei in Papierkorb verschieben",
      }),
    );
    expect(
      await within(files).findByText(
        "Treffpunkt.pdf wurde in den Papierkorb verschoben.",
      ),
    ).toHaveAttribute("role", "status");
    const deleteCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/files/${attachmentId}`) &&
        init?.method === "DELETE",
    );
    expect(deleteCall?.[1]?.headers).toMatchObject({ "If-Match": '"2"' });
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
    let restored = false;
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
        if (init?.method === "POST") {
          restored = true;
          return Promise.resolve(new Response(null, { status: 200 }));
        }
        if (!path.endsWith("/trash"))
          return Promise.resolve(
            new Response(JSON.stringify([]), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        return Promise.resolve(
          new Response(
            JSON.stringify(
              restored
                ? []
                : [
                    {
                      objectType: "Note",
                      objectId: "41000000-0000-0000-0000-000000000001",
                      title: "Packliste",
                      deletedAt: "2026-08-09T10:00:00Z",
                      purgeAt: "2026-09-08T10:00:00Z",
                      version: 3,
                      restorePath: "/api/v1/restore-note",
                    },
                  ],
            ),
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

    expect(
      await screen.findByText("Packliste wurde wiederhergestellt."),
    ).toHaveAttribute("role", "status");
    expect(
      screen.queryByRole("button", { name: "Packliste wiederherstellen" }),
    ).toBeNull();

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

  it.each([
    ["ScheduleEntry", "schedule"],
    ["Meal", "meals"],
    ["MaterialRequirement", "material"],
    ["ShoppingList", "shopping-lists"],
    ["ShoppingItem", "shopping-lists"],
    ["Devotion", "devotions"],
    ["Note", "notes"],
    ["Attachment", "files"],
  ])(
    "refreshes %s, search and activity after aggregate restore",
    async (restoredType, queryScope) => {
      const organizationId = "20000000-0000-0000-0000-000000000001";
      const campId = "30000000-0000-0000-0000-000000000001";
      const invalidate = vi.spyOn(QueryClient.prototype, "invalidateQueries");
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
          if (init?.method === "POST")
            return Promise.resolve(new Response(null, { status: 200 }));
          if (path.endsWith("/trash"))
            return Promise.resolve(
              new Response(
                JSON.stringify([
                  {
                    objectType: restoredType,
                    objectId: "41000000-0000-0000-0000-000000000009",
                    title: restoredType,
                    deletedAt: "2026-08-09T10:00:00Z",
                    purgeAt: "2026-09-08T10:00:00Z",
                    version: 3,
                    restorePath: `/api/v1/restore-${restoredType}`,
                  },
                ]),
                {
                  status: 200,
                  headers: { "Content-Type": "application/json" },
                },
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
      renderRoute("/o/sonnenhoehe/camps/sommerfreizeit-2026/suche");

      await user.click(
        await screen.findByRole("button", {
          name: `${restoredType} wiederherstellen`,
        }),
      );
      expect(
        await screen.findByText(`${restoredType} wurde wiederhergestellt.`),
      ).toHaveAttribute("role", "status");
      expect(invalidate).toHaveBeenCalledWith({
        queryKey: [organizationId, campId, queryScope],
      });
      expect(invalidate).toHaveBeenCalledWith({
        queryKey: [organizationId, campId, "search"],
      });
      expect(invalidate).toHaveBeenCalledWith({
        queryKey: [organizationId, campId, "activity"],
      });
    },
  );

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

  it("uploads and moves a private schedule attachment to the camp trash", async () => {
    const entryId = "40000000-0000-0000-0000-000000000004";
    const attachmentId = "4f000000-0000-0000-0000-00000000000a";
    const entry = {
      id: entryId,
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
    const attachment = {
      id: attachmentId,
      originalFileName: "Stationsplan.pdf",
      mediaType: 0,
      contentType: "application/pdf",
      sizeBytes: 204800,
      version: 2,
    };
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
        if (
          init?.method === "POST" &&
          path.includes(`/files?ownerType=ScheduleEntry&ownerId=${entryId}`)
        )
          return Promise.resolve(
            new Response(
              JSON.stringify({
                ...attachment,
                id: "4f000000-0000-0000-0000-00000000000b",
                originalFileName: "Wegmarken.png",
                mediaType: 2,
                contentType: "image/png",
                sizeBytes: 8,
                version: 1,
              }),
              { status: 201, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (
          init?.method === "DELETE" &&
          path.endsWith(`/files/${attachmentId}`)
        )
          return Promise.resolve(new Response(null, { status: 204 }));
        if (path.endsWith("/files/quota"))
          return Promise.resolve(
            new Response(
              JSON.stringify({
                limitBytes: 104857600,
                usedBytes: 1048576,
                pendingBytes: 0,
                availableBytes: 103809024,
              }),
              { status: 200, headers: { "Content-Type": "application/json" } },
            ),
          );
        if (path.includes(`/files?ownerType=ScheduleEntry&ownerId=${entryId}`))
          return Promise.resolve(
            new Response(JSON.stringify([attachment]), {
              status: 200,
              headers: { "Content-Type": "application/json" },
            }),
          );
        if (path.includes("/schedule?"))
          return Promise.resolve(
            new Response(JSON.stringify([entry]), {
              status: 200,
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

    await user.click(
      await screen.findByRole("button", {
        name: "Dateien zu Geländespiel öffnen",
      }),
    );
    const files = await screen.findByRole("region", {
      name: "Dateien zu Geländespiel",
    });
    expect(await within(files).findByText("Stationsplan.pdf")).toBeVisible();
    const upload = new File(
      [new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a])],
      "Wegmarken.png",
      { type: "image/png" },
    );
    await user.upload(
      within(files).getByLabelText("Datei für den Zeitplaneintrag"),
      upload,
    );
    await user.click(
      within(files).getByRole("button", { name: "Wegmarken.png hochladen" }),
    );
    expect(
      await within(files).findByText("Wegmarken.png wurde sicher hochgeladen."),
    ).toHaveAttribute("role", "status");

    await user.click(
      within(files).getByRole("button", { name: "Stationsplan.pdf löschen" }),
    );
    await user.click(
      within(files).getByRole("checkbox", {
        name: "Stationsplan.pdf wirklich in den Papierkorb verschieben",
      }),
    );
    await user.click(
      within(files).getByRole("button", {
        name: "Datei in Papierkorb verschieben",
      }),
    );
    expect(
      await within(files).findByText(
        "Stationsplan.pdf wurde in den Papierkorb verschoben.",
      ),
    ).toHaveAttribute("role", "status");
    const deleteCall = fetchMock.mock.calls.find(
      ([request, init]) =>
        requestPath(request).endsWith(`/files/${attachmentId}`) &&
        init?.method === "DELETE",
    );
    expect(deleteCall?.[1]?.headers).toMatchObject({ "If-Match": '"2"' });
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
