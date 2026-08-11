import {
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { loadOfflineSnapshot, saveOfflineSnapshot } from "../offlineSnapshot";
import type {
  CampMemberSummary,
  MaterialRequirement,
  MaterialRequirementContent,
  MaterialRequirementSummary,
  ScheduleEntry,
  ShoppingItem,
  ShoppingItemContentDraft,
  ShoppingList,
  ShoppingListChange,
  ShoppingListSummary,
  ShoppingTransferResult,
} from "./types";
import { getJson, mutateCateringJson } from "./api";
import { useCampRuntime } from "./runtime";
import { nextLocalDate } from "./schedule";
import {
  formatGermanDateTime,
  PageHeading,
  PrintButton,
  QueryState,
  ResponsibilityFields,
} from "./ui";
import { OwnerAttachmentsPanel } from "./AttachmentsPanel";
import {
  formatLogisticsQuantity,
  MaterialRequirementForm,
  materialStatusLabels,
  ShoppingItemEditForm,
  shoppingUnitLabels,
} from "./LogisticsForms";

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
  const queryClient = useQueryClient();
  const basePath = `/api/v1/organizations/${organizationId}/camps/${campId}/logistics`;
  const [selectedMaterialId, setSelectedMaterialId] = useState<string | null>(
    null,
  );
  const [creatingMaterial, setCreatingMaterial] = useState(false);
  const [editingMaterial, setEditingMaterial] = useState(false);
  const [deletingMaterial, setDeletingMaterial] = useState(false);
  const [deleteMaterialConfirmed, setDeleteMaterialConfirmed] = useState(false);
  const [transferringMaterial, setTransferringMaterial] = useState(false);
  const [materialTargetListId, setMaterialTargetListId] = useState("");
  const [materialTransferName, setMaterialTransferName] = useState("");
  const [materialTransferQuantity, setMaterialTransferQuantity] = useState("1");
  const [materialTransferUnit, setMaterialTransferUnit] = useState("4");
  const [materialTransferCustomUnit, setMaterialTransferCustomUnit] =
    useState("");
  const [materialTransferStore, setMaterialTransferStore] = useState("");
  const [materialTransferNote, setMaterialTransferNote] = useState("");
  const [
    materialTransferResponsibleUserIds,
    setMaterialTransferResponsibleUserIds,
  ] = useState<string[]>([]);
  const [selectedListId, setSelectedListId] = useState<string | null>(null);
  const [listName, setListName] = useState("");
  const [itemName, setItemName] = useState("");
  const [itemQuantity, setItemQuantity] = useState("1");
  const [itemUnit, setItemUnit] = useState("4");
  const [itemCustomUnit, setItemCustomUnit] = useState("");
  const [itemStore, setItemStore] = useState("");
  const [itemNote, setItemNote] = useState("");
  const [notice, setNotice] = useState("");
  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  const [deletingItemId, setDeletingItemId] = useState<string | null>(null);
  const [deleteItemConfirmed, setDeleteItemConfirmed] = useState(false);
  const [renamingList, setRenamingList] = useState(false);
  const [renameListName, setRenameListName] = useState("");
  const [deletingList, setDeletingList] = useState(false);
  const [deleteListConfirmed, setDeleteListConfirmed] = useState(false);
  const material = useQuery({
    queryKey: [organizationId, campId, "material"],
    queryFn: () =>
      getJson<MaterialRequirementSummary[]>(`${basePath}/material`),
    retry: false,
    enabled: !offline,
    initialData: offline
      ? (storedSnapshot?.material?.summaries as
          MaterialRequirementSummary[] | undefined)
      : undefined,
  });
  const selectedMaterial = useQuery({
    queryKey: [organizationId, campId, "material", selectedMaterialId],
    queryFn: () =>
      getJson<MaterialRequirement>(
        `${basePath}/material/${selectedMaterialId}`,
      ),
    enabled: selectedMaterialId !== null && !offline,
    retry: false,
    initialData: () =>
      offline
        ? (
            storedSnapshot?.material?.requirements as
              MaterialRequirement[] | undefined
          )?.find((item) => item.id === selectedMaterialId)
        : undefined,
  });
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
  const shoppingLists = useQuery({
    queryKey: [organizationId, campId, "shopping-lists"],
    queryFn: () => getJson<ShoppingListSummary[]>(`${basePath}/shopping-lists`),
    retry: false,
    enabled: !offline,
    initialData: offline
      ? (storedSnapshot?.shopping?.summaries as
          ShoppingListSummary[] | undefined)
      : undefined,
    refetchInterval: offline ? false : 15_000,
    refetchOnWindowFocus: !offline,
  });
  const selectedList = useQuery({
    queryKey: [organizationId, campId, "shopping-list", selectedListId],
    queryFn: () =>
      getJson<ShoppingList>(`${basePath}/shopping-lists/${selectedListId}`),
    enabled: selectedListId !== null && !offline,
    retry: false,
    refetchInterval: offline ? false : 15_000,
    refetchOnWindowFocus: !offline,
    initialData: () =>
      offline
        ? (storedSnapshot?.shopping?.lists as ShoppingList[] | undefined)?.find(
            (item) => item.id === selectedListId,
          )
        : undefined,
  });
  const materialSnapshotQueries = useQueries({
    queries: offline
      ? []
      : (material.data ?? []).map((item) => ({
          queryKey: [organizationId, campId, "material", item.id],
          queryFn: () =>
            getJson<MaterialRequirement>(`${basePath}/material/${item.id}`),
          retry: false,
        })),
  });
  const shoppingSnapshotQueries = useQueries({
    queries: offline
      ? []
      : (shoppingLists.data ?? []).map((item) => ({
          queryKey: [organizationId, campId, "shopping-list", item.id],
          queryFn: () =>
            getJson<ShoppingList>(`${basePath}/shopping-lists/${item.id}`),
          retry: false,
        })),
  });
  useEffect(() => {
    if (
      !offline &&
      material.data &&
      materialSnapshotQueries.every((query) => query.data)
    )
      saveOfflineSnapshot(runtime, {
        material: {
          summaries: material.data,
          requirements: materialSnapshotQueries.flatMap((query) =>
            query.data ? [query.data] : [],
          ),
        },
      });
  }, [offline, material.data, materialSnapshotQueries]);
  useEffect(() => {
    if (
      !offline &&
      shoppingLists.data &&
      shoppingSnapshotQueries.every((query) => query.data)
    )
      saveOfflineSnapshot(runtime, {
        shopping: {
          summaries: shoppingLists.data,
          lists: shoppingSnapshotQueries.flatMap((query) =>
            query.data ? [query.data] : [],
          ),
        },
      });
  }, [offline, shoppingLists.data, shoppingSnapshotQueries]);
  const updateListSummary = (
    listId: string,
    update: (summary: ShoppingListSummary) => ShoppingListSummary,
  ) =>
    queryClient.setQueryData<ShoppingListSummary[]>(
      [organizationId, campId, "shopping-lists"],
      (current) =>
        current?.map((summary) =>
          summary.id === listId ? update(summary) : summary,
        ),
    );
  const applyChange = (change: ShoppingListChange) => {
    queryClient.setQueryData<ShoppingList>(
      [organizationId, campId, "shopping-list", change.shoppingListId],
      (current) => {
        if (!current || !change.item) return current;
        const exists = current.items.some(
          (item) => item.id === change.item?.id,
        );
        return {
          ...current,
          version: change.listVersion,
          changeSequence: change.changeSequence,
          items: exists
            ? current.items.map((item) =>
                item.id === change.item?.id ? change.item : item,
              )
            : [...current.items, change.item],
        };
      },
    );
  };
  const createList = useMutation({
    mutationFn: () =>
      mutateCateringJson<ShoppingList>(`${basePath}/shopping-lists`, "POST", {
        name: listName,
      }),
    onSuccess: (created) => {
      queryClient.setQueryData<ShoppingListSummary[]>(
        [organizationId, campId, "shopping-lists"],
        (current) => [
          ...(current ?? []),
          {
            id: created.id,
            name: created.name,
            openItemCount: 0,
            checkedItemCount: 0,
            version: created.version,
            changeSequence: created.changeSequence,
          },
        ],
      );
      queryClient.setQueryData(
        [organizationId, campId, "shopping-list", created.id],
        created,
      );
      setSelectedListId(created.id);
      setListName("");
      setNotice(`${created.name} wurde angelegt.`);
    },
  });
  const addItem = useMutation({
    mutationFn: () => {
      const current = selectedList.data;
      if (!current) throw new Error("Öffne zuerst eine Einkaufsliste.");
      return mutateCateringJson<ShoppingListChange>(
        `${basePath}/shopping-lists/${current.id}/items`,
        "POST",
        {
          name: itemName,
          quantity: {
            value: Number(itemQuantity),
            unit: Number(itemUnit),
            customUnitName: itemUnit === "5" ? itemCustomUnit : null,
          },
          responsibleUserIds: [],
          store: itemStore || null,
          note: itemNote || null,
        },
        current.version,
        "Die Einkaufsliste wurde zwischenzeitlich geändert. Prüfe die aktuelle Liste und versuche es erneut.",
      );
    },
    onSuccess: (change) => {
      applyChange(change);
      updateListSummary(change.shoppingListId, (summary) => ({
        ...summary,
        openItemCount: summary.openItemCount + 1,
        version: change.listVersion,
        changeSequence: change.changeSequence,
      }));
      setNotice(`${change.item?.name ?? itemName} wurde hinzugefügt.`);
      setItemName("");
      setItemQuantity("1");
      setItemUnit("4");
      setItemCustomUnit("");
      setItemStore("");
      setItemNote("");
    },
  });
  const checkItem = useMutation({
    mutationFn: ({
      item,
      isChecked,
    }: {
      item: ShoppingItem;
      isChecked: boolean;
    }) => {
      if (!selectedListId) throw new Error("Öffne zuerst eine Einkaufsliste.");
      return mutateCateringJson<ShoppingListChange>(
        `${basePath}/shopping-lists/${selectedListId}/items/${item.id}/checked`,
        "PATCH",
        { isChecked },
        item.version,
        "Die Position wurde zwischenzeitlich geändert. Die aktuelle Liste wird erneut geladen.",
      );
    },
    onSuccess: (change, variables) => {
      applyChange(change);
      updateListSummary(change.shoppingListId, (summary) => ({
        ...summary,
        openItemCount: Math.max(
          0,
          summary.openItemCount + (variables.isChecked ? -1 : 1),
        ),
        checkedItemCount: Math.max(
          0,
          summary.checkedItemCount + (variables.isChecked ? 1 : -1),
        ),
        version: change.listVersion,
        changeSequence: change.changeSequence,
      }));
      setNotice(
        `${change.item?.name ?? variables.item.name} wurde ${variables.isChecked ? "abgehakt" : "wieder geöffnet"}.`,
      );
    },
    onError: async () => {
      await selectedList.refetch();
    },
  });
  const updateItem = useMutation({
    mutationFn: ({
      item,
      content,
    }: {
      item: ShoppingItem;
      content: ShoppingItemContentDraft;
    }) => {
      if (!selectedListId) throw new Error("Öffne zuerst eine Einkaufsliste.");
      return mutateCateringJson<ShoppingListChange>(
        `${basePath}/shopping-lists/${selectedListId}/items/${item.id}`,
        "PUT",
        content,
        item.version,
        "Die Position wurde zwischenzeitlich geändert. Öffne die aktuelle Position erneut.",
      );
    },
    onSuccess: (change) => {
      applyChange(change);
      updateListSummary(change.shoppingListId, (summary) => ({
        ...summary,
        changeSequence: change.changeSequence,
      }));
      setEditingItemId(null);
      setNotice(`${change.item?.name ?? "Die Position"} wurde gespeichert.`);
    },
  });
  const deleteItem = useMutation({
    mutationFn: (item: ShoppingItem) => {
      if (!selectedListId) throw new Error("Öffne zuerst eine Einkaufsliste.");
      return mutateCateringJson<ShoppingListChange>(
        `${basePath}/shopping-lists/${selectedListId}/items/${item.id}`,
        "DELETE",
        {},
        item.version,
        "Die Position wurde zwischenzeitlich geändert. Öffne die aktuelle Position erneut.",
      );
    },
    onSuccess: (change, item) => {
      queryClient.setQueryData<ShoppingList>(
        [organizationId, campId, "shopping-list", change.shoppingListId],
        (current) =>
          current
            ? {
                ...current,
                version: change.listVersion,
                changeSequence: change.changeSequence,
                items: current.items.filter(
                  (candidate) => candidate.id !== item.id,
                ),
              }
            : current,
      );
      updateListSummary(change.shoppingListId, (summary) => ({
        ...summary,
        openItemCount: Math.max(
          0,
          summary.openItemCount - (item.isChecked ? 0 : 1),
        ),
        checkedItemCount: Math.max(
          0,
          summary.checkedItemCount - (item.isChecked ? 1 : 0),
        ),
        version: change.listVersion,
        changeSequence: change.changeSequence,
      }));
      setDeletingItemId(null);
      setDeleteItemConfirmed(false);
      setNotice(`${item.name} wurde in den Papierkorb verschoben.`);
    },
  });
  const renameList = useMutation({
    mutationFn: () => {
      const current = selectedList.data;
      if (!current) throw new Error("Öffne zuerst eine Einkaufsliste.");
      return mutateCateringJson<ShoppingList>(
        `${basePath}/shopping-lists/${current.id}`,
        "PUT",
        { name: renameListName },
        current.version,
        "Die Einkaufsliste wurde zwischenzeitlich geändert. Öffne den aktuellen Stand erneut.",
      );
    },
    onSuccess: (updated) => {
      queryClient.setQueryData(
        [organizationId, campId, "shopping-list", updated.id],
        updated,
      );
      updateListSummary(updated.id, (summary) => ({
        ...summary,
        name: updated.name,
        version: updated.version,
        changeSequence: updated.changeSequence,
      }));
      setRenamingList(false);
      setNotice(`${updated.name} wurde umbenannt.`);
    },
  });
  const deleteList = useMutation({
    mutationFn: async () => {
      const current = selectedList.data;
      if (!current) throw new Error("Öffne zuerst eine Einkaufsliste.");
      await mutateCateringJson<void>(
        `${basePath}/shopping-lists/${current.id}`,
        "DELETE",
        {},
        current.version,
        "Die Einkaufsliste wurde zwischenzeitlich geändert. Öffne den aktuellen Stand erneut.",
      );
      return { id: current.id, name: current.name };
    },
    onSuccess: ({ id, name }) => {
      queryClient.setQueryData<ShoppingListSummary[]>(
        [organizationId, campId, "shopping-lists"],
        (current) => current?.filter((summary) => summary.id !== id),
      );
      queryClient.removeQueries({
        queryKey: [organizationId, campId, "shopping-list", id],
      });
      setSelectedListId(null);
      setDeletingList(false);
      setDeleteListConfirmed(false);
      setNotice(`${name} wurde in den Papierkorb verschoben.`);
    },
  });
  const createMaterial = useMutation({
    mutationFn: (content: MaterialRequirementContent) =>
      mutateCateringJson<MaterialRequirement>(
        `${basePath}/material`,
        "POST",
        content,
      ),
    onSuccess: (created) => {
      queryClient.setQueryData<MaterialRequirementSummary[]>(
        [organizationId, campId, "material"],
        (current) => [
          ...(current ?? []),
          {
            id: created.id,
            name: created.name,
            quantity: created.quantity,
            status: created.status,
            scheduleEntryId: created.scheduleEntryId,
            version: created.version,
          },
        ],
      );
      queryClient.setQueryData(
        [organizationId, campId, "material", created.id],
        created,
      );
      setCreatingMaterial(false);
      setSelectedMaterialId(created.id);
      setNotice(`${created.name} wurde angelegt.`);
    },
  });
  const updateMaterial = useMutation({
    mutationFn: (content: MaterialRequirementContent) => {
      const current = selectedMaterial.data;
      if (!current) throw new Error("Öffne zuerst den Materialbedarf.");
      return mutateCateringJson<MaterialRequirement>(
        `${basePath}/material/${current.id}`,
        "PUT",
        content,
        current.version,
        "Der Materialbedarf wurde zwischenzeitlich geändert. Öffne den aktuellen Stand erneut.",
      );
    },
    onSuccess: (updated) => {
      queryClient.setQueryData(
        [organizationId, campId, "material", updated.id],
        updated,
      );
      queryClient.setQueryData<MaterialRequirementSummary[]>(
        [organizationId, campId, "material"],
        (current) =>
          current?.map((summary) =>
            summary.id === updated.id
              ? {
                  id: updated.id,
                  name: updated.name,
                  quantity: updated.quantity,
                  status: updated.status,
                  scheduleEntryId: updated.scheduleEntryId,
                  version: updated.version,
                }
              : summary,
          ),
      );
      setEditingMaterial(false);
      setNotice(`${updated.name} wurde gespeichert.`);
    },
  });
  const deleteMaterial = useMutation({
    mutationFn: async () => {
      const current = selectedMaterial.data;
      if (!current) throw new Error("Öffne zuerst den Materialbedarf.");
      await mutateCateringJson<void>(
        `${basePath}/material/${current.id}`,
        "DELETE",
        {},
        current.version,
        "Der Materialbedarf wurde zwischenzeitlich geändert. Öffne den aktuellen Stand erneut.",
      );
      return { id: current.id, name: current.name };
    },
    onSuccess: ({ id, name }) => {
      queryClient.setQueryData<MaterialRequirementSummary[]>(
        [organizationId, campId, "material"],
        (current) => current?.filter((summary) => summary.id !== id),
      );
      queryClient.removeQueries({
        queryKey: [organizationId, campId, "material", id],
      });
      setSelectedMaterialId(null);
      setDeletingMaterial(false);
      setDeleteMaterialConfirmed(false);
      setNotice(`${name} wurde in den Papierkorb verschoben.`);
    },
  });
  const transferMaterial = useMutation({
    mutationFn: () => {
      const requirement = selectedMaterial.data;
      const list = shoppingLists.data?.find(
        (candidate) => candidate.id === materialTargetListId,
      );
      if (!requirement || !list)
        throw new Error("Wähle eine aktuelle Einkaufsliste aus.");
      return mutateCateringJson<ShoppingTransferResult>(
        `${basePath}/shopping-lists/${list.id}/transfer/material/${requirement.id}`,
        "POST",
        {
          expectedListVersion: list.version,
          expectedRequirementVersion: requirement.version,
          content: {
            name: materialTransferName,
            quantity: {
              value: Number(materialTransferQuantity),
              unit: Number(materialTransferUnit),
              customUnitName:
                materialTransferUnit === "5"
                  ? materialTransferCustomUnit
                  : null,
            },
            responsibleUserIds: materialTransferResponsibleUserIds,
            store: materialTransferStore || null,
            note: materialTransferNote || null,
          },
        },
        list.version,
        "Die Einkaufsliste wurde zwischenzeitlich geändert. Prüfe die aktuelle Liste und versuche es erneut.",
      );
    },
    onSuccess: (result) => {
      const targetName =
        shoppingLists.data?.find(
          (candidate) => candidate.id === result.shoppingListId,
        )?.name ?? "die Einkaufsliste";
      updateListSummary(result.shoppingListId, (summary) => ({
        ...summary,
        openItemCount: summary.openItemCount + result.items.length,
        version: result.listVersion,
        changeSequence: result.changeSequence,
      }));
      void queryClient.invalidateQueries({
        queryKey: [
          organizationId,
          campId,
          "shopping-list",
          result.shoppingListId,
        ],
      });
      setTransferringMaterial(false);
      setNotice(
        `${selectedMaterial.data?.name ?? materialTransferName} wurde in ${targetName} übernommen.`,
      );
    },
  });
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
                    {materialStatusLabels[selectedMaterial.data.status] ??
                      "Offen"}
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
                  <dd>
                    {formatLogisticsQuantity(selectedMaterial.data.quantity)}
                  </dd>
                </div>
                <div>
                  <dt>Beschaffungsquelle</dt>
                  <dd>
                    {selectedMaterial.data.procurementSource ??
                      "Nicht angegeben"}
                  </dd>
                </div>
                <div>
                  <dt>Verantwortlich</dt>
                  <dd>
                    {selectedMaterial.data.responsibleUserIds.length
                      ? selectedMaterial.data.responsibleUserIds
                          .map(
                            (userId) =>
                              memberNames.get(userId) ?? "Camp-Mitglied",
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
                    Der Materialbedarf bleibt 30 Tage im Papierkorb und kann
                    dort wiederhergestellt werden.
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
                    Menge und Einheit können vor der Übernahme angepasst werden.
                    Die Materialquelle bleibt nachvollziehbar erhalten.
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
                        {Object.entries(shoppingUnitLabels).map(
                          ([unit, label]) => (
                            <option key={unit} value={unit}>
                              {label}
                            </option>
                          ),
                        )}
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
