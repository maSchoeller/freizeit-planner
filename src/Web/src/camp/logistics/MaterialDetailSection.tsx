import type { UseQueryResult } from "@tanstack/react-query";
import type { CampMemberSummary, CampRuntime, ScheduleEntry } from "../types";
import { QueryState, ResponsibilityFields } from "../ui";
import { OwnerAttachmentsPanel } from "../AttachmentsPanel";
import {
  formatLogisticsQuantity,
  MaterialRequirementForm,
  materialStatusLabels,
  shoppingUnitLabels,
} from "../LogisticsForms";
import type { useMaterialWorkspace } from "./materialWorkspace";
import type { useShoppingWorkspace } from "./shoppingWorkspace";

export function MaterialDetailSection({
  runtime,
  readOnly,
  workspace,
  shopping,
  members,
  scheduleEntries,
  memberNames,
  setNotice,
}: {
  runtime: CampRuntime;
  readOnly: boolean;
  workspace: ReturnType<typeof useMaterialWorkspace>;
  shopping: ReturnType<typeof useShoppingWorkspace>;
  members: UseQueryResult<CampMemberSummary[]>;
  scheduleEntries: UseQueryResult<ScheduleEntry[]>;
  memberNames: Map<string, string>;
  setNotice: (notice: string) => void;
}) {
  const { organizationId, campId } = runtime;
  const {
    selectedMaterial,
    setSelectedMaterialId,
    editingMaterial,
    setEditingMaterial,
    deletingMaterial,
    setDeletingMaterial,
    deleteMaterialConfirmed,
    setDeleteMaterialConfirmed,
    transferringMaterial,
    setTransferringMaterial,
    materialTargetListId,
    setMaterialTargetListId,
    materialTransferName,
    setMaterialTransferName,
    materialTransferQuantity,
    setMaterialTransferQuantity,
    materialTransferUnit,
    setMaterialTransferUnit,
    materialTransferCustomUnit,
    setMaterialTransferCustomUnit,
    materialTransferStore,
    setMaterialTransferStore,
    materialTransferNote,
    setMaterialTransferNote,
    materialTransferResponsibleUserIds,
    setMaterialTransferResponsibleUserIds,
    updateMaterial,
    deleteMaterial,
    transferMaterial,
  } = workspace;
  const { shoppingLists } = shopping;
  return (
    <section
      className="settings-section material-detail"
      aria-label="Geöffneter Materialbedarf"
      data-print-section="material"
    >
      <QueryState
        loading={selectedMaterial.isLoading}
        error={selectedMaterial.error}
      />
      {selectedMaterial.data ? (
        <>
          <div className="section-heading">
            <div>
              <p className="eyebrow">
                {materialStatusLabels[selectedMaterial.data.status] ?? "Offen"}
              </p>
              <h2>{selectedMaterial.data.name}</h2>
            </div>
            <div className="toolbar compact-toolbar">
              {!readOnly ? (
                <>
                  <button
                    type="button"
                    className="secondary-action"
                    aria-label={`${selectedMaterial.data.name} bearbeiten`}
                    onClick={() => {
                      updateMaterial.reset();
                      setEditingMaterial(true);
                      setDeletingMaterial(false);
                      setTransferringMaterial(false);
                    }}
                  >
                    Bearbeiten
                  </button>
                  <button
                    type="button"
                    className="danger-action"
                    aria-label={`${selectedMaterial.data.name} löschen`}
                    onClick={() => {
                      deleteMaterial.reset();
                      setDeletingMaterial(true);
                      setDeleteMaterialConfirmed(false);
                      setEditingMaterial(false);
                      setTransferringMaterial(false);
                    }}
                  >
                    Material löschen
                  </button>
                  <button
                    type="button"
                    className="primary-action"
                    aria-label={`${selectedMaterial.data.name} einkaufen`}
                    disabled={shoppingLists.data?.length === 0}
                    onClick={() => {
                      const requirement = selectedMaterial.data;
                      setMaterialTargetListId(
                        shoppingLists.data?.[0]?.id ?? "",
                      );
                      setMaterialTransferName(requirement.name);
                      setMaterialTransferQuantity(
                        String(requirement.quantity.value),
                      );
                      setMaterialTransferUnit(
                        String(requirement.quantity.unit),
                      );
                      setMaterialTransferCustomUnit(
                        requirement.quantity.customUnitName ?? "",
                      );
                      setMaterialTransferStore(
                        requirement.procurementSource ?? "",
                      );
                      setMaterialTransferNote(requirement.note ?? "");
                      setMaterialTransferResponsibleUserIds(
                        requirement.responsibleUserIds,
                      );
                      transferMaterial.reset();
                      setTransferringMaterial(true);
                      setEditingMaterial(false);
                      setDeletingMaterial(false);
                    }}
                  >
                    In Einkaufsliste übernehmen
                  </button>
                </>
              ) : null}
              <button
                type="button"
                className="secondary-action"
                onClick={() => {
                  setSelectedMaterialId(null);
                  setTransferringMaterial(false);
                  setEditingMaterial(false);
                  setDeletingMaterial(false);
                }}
              >
                Material schließen
              </button>
            </div>
          </div>
          {selectedMaterial.data.description ? (
            <p>{selectedMaterial.data.description}</p>
          ) : null}
          <dl className="definition-grid">
            <div>
              <dt>Menge</dt>
              <dd>{formatLogisticsQuantity(selectedMaterial.data.quantity)}</dd>
            </div>
            <div>
              <dt>Beschaffungsquelle</dt>
              <dd>
                {selectedMaterial.data.procurementSource ?? "Nicht angegeben"}
              </dd>
            </div>
            <div>
              <dt>Verantwortlich</dt>
              <dd>
                {selectedMaterial.data.responsibleUserIds.length
                  ? selectedMaterial.data.responsibleUserIds
                      .map(
                        (userId) => memberNames.get(userId) ?? "Camp-Mitglied",
                      )
                      .join(", ")
                  : "Nicht zugewiesen"}
              </dd>
            </div>
            <div>
              <dt>Notiz</dt>
              <dd>{selectedMaterial.data.note ?? "Keine Notiz"}</dd>
            </div>
          </dl>
          <p className="form-hint">
            {selectedMaterial.data.scheduleEntryId
              ? `Tagesplan: ${
                  scheduleEntries.data?.find(
                    (entry) =>
                      entry.id === selectedMaterial.data?.scheduleEntryId,
                  )?.title ?? "Verknüpfter Eintrag"
                }`
              : "Campweiter Bedarf ohne Tagesplan-Verknüpfung"}
          </p>
          {editingMaterial ? (
            <MaterialRequirementForm
              key={`${selectedMaterial.data.id}:${selectedMaterial.data.version}`}
              mode="edit"
              initial={selectedMaterial.data}
              members={members.data ?? []}
              scheduleEntries={scheduleEntries.data ?? []}
              pending={updateMaterial.isPending}
              error={updateMaterial.error}
              onSave={(content) => updateMaterial.mutate(content)}
              onCancel={() => setEditingMaterial(false)}
            />
          ) : null}
          {deletingMaterial ? (
            <section
              className="confirmation-panel"
              aria-label="Material löschen"
            >
              <p>
                Der Materialbedarf bleibt 30 Tage im Papierkorb und kann dort
                wiederhergestellt werden.
              </p>
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={deleteMaterialConfirmed}
                  onChange={(event) =>
                    setDeleteMaterialConfirmed(event.target.checked)
                  }
                />
                {selectedMaterial.data.name} wirklich in den Papierkorb
                verschieben
              </label>
              {deleteMaterial.error ? (
                <p role="alert" className="error-message">
                  {deleteMaterial.error.message}
                </p>
              ) : null}
              <div className="toolbar">
                <button
                  type="button"
                  className="danger-action"
                  disabled={
                    !deleteMaterialConfirmed || deleteMaterial.isPending
                  }
                  onClick={() => deleteMaterial.mutate()}
                >
                  Material in Papierkorb verschieben
                </button>
                <button
                  type="button"
                  className="secondary-action"
                  disabled={deleteMaterial.isPending}
                  onClick={() => setDeletingMaterial(false)}
                >
                  Abbrechen
                </button>
              </div>
            </section>
          ) : null}
          {shoppingLists.data?.length === 0 && !readOnly ? (
            <p className="form-hint">
              Lege zuerst eine Einkaufsliste an, um Material zu übernehmen.
            </p>
          ) : null}
          {transferringMaterial ? (
            <form
              className="schedule-create-form material-transfer"
              aria-label="Material in Einkaufsliste übernehmen"
              onSubmit={(event) => {
                event.preventDefault();
                setNotice("");
                transferMaterial.mutate();
              }}
            >
              <h3>Material übernehmen</h3>
              <p className="form-hint">
                Menge und Einheit können vor der Übernahme angepasst werden. Die
                Materialquelle bleibt nachvollziehbar erhalten.
              </p>
              <div className="camp-form-grid">
                <label>
                  Ziel-Einkaufsliste
                  <select
                    required
                    value={materialTargetListId}
                    onChange={(event) =>
                      setMaterialTargetListId(event.target.value)
                    }
                  >
                    {shoppingLists.data?.map((list) => (
                      <option key={list.id} value={list.id}>
                        {list.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Bezeichnung der Einkaufsposition
                  <input
                    required
                    value={materialTransferName}
                    onChange={(event) =>
                      setMaterialTransferName(event.target.value)
                    }
                  />
                </label>
                <label>
                  Menge für die Einkaufsposition
                  <input
                    required
                    type="number"
                    min="0.000001"
                    step="any"
                    inputMode="decimal"
                    value={materialTransferQuantity}
                    onChange={(event) =>
                      setMaterialTransferQuantity(event.target.value)
                    }
                  />
                </label>
                <label>
                  Einheit der Einkaufsposition
                  <select
                    value={materialTransferUnit}
                    onChange={(event) =>
                      setMaterialTransferUnit(event.target.value)
                    }
                  >
                    {Object.entries(shoppingUnitLabels).map(([unit, label]) => (
                      <option key={unit} value={unit}>
                        {label}
                      </option>
                    ))}
                  </select>
                </label>
                {materialTransferUnit === "5" ? (
                  <label>
                    Name der benutzerdefinierten Einheit
                    <input
                      required
                      value={materialTransferCustomUnit}
                      onChange={(event) =>
                        setMaterialTransferCustomUnit(event.target.value)
                      }
                    />
                  </label>
                ) : null}
                <label>
                  Geschäft (optional)
                  <input
                    value={materialTransferStore}
                    onChange={(event) =>
                      setMaterialTransferStore(event.target.value)
                    }
                  />
                </label>
                <label className="full-row">
                  Notiz (optional)
                  <textarea
                    value={materialTransferNote}
                    onChange={(event) =>
                      setMaterialTransferNote(event.target.value)
                    }
                  />
                </label>
              </div>
              <ResponsibilityFields
                candidates={members.data ?? []}
                selected={materialTransferResponsibleUserIds}
                onChange={setMaterialTransferResponsibleUserIds}
              />
              {transferMaterial.error ? (
                <p role="alert" className="error-message">
                  {transferMaterial.error.message}
                </p>
              ) : null}
              <div className="toolbar">
                <button
                  type="submit"
                  className="primary-action"
                  disabled={transferMaterial.isPending}
                >
                  Material übernehmen
                </button>
                <button
                  type="button"
                  className="secondary-action"
                  disabled={transferMaterial.isPending}
                  onClick={() => setTransferringMaterial(false)}
                >
                  Abbrechen
                </button>
              </div>
            </form>
          ) : null}
          <OwnerAttachmentsPanel
            organizationId={organizationId}
            campId={campId}
            ownerType="MaterialRequirement"
            ownerId={selectedMaterial.data.id}
            ownerName={selectedMaterial.data.name}
            ownerNoun="das Material"
            canUpload={!readOnly}
            canDelete={!readOnly}
          />
        </>
      ) : null}
    </section>
  );
}
