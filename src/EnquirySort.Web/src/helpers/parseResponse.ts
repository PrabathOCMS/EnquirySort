export type ParsedResponse<T = unknown> = {
  response: Response;
  data: T | null;
  ok: boolean;
  status: number;
};

export async function parseResponse<T = unknown>(
  response: Response,
): Promise<ParsedResponse<T>> {
  const contentType = response.headers.get("content-type") ?? "";
  let data: T | null = null;

  if (response.status === 204) {
    return {
      response,
      data: null,
      ok: response.ok,
      status: response.status,
    };
  }

  if (contentType.includes("application/json")) {
    data = (await response.json()) as T;
  } else {
    const text = await response.text();
    data = (text.length > 0 ? text : null) as T | null;
  }

  return {
    response,
    data,
    ok: response.ok,
    status: response.status,
  };
}

export type ErrorMessageItem = {
  message: string;
  errorCode?: string | null;
};

export type MyErrorResponse = {
  errorMessages?: Record<string, ErrorMessageItem[]>;
  fatalError?: boolean;
  concurrencyKeyInvalid?: boolean;
  additionalData?: string | null;
  traceId?: string | null;
};

export function getFieldErrors(
  error: MyErrorResponse | null | undefined,
  field: string,
): string {
  if (!error?.errorMessages) {
    return "";
  }

  const match = Object.entries(error.errorMessages).find(
    ([key]) => key.toLowerCase() === field.toLowerCase(),
  );

  if (!match) {
    return "";
  }

  return match[1].map((item) => item.message).join(" ");
}

export function getGeneralError(
  error: MyErrorResponse | null | undefined,
): string {
  if (!error?.errorMessages) {
    return "An unknown error occurred.";
  }

  const general =
    error.errorMessages.General ??
    error.errorMessages.general ??
    error.errorMessages[""];

  if (general && general.length > 0) {
    return general.map((item) => item.message).join(" ");
  }

  const first = Object.values(error.errorMessages)[0];
  if (first && first.length > 0) {
    return first.map((item) => item.message).join(" ");
  }

  return "An unknown error occurred.";
}
