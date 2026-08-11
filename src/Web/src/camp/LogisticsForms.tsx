import { useState } from "react";
import type {
  CampMemberSummary,
  LogisticsQuantity,
  MaterialRequirement,
  MaterialRequirementContent,
  ScheduleEntry,
  ShoppingItem,
  ShoppingItemContentDraft,
} from "./types";
import { ResponsibilityFields } from "./ui";

export const shoppingUnitLabels: Record<number, string> = {
  0: "Gramm",
  1: "Kilogramm",
  2: "Milliliter",
  3: "Liter",
  4: "Stück",
  5: "Benutzerdefinierte Einheit",
};

export const materialStatusLabels: Record<number, string> = {
  0: "Offen",
  1: "Geplant",
  2: "Beschafft",
  3: "Nicht benötigt",
};

export function formatLogisticsQuantity(quantity: LogisticsQuantity) {
  const value = new Intl.NumberFormat("de-DE", {
    maximumFractionDigits: 6,
  }).format(quantity.value);
  const unit =
    quantity.unit === 5
      ? (quantity.customUnitName ?? shoppingUnitLabels[quantity.unit])
      : shoppingUnitLabels[quantity.unit];
  return `${value} ${unit}`;
}

export function MaterialRequirementForm({
  mode,
  initial,
  members,
  scheduleEntries,
  pending,
  error,
  onSave,
  onCancel,
}: {
  mode: "create" | "edit";
  initial?: MaterialRequirement;
  members: CampMemberSummary[];
  scheduleEntries: ScheduleEntry[];
  pending: boolean;
  error: Error | null;
  onSave: (content: MaterialRequirementContent) => void;
  onCancel: () => void;
}) {
  const [name, setName] = useState(initial?.name ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [quantity, setQuantity] = useState(
    String(initial?.quantity.value ?? 1),
  );
  const [unit, setUnit] = useState(String(initial?.quantity.unit ?? 4));
  const [customUnit, setCustomUnit] = useState(
    initial?.quantity.customUnitName ?? "",
  );
  const [status, setStatus] = useState(String(initial?.status ?? 0));
  const [scheduleEntryId, setScheduleEntryId] = useState(
    initial?.scheduleEntryId ?? "",
  );
  const [procurementSource, setProcurementSource] = useState(
    initial?.procurementSource ?? "",
  );
  const [note, setNote] = useState(initial?.note ?? "");
  const [responsibleUserIds, setResponsibleUserIds] = useState(
    initial?.responsibleUserIds ?? [],
  );
  return (
    <form
      className="schedule-create-form material-form"
      aria-label={
        mode === "create" ? "Materialbedarf anlegen" : "Material bearbeiten"
      }
      onSubmit={(event) => {
        event.preventDefault();
        onSave({
          name,
          description: description || null,
          quantity: {
            value: Number(quantity),
            unit: Number(unit),
            customUnitName: unit === "5" ? customUnit : null,
          },
          responsibleUserIds,
          procurementSource: procurementSource || null,
          note: note || null,
          status: Number(status),
          scheduleEntryId: scheduleEntryId || null,
        });
      }}
    >
      <h3>
        {mode === "create"
          ? "Neuen Materialbedarf planen"
          : "Material bearbeiten"}
      </h3>
      <p className="form-hint">
        Plane Bedarf und Beschaffung. Lagerbestand und Ausleihen werden hier
        bewusst nicht verwaltet.
      </p>
      <div className="camp-form-grid">
        <label>
          Bezeichnung des Materials
          <input
            required
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
        </label>
        <label className="full-row">
          Beschreibung des Materials
          <textarea
            value={description}
            onChange={(event) => setDescription(event.target.value)}
          />
        </label>
        <label>
          Menge des Materials
          <input
            required
            type="number"
            min="0.000001"
            step="any"
            inputMode="decimal"
            value={quantity}
            onChange={(event) => setQuantity(event.target.value)}
          />
        </label>
        <label>
          Einheit des Materials
          <select
            value={unit}
            onChange={(event) => setUnit(event.target.value)}
          >
            {Object.entries(shoppingUnitLabels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        {unit === "5" ? (
          <label>
            Name der benutzerdefinierten Einheit
            <input
              required
              value={customUnit}
              onChange={(event) => setCustomUnit(event.target.value)}
            />
          </label>
        ) : null}
        <label>
          Beschaffungsstatus
          <select
            value={status}
            onChange={(event) => setStatus(event.target.value)}
          >
            {Object.entries(materialStatusLabels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <label>
          Verknüpfung zum Tagesplan
          <select
            value={scheduleEntryId}
            onChange={(event) => setScheduleEntryId(event.target.value)}
          >
            <option value="">Campweit, ohne Zeitplaneintrag</option>
            {scheduleEntries.map((entry) => (
              <option key={entry.id} value={entry.id}>
                {entry.title}
              </option>
            ))}
          </select>
        </label>
        <label>
          Beschaffungsquelle
          <input
            value={procurementSource}
            onChange={(event) => setProcurementSource(event.target.value)}
          />
        </label>
        <label className="full-row">
          Materialnotiz
          <textarea
            value={note}
            onChange={(event) => setNote(event.target.value)}
          />
        </label>
      </div>
      <ResponsibilityFields
        candidates={members}
        selected={responsibleUserIds}
        onChange={setResponsibleUserIds}
      />
      {error ? (
        <p role="alert" className="error-message">
          {error.message}
        </p>
      ) : null}
      <div className="toolbar">
        <button type="submit" className="primary-action" disabled={pending}>
          {mode === "create"
            ? "Materialbedarf speichern"
            : "Materialänderung speichern"}
        </button>
        <button
          type="button"
          className="secondary-action"
          disabled={pending}
          onClick={onCancel}
        >
          Abbrechen
        </button>
      </div>
    </form>
  );
}

export function ShoppingItemEditForm({
  item,
  members,
  pending,
  error,
  onSave,
  onCancel,
}: {
  item: ShoppingItem;
  members: CampMemberSummary[];
  pending: boolean;
  error: Error | null;
  onSave: (content: ShoppingItemContentDraft) => void;
  onCancel: () => void;
}) {
  const [name, setName] = useState(item.name);
  const [quantity, setQuantity] = useState(String(item.quantity.value));
  const [unit, setUnit] = useState(String(item.quantity.unit));
  const [customUnitName, setCustomUnitName] = useState(
    item.quantity.customUnitName ?? "",
  );
  const [responsibleUserIds, setResponsibleUserIds] = useState(
    item.responsibleUserIds,
  );
  const [store, setStore] = useState(item.store ?? "");
  const [note, setNote] = useState(item.note ?? "");
  return (
    <form
      className="schedule-create-form shopping-item-edit"
      aria-label={`${item.name} bearbeiten`}
      onSubmit={(event) => {
        event.preventDefault();
        onSave({
          name,
          quantity: {
            value: Number(quantity),
            unit: Number(unit),
            customUnitName: unit === "5" ? customUnitName : null,
          },
          responsibleUserIds,
          store: store || null,
          note: note || null,
        });
      }}
    >
      <div className="camp-form-grid">
        <label>
          Bezeichnung für {item.name} bearbeiten
          <input
            required
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
        </label>
        <label>
          Menge für {item.name} bearbeiten
          <input
            required
            type="number"
            min="0.000001"
            step="any"
            inputMode="decimal"
            value={quantity}
            onChange={(event) => setQuantity(event.target.value)}
          />
        </label>
        <label>
          Einheit für {item.name} bearbeiten
          <select
            value={unit}
            onChange={(event) => setUnit(event.target.value)}
          >
            {Object.entries(shoppingUnitLabels).map(([value, label]) => (
              <option value={value} key={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        {unit === "5" ? (
          <label>
            Name der Einheit für {item.name} bearbeiten
            <input
              required
              value={customUnitName}
              onChange={(event) => setCustomUnitName(event.target.value)}
            />
          </label>
        ) : null}
        <label>
          Geschäft für {item.name} bearbeiten
          <input
            value={store}
            onChange={(event) => setStore(event.target.value)}
          />
        </label>
        <label>
          Notiz für {item.name} bearbeiten
          <input
            value={note}
            onChange={(event) => setNote(event.target.value)}
          />
        </label>
      </div>
      <ResponsibilityFields
        candidates={members}
        selected={responsibleUserIds}
        onChange={setResponsibleUserIds}
      />
      {error ? (
        <p className="error-message" role="alert">
          {error.message}
        </p>
      ) : null}
      <div className="toolbar">
        <button type="submit" className="primary-action" disabled={pending}>
          Position speichern
        </button>
        <button
          type="button"
          className="secondary-action"
          disabled={pending}
          onClick={onCancel}
        >
          Bearbeitung abbrechen
        </button>
      </div>
    </form>
  );
}
