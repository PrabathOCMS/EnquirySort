export const ENQUIRY_ACTION = {
  IGNORE: 0,
  RESPOND: 1,
  ROUTE: 2,
} as const;

export const ENQUIRY_ACTION_LABELS: Record<number, string> = {
  [ENQUIRY_ACTION.IGNORE]: "Ignore",
  [ENQUIRY_ACTION.RESPOND]: "Respond",
  [ENQUIRY_ACTION.ROUTE]: "Route",
};

export const SORT = {
  UNSORTED: "unsorted",
  UPDATED: "updated",
  CREATED: "created",
  NAME: "name",
  EMAIL: "email",
} as const;

export type SortValue = (typeof SORT)[keyof typeof SORT];
export type SortOrder = "asc" | "desc";

export function enquiryActionLabel(action: number | string | null | undefined): string {
  if (typeof action === "string") {
    const normalized = action.trim().toLowerCase();
    if (normalized === "ignore") {
      return "Ignore";
    }
    if (normalized === "respond") {
      return "Respond";
    }
    if (normalized === "route") {
      return "Route";
    }
  }

  const numeric = typeof action === "number" ? action : Number(action);
  return ENQUIRY_ACTION_LABELS[numeric] ?? String(action ?? "—");
}
