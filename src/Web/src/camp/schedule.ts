import type {
  ScheduleEditDraft,
  ScheduleEntry,
  ScheduleEntryBody,
} from "./types";

export class ScheduleUpdateError extends Error {
  constructor(message: string) {
    super(message);
  }
}

export function formatCampLocalDateTime(
  value: string | undefined,
  timeZone: string,
) {
  if (!value) return { date: "", time: "" };
  const parts = new Intl.DateTimeFormat("sv-SE", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(new Date(value));
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((item) => item.type === type)?.value ?? "";
  return {
    date: `${part("year")}-${part("month")}-${part("day")}`,
    time: `${part("hour")}:${part("minute")}`,
  };
}

export function createScheduleEditDraft(
  entry: ScheduleEntry,
  timeZone: string,
): ScheduleEditDraft {
  const start = entry.timing.isAllDay
    ? { date: entry.timing.startDate ?? "", time: "" }
    : formatCampLocalDateTime(entry.timing.startsAtUtc, timeZone);
  const end = entry.timing.isAllDay
    ? { date: entry.timing.endDateExclusive ?? "", time: "" }
    : formatCampLocalDateTime(entry.timing.endsAtUtc, timeZone);
  return {
    isAllDay: entry.timing.isAllDay,
    startDate: start.date,
    endDate: end.date,
    startTime: start.time,
    endTime: end.time,
    title: entry.title,
    description: entry.description ?? "",
    location: entry.location ?? "",
    category: entry.category,
    status: String(entry.status ?? 0),
    audience: entry.audience ?? "",
    responsibleUserIds: entry.responsibleUserIds ?? [],
  };
}

export function scheduleBodyFromDraft(
  entry: ScheduleEntry,
  draft: ScheduleEditDraft,
): ScheduleEntryBody {
  return {
    timing: draft.isAllDay
      ? {
          isAllDay: true,
          localStart: null,
          localEnd: null,
          startDate: draft.startDate,
          endDateExclusive: draft.endDate,
          startChoice: 0,
          endChoice: 0,
        }
      : {
          isAllDay: false,
          localStart: `${draft.startDate}T${draft.startTime}:00`,
          localEnd: `${draft.endDate}T${draft.endTime}:00`,
          startDate: null,
          endDateExclusive: null,
          startChoice: 0,
          endChoice: 0,
        },
    title: draft.title,
    description: draft.description || null,
    location: draft.location || null,
    category: draft.category,
    status: Number(draft.status),
    responsibleUserIds: draft.responsibleUserIds,
    audience: draft.audience || null,
  };
}

export function optimisticEntryFromDraft(
  entry: ScheduleEntry,
  draft: ScheduleEditDraft,
): ScheduleEntry {
  return {
    ...entry,
    title: draft.title,
    description: draft.description || undefined,
    location: draft.location || undefined,
    category: draft.category,
    status: Number(draft.status),
    audience: draft.audience || undefined,
    responsibleUserIds: draft.responsibleUserIds,
    timing: draft.isAllDay
      ? {
          isAllDay: true,
          startDate: draft.startDate,
          endDateExclusive: draft.endDate,
        }
      : {
          isAllDay: false,
          startsAtUtc: `${draft.startDate}T${draft.startTime}:00`,
          endsAtUtc: `${draft.endDate}T${draft.endTime}:00`,
        },
  };
}

export function localCalendarDateTime(value: string) {
  return value.slice(0, 19);
}

export function nextLocalDate(value: string) {
  const date = new Date(`${value}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + 1);
  return date.toISOString().slice(0, 10);
}

export function scheduleBodyFromCalendar(
  entry: ScheduleEntry,
  event: { allDay: boolean; startStr: string; endStr: string },
): ScheduleEntryBody {
  return {
    timing: event.allDay
      ? {
          isAllDay: true,
          localStart: null,
          localEnd: null,
          startDate: event.startStr.slice(0, 10),
          endDateExclusive: event.endStr.slice(0, 10),
          startChoice: 0,
          endChoice: 0,
        }
      : {
          isAllDay: false,
          localStart: localCalendarDateTime(event.startStr),
          localEnd: localCalendarDateTime(event.endStr),
          startDate: null,
          endDateExclusive: null,
          startChoice: 0,
          endChoice: 0,
        },
    title: entry.title,
    description: entry.description ?? null,
    location: entry.location ?? null,
    category: entry.category,
    status: entry.status ?? 0,
    responsibleUserIds: entry.responsibleUserIds ?? [],
    audience: entry.audience ?? null,
  };
}

export function optimisticEntryFromCalendar(
  entry: ScheduleEntry,
  event: { allDay: boolean; startStr: string; endStr: string },
): ScheduleEntry {
  return {
    ...entry,
    timing: event.allDay
      ? {
          isAllDay: true,
          startDate: event.startStr.slice(0, 10),
          endDateExclusive: event.endStr.slice(0, 10),
        }
      : {
          isAllDay: false,
          startsAtUtc: event.startStr,
          endsAtUtc: event.endStr,
        },
  };
}

export function scheduleTimingLabel(entry: ScheduleEntry, timeZone: string) {
  if (entry.timing.isAllDay)
    return `${entry.timing.startDate ?? ""} · ganztägig`;
  const formatter = new Intl.DateTimeFormat("de-DE", {
    timeZone,
    weekday: "short",
    day: "2-digit",
    month: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
  const start = entry.timing.startsAtUtc
    ? formatter.format(new Date(entry.timing.startsAtUtc))
    : "";
  const end = entry.timing.endsAtUtc
    ? new Intl.DateTimeFormat("de-DE", {
        timeZone,
        hour: "2-digit",
        minute: "2-digit",
      }).format(new Date(entry.timing.endsAtUtc))
    : "";
  return `${start}–${end} Uhr`;
}

export function campLocalDate(timeZone: string) {
  return new Intl.DateTimeFormat("sv-SE", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
}

export function scheduleEntryDate(entry: ScheduleEntry, timeZone: string) {
  return entry.timing.isAllDay
    ? entry.timing.startDate
    : formatCampLocalDateTime(entry.timing.startsAtUtc, timeZone).date;
}

export function scheduleEntryDateTime(entry: ScheduleEntry) {
  return entry.timing.isAllDay
    ? entry.timing.startDate
    : entry.timing.startsAtUtc;
}

export function scheduleEntryTime(entry: ScheduleEntry, timeZone: string) {
  if (entry.timing.isAllDay) return "Ganztägig";
  return formatCampLocalDateTime(entry.timing.startsAtUtc, timeZone).time;
}

export function compareScheduleEntries(
  left: ScheduleEntry,
  right: ScheduleEntry,
) {
  const value = (entry: ScheduleEntry) =>
    entry.timing.isAllDay
      ? `${entry.timing.startDate ?? ""}T00:00:00`
      : (entry.timing.startsAtUtc ?? "");
  return value(left).localeCompare(value(right));
}

export function formatDashboardDate(localDate: string) {
  return new Intl.DateTimeFormat("de-DE", {
    dateStyle: "full",
    timeZone: "UTC",
  }).format(new Date(`${localDate}T12:00:00Z`));
}

export const scheduleStatusLabel: Record<number, string> = {
  0: "Geplant",
  1: "Bestätigt",
  2: "Abgesagt",
};
