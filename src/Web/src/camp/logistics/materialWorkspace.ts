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
  MaterialRequirement,
  MaterialRequirementContent,
  MaterialRequirementSummary,
  ShoppingListSummary,
  ShoppingTransferResult,
} from "../types";
import { getJson, mutateCateringJson } from "../api";

export type MaterialWorkspaceOptions = {
  runtime: CampRuntime;
  offline: boolean;
  storedSnapshot: OfflineSnapshot | null;
  basePath: string;
  setNotice: (notice: string) => void;
  shoppingListSummaries: ShoppingListSummary[] | undefined;
  updateListSummary: (
    listId: string,
    update: (summary: ShoppingListSummary) => ShoppingListSummary,
  ) => unknown;
};

export function useMaterialWorkspace({
  runtime,
  offline,
  storedSnapshot,
  basePath,
  setNotice,
  shoppingListSummaries,
  updateListSummary,
}: MaterialWorkspaceOptions) {
  const { organizationId, campId } = runtime;
  const queryClient = useQueryClient();
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
      const list = shoppingListSummaries?.find(
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
        shoppingListSummaries?.find(
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

  return {
    selectedMaterialId,
    setSelectedMaterialId,
    creatingMaterial,
    setCreatingMaterial,
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
    material,
    selectedMaterial,
    createMaterial,
    updateMaterial,
    deleteMaterial,
    transferMaterial,
  };
}
