import { useState } from "react";
import type { FormEvent } from "react";
import { PageHeading } from "./ui";

export function FilesPage({ offline }: { offline: boolean }) {
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
