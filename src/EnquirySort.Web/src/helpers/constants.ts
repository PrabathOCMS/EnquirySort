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

export const ENQUIRY_FILTER = {
  OPEN: "open",
  RESPONDED: "responded",
  IGNORED: "ignored",
  ROUTED: "routed",
  ALL: "all",
} as const;

export type EnquiryFilterValue = (typeof ENQUIRY_FILTER)[keyof typeof ENQUIRY_FILTER];

export const ENQUIRY_FILTER_OPTIONS: { value: EnquiryFilterValue; label: string; help: string }[] = [
  { value: ENQUIRY_FILTER.OPEN, label: "Open", help: "Draft replies waiting for review" },
  { value: ENQUIRY_FILTER.RESPONDED, label: "Responded", help: "Replies already sent" },
  { value: ENQUIRY_FILTER.IGNORED, label: "Ignored", help: "Marked ignore by the classifier" },
  { value: ENQUIRY_FILTER.ROUTED, label: "Routed", help: "Forwarded to a mailing list" },
  { value: ENQUIRY_FILTER.ALL, label: "All", help: "Every processed enquiry" },
];

export function parseEnquiryFilter(value: string | null | undefined): EnquiryFilterValue {
  const normalized = (value ?? "").trim().toLowerCase();
  if (
    normalized === ENQUIRY_FILTER.RESPONDED
    || normalized === ENQUIRY_FILTER.IGNORED
    || normalized === ENQUIRY_FILTER.ROUTED
    || normalized === ENQUIRY_FILTER.ALL
  ) {
    return normalized;
  }
  return ENQUIRY_FILTER.OPEN;
}

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
