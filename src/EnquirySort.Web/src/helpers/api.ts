export function getApiUrl(): string {
  const url = import.meta.env.VITE_API_URL as string | undefined;
  if (typeof url === "string" && url.trim().length > 0) {
    return url.replace(/\/$/, "");
  }

  return "http://localhost:5288";
}
