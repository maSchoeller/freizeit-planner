import type { ReactNode } from "react";
import type { CampMemberSummary, PrintScope } from "./types";

export function ResponsibilityFields({
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

export function PageHeading({
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

export function PrintButton({
  scope,
  children,
}: {
  scope: PrintScope;
  children: ReactNode;
}) {
  const print = () => {
    const root = document.documentElement;
    root.dataset.printScope = scope;
    const clearScope = () => {
      delete root.dataset.printScope;
    };
    window.addEventListener("afterprint", clearScope, { once: true });
    window.print();
  };
  return (
    <button type="button" className="secondary-action" onClick={print}>
      {children}
    </button>
  );
}

export function QueryState({
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

export function SummaryCard({
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

export function formatGermanDateTime(value: string) {
  return new Intl.DateTimeFormat("de-DE", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}
