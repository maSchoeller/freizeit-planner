import { useQuery } from "@tanstack/react-query";
import {
  CalendarDays,
  ChefHat,
  Church,
  ClipboardList,
  FileText,
  NotebookPen,
  Search,
  Settings,
  ShoppingCart,
  Trash2,
} from "lucide-react";
import { createContext, useContext } from "react";
import type { CampRuntime, NavigationGroup, NavigationItem } from "./types";
import { getJson } from "./api";

export const CampRuntimeContext = createContext<CampRuntime | null>(null);

export function useCampRuntime() {
  const runtime = useContext(CampRuntimeContext);
  if (!runtime) throw new Error("Camp-Kontext fehlt.");
  return runtime;
}

export const navigationGroups: NavigationGroup[] = [
  {
    label: "Planung",
    items: [
      { to: "", label: "Übersicht", icon: ClipboardList, end: true },
      { to: "tagesplan", label: "Tagesplan", icon: CalendarDays },
    ],
  },
  {
    label: "Versorgung",
    items: [
      { to: "essen", label: "Verpflegung", icon: ChefHat },
      { to: "logistik", label: "Material & Einkauf", icon: ShoppingCart },
    ],
  },
  {
    label: "Inhalte",
    items: [
      { to: "andachten", label: "Andachten", icon: Church },
      { to: "notizen", label: "Notizbuch", icon: NotebookPen },
      { to: "dateien", label: "Dateien", icon: FileText },
    ],
  },
  {
    label: "Werkzeuge",
    items: [
      { to: "suche", label: "Suche", icon: Search },
      {
        to: "suche#papierkorb",
        label: "Papierkorb",
        icon: Trash2,
        anchor: true,
      },
      { to: "einstellungen", label: "Einstellungen", icon: Settings },
    ],
  },
];

export const mobilePrimaryNavigation: NavigationItem[] = [
  { to: "", label: "Übersicht", icon: ClipboardList, end: true },
  { to: "tagesplan", label: "Tagesplan", icon: CalendarDays },
  { to: "essen", label: "Verpflegung", icon: ChefHat },
  { to: "logistik#einkaufslisten", label: "Einkauf", icon: ShoppingCart },
];

export function useCampQuery<T>(key: string, path: string, enabled = true) {
  const { organizationId, campId } = useCampRuntime();
  return useQuery({
    queryKey: [organizationId, campId, key],
    queryFn: () => getJson<T>(path),
    retry: false,
    enabled,
  });
}
