import type { components } from "../api/schema";

export type User = components["schemas"]["UserAdministrationView"];
export type Page =
  components["schemas"]["AdministrationPageOfUserAdministrationView"];
export type Membership =
  components["schemas"]["OrganizationAdministrationView"];
export type MembershipStatus = components["schemas"]["MembershipStatus"];
export type Organization = components["schemas"]["SuperAdminOrganizationView"];
export type Camp = components["schemas"]["CampSummary"];
export type CampRole = components["schemas"]["CampRole"];

export type AdministrationMode = "superadmin" | "organization";

export type PendingAction = {
  title: string;
  description: string;
  confirmLabel: string;
  run: () => Promise<void>;
};

export const active = 0;
export const suspended = 1;
export const removed = 2;
export const organizationAdmin = 0;

export function membershipFor(user: User, organizationId: string) {
  return user.organizations.find(
    (membership) => membership.organizationId === organizationId,
  );
}

export function versionHeaders(token: string, version: number | string) {
  return {
    "X-CSRF-TOKEN": token,
    "If-Match": `"${version}"`,
  };
}

export function message(caught: unknown, fallback: string) {
  return caught instanceof Error ? caught.message : fallback;
}
