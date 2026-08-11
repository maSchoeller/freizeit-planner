import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import type { components } from "./api/schema";
import { authenticatedFetch as fetch } from "./api/authentication";

type Account = components["schemas"]["AccountView"];
type Membership = components["schemas"]["AccountMembershipView"];

export function LandingPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    void Promise.all([
      fetch("/api/v1/account", { signal: controller.signal }),
      fetch("/api/v1/account/memberships", { signal: controller.signal }),
    ])
      .then(async ([accountResponse, membershipsResponse]) => {
        if (
          accountResponse.status === 401 ||
          membershipsResponse.status === 401
        ) {
          void navigate("/anmelden", {
            replace: true,
            state: { returnTo: location.pathname },
          });
          return;
        }
        if (!accountResponse.ok || !membershipsResponse.ok)
          throw new Error(
            "Der persönliche Startbereich konnte nicht geladen werden.",
          );
        const account = (await accountResponse.json()) as Account;
        const memberships = (await membershipsResponse.json()) as Membership[];
        if (memberships.length === 1) {
          void navigate(`/o/${memberships[0].organizationSlug}/camps`, {
            replace: true,
          });
          return;
        }
        if (memberships.length > 1) {
          void navigate("/konto/organisationen", { replace: true });
          return;
        }
        void navigate(
          account.isSuperAdmin
            ? "/superadmin/organisationen"
            : "/konto/organisationen",
          { replace: true },
        );
      })
      .catch((reason: unknown) => {
        if (reason instanceof DOMException && reason.name === "AbortError")
          return;
        setError(
          reason instanceof Error
            ? reason.message
            : "Der persönliche Startbereich konnte nicht geladen werden.",
        );
      });
    return () => controller.abort();
  }, [location.pathname, navigate]);

  return (
    <main id="main" className="landing-page" tabIndex={-1}>
      <h1>Freizeit-Cockpit wird geöffnet</h1>
      {error ? (
        <div className="error-message" role="alert">
          {error}
        </div>
      ) : (
        <p role="status">Dein persönlicher Startbereich wird geladen …</p>
      )}
    </main>
  );
}
