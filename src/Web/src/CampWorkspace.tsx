import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import {
  activateOfflineOrganization,
  loadOfflineSnapshot,
} from "./offlineSnapshot";
import { authenticatedFetch as fetch } from "./api/authentication";
import type {
  AccountMembership,
  CampRuntime,
  WorkspaceCamp,
} from "./camp/types";
import { CampRuntimeContext } from "./camp/runtime";
import { CampWorkspaceShell } from "./camp/WorkspaceShell";

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
  const offlineSnapshot = loadOfflineSnapshot({
    organizationSlug,
    campSlug,
  });
  if (!navigator.onLine && offlineSnapshot)
    return offlineSnapshot.workspace as CampRuntime;

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
  const runtime: CampRuntime = {
    organizationId: membership.organizationId,
    organizationName: membership.organizationName,
    organizationSlug,
    organizationRole: membership.role,
    campId: camp.id,
    campSlug: camp.slug,
    campBase: `/o/${organizationSlug}/camps/${camp.slug}`,
    camp,
  };
  activateOfflineOrganization(runtime.organizationId);
  return runtime;
}
