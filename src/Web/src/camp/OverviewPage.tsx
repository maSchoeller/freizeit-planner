import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import type {
  Account,
  ActivityEvent,
  MaterialRequirementSummary,
  ScheduleEntry,
  ShoppingListSummary,
} from "./types";
import { getJson } from "./api";
import { useCampQuery, useCampRuntime } from "./runtime";
import {
  campLocalDate,
  compareScheduleEntries,
  formatDashboardDate,
  nextLocalDate,
  scheduleEntryDate,
  scheduleEntryDateTime,
  scheduleEntryTime,
  scheduleStatusLabel,
} from "./schedule";
import { PageHeading, QueryState, SummaryCard } from "./ui";
import { searchTypeLabel } from "./SearchTrashPage";

export function OverviewPage() {
  const { organizationId, campId, camp } = useCampRuntime();
  const account = useQuery({
    queryKey: ["account"],
    queryFn: () => getJson<Account>("/api/v1/account"),
    retry: false,
    staleTime: 5 * 60 * 1000,
  });
  const schedulePath = `/api/v1/organizations/${organizationId}/camps/${campId}/schedule?fromDate=${camp.startsOn}&toDateExclusive=${nextLocalDate(camp.endsOn)}`;
  const schedule = useCampQuery<ScheduleEntry[]>("schedule", schedulePath);
  const material = useCampQuery<MaterialRequirementSummary[]>(
    "material",
    `/api/v1/organizations/${organizationId}/camps/${campId}/logistics/material`,
  );
  const shopping = useCampQuery<ShoppingListSummary[]>(
    "shopping-lists",
    `/api/v1/organizations/${organizationId}/camps/${campId}/logistics/shopping-lists`,
  );
  const activity = useCampQuery<ActivityEvent[]>(
    "activity",
    `/api/v1/organizations/${organizationId}/camps/${campId}/activity?limit=5`,
  );
  const today = campLocalDate(camp.timeZoneId);
  const scheduleEntries = schedule.data ?? [];
  const availableDates = Array.from(
    new Set(
      scheduleEntries
        .map((entry) => scheduleEntryDate(entry, camp.timeZoneId))
        .filter((date): date is string => Boolean(date)),
    ),
  ).sort();
  const planDate =
    availableDates.find((date) => date >= today) ??
    availableDates.at(-1) ??
    (today < camp.startsOn
      ? camp.startsOn
      : today > camp.endsOn
        ? camp.endsOn
        : today);
  const planEntries = scheduleEntries
    .filter((entry) => scheduleEntryDate(entry, camp.timeZoneId) === planDate)
    .sort(compareScheduleEntries);
  const planHeading =
    planDate === today
      ? "Heute im Tagesplan"
      : planDate > today
        ? "Nächster Tagesplan"
        : "Letzter Tagesplan";
  const accountDisplayName = account.data?.displayName?.trim();
  const accountId = account.data?.id;
  const responsibilities = accountId
    ? scheduleEntries.filter(
        (entry) =>
          entry.status !== 2 && entry.responsibleUserIds.includes(accountId),
      )
    : [];
  const openMaterial = (material.data ?? []).filter(
    (requirement) => requirement.status === 0 || requirement.status === 1,
  ).length;
  const openShopping = (shopping.data ?? []).reduce(
    (sum, list) => sum + list.openItemCount,
    0,
  );
  return (
    <>
      <PageHeading
        eyebrow={formatDashboardDate(planDate)}
        title={
          accountDisplayName ? `Hallo, ${accountDisplayName}` : "Camp-Übersicht"
        }
      >
        <p>Hier siehst du, was als Nächstes für euer Team wichtig ist.</p>
      </PageHeading>
      <section aria-labelledby="today-heading">
        <div className="section-heading">
          <h2 id="today-heading">{planHeading}</h2>
          <Link to="tagesplan">Ganzen Plan öffnen</Link>
        </div>
        <QueryState loading={schedule.isLoading} error={schedule.error} />
        {planEntries.length ? (
          <ol className="timeline">
            {planEntries.map((entry) => (
              <li key={entry.id}>
                <time dateTime={scheduleEntryDateTime(entry)}>
                  {scheduleEntryTime(entry, camp.timeZoneId)}
                </time>
                <div>
                  <strong>{entry.title}</strong>
                  <span>
                    {[entry.location, entry.category]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                </div>
                <span
                  className={
                    entry.overlapsAnotherEntry ? "status info" : "status"
                  }
                >
                  {entry.overlapsAnotherEntry
                    ? "Parallel"
                    : scheduleStatusLabel[entry.status]}
                </span>
              </li>
            ))}
          </ol>
        ) : (
          !schedule.isLoading && (
            <p className="empty-state">
              Für diesen Tag sind noch keine Einträge geplant.
            </p>
          )
        )}
      </section>
      <div className="dashboard-grid">
        <SummaryCard
          title="Meine Verantwortungen"
          value={String(responsibilities.length)}
          text={
            responsibilities.length === 1
              ? "aktiver Zeitplaneintrag"
              : "aktive Zeitplaneinträge"
          }
        >
          <QueryState
            loading={account.isLoading || schedule.isLoading}
            error={account.error ?? schedule.error}
          />
        </SummaryCard>
        <SummaryCard
          title="Beschaffung"
          value={String(openMaterial + openShopping)}
          text="noch zu beschaffen"
        >
          <p className="metric-detail">
            {openMaterial} Material · {openShopping} Einkauf
          </p>
          <QueryState
            loading={material.isLoading || shopping.isLoading}
            error={material.error ?? shopping.error}
          />
        </SummaryCard>
        <section className="card activity-card">
          <h2>Jüngste Aktivitäten</h2>
          <QueryState loading={activity.isLoading} error={activity.error} />
          {activity.data?.length ? (
            <ul>
              {activity.data.map((event) => (
                <li key={event.id}>
                  <div>
                    <span>
                      {activityKindLabel[event.kind]}: „{event.title}“
                    </span>
                    <small>
                      {event.actorDisplayName} ·{" "}
                      {searchTypeLabel[event.objectType] ?? event.objectType}
                    </small>
                  </div>
                  <time dateTime={event.timestamp}>
                    {new Intl.DateTimeFormat("de-DE", {
                      dateStyle: "short",
                      timeStyle: "short",
                    }).format(new Date(event.timestamp))}
                  </time>
                </li>
              ))}
            </ul>
          ) : (
            !activity.isLoading && (
              <p className="empty-state">Noch keine Aktivität vorhanden.</p>
            )
          )}
        </section>
      </div>
    </>
  );
}

export const activityKindLabel: Record<ActivityEvent["kind"], string> = {
  0: "Erstellt",
  1: "Geändert",
  2: "In den Papierkorb verschoben",
  3: "Wiederhergestellt",
};
