import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { loadOfflineSnapshot, saveOfflineSnapshot } from "../offlineSnapshot";
import { getAntiforgeryToken } from "../api/security";
import { authenticatedFetch as fetch } from "../api/authentication";
import type {
  Ingredient,
  Meal,
  MealDetail,
  RecipeCreateResult,
  RecipeIngredientDraft,
  RecipeSummary,
  ScheduleEntry,
} from "./types";
import { getJson, mutateCateringJson } from "./api";
import { useCampQuery, useCampRuntime } from "./runtime";
import { nextLocalDate } from "./schedule";
import { PageHeading, PrintButton, QueryState } from "./ui";
import { IngredientLibraryPanel, RecipeDetailPanel } from "./RecipePanels";
import { MealDetailPanel } from "./MealPanels";

export function MealsPage({
  offline,
  readOnly,
}: {
  offline: boolean;
  readOnly: boolean;
}) {
  const runtime = useCampRuntime();
  const { organizationId, organizationRole, campId, camp } = runtime;
  const canManageLibrary = organizationRole === 0 || organizationRole === 1;
  const queryClient = useQueryClient();
  const path = `/api/v1/organizations/${organizationId}/camps/${campId}/catering/meals`;
  const query = useCampQuery<Meal[]>("meals", path, !offline);
  const recipes = useQuery({
    queryKey: [organizationId, "catering", "recipes"],
    queryFn: () =>
      getJson<RecipeSummary[]>(
        `/api/v1/organizations/${organizationId}/catering/recipes`,
      ),
    retry: false,
    enabled: !offline,
  });
  const [showRecipeForm, setShowRecipeForm] = useState(false);
  const [showIngredientLibrary, setShowIngredientLibrary] = useState(false);
  const [selectedRecipeId, setSelectedRecipeId] = useState<string | null>(null);
  const [showMealForm, setShowMealForm] = useState(false);
  const [selectedMealId, setSelectedMealId] = useState<string | null>(null);
  const [mealName, setMealName] = useState("");
  const [overridePortions, setOverridePortions] = useState(false);
  const [mealPortions, setMealPortions] = useState(
    String(camp.defaultPortions),
  );
  const [mealScheduleEntryId, setMealScheduleEntryId] = useState("");
  const [mealRecipeIds, setMealRecipeIds] = useState<string[]>([]);
  const [mealNotice, setMealNotice] = useState("");
  const [recipeName, setRecipeName] = useState("");
  const [recipeDescription, setRecipeDescription] = useState("");
  const [recipePreparation, setRecipePreparation] = useState("");
  const [recipeBasePortions, setRecipeBasePortions] = useState("4");
  const [recipeDietaryTags, setRecipeDietaryTags] = useState("");
  const [recipeAllergenNotes, setRecipeAllergenNotes] = useState("");
  const [recipeKitchenNotes, setRecipeKitchenNotes] = useState("");
  const [ingredientSearch, setIngredientSearch] = useState("");
  const [recipeIngredients, setRecipeIngredients] = useState<
    RecipeIngredientDraft[]
  >([]);
  const [recipeFilter, setRecipeFilter] = useState("");
  const [recipeNotice, setRecipeNotice] = useState("");
  const ingredientSuggestions = useQuery({
    queryKey: [
      organizationId,
      "catering",
      "ingredients",
      ingredientSearch.trim(),
    ],
    queryFn: () =>
      getJson<Ingredient[]>(
        `/api/v1/organizations/${organizationId}/catering/ingredients?query=${encodeURIComponent(ingredientSearch.trim())}&limit=10`,
      ),
    enabled: showRecipeForm && ingredientSearch.trim().length >= 2,
    retry: false,
  });
  const mealScheduleEntries = useQuery({
    queryKey: [organizationId, campId, "meal-schedule-candidates"],
    queryFn: () =>
      getJson<ScheduleEntry[]>(
        `/api/v1/organizations/${organizationId}/camps/${campId}/schedule?fromDate=${camp.startsOn}&toDateExclusive=${nextLocalDate(camp.endsOn)}`,
      ),
    enabled: showMealForm,
    retry: false,
  });
  const createMeal = useMutation({
    mutationFn: () =>
      mutateCateringJson<MealDetail>(path, "POST", {
        name: mealName,
        portionOverride: overridePortions ? Number(mealPortions) : null,
        scheduleEntryId: mealScheduleEntryId || null,
        recipeIds: mealRecipeIds,
      }),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({
        queryKey: [organizationId, campId, "meals"],
      });
      setMealNotice(
        `${created.name} wurde mit ${created.effectivePortions} Personen und ${created.recipeSnapshots.length} Rezept-${created.recipeSnapshots.length === 1 ? "Snapshot" : "Snapshots"} angelegt.`,
      );
      setShowMealForm(false);
      setMealName("");
      setOverridePortions(false);
      setMealPortions(String(camp.defaultPortions));
      setMealScheduleEntryId("");
      setMealRecipeIds([]);
    },
  });
  const createRecipe = useMutation({
    mutationFn: async () => {
      const token = await getAntiforgeryToken();
      const response = await fetch(
        `/api/v1/organizations/${organizationId}/catering/recipes`,
        {
          method: "POST",
          credentials: "same-origin",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token,
          },
          body: JSON.stringify({
            name: recipeName,
            description: recipeDescription,
            preparation: recipePreparation,
            basePortions: Number(recipeBasePortions),
            ingredients: recipeIngredients.map((row) => ({
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
                recipeDietaryTags
                  .split(/[,;\n]/)
                  .map((tag) => tag.trim())
                  .filter(Boolean),
              ),
            ),
            allergenNotes: recipeAllergenNotes || null,
            kitchenNotes: recipeKitchenNotes || null,
          }),
        },
      );
      if (!response.ok) {
        const problem = (await response.json().catch(() => null)) as {
          detail?: string;
        } | null;
        throw new Error(
          problem?.detail ?? "Das Rezept konnte nicht gespeichert werden.",
        );
      }
      return (await response.json()) as RecipeCreateResult;
    },
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({
        queryKey: [organizationId, "catering", "recipes"],
      });
      setRecipeNotice(
        `${created.currentVersion.name} wurde als Rezeptversion ${created.currentVersion.number} gespeichert.`,
      );
      setShowRecipeForm(false);
      setRecipeName("");
      setRecipeDescription("");
      setRecipePreparation("");
      setRecipeBasePortions("4");
      setRecipeDietaryTags("");
      setRecipeAllergenNotes("");
      setRecipeKitchenNotes("");
      setIngredientSearch("");
      setRecipeIngredients([]);
    },
  });
  const meals =
    query.data ??
    (offline
      ? ((loadOfflineSnapshot({ organizationId, campId })?.meals ??
          []) as Meal[])
      : []);
  const filteredRecipes = (recipes.data ?? []).filter((recipe) =>
    recipe.name
      .toLocaleLowerCase("de-DE")
      .includes(recipeFilter.trim().toLocaleLowerCase("de-DE")),
  );
  const updateRecipeIngredient = (
    ingredientId: string,
    changes: Partial<RecipeIngredientDraft>,
  ) =>
    setRecipeIngredients((current) =>
      current.map((row) =>
        row.ingredient.id === ingredientId ? { ...row, ...changes } : row,
      ),
    );
  useEffect(() => {
    if (query.data) saveOfflineSnapshot(runtime, { meals: query.data });
  }, [query.data]);
  return (
    <>
      <PageHeading eyebrow="Versorgung" title="Verpflegung">
        <p>
          Mengen werden dezimal und nur innerhalb kompatibler Einheiten
          skaliert. Allergenhinweise sind keine medizinische Garantie.
        </p>
      </PageHeading>
      <nav className="section-navigation" aria-label="Verpflegung">
        <a href="#essensplan">Essensplan</a>
        <a href="#rezepte">Rezepte</a>
      </nav>
      <div className="toolbar print-actions">
        <PrintButton scope="meals">Mahlzeiten drucken</PrintButton>
      </div>
      <QueryState loading={query.isLoading && !offline} error={query.error} />
      {!offline ? (
        <div className="toolbar">
          <button
            type="button"
            className="primary-action"
            disabled={readOnly}
            aria-expanded={showMealForm}
            onClick={() => {
              setShowMealForm((current) => !current);
              setSelectedMealId(null);
              setSelectedRecipeId(null);
              setShowRecipeForm(false);
              setShowIngredientLibrary(false);
              setMealNotice("");
            }}
          >
            {showMealForm ? "Mahlzeitformular schließen" : "Mahlzeit planen"}
          </button>
          <button
            type="button"
            className="secondary-action"
            disabled={readOnly || !canManageLibrary}
            aria-expanded={showRecipeForm}
            title={
              canManageLibrary
                ? undefined
                : "Nur Organisationsadmins verwalten Rezepte."
            }
            onClick={() => {
              setShowRecipeForm((current) => !current);
              setShowIngredientLibrary(false);
              setSelectedRecipeId(null);
              setShowMealForm(false);
              setSelectedMealId(null);
              setRecipeNotice("");
            }}
          >
            {showRecipeForm ? "Rezeptformular schließen" : "Rezept anlegen"}
          </button>
          <button
            type="button"
            className="secondary-action"
            disabled={readOnly || !canManageLibrary}
            aria-expanded={showIngredientLibrary}
            title={
              canManageLibrary
                ? undefined
                : "Nur Organisationsadmins verwalten Zutaten."
            }
            onClick={() => {
              setShowIngredientLibrary((current) => !current);
              setShowRecipeForm(false);
              setSelectedRecipeId(null);
              setShowMealForm(false);
              setSelectedMealId(null);
              setRecipeNotice("");
            }}
          >
            {showIngredientLibrary
              ? "Zutatenverwaltung schließen"
              : "Zutaten verwalten"}
          </button>
          <label className="search-field">
            Rezepte suchen
            <input
              type="search"
              placeholder="z. B. Kartoffelsuppe"
              value={recipeFilter}
              onChange={(event) => setRecipeFilter(event.target.value)}
            />
          </label>
        </div>
      ) : null}
      {mealNotice ? (
        <p className="form-feedback" role="status">
          {mealNotice}
        </p>
      ) : null}
      {!offline && showMealForm ? (
        <form
          className="schedule-create-form meal-create-form"
          aria-labelledby="new-meal-heading"
          onSubmit={(event) => {
            event.preventDefault();
            setMealNotice("");
            createMeal.mutate();
          }}
        >
          <h2 id="new-meal-heading">Neue Mahlzeit</h2>
          <p className="form-hint">
            Camp-Standard: {camp.defaultPortions} Personen
          </p>
          <div className="camp-form-grid">
            <label>
              Name der Mahlzeit
              <input
                required
                value={mealName}
                onChange={(event) => setMealName(event.target.value)}
              />
            </label>
            <label>
              Zeitplaneintrag
              <select
                value={mealScheduleEntryId}
                onChange={(event) => setMealScheduleEntryId(event.target.value)}
              >
                <option value="">Nicht mit dem Zeitplan verknüpfen</option>
                {mealScheduleEntries.data?.map((entry) => (
                  <option key={entry.id} value={entry.id}>
                    {entry.title}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={overridePortions}
              onChange={(event) => setOverridePortions(event.target.checked)}
            />
            Personenzahl überschreiben
          </label>
          {overridePortions ? (
            <label>
              Personenzahl
              <input
                required
                type="number"
                min="1"
                step="1"
                value={mealPortions}
                onChange={(event) => setMealPortions(event.target.value)}
              />
            </label>
          ) : null}
          <fieldset>
            <legend>Rezept-Snapshots</legend>
            <p className="form-hint">
              Ausgewählte Rezepte werden in ihrem aktuellen Stand kopiert und
              später nicht still verändert.
            </p>
            <div className="meal-recipe-options">
              {(recipes.data ?? []).map((recipe) => (
                <label className="checkbox-label" key={recipe.id}>
                  <input
                    type="checkbox"
                    checked={mealRecipeIds.includes(recipe.id)}
                    onChange={(event) =>
                      setMealRecipeIds((current) =>
                        event.target.checked
                          ? [...current, recipe.id]
                          : current.filter((id) => id !== recipe.id),
                      )
                    }
                  />
                  {recipe.name} als Snapshot hinzufügen
                </label>
              ))}
              {!recipes.isLoading && recipes.data?.length === 0 ? (
                <p className="empty-state">
                  Noch kein Bibliotheksrezept vorhanden. Die Mahlzeit kann
                  trotzdem ohne Rezept angelegt werden.
                </p>
              ) : null}
            </div>
          </fieldset>
          <QueryState
            loading={mealScheduleEntries.isLoading}
            error={mealScheduleEntries.error}
          />
          {createMeal.error ? (
            <p role="alert" className="error-message">
              {createMeal.error.message}
            </p>
          ) : null}
          <div className="toolbar">
            <button
              type="submit"
              className="primary-action"
              disabled={createMeal.isPending}
            >
              {createMeal.isPending
                ? "Mahlzeit wird gespeichert …"
                : "Mahlzeit speichern"}
            </button>
            <button
              type="button"
              className="secondary-action"
              disabled={createMeal.isPending}
              onClick={() => setShowMealForm(false)}
            >
              Abbrechen
            </button>
          </div>
        </form>
      ) : null}
      {!offline && showIngredientLibrary ? (
        <IngredientLibraryPanel
          organizationId={organizationId}
          onClose={() => setShowIngredientLibrary(false)}
        />
      ) : null}
      {!offline && selectedRecipeId ? (
        <RecipeDetailPanel
          organizationId={organizationId}
          recipeId={selectedRecipeId}
          canManage={canManageLibrary}
          readOnly={readOnly}
          onClose={() => setSelectedRecipeId(null)}
        />
      ) : null}
      {!offline && selectedMealId ? (
        <MealDetailPanel
          organizationId={organizationId}
          campId={campId}
          mealId={selectedMealId}
          readOnly={readOnly}
          onClose={() => setSelectedMealId(null)}
          onDeleted={(name) => {
            setSelectedMealId(null);
            setMealNotice(`${name} wurde in den Papierkorb verschoben.`);
          }}
        />
      ) : null}
      {recipeNotice ? (
        <p className="form-feedback" role="status">
          {recipeNotice}
        </p>
      ) : null}
      {!offline && showRecipeForm ? (
        <form
          className="schedule-create-form recipe-form"
          aria-labelledby="new-recipe-heading"
          onSubmit={(event) => {
            event.preventDefault();
            setRecipeNotice("");
            createRecipe.mutate();
          }}
        >
          <h2 id="new-recipe-heading">Neues Rezept</h2>
          <div className="camp-form-grid">
            <label>
              Rezeptname
              <input
                required
                value={recipeName}
                onChange={(event) => setRecipeName(event.target.value)}
              />
            </label>
            <label>
              Basisportionen
              <input
                required
                type="number"
                min="1"
                step="1"
                value={recipeBasePortions}
                onChange={(event) => setRecipeBasePortions(event.target.value)}
              />
            </label>
            <label className="full-row">
              Beschreibung
              <textarea
                required
                value={recipeDescription}
                onChange={(event) => setRecipeDescription(event.target.value)}
              />
            </label>
            <label className="full-row">
              Zubereitung
              <textarea
                required
                value={recipePreparation}
                onChange={(event) => setRecipePreparation(event.target.value)}
              />
            </label>
          </div>
          <fieldset>
            <legend>Zutatenpositionen</legend>
            <label>
              Zutat suchen
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
            {ingredientSearch.trim().length >= 2 &&
            ingredientSuggestions.data?.length === 0 ? (
              <p className="empty-state">Keine passende Zutat gefunden.</p>
            ) : null}
            {ingredientSuggestions.data?.length ? (
              <ul className="autocomplete-results">
                {ingredientSuggestions.data
                  .filter(
                    (ingredient) =>
                      !recipeIngredients.some(
                        (row) => row.ingredient.id === ingredient.id,
                      ),
                  )
                  .map((ingredient) => (
                    <li key={ingredient.id}>
                      <button
                        type="button"
                        className="secondary-action"
                        aria-label={`${ingredient.name} hinzufügen`}
                        onClick={() => {
                          setRecipeIngredients((current) => [
                            ...current,
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
            {recipeIngredients.length === 0 ? (
              <p className="form-hint">
                Füge mindestens eine Zutat aus der Organisationsbibliothek
                hinzu.
              </p>
            ) : (
              <div className="recipe-ingredient-list">
                {recipeIngredients.map((row) => (
                  <section
                    className="recipe-ingredient-row"
                    aria-label={row.ingredient.name}
                    key={row.ingredient.id}
                  >
                    <h3>{row.ingredient.name}</h3>
                    <label>
                      Menge für {row.ingredient.name}
                      <input
                        required
                        type="number"
                        min="0.001"
                        step="0.001"
                        value={row.quantity}
                        onChange={(event) =>
                          updateRecipeIngredient(row.ingredient.id, {
                            quantity: event.target.value,
                          })
                        }
                      />
                    </label>
                    <label>
                      Einheit für {row.ingredient.name}
                      <select
                        value={row.unit}
                        onChange={(event) =>
                          updateRecipeIngredient(row.ingredient.id, {
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
                        Name der Zähleinheit für {row.ingredient.name}
                        <input
                          required
                          value={row.countUnitName}
                          onChange={(event) =>
                            updateRecipeIngredient(row.ingredient.id, {
                              countUnitName: event.target.value,
                            })
                          }
                        />
                      </label>
                    ) : null}
                    <label>
                      Hinweis für {row.ingredient.name}
                      <input
                        value={row.note}
                        onChange={(event) =>
                          updateRecipeIngredient(row.ingredient.id, {
                            note: event.target.value,
                          })
                        }
                      />
                    </label>
                    <button
                      type="button"
                      className="text-action"
                      onClick={() =>
                        setRecipeIngredients((current) =>
                          current.filter(
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
            )}
          </fieldset>
          <div className="camp-form-grid">
            <label className="full-row">
              Ernährungs-Tags
              <input
                value={recipeDietaryTags}
                placeholder="z. B. vegetarisch, glutenfrei"
                onChange={(event) => setRecipeDietaryTags(event.target.value)}
              />
            </label>
            <label className="full-row">
              Allergenhinweise
              <textarea
                value={recipeAllergenNotes}
                onChange={(event) => setRecipeAllergenNotes(event.target.value)}
              />
            </label>
            <label className="full-row">
              Küchenhinweise
              <textarea
                value={recipeKitchenNotes}
                onChange={(event) => setRecipeKitchenNotes(event.target.value)}
              />
            </label>
          </div>
          {createRecipe.error ? (
            <p role="alert" className="error-message">
              {createRecipe.error.message}
            </p>
          ) : null}
          <div className="toolbar">
            <button
              className="primary-action"
              type="submit"
              disabled={
                createRecipe.isPending || recipeIngredients.length === 0
              }
            >
              {createRecipe.isPending
                ? "Rezept wird gespeichert …"
                : "Rezept speichern"}
            </button>
            <button
              className="secondary-action"
              type="button"
              disabled={createRecipe.isPending}
              onClick={() => setShowRecipeForm(false)}
            >
              Abbrechen
            </button>
          </div>
        </form>
      ) : null}
      {!offline ? (
        <section id="rezepte" aria-labelledby="recipe-library-heading">
          <div className="section-heading">
            <h2 id="recipe-library-heading">Rezeptbibliothek</h2>
          </div>
          <QueryState loading={recipes.isLoading} error={recipes.error} />
          <div className="card-grid">
            {filteredRecipes.map((recipe) => (
              <article className="card" key={recipe.id}>
                <p className="eyebrow">
                  Version {recipe.currentVersionNumber} · {recipe.basePortions}{" "}
                  Basisportionen
                </p>
                <h3>{recipe.name}</h3>
                <button
                  className="secondary-action"
                  type="button"
                  disabled={readOnly}
                  aria-label={`${recipe.name} öffnen`}
                  aria-expanded={selectedRecipeId === recipe.id}
                  onClick={() => {
                    setSelectedRecipeId(recipe.id);
                    setSelectedMealId(null);
                    setShowMealForm(false);
                    setShowRecipeForm(false);
                    setShowIngredientLibrary(false);
                    setRecipeNotice("");
                  }}
                >
                  Rezept öffnen
                </button>
              </article>
            ))}
            {!recipes.isLoading && filteredRecipes.length === 0 ? (
              <p className="empty-state">
                Noch kein passendes Rezept vorhanden.
              </p>
            ) : null}
          </div>
        </section>
      ) : null}
      <section
        id="essensplan"
        aria-labelledby="meal-list-heading"
        data-print-section="meals"
      >
        <div className="section-heading">
          <h2 id="meal-list-heading">Geplante Mahlzeiten</h2>
        </div>
        <div className="card-grid">
          {meals.map((meal) => (
            <article className="card" key={meal.id}>
              <p className="eyebrow">{meal.effectivePortions} Portionen</p>
              <h2>{meal.name}</h2>
              <p>
                {meal.recipeCount} Rezept-Snapshots · Änderungen an
                Bibliotheksrezepten werden nicht still übernommen.
              </p>
              <button
                type="button"
                className="secondary-action"
                aria-label={`${meal.name} öffnen`}
                aria-expanded={selectedMealId === meal.id}
                disabled={readOnly}
                onClick={() => {
                  setSelectedMealId(meal.id);
                  setSelectedRecipeId(null);
                  setShowMealForm(false);
                  setShowRecipeForm(false);
                  setShowIngredientLibrary(false);
                  setMealNotice("");
                }}
              >
                Mahlzeit öffnen
              </button>
            </article>
          ))}
          {meals.length === 0 && (
            <p className="empty-state">Noch keine Mahlzeit geplant.</p>
          )}
        </div>
      </section>
    </>
  );
}
