import type { LucideIcon } from "lucide-react";
import type { components } from "../api/schema";

export type AccountMembership = components["schemas"]["AccountMembershipView"];
export type Account = components["schemas"]["AccountView"];
export type WorkspaceCamp = components["schemas"]["CampView"];

export type CampRuntime = {
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  organizationRole: AccountMembership["role"];
  campId: string;
  campSlug: string;
  campBase: string;
  camp: WorkspaceCamp;
};

export type NavigationItem = {
  to: string;
  label: string;
  icon: LucideIcon;
  end?: boolean;
  anchor?: boolean;
};

export type NavigationGroup = {
  label: string;
  items: NavigationItem[];
};

export type ScheduleEntry = {
  id: string;
  title: string;
  description?: string;
  location?: string;
  category: string;
  status: number;
  responsibleUserIds: string[];
  audience?: string;
  overlapsAnotherEntry: boolean;
  timing: {
    isAllDay: boolean;
    startsAtUtc?: string;
    endsAtUtc?: string;
    startDate?: string;
    endDateExclusive?: string;
  };
  version: number;
};

export type ScheduleTimingBody = {
  isAllDay: boolean;
  localStart: string | null;
  localEnd: string | null;
  startDate: string | null;
  endDateExclusive: string | null;
  startChoice: number;
  endChoice: number;
};

export type ScheduleEntryBody = {
  timing: ScheduleTimingBody;
  title: string;
  description: string | null;
  location: string | null;
  category: string;
  status: number;
  responsibleUserIds: string[];
  audience: string | null;
};

export type ScheduleEditDraft = {
  isAllDay: boolean;
  startDate: string;
  endDate: string;
  startTime: string;
  endTime: string;
  title: string;
  description: string;
  location: string;
  category: string;
  status: string;
  audience: string;
  responsibleUserIds: string[];
};

export type CampMemberSummary = { userId: string; displayName: string };

export type Meal = {
  id: string;
  name: string;
  effectivePortions: number;
  scheduleEntryId: string | null;
  recipeCount: number;
  version: number;
};
export type MealRecipeSnapshot = {
  id: string;
  sourceRecipeId: string;
  sourceRecipeVersionNumber: number;
  latestRecipeVersionNumber: number;
  refreshAvailable: boolean;
  name: string;
  description: string;
  preparation: string;
  basePortions: number;
  ingredients: {
    id: string;
    ingredientId: string;
    ingredientName: string;
    baseQuantity: RecipeQuantity;
    scaledQuantity: RecipeQuantity;
    note: string | null;
  }[];
  dietaryTags: string[];
  allergenNotes: string | null;
  kitchenNotes: string | null;
  capturedAt: string;
};
export type MealDetail = {
  id: string;
  organizationId: string;
  campId: string;
  name: string;
  campDefaultPortions: number;
  portionOverride: number | null;
  effectivePortions: number;
  scheduleEntryId: string | null;
  recipeSnapshots: MealRecipeSnapshot[];
  version: number;
};
export type Ingredient = {
  id: string;
  organizationId: string;
  name: string;
  isMerged: boolean;
  mergedIntoIngredientId: string | null;
  version: number;
};
export type RecipeSummary = {
  id: string;
  organizationId: string;
  name: string;
  basePortions: number;
  currentVersionNumber: number;
  version: number;
};
export type RecipeCreateResult = {
  id: string;
  currentVersion: { number: number; name: string };
  version: number;
};
export type RecipeIngredientDraft = {
  ingredient: { id: string; name: string };
  quantity: string;
  unit: string;
  countUnitName: string;
  note: string;
};
export type RecipeQuantity = {
  value: number;
  unit: number;
  countUnitName: string | null;
};
export type RecipeIngredient = {
  id: string;
  ingredientId: string;
  ingredientName: string;
  quantity: RecipeQuantity;
  note: string | null;
};
export type RecipeDetail = {
  id: string;
  organizationId: string;
  currentVersion: {
    id: string;
    number: number;
    name: string;
    description: string;
    preparation: string;
    basePortions: number;
    ingredients: RecipeIngredient[];
    dietaryTags: string[];
    allergenNotes: string | null;
    kitchenNotes: string | null;
    createdAt: string;
  };
  version: number;
};
export type RecipeAttachment = {
  id: string;
  originalFileName: string;
  mediaType: number;
  contentType: string;
  sizeBytes: number;
  version: number;
};
export type RecipeAttachmentQuota = {
  limitBytes: number;
  usedBytes: number;
  pendingBytes: number;
  availableBytes: number;
};
export type AttachmentReadGrant = {
  token: string;
  attachmentId: string;
  expiresAt: string;
  disposition: number;
};
export type IngredientMergePreview = {
  source: Ingredient;
  target: Ingredient;
  affectedRecipes: RecipeSummary[];
};
export type IngredientMergeResult = {
  target: Ingredient;
  revisedRecipeIds: string[];
};
export type NoteSummary = {
  id: string;
  title: string;
  plainTextExcerpt: string;
  tags: string[];
  isPinned: boolean;
  linkCount: number;
  state: number;
  updatedAt: string;
  trashedAt: string | null;
  purgeAfter: string | null;
  version: number;
};
export type NoteLink = {
  type: number;
  targetId: string;
  targetTitle: string;
};
export type NoteLinkReference = Pick<NoteLink, "type" | "targetId">;
export type NoteLinkCandidate = NoteLinkReference & { targetTitle: string };
export type NotebookNote = {
  id: string;
  organizationId: string;
  campId: string;
  title: string;
  markdown: string;
  renderedHtml: string;
  tags: string[];
  isPinned: boolean;
  links: NoteLink[];
  state: number;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
  trashedAt: string | null;
  trashedBy: string | null;
  purgeAfter: string | null;
  version: number;
};

