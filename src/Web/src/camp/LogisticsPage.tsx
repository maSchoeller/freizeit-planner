import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { loadOfflineSnapshot } from "../offlineSnapshot";
import type { CampMemberSummary, ScheduleEntry } from "./types";
import { getJson } from "./api";
import { useCampRuntime } from "./runtime";
import { nextLocalDate } from "./schedule";
import {
  formatGermanDateTime,
  PageHeading,
  PrintButton,
  QueryState,
} from "./ui";
import {
  formatLogisticsQuantity,
  MaterialRequirementForm,
  materialStatusLabels,
  ShoppingItemEditForm,
  shoppingUnitLabels,
} from "./LogisticsForms";
import { MaterialDetailSection } from "./logistics/MaterialDetailSection";
import { useMaterialWorkspace } from "./logistics/materialWorkspace";
import { useShoppingWorkspace } from "./logistics/shoppingWorkspace";

export function LogisticsPage({
  offline,
  readOnly,
}: {
  offline: boolean;
  readOnly: boolean;
}) {
  const runtime = useCampRuntime();
  const { organizationId, campId, camp } = runtime;
  const storedSnapshot = offline
    ? loadOfflineSnapshot({ organizationId, campId })
    : null;
  const basePath = `/api/v1/organizations/${organizationId}/camps/${campId}/logistics`;
  const [notice, setNotice] = useState("");
  const scheduleEntries = useQuery({
    queryKey: [organizationId, campId, "material-schedule-candidates"],
    queryFn: () =>
      getJson<ScheduleEntry[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/schedule?fromDate=${camp.startsOn}&toDateExclusive=${nextLocalDate(camp.endsOn)}`,
      ),
    retry: false,
    enabled: !offline,
  });
  const members = useQuery({
    queryKey: [organizationId, campId, "responsibility-candidates"],
    queryFn: () =>
      getJson<CampMemberSummary[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/responsibility-candidates`,
      ),
    retry: false,
    enabled: !offline,
  });
  const shoppingWorkspace = useShoppingWorkspace({
    runtime,
    offline,
    storedSnapshot,
    basePath,
    setNotice,
  });
  const {
    selectedListId,
    setSelectedListId,
    listName,
    setListName,
    itemName,
    setItemName,
    itemQuantity,
    setItemQuantity,
    itemUnit,
    setItemUnit,
    itemCustomUnit,
    setItemCustomUnit,
    itemStore,
    setItemStore,
    itemNote,
    setItemNote,
    editingItemId,
    setEditingItemId,
    deletingItemId,
    setDeletingItemId,
    deleteItemConfirmed,
    setDeleteItemConfirmed,
    renamingList,
    setRenamingList,
    renameListName,
    setRenameListName,
    deletingList,
    setDeletingList,
    deleteListConfirmed,
    setDeleteListConfirmed,
    shoppingLists,
    selectedList,
    updateListSummary,
    createList,
    addItem,
    checkItem,
    updateItem,
    deleteItem,
    renameList,
    deleteList,
  } = shoppingWorkspace;
  const materialWorkspace = useMaterialWorkspace({
    runtime,
    offline,
    storedSnapshot,
    basePath,
    setNotice,
    shoppingListSummaries: shoppingLists.data,
    updateListSummary,
  });
  const {
    selectedMaterialId,
    setSelectedMaterialId,
    creatingMaterial,
    setCreatingMaterial,
    setEditingMaterial,
    setDeletingMaterial,
    setTransferringMaterial,
    material,
    createMaterial,
  } = materialWorkspace;
  const memberNames = new Map(
    (members.data ?? []).map((member) => [member.userId, member.displayName]),
  );
  return (
    <>
      <PageHeading eyebrow="Logistik" title="Material & Einkaufslisten">
        <p>
          Lebensmittel, Material und spontane Positionen stehen in gemeinsamen,
          nachvollziehbaren Listen.
        </p>
      </PageHeading>
      <nav className="section-navigation" aria-label="Material und Einkauf">
        <a href="#material">Material</a>
        <a href="#einkaufslisten">Einkaufsliste</a>
      </nav>
      <div className="toolbar print-actions">
        <PrintButton scope="material">Material drucken</PrintButton>
        <PrintButton scope="shopping">Einkauf drucken</PrintButton>
      </div>
      {notice ? (
        <p className="form-feedback" role="status">
          {notice}
        </p>
      ) : null}
      <div className="split-view" data-print-container="logistics">
        <section
          id="material"
          className="settings-section"
          data-print-section="material"
        >
          <div className="section-heading">
            <h2>Materialbedarf</h2>
            {!readOnly ? (
              <button
                type="button"
                className="primary-action"
                aria-expanded={creatingMaterial}
                onClick={() => {
                  createMaterial.reset();
                  setCreatingMaterial(true);
                  setSelectedMaterialId(null);
                  setNotice("");
                }}
              >
                Materialbedarf anlegen
              </button>
            ) : null}
          </div>
          <QueryState loading={material.isLoading} error={material.error} />
          {creatingMaterial ? (
            <MaterialRequirementForm
              mode="create"
              members={members.data ?? []}
              scheduleEntries={scheduleEntries.data ?? []}
              pending={createMaterial.isPending}
              error={createMaterial.error}
              onSave={(content) => createMaterial.mutate(content)}
              onCancel={() => setCreatingMaterial(false)}
            />
          ) : null}
          <ul className="detail-list material-summaries">
            {material.data?.map((requirement) => (
              <li key={requirement.id}>
                <div>
                  <strong>{requirement.name}</strong>
                  <span>
                    {formatLogisticsQuantity(requirement.quantity)} ·{" "}
                    {materialStatusLabels[requirement.status] ?? "Offen"}
                  </span>
                </div>
                <button
                  type="button"
                  className="secondary-action"
                  aria-label={`${requirement.name} öffnen`}
                  aria-expanded={selectedMaterialId === requirement.id}
                  onClick={() => {
                    setSelectedMaterialId(requirement.id);
                    setCreatingMaterial(false);
                    setEditingMaterial(false);
                    setDeletingMaterial(false);
                    setTransferringMaterial(false);
                    setNotice("");
                  }}
                >
                  Material öffnen
                </button>
              </li>
            ))}
          </ul>
          {!material.isLoading && material.data?.length === 0 ? (
            <p className="empty-state">Noch kein Materialbedarf geplant.</p>
          ) : null}
        </section>
        <section
          id="einkaufslisten"
          className="settings-section"
          data-print-section="shopping"
        >
          <div className="section-heading">
            <h2>Einkaufslisten</h2>
            <span className="status">
              {offline
                ? "Gespeicherter Stand"
                : "Aktualisierung alle 15 Sekunden"}
            </span>
          </div>
          <QueryState
            loading={shoppingLists.isLoading}
            error={shoppingLists.error}
          />
          {!readOnly ? (
            <form
              className="shopping-list-create"
              onSubmit={(event) => {
                event.preventDefault();
                setNotice("");
                createList.mutate();
              }}
            >
              <label>
                Name der neuen Einkaufsliste
                <input
                  required
                  value={listName}
                  onChange={(event) => setListName(event.target.value)}
                />
              </label>
              <button
                type="submit"
                className="primary-action"
                disabled={createList.isPending}
              >
                Einkaufsliste anlegen
              </button>
            </form>
          ) : null}
          {createList.error ? (
            <p role="alert" className="error-message">
              {createList.error.message}
            </p>
          ) : null}
          <div className="shopping-list-summaries">
            {shoppingLists.data?.map((list) => (
              <article className="card" key={list.id}>
                <p className="eyebrow">
                  {list.openItemCount} offen · {list.checkedItemCount} erledigt
                </p>
                <h3>{list.name}</h3>
                <button
                  type="button"
                  className="secondary-action"
                  aria-label={`${list.name} öffnen`}
                  aria-expanded={selectedListId === list.id}
                  onClick={() => {
                    setSelectedListId(list.id);
                    setNotice("");
                  }}
                >
                  Liste öffnen
                </button>
              </article>
            ))}
          </div>
          {!shoppingLists.isLoading && shoppingLists.data?.length === 0 ? (
            <p className="empty-state">Noch keine Einkaufsliste vorhanden.</p>
          ) : null}
        </section>
      </div>
      {selectedMaterialId ? (
        <MaterialDetailSection
          runtime={runtime}
          readOnly={readOnly}
          workspace={materialWorkspace}
          shopping={shoppingWorkspace}
          members={members}
          scheduleEntries={scheduleEntries}
          memberNames={memberNames}
          setNotice={setNotice}
        />
      ) : null}
      {selectedListId ? (
        <section
          className="settings-section shopping-list-detail"
          aria-label="Geöffnete Einkaufsliste"
          data-print-section="shopping"
        >
          <QueryState
            loading={selectedList.isLoading}
            error={selectedList.error}
          />
          {selectedList.data ? (
            <>
              <div className="section-heading">
                <div>
                  <p className="eyebrow">
                    {
                      selectedList.data.items.filter((item) => !item.isChecked)
                        .length
                    }{" "}
                    offen
                  </p>
                  <h2>{selectedList.data.name}</h2>
                </div>
                <div className="toolbar compact-toolbar">
                  {!readOnly ? (
                    <>
                      <button
                        type="button"
                        className="secondary-action"
                        aria-label={`${selectedList.data.name} umbenennen`}
                        onClick={() => {
                          setRenameListName(selectedList.data.name);
                          setRenamingList(true);
                          setDeletingList(false);
                          renameList.reset();
                        }}
                      >
                        Umbenennen
                      </button>
                      <button
                        type="button"
                        className="danger-action"
                        aria-label={`${selectedList.data.name} löschen`}
                        onClick={() => {
                          setDeletingList(true);
                          setDeleteListConfirmed(false);
                          setRenamingList(false);
                          deleteList.reset();
                        }}
                      >
                        Liste löschen
                      </button>
                    </>
                  ) : null}
                  <button
                    type="button"
                    className="secondary-action"
                    onClick={() => setSelectedListId(null)}
                  >
                    Liste schließen
                  </button>
                </div>
              </div>
              {renamingList ? (
                <form
                  className="schedule-create-form shopping-list-rename"
                  aria-label="Einkaufsliste umbenennen"
                  onSubmit={(event) => {
                    event.preventDefault();
                    setNotice("");
                    renameList.mutate();
                  }}
                >
                  <label>
                    Listenname bearbeiten
                    <input
                      required
                      value={renameListName}
                      onChange={(event) =>
                        setRenameListName(event.target.value)
                      }
                    />
                  </label>
                  {renameList.error ? (
                    <p role="alert" className="error-message">
                      {renameList.error.message}
                    </p>
                  ) : null}
                  <div className="toolbar">
                    <button
                      type="submit"
                      className="primary-action"
                      disabled={renameList.isPending}
                    >
                      Listennamen speichern
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      disabled={renameList.isPending}
                      onClick={() => setRenamingList(false)}
                    >
                      Abbrechen
                    </button>
                  </div>
                </form>
              ) : null}
              {deletingList ? (
                <section
                  className="confirmation-panel"
                  aria-label="Einkaufsliste löschen"
                >
                  <p>
                    Die Liste und ihre Positionen bleiben 30 Tage im Papierkorb
                    und können dort wiederhergestellt werden.
                  </p>
                  <label className="checkbox-label">
                    <input
                      type="checkbox"
                      checked={deleteListConfirmed}
                      onChange={(event) =>
                        setDeleteListConfirmed(event.target.checked)
                      }
                    />
                    {selectedList.data.name} wirklich in den Papierkorb
                    verschieben
                  </label>
                  {deleteList.error ? (
                    <p role="alert" className="error-message">
                      {deleteList.error.message}
                    </p>
                  ) : null}
                  <div className="toolbar">
                    <button
                      type="button"
                      className="danger-action"
                      disabled={!deleteListConfirmed || deleteList.isPending}
                      onClick={() => deleteList.mutate()}
                    >
                      Einkaufsliste in Papierkorb verschieben
                    </button>
                    <button
                      type="button"
                      className="secondary-action"
                      disabled={deleteList.isPending}
                      onClick={() => setDeletingList(false)}
                    >
                      Abbrechen
                    </button>
                  </div>
                </section>
              ) : null}
              {!readOnly ? (
                <form
                  className="schedule-create-form shopping-item-create"
                  aria-label="Spontane Einkaufsposition"
                  onSubmit={(event) => {
                    event.preventDefault();
                    setNotice("");
                    addItem.mutate();
                  }}
                >
                  <h3>Spontane Position</h3>
                  <div className="camp-form-grid">
                    <label>
                      Bezeichnung der spontanen Position
                      <input
                        required
                        value={itemName}
                        onChange={(event) => setItemName(event.target.value)}
                      />
                    </label>
                    <label>
                      Menge der spontanen Position
                      <input
                        required
                        type="number"
                        min="0.000001"
                        step="any"
                        inputMode="decimal"
                        value={itemQuantity}
                        onChange={(event) =>
                          setItemQuantity(event.target.value)
                        }
                      />
                    </label>
                    <label>
                      Einheit der spontanen Position
                      <select
                        value={itemUnit}
                        onChange={(event) => setItemUnit(event.target.value)}
                      >
                        {Object.entries(shoppingUnitLabels).map(
                          ([unit, label]) => (
                            <option key={unit} value={unit}>
                              {label}
                            </option>
                          ),
                        )}
                      </select>
                    </label>
                    {itemUnit === "5" ? (
                      <label>
                        Name der benutzerdefinierten Einheit
                        <input
                          required
                          value={itemCustomUnit}
                          onChange={(event) =>
                            setItemCustomUnit(event.target.value)
                          }
                        />
                      </label>
                    ) : null}
                    <label>
                      Geschäft (optional)
                      <input
                        value={itemStore}
                        onChange={(event) => setItemStore(event.target.value)}
                      />
                    </label>
                    <label>
                      Notiz (optional)
                      <input
                        value={itemNote}
                        onChange={(event) => setItemNote(event.target.value)}
                      />
                    </label>
                  </div>
                  {addItem.error ? (
                    <p role="alert" className="error-message">
                      {addItem.error.message}
                    </p>
                  ) : null}
                  <button
                    type="submit"
                    className="primary-action"
                    disabled={addItem.isPending}
                  >
                    Spontane Position hinzufügen
                  </button>
                </form>
              ) : null}
              <ul className="check-list shopping-items">
                {selectedList.data.items.map((item) => (
                  <li key={item.id}>
                    <label>
                      <input
                        type="checkbox"
                        checked={item.isChecked}
                        disabled={readOnly || checkItem.isPending}
                        aria-label={`${item.name} ${item.isChecked ? "wieder öffnen" : "abhaken"}`}
                        onChange={(event) =>
                          checkItem.mutate({
                            item,
                            isChecked: event.target.checked,
                          })
                        }
                      />
                      <span>
                        {formatLogisticsQuantity(item.quantity)} {item.name}
                      </span>
                    </label>
                    <small>Quelle: {item.source.label}</small>
                    {item.store ? <small>Geschäft: {item.store}</small> : null}
                    {item.note ? <small>Notiz: {item.note}</small> : null}
                    {item.checkedAt ? (
                      <small>
                        Abgehakt von{" "}
                        {item.checkedByUserId
                          ? (memberNames.get(item.checkedByUserId) ??
                            "einem Camp-Mitglied")
                          : "einem Camp-Mitglied"}{" "}
                        am {formatGermanDateTime(item.checkedAt)}
                      </small>
                    ) : null}
                    {!readOnly && editingItemId !== item.id ? (
                      <div className="shopping-item-actions">
                        <button
                          type="button"
                          className="text-action"
                          onClick={() => {
                            updateItem.reset();
                            setEditingItemId(item.id);
                            setDeletingItemId(null);
                          }}
                        >
                          {item.name} bearbeiten
                        </button>
                        <button
                          type="button"
                          className="text-action danger-text"
                          onClick={() => {
                            deleteItem.reset();
                            setDeletingItemId(item.id);
                            setDeleteItemConfirmed(false);
                            setEditingItemId(null);
                          }}
                        >
                          {item.name} löschen
                        </button>
                      </div>
                    ) : null}
                    {editingItemId === item.id ? (
                      <ShoppingItemEditForm
                        item={item}
                        members={members.data ?? []}
                        pending={updateItem.isPending}
                        error={updateItem.error}
                        onSave={(content) =>
                          updateItem.mutate({ item, content })
                        }
                        onCancel={() => setEditingItemId(null)}
                      />
                    ) : null}
                    {deletingItemId === item.id ? (
                      <section
                        className="confirmation-panel"
                        aria-label={`${item.name} löschen`}
                      >
                        <p>
                          Die Position bleibt 30 Tage im Papierkorb und kann
                          dort wiederhergestellt werden.
                        </p>
                        <label className="checkbox-label">
                          <input
                            type="checkbox"
                            checked={deleteItemConfirmed}
                            onChange={(event) =>
                              setDeleteItemConfirmed(event.target.checked)
                            }
                          />
                          {item.name} wirklich in den Papierkorb verschieben
                        </label>
                        {deleteItem.error ? (
                          <p className="error-message" role="alert">
                            {deleteItem.error.message}
                          </p>
                        ) : null}
                        <div className="toolbar">
                          <button
                            type="button"
                            className="danger-action"
                            disabled={
                              !deleteItemConfirmed || deleteItem.isPending
                            }
                            onClick={() => deleteItem.mutate(item)}
                          >
                            Position in Papierkorb verschieben
                          </button>
                          <button
                            type="button"
                            className="secondary-action"
                            disabled={deleteItem.isPending}
                            onClick={() => setDeletingItemId(null)}
                          >
                            Abbrechen
                          </button>
                        </div>
                      </section>
                    ) : null}
                  </li>
                ))}
              </ul>
              {selectedList.data.items.length === 0 ? (
                <p className="empty-state">
                  Diese Einkaufsliste enthält noch keine Position.
                </p>
              ) : null}
              {checkItem.error ? (
                <p role="alert" className="error-message">
                  {checkItem.error.message}
                </p>
              ) : null}
            </>
          ) : null}
        </section>
      ) : null}
    </>
  );
}
