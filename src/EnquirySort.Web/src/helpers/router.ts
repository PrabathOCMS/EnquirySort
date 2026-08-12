export type Route =
  | { name: "mailing-lists"; query: URLSearchParams }
  | { name: "mailing-lists-create" }
  | { name: "mailing-lists-detail"; id: string }
  | { name: "mailing-lists-update"; id: string }
  | { name: "knowledge-articles"; query: URLSearchParams }
  | { name: "knowledge-articles-create" }
  | { name: "knowledge-articles-detail"; id: string }
  | { name: "knowledge-articles-update"; id: string }
  | { name: "enquiries"; query: URLSearchParams }
  | { name: "enquiries-detail"; id: string }
  | { name: "not-found" };

function parseHash(): Route {
  const raw = window.location.hash.replace(/^#/, "") || "/";
  const [pathPart, queryPart = ""] = raw.split("?");
  const path = pathPart.startsWith("/") ? pathPart : `/${pathPart}`;
  const query = new URLSearchParams(queryPart);
  const segments = path.split("/").filter(Boolean);

  if (segments.length === 0) {
    return { name: "mailing-lists", query };
  }

  if (segments[0] === "mailing-lists") {
    if (segments.length === 1) {
      return { name: "mailing-lists", query };
    }
    if (segments[1] === "create" && segments.length === 2) {
      return { name: "mailing-lists-create" };
    }
    if (segments.length === 2) {
      return { name: "mailing-lists-detail", id: segments[1] };
    }
    if (segments.length === 3 && segments[2] === "update") {
      return { name: "mailing-lists-update", id: segments[1] };
    }
  }

  if (segments[0] === "knowledge-articles") {
    if (segments.length === 1) {
      return { name: "knowledge-articles", query };
    }
    if (segments[1] === "create" && segments.length === 2) {
      return { name: "knowledge-articles-create" };
    }
    if (segments.length === 2) {
      return { name: "knowledge-articles-detail", id: segments[1] };
    }
    if (segments.length === 3 && segments[2] === "update") {
      return { name: "knowledge-articles-update", id: segments[1] };
    }
  }

  if (segments[0] === "enquiries") {
    if (segments.length === 1) {
      return { name: "enquiries", query };
    }
    if (segments.length === 2) {
      return { name: "enquiries-detail", id: segments[1] };
    }
  }

  return { name: "not-found" };
}

export function getRoute(): Route {
  return parseHash();
}

export function navigate(path: string): void {
  const normalized = path.startsWith("#")
    ? path
    : path.startsWith("/")
      ? `#${path}`
      : `#/${path}`;

  if (window.location.hash === normalized) {
    window.dispatchEvent(new HashChangeEvent("hashchange"));
    return;
  }

  window.location.hash = normalized;
}

export function href(path: string): string {
  if (path.startsWith("#")) {
    return path;
  }
  if (path.startsWith("/")) {
    return `#${path}`;
  }
  return `#/${path}`;
}

export function formatDate(value: string | null | undefined): string {
  if (!value) {
    return "—";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