export type Devotion = {
  id: string;
  organizationId?: string;
  campId?: string;
  topic: string;
  bibleReference: string;
  translation: number;
  responsibleUserIds: string[];
  scheduleEntryId: string | null;
  hasBibleSnapshot: boolean;
  version: number;
};
export type BibleSnapshot = {
  reference: string;
  textExcerpt: string;
  technicalTranslationId: string;
  translationDisplayName: string;
  license: string;
  attribution: string;
  retrievedAt: string;
  origin: number;
};
export type DevotionDetail = Omit<Devotion, "hasBibleSnapshot"> & {
  organizationId: string;
  campId: string;
  coreMessage: string;
  markdownContent: string;
  materialNotes: string;
  bibleSnapshot: BibleSnapshot | null;
  createdAt: string;
  updatedAt: string;
  deletedAt: string | null;
};
export type BibleSnapshotRefreshResult = {
  status: number;
  devotion: DevotionDetail;
};
export type BibleTranslationView = {
  translation: number;
  technicalId: string;
  displayName: string;
  license: string;
  attribution: string;
  isDefault: boolean;
};
export type ActivityEvent = {
  id: string;
  actorId: string;
  actorDisplayName: string;
  kind: 0 | 1 | 2 | 3;
  objectType: string;
  title: string;
  timestamp: string;
};
export type MaterialRequirementSummary = {
  id: string;
  name: string;
  quantity: LogisticsQuantity;
  status: number;
  scheduleEntryId: string | null;
  version: number;
};
export type MaterialRequirement = MaterialRequirementSummary & {
  organizationId: string;
  campId: string;
  description: string | null;
  responsibleUserIds: string[];
  procurementSource: string | null;
  note: string | null;
};
export type MaterialRequirementContent = {
  name: string;
  description: string | null;
  quantity: LogisticsQuantity;
  responsibleUserIds: string[];
  procurementSource: string | null;
  note: string | null;
  status: number;
  scheduleEntryId: string | null;
};
export type ShoppingListSummary = {
  id: string;
  name: string;
  openItemCount: number;
  checkedItemCount: number;
  version: number;
  changeSequence: number;
};
export type LogisticsQuantity = {
  value: number;
  unit: number;
  customUnitName: string | null;
};
export type ShoppingItem = {
  id: string;
  shoppingListId: string;
  name: string;
  quantity: LogisticsQuantity;
  responsibleUserIds: string[];
  store: string | null;
  note: string | null;
  source: { kind: number; label: string };
  isChecked: boolean;
  checkedByUserId: string | null;
  checkedAt: string | null;
  version: number;
};
export type ShoppingList = {
  id: string;
  organizationId: string;
  campId: string;
  name: string;
  items: ShoppingItem[];
  version: number;
  changeSequence: number;
};
export type ShoppingListChange = {
  shoppingListId: string;
  listVersion: number;
  changeSequence: number;
  item: ShoppingItem | null;
};
export type ShoppingTransferResult = {
  shoppingListId: string;
  listVersion: number;
  changeSequence: number;
  items: ShoppingItem[];
};
export type MealShoppingLine = {
  recipeSnapshotId: string;
  snapshotIngredientId: string;
  sourceRecipeId: string;
  sourceRecipeVersionNumber: number;
  sourceLabel: string;
  ingredientName: string;
  suggestedQuantity: RecipeQuantity;
  dimension: number;
  compatibleUnits: number[];
};
export type MealShoppingDraft = {
  mealId: string;
  mealName: string;
  effectivePortions: number;
  mealVersion: number;
  lines: MealShoppingLine[];
};
export type ShoppingTransferLineDraft = MealShoppingLine & {
  included: boolean;
  quantity: string;
  unit: number;
};
export type SearchResult = {
  objectType: string;
  objectId: string;
  title: string;
  metadata: Record<string, string>;
  updatedAt: string;
  version: number;
};
export type CampTrashItem = {
  objectType:
    | "Note"
    | "Devotion"
    | "Attachment"
    | "MaterialRequirement"
    | "ShoppingList"
    | "ShoppingItem"
    | "ScheduleEntry"
    | "Meal";
  objectId: string;
  title: string;
  deletedAt: string;
  purgeAt: string;
  version: number;
  restorePath: string;
};

export type PrintScope = "schedule" | "meals" | "material" | "shopping";

export type ShoppingItemContentDraft = {
  name: string;
  quantity: LogisticsQuantity;
  responsibleUserIds: string[];
  store: string | null;
  note: string | null;
};
