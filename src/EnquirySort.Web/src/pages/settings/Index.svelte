<script lang="ts">
  import { onDestroy, onMount } from "svelte";
  import SignatureEditor from "../../components/SignatureEditor.svelte";
  import { getApiUrl } from "../../helpers/api";
  import { RESPONSE_MODE, responseModeLabel } from "../../helpers/constants";
  import {
    getGeneralError,
    parseResponse,
    type MyErrorResponse,
  } from "../../helpers/parseResponse";

  type AppSetting = {
    id: string;
    responseMode: number | string;
    emailSignatureHtml?: string | null;
    concurrencyKey: string;
    insertDateUtc: string;
    updatedDateUtc: string;
  };

  let pageLoading = $state<"loading" | "done" | "error">("loading");
  let loadError = $state("");
  let formDisabled = $state(false);
  let formError = $state("");
  let formSuccess = $state("");
  let concurrencyAlert = $state("");

  let responseMode = $state<number>(RESPONSE_MODE.DRAFT);
  let signatureHtml = $state("<p>Kind regards,<br/>Support Team</p>");
  let concurrencyKey = $state("");
  let settingsId = $state("");
  let abortController: AbortController | null = null;

  function normalizeMode(value: number | string | null | undefined): number {
    if (typeof value === "string") {
      const normalized = value.trim().toLowerCase();
      if (normalized === "automatic") {
        return RESPONSE_MODE.AUTOMATIC;
      }
      if (normalized === "draft") {
        return RESPONSE_MODE.DRAFT;
      }
    }
    const numeric = typeof value === "number" ? value : Number(value);
    return numeric === RESPONSE_MODE.AUTOMATIC
      ? RESPONSE_MODE.AUTOMATIC
      : RESPONSE_MODE.DRAFT;
  }

  function applyEntity(entity: AppSetting): void {
    settingsId = entity.id;
    responseMode = normalizeMode(entity.responseMode);
    signatureHtml = entity.emailSignatureHtml || "";
    concurrencyKey = entity.concurrencyKey ?? "";
  }

  async function loadData(): Promise<void> {
    if (abortController) {
      abortController.abort();
    }
    abortController = new AbortController();
    const currentController = abortController;

    pageLoading = "loading";
    loadError = "";
    formError = "";
    formSuccess = "";
    concurrencyAlert = "";

    try {
      const response = await fetch(`${getApiUrl()}/appSettings/get`, {
        method: "GET",
        headers: { Accept: "application/json" },
        signal: currentController.signal,
      });

      const parsed = await parseResponse<AppSetting | MyErrorResponse>(response);
      if (!parsed.ok) {
        pageLoading = "error";
        loadError = getGeneralError(parsed.data as MyErrorResponse);
        return;
      }

      applyEntity(parsed.data as AppSetting);
      pageLoading = "done";
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        return;
      }
      pageLoading = "error";
      loadError = "Unable to load settings.";
    }
  }

  async function saveSettings(): Promise<void> {
    if (formDisabled || pageLoading !== "done") {
      return;
    }

    formDisabled = true;
    formError = "";
    formSuccess = "";
    concurrencyAlert = "";

    try {
      const response = await fetch(`${getApiUrl()}/appSettings/update`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          id: settingsId,
          responseMode,
          emailSignatureHtml: signatureHtml,
          concurrencyKey,
        }),
      });

      const parsed = await parseResponse<AppSetting | MyErrorResponse>(response);
      if (!parsed.ok) {
        const error = parsed.data as MyErrorResponse;
        if (error?.concurrencyKeyInvalid) {
          concurrencyAlert = getGeneralError(error);
          if (error.additionalData) {
            try {
              applyEntity(JSON.parse(error.additionalData) as AppSetting);
            } catch {
              // keep local edits when payload is not JSON
            }
          }
          return;
        }
        formError = getGeneralError(error);
        return;
      }

      applyEntity(parsed.data as AppSetting);
      formSuccess = "Settings saved.";
    } catch {
      formError = "Unable to save settings.";
    } finally {
      formDisabled = false;
    }
  }

  function handleKeyDown(event: KeyboardEvent): void {
    if (event.repeat || event.altKey || event.metaKey || !event.ctrlKey) {
      return;
    }
    if (event.code !== "KeyS") {
      return;
    }
    event.preventDefault();
    void saveSettings();
  }

  onMount(() => {
    void loadData();
  });

  onDestroy(() => {
    if (abortController) {
      abortController.abort();
    }
  });
