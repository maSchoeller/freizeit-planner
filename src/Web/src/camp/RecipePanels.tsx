import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import type {
  Ingredient,
  IngredientMergePreview,
  IngredientMergeResult,
  RecipeDetail,
  RecipeIngredientDraft,
  RecipeQuantity,
} from "./types";
import { getJson, mutateCateringJson } from "./api";
import { QueryState } from "./ui";
import { OwnerAttachmentsPanel } from "./AttachmentsPanel";

export function IngredientLibraryPanel({
  organizationId,
  onClose,
}: {
  organizationId: string;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const queryKey = [organizationId, "catering", "ingredient-management"];
  const ingredients = useQuery({
    queryKey,
    queryFn: () =>
      getJson<Ingredient[]>(
        `/api/v1/organizations/${organizationId}/catering/ingredients?query=&limit=100`,
      ),
    retry: false,
  });
  const [newIngredientName, setNewIngredientName] = useState("");
  const [renameIngredient, setRenameIngredient] = useState<Ingredient | null>(
    null,
  );
  const [renameName, setRenameName] = useState("");
  const [sourceIngredientId, setSourceIngredientId] = useState("");
  const [targetIngredientId, setTargetIngredientId] = useState("");
  const [mergeConfirmed, setMergeConfirmed] = useState(false);
  const [notice, setNotice] = useState("");
  const createIngredient = useMutation({
    mutationFn: () =>
      mutateCateringJson<Ingredient>(
        `/api/v1/organizations/${organizationId}/catering/ingredients`,
        "POST",
        { name: newIngredientName },
      ),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey });
      setNewIngredientName("");
      setNotice(`${created.name} wurde angelegt.`);
    },
  });
  const renameMutation = useMutation({
    mutationFn: () => {
      if (!renameIngredient) throw new Error("Wähle zuerst eine Zutat aus.");
      return mutateCateringJson<Ingredient>(
        `/api/v1/organizations/${organizationId}/catering/ingredients/${renameIngredient.id}`,
        "PUT",
        { name: renameName },
        renameIngredient.version,
      );
    },
    onSuccess: async (renamed) => {
      await queryClient.invalidateQueries({ queryKey });
      setRenameIngredient(null);
      setRenameName("");
      setNotice(`${renamed.name} wurde gespeichert.`);
    },
  });
  const previewMerge = useMutation({
    mutationFn: () =>
      mutateCateringJson<IngredientMergePreview>(
        `/api/v1/organizations/${organizationId}/catering/ingredients/merge-preview`,
        "POST",
        {
          sourceIngredientId,
          targetIngredientId,
          expectedSourceVersion: 0,
          expectedTargetVersion: 0,
        },
      ),
    onSuccess: () => {
      setMergeConfirmed(false);
      setNotice("");
    },
  });
  const mergeIngredients = useMutation({
    mutationFn: () => {
      const preview = previewMerge.data;
      if (!preview) throw new Error("Prüfe die Zusammenführung zuerst erneut.");
      return mutateCateringJson<IngredientMergeResult>(
        `/api/v1/organizations/${organizationId}/catering/ingredients/merge`,
        "POST",
        {
          sourceIngredientId: preview.source.id,
          targetIngredientId: preview.target.id,
          expectedSourceVersion: preview.source.version,
          expectedTargetVersion: preview.target.version,
        },
      );
    },
    onSuccess: async () => {
      const preview = previewMerge.data;
      await Promise.all([
        queryClient.invalidateQueries({ queryKey }),
        queryClient.invalidateQueries({
          queryKey: [organizationId, "catering", "recipes"],
        }),
      ]);
      setNotice(
        `${preview?.source.name ?? "Die Zutat"} wurde kontrolliert in ${preview?.target.name ?? "die Zielzutat"} zusammengeführt.`,
      );
      setSourceIngredientId("");
      setTargetIngredientId("");
      setMergeConfirmed(false);
      previewMerge.reset();
    },
  });
  const mutationError =
    createIngredient.error ??
    renameMutation.error ??
    previewMerge.error ??
    mergeIngredients.error;

  return (
    <section
      className="settings-section ingredient-management"
      aria-labelledby="ingredient-management-heading"
    >
      <div className="section-heading">
        <div>
          <p className="eyebrow">Organisationsbibliothek</p>
          <h2 id="ingredient-management-heading">
            Zutatenbibliothek verwalten
          </h2>
        </div>
        <button className="secondary-action" type="button" onClick={onClose}>
          Verwaltung schließen
        </button>
      </div>
      <p>
        Namen werden normalisiert und sind innerhalb der Organisation eindeutig.
        Eine Zusammenführung ändert aktuelle Bibliotheksrezepte, aber keine
        vorhandenen Mahlzeiten-Snapshots.
      </p>
      {notice ? (
        <p role="status" className="form-feedback">
          {notice}
        </p>
      ) : null}
      {mutationError ? (
        <p role="alert" className="error-message">
          {mutationError.message}
        </p>
      ) : null}
      <form
        className="toolbar"
        onSubmit={(event) => {
          event.preventDefault();
          setNotice("");
          createIngredient.mutate();
        }}
      >
        <label>
          Neue Zutat
          <input
            required
            value={newIngredientName}
            onChange={(event) => setNewIngredientName(event.target.value)}
          />
        </label>
        <button
          className="primary-action"
          type="submit"
          disabled={createIngredient.isPending}
        >
          {createIngredient.isPending
            ? "Zutat wird angelegt …"
            : "Zutat anlegen"}
        </button>
      </form>
      <QueryState loading={ingredients.isLoading} error={ingredients.error} />
      {ingredients.data?.length ? (
        <ul className="ingredient-list">
          {ingredients.data.map((ingredient) => (
            <li key={ingredient.id}>
              <span>
                <strong>{ingredient.name}</strong>
                <small>Version {ingredient.version}</small>
              </span>
              <button
                className="text-action"
                type="button"
                onClick={() => {
                  setRenameIngredient(ingredient);
                  setRenameName(ingredient.name);
                  setNotice("");
                }}
              >
                {ingredient.name} umbenennen
              </button>
            </li>
          ))}
        </ul>
      ) : (
        !ingredients.isLoading && (
          <p className="empty-state">Noch keine Zutat vorhanden.</p>
        )
      )}
      {renameIngredient ? (
        <form
          className="schedule-create-form compact-form"
          onSubmit={(event) => {
            event.preventDefault();
            setNotice("");
            renameMutation.mutate();
          }}
        >
          <h3>{renameIngredient.name} umbenennen</h3>
          <label>
            Neuer Name für {renameIngredient.name}
            <input
              required
              value={renameName}
              onChange={(event) => setRenameName(event.target.value)}
            />
          </label>
          <div className="toolbar">
            <button
              className="primary-action"
              type="submit"
              disabled={renameMutation.isPending}
            >
              Neuen Namen speichern
            </button>
            <button
              className="secondary-action"
              type="button"
              onClick={() => setRenameIngredient(null)}
            >
              Abbrechen
            </button>
          </div>
        </form>
      ) : null}
      <form
        className="schedule-create-form ingredient-merge-form"
        aria-labelledby="ingredient-merge-heading"
        onSubmit={(event) => {
          event.preventDefault();
          previewMerge.mutate();
        }}
      >
        <h3 id="ingredient-merge-heading">Doppelte Zutaten zusammenführen</h3>
        <p className="form-hint">
          Die doppelte Zutat wird nach der Bestätigung nicht mehr angeboten. Die
          Zielzutat bleibt erhalten.
        </p>
        <div className="schedule-create-grid schedule-all-day-grid">
          <label>
            Doppelte Zutat
            <select
              required
              value={sourceIngredientId}
              onChange={(event) => {
                setSourceIngredientId(event.target.value);
                previewMerge.reset();
                setMergeConfirmed(false);
              }}
            >
              <option value="">Bitte wählen</option>
              {ingredients.data?.map((ingredient) => (
                <option key={ingredient.id} value={ingredient.id}>
                  {ingredient.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Zielzutat
            <select
              required
              value={targetIngredientId}
              onChange={(event) => {
                setTargetIngredientId(event.target.value);
                previewMerge.reset();
                setMergeConfirmed(false);
              }}
            >
              <option value="">Bitte wählen</option>
              {ingredients.data?.map((ingredient) => (
                <option key={ingredient.id} value={ingredient.id}>
                  {ingredient.name}
                </option>
              ))}
            </select>
          </label>
        </div>
        <button
          className="secondary-action"
          type="submit"
          disabled={
            previewMerge.isPending ||
            !sourceIngredientId ||
            !targetIngredientId ||
            sourceIngredientId === targetIngredientId
          }
        >
          {previewMerge.isPending
            ? "Zusammenführung wird geprüft …"
            : "Zusammenführung prüfen"}
        </button>
        {previewMerge.data ? (
          <section
            className="merge-preview"
            aria-labelledby="merge-preview-heading"
          >
            <h4 id="merge-preview-heading">
              Auswirkung: {previewMerge.data.source.name} →{" "}
              {previewMerge.data.target.name}
            </h4>
            {previewMerge.data.affectedRecipes.length ? (
              <>
                <p>Folgende aktuelle Rezepte erhalten eine neue Version:</p>
                <ul>
                  {previewMerge.data.affectedRecipes.map((recipe) => (
                    <li key={recipe.id}>
                      {recipe.name} · Version {recipe.currentVersionNumber}
                    </li>
                  ))}
                </ul>
              </>
            ) : (
              <p>Kein aktuelles Rezept ist betroffen.</p>
            )}
            <p>
              Bereits gespeicherte Mahlzeiten-Snapshots bleiben unverändert.
            </p>
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={mergeConfirmed}
                onChange={(event) => setMergeConfirmed(event.target.checked)}
              />
              Ich habe die betroffenen Rezepte geprüft.
            </label>
            <button
              className="danger-action"
              type="button"
              disabled={!mergeConfirmed || mergeIngredients.isPending}
              onClick={() => mergeIngredients.mutate()}
            >
              {mergeIngredients.isPending
                ? "Zutaten werden zusammengeführt …"
                : "Zusammenführung bestätigen"}
            </button>
          </section>
        ) : null}
      </form>
    </section>
  );
}

export const measurementUnitLabels = [
  "Gramm",
  "Kilogramm",
  "Milliliter",
  "Liter",
  "Stück",
] as const;

export function formatRecipeQuantity(quantity: RecipeQuantity) {
  const value = new Intl.NumberFormat("de-DE", {
    maximumFractionDigits: 3,
  }).format(quantity.value);
  const unit =
    quantity.unit === 5
      ? quantity.countUnitName || "Zähleinheit"
      : measurementUnitLabels[quantity.unit] || "Einheit";
  return `${value} ${unit}`;
}

export function RecipeDetailPanel({
  organizationId,
  recipeId,
  canManage,
  readOnly,
  onClose,
}: {
  organizationId: string;
  recipeId: string;
  canManage: boolean;
  readOnly: boolean;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const detailQueryKey = [organizationId, "catering", "recipes", recipeId];
  const recipe = useQuery({
    queryKey: detailQueryKey,
    queryFn: () =>
      getJson<RecipeDetail>(
        `/api/v1/organizations/${organizationId}/catering/recipes/${recipeId}`,
      ),
    retry: false,
  });
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [preparation, setPreparation] = useState("");
  const [basePortions, setBasePortions] = useState("4");
  const [dietaryTags, setDietaryTags] = useState("");
  const [allergenNotes, setAllergenNotes] = useState("");
  const [kitchenNotes, setKitchenNotes] = useState("");
  const [ingredientSearch, setIngredientSearch] = useState("");
  const [ingredients, setIngredients] = useState<RecipeIngredientDraft[]>([]);
  const [notice, setNotice] = useState("");
  const ingredientSuggestions = useQuery({
    queryKey: [
      organizationId,
      "catering",
      "ingredients",
      "recipe-edit",
      ingredientSearch.trim(),
    ],
    queryFn: () =>
      getJson<Ingredient[]>(
        `/api/v1/organizations/${organizationId}/catering/ingredients?query=${encodeURIComponent(ingredientSearch.trim())}&limit=10`,
      ),
    enabled: editing && ingredientSearch.trim().length >= 2,
    retry: false,
  });
  const reviseRecipe = useMutation({
    mutationFn: () => {
      const current = recipe.data;
      if (!current) throw new Error("Das Rezept ist noch nicht geladen.");
      return mutateCateringJson<RecipeDetail>(
        `/api/v1/organizations/${organizationId}/catering/recipes/${recipeId}`,
        "PUT",
        {
          name,
          description,
          preparation,
          basePortions: Number(basePortions),
          ingredients: ingredients.map((row) => ({
            ingredientId: row.ingredient.id,
            quantity: {
              value: Number(row.quantity),
              unit: Number(row.unit),
              countUnitName:
                row.unit === "5" ? row.countUnitName || null : null,
            },
            note: row.note || null,
          })),
          dietaryTags: Array.from(
            new Set(
              dietaryTags
                .split(/[,;\n]/)
                .map((tag) => tag.trim())
                .filter(Boolean),
            ),
          ),
          allergenNotes: allergenNotes || null,
          kitchenNotes: kitchenNotes || null,
        },
        current.version,
        "Das Rezept wurde zwischenzeitlich geändert. Schließe die Bearbeitung und öffne das Rezept erneut.",
      );
    },
    onSuccess: async (revised) => {
      queryClient.setQueryData(detailQueryKey, revised);
      await queryClient.invalidateQueries({
        queryKey: [organizationId, "catering", "recipes"],
      });
      setEditing(false);
      setIngredientSearch("");
      setNotice(
        `${revised.currentVersion.name} wurde als Rezeptversion ${revised.currentVersion.number} gespeichert.`,
      );
    },
  });
  const beginEditing = () => {
    const current = recipe.data?.currentVersion;
    if (!current) return;
    setName(current.name);
    setDescription(current.description);
    setPreparation(current.preparation);
    setBasePortions(String(current.basePortions));
    setDietaryTags(current.dietaryTags.join(", "));
    setAllergenNotes(current.allergenNotes ?? "");
    setKitchenNotes(current.kitchenNotes ?? "");
    setIngredients(
      current.ingredients.map((row) => ({
        ingredient: { id: row.ingredientId, name: row.ingredientName },
        quantity: String(row.quantity.value),
        unit: String(row.quantity.unit),
        countUnitName: row.quantity.countUnitName ?? "",
        note: row.note ?? "",
      })),
    );
    setIngredientSearch("");
    setNotice("");
    reviseRecipe.reset();
    setEditing(true);
  };
  const updateIngredient = (
    ingredientId: string,
    changes: Partial<RecipeIngredientDraft>,
  ) =>
    setIngredients((current) =>
      current.map((row) =>
        row.ingredient.id === ingredientId ? { ...row, ...changes } : row,
      ),
    );
  const current = recipe.data?.currentVersion;

  return (
    <section className="recipe-detail-panel" aria-label="Rezeptdetails">
      <div className="section-heading">
        <div>
          <p className="eyebrow">
            {current ? `Aktuelle Version ${current.number}` : "Rezept"}
          </p>
          <h2>
            {current ? `Rezeptdetails: ${current.name}` : "Rezept wird geladen"}
          </h2>
        </div>
        <button type="button" className="secondary-action" onClick={onClose}>
          Rezept schließen
        </button>
      </div>
      <QueryState loading={recipe.isLoading} error={recipe.error} />
      {notice ? (
        <p className="form-feedback" role="status">
          {notice}
        </p>
      ) : null}
      {current && !editing ? (
        <div className="recipe-detail-content">
          <div className="recipe-detail-grid">
            <section>
              <h3>Beschreibung</h3>
              <p>{current.description}</p>
            </section>
            <section>
              <h3>Zubereitung</h3>
              <p className="long-text">{current.preparation}</p>
            </section>
          </div>
          <section>
            <h3>Zutaten für {current.basePortions} Basisportionen</h3>
            <ul className="recipe-detail-ingredients">
              {current.ingredients.map((row) => (
                <li key={row.id}>
                  <span>
                    {formatRecipeQuantity(row.quantity)} {row.ingredientName}
                  </span>
                  {row.note ? <small>{row.note}</small> : null}
                </li>
              ))}
            </ul>
          </section>
          <div className="recipe-detail-grid">
            <section>
              <h3>Ernährungs-Tags</h3>
              <p>
                {current.dietaryTags.length
                  ? current.dietaryTags.join(", ")
                  : "Keine Tags hinterlegt"}
              </p>
            </section>
            <section>
              <h3>Allergenhinweise</h3>
              <p>{current.allergenNotes || "Keine Hinweise hinterlegt"}</p>
            </section>
            <section>
              <h3>Küchenhinweise</h3>
              <p>{current.kitchenNotes || "Keine Hinweise hinterlegt"}</p>
            </section>
          </div>
          <p className="form-hint">
            Gespeichert am{" "}
            {new Intl.DateTimeFormat("de-DE", {
              dateStyle: "medium",
              timeStyle: "short",
            }).format(new Date(current.createdAt))}
            . Bereits geplante Mahlzeiten behalten ihren unveränderten
            Rezept-Snapshot.
          </p>
          {canManage && !readOnly ? (
            <button
              type="button"
              className="primary-action"
              onClick={beginEditing}
            >
              Rezept bearbeiten
            </button>
          ) : null}
        </div>
      ) : null}
      {current && editing ? (
        <form
          className="schedule-create-form recipe-form recipe-edit-form"
          aria-label={`${current.name} bearbeiten`}
          onSubmit={(event) => {
            event.preventDefault();
            setNotice("");
            reviseRecipe.mutate();
          }}
        >
          <p className="form-hint">
            Änderungen erzeugen eine neue Version. Bestehende
            Mahlzeiten-Snapshots werden nicht still verändert.
          </p>
          <div className="camp-form-grid">
            <label>
              Rezeptname bearbeiten
              <input
                required
                value={name}
                onChange={(event) => setName(event.target.value)}
              />
            </label>
            <label>
              Basisportionen bearbeiten
              <input
                required
                type="number"
                min="1"
                step="1"
                value={basePortions}
                onChange={(event) => setBasePortions(event.target.value)}
              />
            </label>
            <label className="full-row">
              Beschreibung bearbeiten
              <textarea
                required
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
            </label>
            <label className="full-row">
              Zubereitung bearbeiten
              <textarea
                required
                value={preparation}
                onChange={(event) => setPreparation(event.target.value)}
              />
            </label>
          </div>
          <fieldset>
            <legend>Zutatenpositionen bearbeiten</legend>
            <label>
              Weitere Zutat suchen
              <input
                type="search"
                value={ingredientSearch}
                placeholder="Mindestens zwei Zeichen"
                onChange={(event) => setIngredientSearch(event.target.value)}
              />
            </label>
            {ingredientSuggestions.isLoading ? (
              <p role="status">Zutaten werden gesucht …</p>
            ) : null}
            {ingredientSuggestions.error ? (
              <p role="alert" className="error-message">
                {ingredientSuggestions.error.message}
              </p>
            ) : null}
            {ingredientSuggestions.data?.length ? (
              <ul className="autocomplete-results">
                {ingredientSuggestions.data
                  .filter(
                    (ingredient) =>
                      !ingredients.some(
                        (row) => row.ingredient.id === ingredient.id,
                      ),
                  )
                  .map((ingredient) => (
                    <li key={ingredient.id}>
                      <button
                        type="button"
                        className="secondary-action"
                        aria-label={`${ingredient.name} zum Rezept hinzufügen`}
                        onClick={() => {
                          setIngredients((rows) => [
                            ...rows,
                            {
                              ingredient,
                              quantity: "1",
                              unit: "0",
                              countUnitName: "",
                              note: "",
                            },
                          ]);
                          setIngredientSearch("");
                        }}
                      >
                        {ingredient.name}
                      </button>
                    </li>
                  ))}
              </ul>
            ) : null}
            <div className="recipe-ingredient-list">
              {ingredients.map((row) => (
                <section
                  className="recipe-ingredient-row"
                  aria-label={`${row.ingredient.name} bearbeiten`}
                  key={row.ingredient.id}
                >
                  <h3>{row.ingredient.name}</h3>
                  <label>
                    Menge für {row.ingredient.name} bearbeiten
                    <input
                      required
                      type="number"
                      min="0.001"
                      step="0.001"
                      value={row.quantity}
                      onChange={(event) =>
                        updateIngredient(row.ingredient.id, {
                          quantity: event.target.value,
                        })
                      }
                    />
                  </label>
                  <label>
                    Einheit für {row.ingredient.name} bearbeiten
                    <select
                      value={row.unit}
                      onChange={(event) =>
                        updateIngredient(row.ingredient.id, {
                          unit: event.target.value,
                        })
                      }
                    >
                      <option value="0">Gramm</option>
                      <option value="1">Kilogramm</option>
                      <option value="2">Milliliter</option>
                      <option value="3">Liter</option>
                      <option value="4">Stück</option>
                      <option value="5">Benannte Zähleinheit</option>
                    </select>
                  </label>
                  {row.unit === "5" ? (
                    <label>
                      Name der Zähleinheit für {row.ingredient.name} bearbeiten
                      <input
                        required
                        value={row.countUnitName}
                        onChange={(event) =>
                          updateIngredient(row.ingredient.id, {
                            countUnitName: event.target.value,
                          })
                        }
                      />
                    </label>
                  ) : null}
                  <label>
                    Hinweis für {row.ingredient.name} bearbeiten
                    <input
                      value={row.note}
                      onChange={(event) =>
                        updateIngredient(row.ingredient.id, {
                          note: event.target.value,
                        })
                      }
                    />
                  </label>
                  <button
                    type="button"
                    className="text-action"
                    onClick={() =>
                      setIngredients((rows) =>
                        rows.filter(
                          (item) => item.ingredient.id !== row.ingredient.id,
                        ),
                      )
                    }
                  >
                    {row.ingredient.name} entfernen
                  </button>
                </section>
              ))}
            </div>
          </fieldset>
          <div className="camp-form-grid">
            <label className="full-row">
              Ernährungs-Tags bearbeiten
              <input
                value={dietaryTags}
                onChange={(event) => setDietaryTags(event.target.value)}
              />
            </label>
            <label className="full-row">
              Allergenhinweise bearbeiten
              <textarea
                value={allergenNotes}
                onChange={(event) => setAllergenNotes(event.target.value)}
              />
            </label>
            <label className="full-row">
              Küchenhinweise bearbeiten
              <textarea
                value={kitchenNotes}
                onChange={(event) => setKitchenNotes(event.target.value)}
              />
            </label>
          </div>
          {reviseRecipe.error ? (
            <p role="alert" className="error-message">
              {reviseRecipe.error.message}
            </p>
          ) : null}
          <div className="toolbar">
            <button
              className="primary-action"
              type="submit"
              disabled={reviseRecipe.isPending || ingredients.length === 0}
            >
              {reviseRecipe.isPending
                ? "Neue Rezeptversion wird gespeichert …"
                : "Neue Rezeptversion speichern"}
            </button>
            <button
              className="secondary-action"
              type="button"
              disabled={reviseRecipe.isPending}
              onClick={() => {
                setEditing(false);
                reviseRecipe.reset();
              }}
            >
              Bearbeitung abbrechen
            </button>
          </div>
        </form>
      ) : null}
      {current ? (
        <OwnerAttachmentsPanel
          organizationId={organizationId}
          ownerType="Recipe"
          ownerId={recipeId}
          ownerName={current.name}
          ownerNoun="das Rezept"
          canUpload={canManage && !readOnly}
        />
      ) : null}
    </section>
  );
}
