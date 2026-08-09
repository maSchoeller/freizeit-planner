import { useEffect, useState } from "react";

export function PwaUpdatePrompt() {
  const [offlineReady, setOfflineReady] = useState(false);
  const [needRefresh, setNeedRefresh] = useState(false);
  const [updateServiceWorker, setUpdateServiceWorker] = useState<
    (reloadPage?: boolean) => Promise<void>
  >(() => () => Promise.resolve());

  useEffect(() => {
    if (import.meta.env.MODE === "test") return;
    let active = true;
    void import("virtual:pwa-register").then(({ registerSW }) => {
      if (!active) return;
      const update = registerSW({
        onOfflineReady: () => setOfflineReady(true),
        onNeedRefresh: () => setNeedRefresh(true),
      });
      setUpdateServiceWorker(() => update);
    });
    return () => {
      active = false;
    };
  }, []);

  return (
    <PwaUpdatePromptView
      offlineReady={offlineReady}
      needRefresh={needRefresh}
      onCloseOfflineReady={() => setOfflineReady(false)}
      onCloseNeedRefresh={() => setNeedRefresh(false)}
      onUpdate={() => void updateServiceWorker(true)}
    />
  );
}

export function PwaUpdatePromptView({
  offlineReady,
  needRefresh,
  onCloseOfflineReady,
  onCloseNeedRefresh,
  onUpdate,
}: {
  offlineReady: boolean;
  needRefresh: boolean;
  onCloseOfflineReady: () => void;
  onCloseNeedRefresh: () => void;
  onUpdate: () => void;
}) {
  if (!offlineReady && !needRefresh) return null;
  if (needRefresh)
    return (
      <section
        className="pwa-prompt"
        aria-labelledby="pwa-update-heading"
        role="status"
      >
        <h2 id="pwa-update-heading">Neue Version verfügbar</h2>
        <p>
          Aktualisiere das Freizeit-Cockpit, um mit dem neuesten Stand
          weiterzuarbeiten.
        </p>
        <div className="toolbar">
          <button className="primary-action" onClick={onUpdate} type="button">
            Jetzt aktualisieren
          </button>
          <button
            className="secondary-action"
            onClick={onCloseNeedRefresh}
            type="button"
          >
            Später
          </button>
        </div>
      </section>
    );

  return (
    <section className="pwa-prompt" role="status">
      <p>Die App ist für die Offline-Nutzung bereit.</p>
      <button
        className="secondary-action"
        onClick={onCloseOfflineReady}
        type="button"
      >
        Hinweis schließen
      </button>
    </section>
  );
}
