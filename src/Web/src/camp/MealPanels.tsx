import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import type {
  MealDetail,
  MealRecipeSnapshot,
  MealShoppingDraft,
  RecipeSummary,
  ShoppingListSummary,
  ShoppingTransferLineDraft,
} from "./types";
import { getJson, mutateCateringJson } from "./api";
import { QueryState } from "./ui";
import { OwnerAttachmentsPanel } from "./AttachmentsPanel";
import { formatRecipeQuantity } from "./RecipePanels";
import { shoppingUnitLabels } from "./LogisticsForms";

export function MealShoppingTransferPanel({
  organizationId,
  campId,
  mealId,
  mealName,
}: {
  organizationId: string;
  campId: string;
  mealId: string;
  mealName: string;
}) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [targetListId, setTargetListId] = useState("");
  const [lines, setLines] = useState<ShoppingTransferLineDraft[]>([]);
  const [status, setStatus] = useState("");
  const shoppingLists = useQuery({
    queryKey: [organizationId, campId, "shopping-lists"],
    queryFn: () =>
      getJson<ShoppingListSummary[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/logistics/shopping-lists`,
      ),
    enabled: open,
    retry: false,
  });
  const shoppingDraft = useQuery({
    queryKey: [organizationId, campId, "meal-shopping-draft", mealId],
    queryFn: () =>
      getJson<MealShoppingDraft>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}/shopping-draft`,
      ),
    enabled: open,
    retry: false,
  });

  useEffect(() => {
    if (!shoppingLists.data?.length || targetListId) return;
    setTargetListId(shoppingLists.data[0].id);
  }, [shoppingLists.data, targetListId]);

  useEffect(() => {
    if (!shoppingDraft.data) return;
    setLines(
      shoppingDraft.data.lines.map((line) => ({
        ...line,
        included: true,
        quantity: String(line.suggestedQuantity.value),
        unit: line.suggestedQuantity.unit,
      })),
    );
  }, [shoppingDraft.data]);

  const selectedList = shoppingLists.data?.find(
    (list) => list.id === targetListId,
  );
  const selectedLines = lines.filter((line) => line.included);
  const transfer = useMutation({
    mutationFn: async () => {
      if (!selectedList)
        throw new Error("Wähle eine Einkaufsliste für die Übernahme aus.");
      if (selectedLines.length === 0)
        throw new Error("Wähle mindestens eine Position aus.");
      const invalidLine = selectedLines.find(
        (line) =>
          !Number.isFinite(Number(line.quantity)) || Number(line.quantity) <= 0,
      );
      if (invalidLine)
        throw new Error(
          `Gib für ${invalidLine.ingredientName} eine Menge größer als null ein.`,
        );
      return mutateCateringJson<unknown>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/logistics/shopping-lists/${selectedList.id}/transfer/meal/${mealId}`,
        "POST",
        {
          expectedListVersion: selectedList.version,
          lines: selectedLines.map((line) => ({
            recipeSnapshotId: line.recipeSnapshotId,
            snapshotIngredientId: line.snapshotIngredientId,
            content: {
              name: line.ingredientName,
              quantity: {
                value: Number(line.quantity),
                unit: line.unit,
                customUnitName:
                  line.unit === 5 ? line.suggestedQuantity.countUnitName : null,
              },
              responsibleUserIds: [],
              store: null,
              note: null,
            },
          })),
        },
        selectedList.version,
        "Die Einkaufsliste wurde zwischenzeitlich geändert. Lade den Entwurf neu und prüfe ihn noch einmal.",
      );
    },
    onSuccess: async () => {
      const count = selectedLines.length;
      const listName = selectedList?.name ?? "die Einkaufsliste";
      setOpen(false);
      setStatus(
        `${count} ${count === 1 ? "Position" : "Positionen"} aus ${mealName} ${count === 1 ? "wurde" : "wurden"} in ${listName} übernommen.`,
      );
      await queryClient.invalidateQueries({
        queryKey: [organizationId, campId, "shopping-lists"],
      });
    },
  });

  if (!open)
    return (
      <section className="meal-shopping-transfer">
        {status ? (
          <p
            className="form-feedback"
            role="status"
            aria-label="Einkaufsübernahme"
          >
            {status}
          </p>
        ) : null}
        <button
          type="button"
          className="primary-action"
          onClick={() => {
            setStatus("");
            setTargetListId("");
            setLines([]);
            transfer.reset();
            setOpen(true);
          }}
        >
          In Einkaufsliste übernehmen
        </button>
      </section>
    );

  return (
    <form
      className="schedule-create-form meal-shopping-transfer"
      aria-label="Einkaufsübernahme prüfen"
      onSubmit={(event) => {
        event.preventDefault();
        transfer.mutate();
      }}
    >
      <div className="section-heading">
        <div>
          <p className="eyebrow">Vor der Übernahme prüfen</p>
          <h3>Einkaufspositionen für {mealName}</h3>
        </div>
        <button
          type="button"
          className="secondary-action"
          onClick={() => setOpen(false)}
        >
          Übernahme schließen
        </button>
      </div>
      <p className="form-hint">
        Passe Mengen und Einheiten bewusst an. Es gibt keine automatische
        Packungsrundung; angeboten werden nur fachlich kompatible Einheiten.
      </p>
      <QueryState
        loading={shoppingLists.isLoading || shoppingDraft.isLoading}
        error={shoppingLists.error ?? shoppingDraft.error}
      />
      {shoppingLists.data?.length === 0 ? (
        <p className="empty-state">
          Lege zuerst unter Material &amp; Einkauf eine Einkaufsliste an.
        </p>
      ) : null}
      {shoppingLists.data?.length ? (
        <label>
          Ziel-Einkaufsliste
          <select
            required
            value={targetListId}
            onChange={(event) => setTargetListId(event.target.value)}
          >
            {shoppingLists.data.map((list) => (
              <option key={list.id} value={list.id}>
                {list.name} · {list.openItemCount} offen
              </option>
            ))}
          </select>
        </label>
      ) : null}
      {lines.map((line) => (
        <fieldset
          className="shopping-transfer-line"
          key={line.snapshotIngredientId}
        >
          <legend>{line.ingredientName}</legend>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={line.included}
              onChange={(event) =>
                setLines((current) =>
                  current.map((candidate) =>
                    candidate.snapshotIngredientId === line.snapshotIngredientId
                      ? { ...candidate, included: event.target.checked }
                      : candidate,
                  ),
                )
              }
            />
            {line.ingredientName} übernehmen
          </label>
          <div className="shopping-transfer-fields">
            <label>
              Menge für {line.ingredientName}
              <input
                type="number"
                min="0.000001"
                step="any"
                inputMode="decimal"
                disabled={!line.included}
                value={line.quantity}
                onChange={(event) =>
                  setLines((current) =>
                    current.map((candidate) =>
                      candidate.snapshotIngredientId ===
                      line.snapshotIngredientId
                        ? { ...candidate, quantity: event.target.value }
                        : candidate,
                    ),
                  )
                }
              />
            </label>
            <label>
              Einheit für {line.ingredientName}
              <select
                disabled={!line.included}
                value={line.unit}
                onChange={(event) =>
                  setLines((current) =>
                    current.map((candidate) =>
                      candidate.snapshotIngredientId ===
                      line.snapshotIngredientId
                        ? { ...candidate, unit: Number(event.target.value) }
                        : candidate,
                    ),
                  )
                }
              >
                {line.compatibleUnits.map((unit) => (
                  <option key={unit} value={unit}>
                    {unit === 5
                      ? (line.suggestedQuantity.countUnitName ??
                        shoppingUnitLabels[unit])
                      : shoppingUnitLabels[unit]}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <small>Quelle: {line.sourceLabel}</small>
        </fieldset>
      ))}
      {shoppingDraft.data && lines.length === 0 ? (
        <p className="empty-state">
          Diese Mahlzeit enthält keine Einkaufspositionen.
        </p>
      ) : null}
      {transfer.error ? (
        <p role="alert" className="error-message">
          {transfer.error.message}
        </p>
      ) : null}
      <button
        type="submit"
        className="primary-action"
        disabled={
          transfer.isPending || !selectedList || selectedLines.length === 0
        }
      >
        {transfer.isPending
          ? "Positionen werden übernommen …"
          : `${selectedLines.length} ${selectedLines.length === 1 ? "Position" : "Positionen"} übernehmen`}
      </button>
    </form>
  );
}

export function MealDetailPanel({
  organizationId,
  campId,
  mealId,
  readOnly,
  onClose,
  onDeleted,
}: {
  organizationId: string;
  campId: string;
  mealId: string;
  readOnly: boolean;
  onClose: () => void;
  onDeleted: (name: string) => void;
}) {
  const queryClient = useQueryClient();
  const detailQueryKey = [organizationId, campId, "meal", mealId];
  const meal = useQuery({
    queryKey: detailQueryKey,
    queryFn: () =>
      getJson<MealDetail>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}`,
      ),
    retry: false,
  });
  const [notice, setNotice] = useState("");
  const [editing, setEditing] = useState(false);
  const [editName, setEditName] = useState("");
  const [editOverride, setEditOverride] = useState(false);
  const [editPortions, setEditPortions] = useState("");
  const [editScheduleEntryId, setEditScheduleEntryId] = useState("");
  const [recipeToAdd, setRecipeToAdd] = useState("");
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleteConfirmed, setDeleteConfirmed] = useState(false);
  const recipes = useQuery({
    queryKey: [organizationId, "catering", "recipes"],
    queryFn: () =>
      getJson<RecipeSummary[]>(
        `/api/v1/organizations/${organizationId}/catering/recipes`,
      ),
    retry: false,
  });
  const updateMeal = useMutation({
    mutationFn: () => {
      if (!meal.data) throw new Error("Die Mahlzeit ist noch nicht geladen.");
      return mutateCateringJson<MealDetail>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}`,
        "PUT",
        {
          name: editName,
          portionOverride: editOverride ? Number(editPortions) : null,
          scheduleEntryId: editScheduleEntryId || null,
          recipeIds: [],
        },
        meal.data.version,
        "Die Mahlzeit wurde zwischenzeitlich geändert. Schließe die Bearbeitung und öffne sie erneut.",
      );
    },
    onSuccess: async (updated) => {
      queryClient.setQueryData(detailQueryKey, updated);
      await queryClient.invalidateQueries({
        queryKey: [organizationId, campId, "meals"],
      });
      setEditing(false);
      setNotice(`${updated.name} wurde gespeichert.`);
    },
  });
  const addSnapshot = useMutation({
    mutationFn: () => {
      if (!meal.data) throw new Error("Die Mahlzeit ist noch nicht geladen.");
      return mutateCateringJson<MealDetail>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}/recipes`,
        "POST",
        { recipeId: recipeToAdd },
        meal.data.version,
      );
    },
    onSuccess: (updated) => {
      const added = recipes.data?.find((recipe) => recipe.id === recipeToAdd);
      queryClient.setQueryData(detailQueryKey, updated);
      setRecipeToAdd("");
      setNotice(`${added?.name ?? "Rezept"} wurde hinzugefügt.`);
    },
  });
  const removeSnapshot = useMutation({
    mutationFn: (snapshot: MealRecipeSnapshot) => {
      if (!meal.data) throw new Error("Die Mahlzeit ist noch nicht geladen.");
      return mutateCateringJson<MealDetail>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}/recipes/${snapshot.id}`,
        "DELETE",
        {},
        meal.data.version,
      );
    },
    onSuccess: (updated, snapshot) => {
      queryClient.setQueryData(detailQueryKey, updated);
      setNotice(`${snapshot.name} wurde entfernt.`);
    },
  });
  const deleteMeal = useMutation({
    mutationFn: async () => {
      if (!meal.data) throw new Error("Die Mahlzeit ist noch nicht geladen.");
      const name = meal.data.name;
      await mutateCateringJson<void>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}`,
        "DELETE",
        {},
        meal.data.version,
      );
      return name;
    },
    onSuccess: async (name) => {
      await queryClient.invalidateQueries({
        queryKey: [organizationId, campId, "meals"],
      });
      onDeleted(name);
    },
  });
  const refreshSnapshot = useMutation({
    mutationFn: (snapshot: MealRecipeSnapshot) => {
      const current = meal.data;
      if (!current) throw new Error("Die Mahlzeit ist noch nicht geladen.");
      return mutateCateringJson<MealDetail>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals/${mealId}/recipes/${snapshot.id}/refresh`,
        "POST",
        {},
        current.version,
        "Die Mahlzeit wurde zwischenzeitlich geändert. Schließe die Details und öffne sie erneut.",
      );
    },
    onSuccess: async (revised, snapshot) => {
      queryClient.setQueryData(detailQueryKey, revised);
      await queryClient.invalidateQueries({
        queryKey: [organizationId, campId, "meals"],
      });
      setNotice(
        `${snapshot.name} wurde ausdrücklich auf Rezeptversion ${snapshot.latestRecipeVersionNumber} aktualisiert.`,
      );
    },
  });
  const current = meal.data;

  return (
    <section className="meal-detail-panel" aria-label="Mahlzeitdetails">
      <div className="section-heading">
        <div>
          <p className="eyebrow">
            {current
              ? `${current.effectivePortions} Personen`
              : "Mahlzeit wird geladen"}
          </p>
          <h2>
            {current ? `Mahlzeitdetails: ${current.name}` : "Mahlzeitdetails"}
          </h2>
        </div>
        <button type="button" className="secondary-action" onClick={onClose}>
          Mahlzeit schließen
        </button>
      </div>
      <QueryState loading={meal.isLoading} error={meal.error} />
      {notice ? (
        <p className="form-feedback" role="status">
          {notice}
        </p>
      ) : null}
      {current ? (
        <>
          <p className="form-hint">
            {current.portionOverride === null
              ? `Verwendet den Camp-Standard von ${current.campDefaultPortions} Personen.`
              : `Überschreibt den Camp-Standard von ${current.campDefaultPortions} mit ${current.portionOverride} Personen.`}
            {current.scheduleEntryId
              ? " Mit einem Zeitplaneintrag verknüpft."
              : " Ohne Zeitplaneintrag."}
          </p>
          {!readOnly && !editing ? (
            <button
              type="button"
              className="secondary-action"
              onClick={() => {
                setEditName(current.name);
                setEditOverride(current.portionOverride !== null);
                setEditPortions(
                  String(
                    current.portionOverride ?? current.campDefaultPortions,
                  ),
                );
                setEditScheduleEntryId(current.scheduleEntryId ?? "");
                setEditing(true);
                setNotice("");
              }}
            >
              Mahlzeit bearbeiten
            </button>
          ) : null}
          {editing ? (
            <form
              className="schedule-create-form meal-create-form"
              aria-label={`${current.name} bearbeiten`}
              onSubmit={(event) => {
                event.preventDefault();
                updateMeal.mutate();
              }}
            >
              <label>
                Name bearbeiten
                <input
                  required
                  value={editName}
                  onChange={(event) => setEditName(event.target.value)}
                />
              </label>
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={editOverride}
                  onChange={(event) => setEditOverride(event.target.checked)}
                />
                Personenzahl weiter überschreiben
              </label>
              {editOverride ? (
                <label>
                  Personenzahl bearbeiten
                  <input
                    required
                    type="number"
                    min="1"
                    step="1"
                    value={editPortions}
                    onChange={(event) => setEditPortions(event.target.value)}
                  />
                </label>
              ) : null}
              <label>
                Zeitplaneintrag-ID bearbeiten
                <input
                  value={editScheduleEntryId}
                  onChange={(event) =>
                    setEditScheduleEntryId(event.target.value)
                  }
                />
              </label>
              {updateMeal.error ? (
                <p role="alert" className="error-message">
                  {updateMeal.error.message}
                </p>
              ) : null}
              <div className="toolbar">
                <button
                  type="submit"
                  className="primary-action"
                  disabled={updateMeal.isPending}
                >
                  Änderungen speichern
                </button>
                <button
                  type="button"
                  className="secondary-action"
                  onClick={() => setEditing(false)}
                >
                  Abbrechen
                </button>
              </div>
            </form>
          ) : null}
          {!readOnly ? (
            <form
              className="meal-snapshot-add"
              onSubmit={(event) => {
                event.preventDefault();
                addSnapshot.mutate();
              }}
            >
              <label>
                Rezept-Snapshot hinzufügen
                <select
                  value={recipeToAdd}
                  onChange={(event) => setRecipeToAdd(event.target.value)}
                >
                  <option value="">Rezept auswählen</option>
                  {recipes.data
                    ?.filter(
                      (recipe) =>
                        !current.recipeSnapshots.some(
                          (snapshot) => snapshot.sourceRecipeId === recipe.id,
                        ),
                    )
                    .map((recipe) => (
                      <option key={recipe.id} value={recipe.id}>
                        {recipe.name}
                      </option>
                    ))}
                </select>
              </label>
              <button
                type="submit"
                className="secondary-action"
                disabled={!recipeToAdd || addSnapshot.isPending}
              >
                Snapshot hinzufügen
              </button>
            </form>
          ) : null}
          <div className="meal-snapshot-list">
            {current.recipeSnapshots.map((snapshot) => (
              <article className="meal-snapshot-card" key={snapshot.id}>
                <div className="section-heading">
                  <div>
                    <p className="eyebrow">
                      Rezeptversion {snapshot.sourceRecipeVersionNumber} von{" "}
                      {snapshot.latestRecipeVersionNumber}
                    </p>
                    <h3>{snapshot.name}</h3>
                  </div>
                  {snapshot.refreshAvailable ? (
                    <span className="status warn">Neue Version verfügbar</span>
                  ) : (
                    <span className="status done">Aktuell</span>
                  )}
                </div>
                <p>{snapshot.description}</p>
                <h4>Skalierte Zutaten</h4>
                <ul className="recipe-detail-ingredients">
                  {snapshot.ingredients.map((ingredient) => (
                    <li key={ingredient.id}>
                      <span>
                        {formatRecipeQuantity(ingredient.scaledQuantity)}{" "}
                        {ingredient.ingredientName}
                      </span>
                      {ingredient.note ? (
                        <small>{ingredient.note}</small>
                      ) : null}
                    </li>
                  ))}
                </ul>
                {snapshot.allergenNotes ? (
                  <p>
                    <strong>Allergenhinweis:</strong> {snapshot.allergenNotes}
                  </p>
                ) : null}
                {snapshot.refreshAvailable ? (
                  <button
                    type="button"
                    className="primary-action"
                    disabled={readOnly || refreshSnapshot.isPending}
                    onClick={() => {
                      setNotice("");
                      refreshSnapshot.mutate(snapshot);
                    }}
                  >
                    {snapshot.name} auf Version{" "}
                    {snapshot.latestRecipeVersionNumber} aktualisieren
                  </button>
                ) : null}
                {!readOnly ? (
                  <button
                    type="button"
                    className="text-action"
                    disabled={removeSnapshot.isPending}
                    onClick={() => removeSnapshot.mutate(snapshot)}
                  >
                    {snapshot.name} aus Mahlzeit entfernen
                  </button>
                ) : null}
              </article>
            ))}
            {current.recipeSnapshots.length === 0 ? (
              <p className="empty-state">
                Diese Mahlzeit enthält noch keinen Rezept-Snapshot.
              </p>
            ) : null}
          </div>
          {refreshSnapshot.error ? (
            <p role="alert" className="error-message">
              {refreshSnapshot.error.message}
            </p>
          ) : null}
          {addSnapshot.error || removeSnapshot.error ? (
            <p role="alert" className="error-message">
              {addSnapshot.error?.message ?? removeSnapshot.error?.message}
            </p>
          ) : null}
          {!readOnly ? (
            <MealShoppingTransferPanel
              organizationId={organizationId}
              campId={campId}
              mealId={mealId}
              mealName={current.name}
            />
          ) : null}
          <OwnerAttachmentsPanel
            organizationId={organizationId}
            campId={campId}
            ownerType="Meal"
            ownerId={mealId}
            ownerName={current.name}
            ownerNoun="die Mahlzeit"
            canUpload={!readOnly}
            canDelete={!readOnly}
          />
          {!readOnly && !confirmDelete ? (
            <button
              type="button"
              className="danger-action"
              onClick={() => {
                setConfirmDelete(true);
                setDeleteConfirmed(false);
              }}
            >
              Mahlzeit in Papierkorb verschieben
            </button>
          ) : null}
          {confirmDelete ? (
            <section
              className="confirmation-panel"
              aria-label="Mahlzeit löschen"
            >
              <p>
                Die Mahlzeit bleibt 30 Tage im Papierkorb und kann dort
                wiederhergestellt werden.
              </p>
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={deleteConfirmed}
                  onChange={(event) => setDeleteConfirmed(event.target.checked)}
                />
                Ich möchte diese Mahlzeit in den Papierkorb verschieben.
              </label>
              {deleteMeal.error ? (
                <p role="alert" className="error-message">
                  {deleteMeal.error.message}
                </p>
              ) : null}
              <div className="toolbar">
                <button
                  type="button"
                  className="danger-action"
                  disabled={!deleteConfirmed || deleteMeal.isPending}
                  onClick={() => deleteMeal.mutate()}
                >
                  Verschieben bestätigen
                </button>
                <button
                  type="button"
                  className="secondary-action"
                  onClick={() => setConfirmDelete(false)}
                >
                  Abbrechen
                </button>
              </div>
            </section>
          ) : null}
        </>
      ) : null}
    </section>
  );
}
