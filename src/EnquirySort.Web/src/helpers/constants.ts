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

export const REPLY_STATUS = {
  NONE: 0,
  DRAFT: 1,
  SENT: 2,
} as const;

export const REPLY_STATUS_LABELS: Record<number, string> = {
  [REPLY_STATUS.NONE]: "None",
  [REPLY_STATUS.DRAFT]: "Draft",
  [REPLY_STATUS.SENT]: "Sent",
};

export const RESPONSE_MODE = {
  AUTOMATIC: 0,
  DRAFT: 1,
} as const;

export const RESPONSE_MODE_LABELS: Record<number, string> = {
  [RESPONSE_MODE.AUTOMATIC]: "Automatic",
  [RESPONSE_MODE.DRAFT]: "Draft",
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

export function replyStatusLabel(status: number | string | null | undefined): string {
  if (typeof status === "string") {
    const normalized = status.trim().toLowerCase();
    if (normalized === "none") {
      return "None";
    }
    if (normalized === "draft") {
      return "Draft";
    }
    if (normalized === "sent") {
      return "Sent";
    }
  }

  const numeric = typeof status === "number" ? status : Number(status);
  return REPLY_STATUS_LABELS[numeric] ?? String(status ?? "—");
}

export function responseModeLabel(mode: number | string | null | undefined): string {
  if (typeof mode === "string") {
    const normalized = mode.trim().toLowerCase();
    if (normalized === "automatic") {
      return "Automatic";
    }
    if (normalized === "draft") {
      return "Draft";
    }
  }

  const numeric = typeof mode === "number" ? mode : Number(mode);
  return RESPONSE_MODE_LABELS[numeric] ?? String(mode ?? "—");
}
