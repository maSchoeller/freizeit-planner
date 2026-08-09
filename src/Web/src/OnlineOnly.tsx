import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { loadOfflineSnapshot } from "./offlineSnapshot";

export function OnlineOnly({ children }: { children: ReactNode }) {
  const [online, setOnline] = useState(navigator.onLine);
  useEffect(() => {
    const markOnline = () => setOnline(true);
    const markOffline = () => setOnline(false);
    window.addEventListener("online", markOnline);
    window.addEventListener("offline", markOffline);
    return () => {
      window.removeEventListener("online", markOnline);
      window.removeEventListener("offline", markOffline);
    };
  }, []);

  if (online) return children;
  const snapshot = loadOfflineSnapshot();
  return (
    <div className="account-layout">
      <header className="topbar">
        <span className="brand">
          <span className="brand-mark" aria-hidden="true">
            F
          </span>
          <span>Freizeit-Cockpit</span>
        </span>
      </header>
      <main id="main" className="account-page">
        <p className="eyebrow">Offline</p>
        <h1>Offline nicht verfügbar</h1>
        <p>
          Identität, Konto und Administration benötigen aus Sicherheitsgründen
          eine Internetverbindung.
        </p>
        {snapshot ? (
          <Link className="primary-action" to={snapshot.workspace.campBase}>
            Gespeicherte Camp-Planung öffnen
          </Link>
        ) : null}
      </main>
    </div>
  );
}
