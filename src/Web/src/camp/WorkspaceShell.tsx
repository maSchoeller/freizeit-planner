import { useQuery } from "@tanstack/react-query";
import { Menu } from "lucide-react";
import { useEffect, useState } from "react";
import { Link, NavLink, Route, Routes } from "react-router-dom";
import { loadOfflineSnapshot } from "../offlineSnapshot";
import { AppHeader } from "../AppShell";
import type { Account } from "./types";
import { getJson } from "./api";
import {
  mobilePrimaryNavigation,
  navigationGroups,
  useCampRuntime,
} from "./runtime";
import { formatGermanDateTime, PageHeading } from "./ui";
import { OverviewPage } from "./OverviewPage";
import { SchedulePage } from "./SchedulePage";
import { MealsPage } from "./MealsPage";
import { LogisticsPage } from "./LogisticsPage";
import { DevotionsPage } from "./DevotionsPage";
import { NotesPage } from "./NotesPage";
import { FilesPage } from "./FilesPage";
import { SearchTrashPage } from "./SearchTrashPage";

export function CampWorkspaceShell() {
  const runtime = useCampRuntime();
  const { campBase, camp } = runtime;
  const [offline, setOffline] = useState(!navigator.onLine);
  const offlineSnapshot = offline
    ? loadOfflineSnapshot({
        organizationId: runtime.organizationId,
        campId: runtime.campId,
      })
    : null;
  const account = useQuery({
    queryKey: ["account"],
    queryFn: () => getJson<Account>("/api/v1/account"),
    retry: false,
    staleTime: 5 * 60 * 1000,
    enabled: !offline,
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
  const readOnly = offline || camp.status === 1;
  const accountDisplayName = offline
    ? undefined
    : account.data?.displayName?.trim();
  const visibleNavigationGroups = navigationGroups
    .map((group) => ({
      ...group,
      items: offline
        ? group.items.filter(({ to }) =>
            ["tagesplan", "essen", "logistik"].includes(to),
          )
        : group.items,
    }))
    .filter((group) => group.items.length > 0);

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main">
        Zum Inhalt springen
      </a>
      <AppHeader
        homeTo={campBase}
        displayName={accountDisplayName}
        organizationName={offline ? undefined : runtime.organizationName}
        organizationSlug={offline ? undefined : runtime.organizationSlug}
        canManageOrganization={!offline && runtime.organizationRole === 1}
        isSuperAdmin={!offline && (account.data?.isSuperAdmin ?? false)}
        searchTo={offline ? undefined : `${campBase}/suche`}
        profileAvailable={!offline}
        status={
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
        }
      />
      <div className="workspace">
        <aside className="sidebar" aria-label="Freizeit-Navigation">
          <p className="eyebrow">{runtime.organizationName}</p>
          <p className="camp-name">{camp.name}</p>
          <nav aria-label="Freizeit-Navigation">
            {visibleNavigationGroups.map((group) => (
              <section className="navigation-group" key={group.label}>
                <h2>{group.label}</h2>
                <ul>
                  {group.items.map((item) => {
                    const destination = item.to
                      ? `${campBase}/${item.to}`
                      : campBase;
                    const Icon = item.icon;
                    return (
                      <li key={item.label}>
                        {"anchor" in item && item.anchor ? (
                          <Link to={destination}>
                            <Icon aria-hidden="true" size={20} />
                            <span>{item.label}</span>
                          </Link>
                        ) : (
                          <NavLink
                            to={destination}
                            end={"end" in item ? item.end : undefined}
                          >
                            <Icon aria-hidden="true" size={20} />
                            <span>{item.label}</span>
                          </NavLink>
                        )}
                      </li>
                    );
                  })}
                </ul>
              </section>
            ))}
          </nav>
          <a className="help-link" href="/hilfe/">
            Hilfe & Anleitung
          </a>
        </aside>
        <nav
          className="mobile-navigation"
          aria-label="Mobile Freizeit-Navigation"
        >
          {mobilePrimaryNavigation.map(({ to, label, icon: Icon, end }) => (
            <NavLink
              key={label}
              to={to ? `${campBase}/${to}` : campBase}
              end={end}
            >
              <Icon aria-hidden="true" size={20} />
              <span>{label}</span>
            </NavLink>
          ))}
          <details>
            <summary>
              <Menu aria-hidden="true" size={20} />
              <span>Mehr</span>
            </summary>
            <div className="mobile-more-panel">
              {!offline
                ? navigationGroups
                    .flatMap((group) => group.items)
                    .filter(
                      ({ to }) =>
                        !["", "tagesplan", "essen", "logistik"].includes(to),
                    )
                    .map(({ to, label }) => (
                      <Link key={label} to={`${campBase}/${to}`}>
                        {label}
                      </Link>
                    ))
                : null}
              {!offline && runtime.organizationRole === 1 ? (
                <Link to={`/o/${runtime.organizationSlug}/verwaltung/team`}>
                  Organisation verwalten
                </Link>
              ) : null}
              {!offline ? <Link to="/konto/profil">Mein Konto</Link> : null}
            </div>
          </details>
        </nav>
        <main id="main" tabIndex={-1}>
          {camp.status === 1 ? (
            <p className="notice" role="status">
              Archiviert · nur lesen. Inhalte bleiben lesbar und exportierbar;
              Änderungen sind erst nach der Reaktivierung möglich.
            </p>
          ) : null}
          {offline && offlineSnapshot ? (
            <p className="offline-snapshot-notice" role="status">
              Offline-Snapshot · Zuletzt synchronisiert:{" "}
              {formatGermanDateTime(offlineSnapshot.synchronizedAt)}
            </p>
          ) : null}
          <Routes>
            <Route
              index
              element={offline ? <OfflineStartPage /> : <OverviewPage />}
            />
            <Route
              path="tagesplan"
              element={<SchedulePage offline={offline} readOnly={readOnly} />}
            />
            <Route
              path="essen"
              element={<MealsPage offline={offline} readOnly={readOnly} />}
            />
            <Route
              path="logistik"
              element={<LogisticsPage offline={offline} readOnly={readOnly} />}
            />
            <Route
              path="andachten"
              element={
                offline ? (
                  <OfflineUnavailablePage />
                ) : (
                  <DevotionsPage offline={readOnly} />
                )
              }
            />
            <Route
              path="notizen"
              element={
                offline ? (
                  <OfflineUnavailablePage />
                ) : (
                  <NotesPage offline={readOnly} />
                )
              }
            />
            <Route
              path="dateien"
              element={
                offline ? (
                  <OfflineUnavailablePage />
                ) : (
                  <FilesPage offline={readOnly} />
                )
              }
            />
            <Route
              path="suche"
              element={
                offline ? (
                  <OfflineUnavailablePage />
                ) : (
                  <SearchTrashPage offline={readOnly} />
                )
              }
            />
          </Routes>
        </main>
      </div>
    </div>
  );
}

export function OfflineStartPage() {
  const { campBase } = useCampRuntime();
  return (
    <>
      <PageHeading eyebrow="Offline" title="Gespeicherte Planung">
        <p>
          Verfügbar sind ausschließlich der zuletzt synchronisierte Tagesplan,
          Speiseplan, Materialbedarf und Einkauf.
        </p>
      </PageHeading>
      <div className="card-grid offline-area-grid">
        <Link className="card" to={`${campBase}/tagesplan`}>
          Tagesplan öffnen
        </Link>
        <Link className="card" to={`${campBase}/essen`}>
          Speiseplan öffnen
        </Link>
        <Link className="card" to={`${campBase}/logistik`}>
          Material und Einkauf öffnen
        </Link>
      </div>
    </>
  );
}

export function OfflineUnavailablePage() {
  return (
    <PageHeading eyebrow="Offline" title="Offline nicht verfügbar">
      <p>
        Dieser Bereich enthält sensible oder administrative Daten und steht
        deshalb nur mit Internetverbindung zur Verfügung.
      </p>
    </PageHeading>
  );
}