</script>

<svelte:window onkeydown={handleKeyDown} />

<div class="page-card">
  <div class="page-heading">
    <div>
      <h1>Settings</h1>
      <p>Choose reply mode and edit the email signature used on customer replies.</p>
    </div>
  </div>

  {#if pageLoading === "loading"}
    <p class="muted">Loading…</p>
  {:else if pageLoading === "error"}
    <div class="alert alert-error">{loadError}</div>
    <button type="button" class="btn btn-secondary" onclick={loadData}>Retry</button>
  {:else}
    {#if concurrencyAlert}
      <div class="alert alert-error">{concurrencyAlert}</div>
    {/if}
    {#if formError}
      <div class="alert alert-error">{formError}</div>
    {/if}
    {#if formSuccess}
      <div class="alert alert-ok">{formSuccess}</div>
    {/if}

    <form
      class="settings-form"
      onsubmit={(event) => {
        event.preventDefault();
        void saveSettings();
      }}
    >
      <section class="section">
        <h2>Response mode</h2>
        <p class="section-help">
          Draft mode saves AI replies for review. Automatic mode sends them immediately.
        </p>
        <div class="mode-grid" role="radiogroup" aria-label="Response mode">
          <label class="mode-card" class:selected={responseMode === RESPONSE_MODE.DRAFT}>
            <input
              type="radio"
              name="responseMode"
              value={RESPONSE_MODE.DRAFT}
              checked={responseMode === RESPONSE_MODE.DRAFT}
              disabled={formDisabled}
              onchange={() => {
                responseMode = RESPONSE_MODE.DRAFT;
              }}
            />
            <span class="mode-title">{responseModeLabel(RESPONSE_MODE.DRAFT)}</span>
            <span class="mode-copy">Write a draft on each enquiry. Edit, approve, then send.</span>
          </label>
          <label class="mode-card" class:selected={responseMode === RESPONSE_MODE.AUTOMATIC}>
            <input
              type="radio"
              name="responseMode"
              value={RESPONSE_MODE.AUTOMATIC}
              checked={responseMode === RESPONSE_MODE.AUTOMATIC}
              disabled={formDisabled}
              onchange={() => {
                responseMode = RESPONSE_MODE.AUTOMATIC;
              }}
            />
            <span class="mode-title">{responseModeLabel(RESPONSE_MODE.AUTOMATIC)}</span>
            <span class="mode-copy">Send the AI reply as soon as the inbox message is processed.</span>
          </label>
        </div>
      </section>

      <section class="section">
        <h2>Email signature</h2>
        <p class="section-help">
          Appended to every customer reply (automatic sends and approved drafts). Supports pasted
          images.
        </p>
        <SignatureEditor
          value={signatureHtml}
          disabled={formDisabled}
          onChange={(html) => {
            signatureHtml = html;
          }}
        />
      </section>

      <div class="actions">
        <button type="submit" class="btn btn-primary" disabled={formDisabled} title="Ctrl + S">
          {formDisabled ? "Saving…" : "Save settings"}
        </button>
      </div>
    </form>
  {/if}
</div>

<style>
  .settings-form {
    display: grid;
    gap: 1.5rem;
  }

  .section h2 {
    margin: 0 0 0.35rem;
    font-size: 1.1rem;
  }

  .section-help {
    margin: 0 0 0.85rem;
    color: color-mix(in srgb, CanvasText 65%, transparent);
  }

  .mode-grid {
    display: grid;
    gap: 0.75rem;
    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  }

  .mode-card {
    display: grid;
    gap: 0.35rem;
    padding: 0.9rem 1rem;
    border-radius: 0.75rem;
    border: 1px solid color-mix(in srgb, CanvasText 16%, transparent);
    cursor: pointer;
    background: color-mix(in srgb, CanvasText 3%, Canvas);
  }

  .mode-card.selected {
    border-color: color-mix(in srgb, #1d4f91 55%, transparent);
    background: color-mix(in srgb, #1d4f91 8%, Canvas);
  }

  .mode-card input {
    position: absolute;
    opacity: 0;
    pointer-events: none;
  }

  .mode-title {
    font-weight: 700;
  }

  .mode-copy {
    font-size: 0.9rem;
    color: color-mix(in srgb, CanvasText 70%, transparent);
  }

  .actions {
    display: flex;
    gap: 0.6rem;
  }
</style>
