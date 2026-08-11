import {
  useMutation,
  useQueries,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { useEffect, useState } from "react";
import type { OfflineSnapshot } from "../../offlineSnapshot";
import { saveOfflineSnapshot } from "../../offlineSnapshot";
import type {
  CampRuntime,
  ShoppingItem,
  ShoppingItemContentDraft,
  ShoppingList,
  ShoppingListChange,
  ShoppingListSummary,
} from "../types";
import { getJson, mutateCateringJson } from "../api";

export type ShoppingWorkspaceOptions = {
  runtime: CampRuntime;
  offline: boolean;
  storedSnapshot: OfflineSnapshot | null;
  basePath: string;
  setNotice: (notice: string) => void;
};

export function useShoppingWorkspace({
  runtime,
  offline,
  storedSnapshot,
  basePath,
  setNotice,
}: ShoppingWorkspaceOptions) {
  const { organizationId, campId } = runtime;
  const queryClient = useQueryClient();
  const [selectedListId, setSelectedListId] = useState<string | null>(null);
  const [listName, setListName] = useState("");
  const [itemName, setItemName] = useState("");
  const [itemQuantity, setItemQuantity] = useState("1");
  const [itemUnit, setItemUnit] = useState("4");
  const [itemCustomUnit, setItemCustomUnit] = useState("");
  const [itemStore, setItemStore] = useState("");
  const [itemNote, setItemNote] = useState("");
  const [editingItemId, setEditingItemId] = useState<string | null>(null);
  const [deletingItemId, setDeletingItemId] = useState<string | null>(null);
  const [deleteItemConfirmed, setDeleteItemConfirmed] = useState(false);
  const [renamingList, setRenamingList] = useState(false);
  const [renameListName, setRenameListName] = useState("");
  const [deletingList, setDeletingList] = useState(false);
  const [deleteListConfirmed, setDeleteListConfirmed] = useState(false);
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

  return {
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
    applyChange,
    createList,
    addItem,
    checkItem,
    updateItem,
    deleteItem,
    renameList,
    deleteList,
  };
}
