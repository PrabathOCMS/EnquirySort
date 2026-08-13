<script lang="ts">
  import { onDestroy } from "svelte";
  import { getApiUrl } from "../../helpers/api";
  import {
    enquiryActionLabel,
    replyStatusLabel,
    REPLY_STATUS,
  } from "../../helpers/constants";
  import {
    getGeneralError,
    parseResponse,
    type MyErrorResponse,
  } from "../../helpers/parseResponse";
  import { formatDate, href } from "../../helpers/router";

  type Props = {
    id: string;
  };

  let { id }: Props = $props();

  type Enquiry = {
    id: string;
    messageId?: string | null;
    fromAddress: string;
    subject: string;
    bodyText: string;
    action: number | string;
    confidence: number;
    reason?: string | null;
    customerQuestion?: string | null;
    routedToMailingListId?: string | null;
    routedToMailingListName?: string | null;
    replyBody?: string | null;
    replySent: boolean;
    replyStatus: number | string;
    concurrencyKey: string;
    processedUtc: string;
    insertDateUtc: string;
    updatedDateUtc: string;
  };

  let pageLoading = $state<"loading" | "done" | "error">("loading");
  let loadError = $state("");
  let record = $state<Enquiry | null>(null);
  let pageTitle = $state("Enquiry");
  let abortController: AbortController | null = null;
  let lastLoadedId = $state("");

  let draftBody = $state("");
  let formDisabled = $state(false);
  let formError = $state("");
  let formSuccess = $state("");
  let concurrencyKey = $state("");
  let concurrencyAlert = $state("");

  function buildBreadCrumbsAndPageTitle(entity: Enquiry): void {
    pageTitle = entity.subject || "(no subject)";
  }

  function isDraft(entity: Enquiry): boolean {
    if (entity.replySent) {
      return false;
    }
    if (typeof entity.replyStatus === "string") {
      return entity.replyStatus.toLowerCase() === "draft";
    }
    return Number(entity.replyStatus) === REPLY_STATUS.DRAFT;
  }

  function replyStatusClass(status: number | string): string {
    const label = replyStatusLabel(status).toLowerCase();
    if (label === "draft") {
      return "draft";
    }
    if (label === "sent") {
      return "sent";
    }
    return "none";
  }

  function applyConcurrencyConflict(error: MyErrorResponse): void {
    concurrencyAlert = getGeneralError(error);
    formError = "";
    formSuccess = "";
    if (!error.additionalData) {
      return;
    }
    try {
      const current = JSON.parse(error.additionalData) as Enquiry;
      record = current;
      draftBody = current.replyBody ?? "";
      if (current.concurrencyKey) {
        concurrencyKey = current.concurrencyKey;
      }
      buildBreadCrumbsAndPageTitle(current);
    } catch {
      // keep local draft when payload is not JSON
    }
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
      const response = await fetch(`${getApiUrl()}/enquiries/get/${id}`, {
        method: "GET",
        headers: { Accept: "application/json" },
        signal: currentController.signal,
      });

      const parsed = await parseResponse<Enquiry | MyErrorResponse>(response);

      if (!parsed.ok) {
        pageLoading = "error";
        loadError = getGeneralError(parsed.data as MyErrorResponse);
        return;
      }

      record = parsed.data as Enquiry;
      draftBody = record.replyBody ?? "";
      concurrencyKey = record.concurrencyKey ?? "";
      buildBreadCrumbsAndPageTitle(record);
      pageLoading = "done";
      lastLoadedId = id;
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        return;
      }
      pageLoading = "error";
      loadError = "Unable to load enquiry.";
    }
  }

  async function saveDraft(): Promise<void> {
    if (!record || formDisabled) {
      return;
    }

    formDisabled = true;
    formError = "";
    formSuccess = "";
    concurrencyAlert = "";

    try {
      const response = await fetch(`${getApiUrl()}/enquiries/updateDraft`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          id: record.id,
          replyBody: draftBody,
          concurrencyKey,
        }),
      });

      const parsed = await parseResponse<Enquiry | MyErrorResponse>(response);
      if (!parsed.ok) {
        const error = parsed.data as MyErrorResponse;
        if (error?.concurrencyKeyInvalid) {
          applyConcurrencyConflict(error);
          return;
        }
        formError = getGeneralError(error);
        return;
      }

      record = parsed.data as Enquiry;
      draftBody = record.replyBody ?? "";
      concurrencyKey = record.concurrencyKey ?? "";
      formSuccess = "Draft saved.";
    } catch {
      formError = "Unable to save draft.";
    } finally {
      formDisabled = false;
    }
  }

  async function sendReply(): Promise<void> {
    if (!record || formDisabled) {
      return;
    }

    formDisabled = true;
    formError = "";
    formSuccess = "";
    concurrencyAlert = "";

    try {
      const response = await fetch(`${getApiUrl()}/enquiries/sendReply`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          id: record.id,
          replyBody: draftBody,
          concurrencyKey,
        }),
      });

      const parsed = await parseResponse<Enquiry | MyErrorResponse>(response);
      if (!parsed.ok) {
        const error = parsed.data as MyErrorResponse;
        if (error?.concurrencyKeyInvalid) {
          applyConcurrencyConflict(error);
          return;
        }
        formError = getGeneralError(error);
        return;
      }

      record = parsed.data as Enquiry;
      draftBody = record.replyBody ?? "";
      concurrencyKey = record.concurrencyKey ?? "";
      formSuccess = "Reply sent to the customer.";
    } catch {
      formError = "Unable to send reply.";
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
    if (!record || !isDraft(record)) {
      return;
    }
    event.preventDefault();
    void saveDraft();
  }

  function actionClass(action: number | string): string {
    const label = enquiryActionLabel(action).toLowerCase();
    if (label === "respond") {
      return "respond";
    }
    if (label === "route") {
      return "route";
    }
    return "ignore";
  }

  onDestroy(() => {
    if (abortController) {
      abortController.abort();
    }
  });

  $effect(() => {
    if (id !== lastLoadedId) {
      void loadData();
    }
  });
</script>

<svelte:window onkeydown={handleKeyDown} />

<div class="page-card">
  <div class="breadcrumbs">
    <a href={href("/enquiries")}>Enquiries</a>
    <span>/</span>
    <span>{pageTitle}</span>
  </div>

  {#if pageLoading === "loading"}
    <p class="muted">Loading…</p>
  {:else if pageLoading === "error"}
    <div class="alert alert-error">{loadError}</div>
    <button type="button" class="btn btn-secondary" onclick={loadData}>Retry</button>
  {:else if record}
    <div class="page-heading">
      <div>
        <h1>{record.subject || "(no subject)"}</h1>
        <p>Enquiry ticket — review, edit draft, and approve the reply.</p>
      </div>
      <div class="heading-badges">
        <span class={"badge " + actionClass(record.action)}>{enquiryActionLabel(record.action)}</span>
        <span class={"badge " + replyStatusClass(record.replyStatus)}
          >{replyStatusLabel(record.replyStatus)}</span
        >
      </div>
    </div>

    {#if concurrencyAlert}
      <div class="alert alert-error">{concurrencyAlert}</div>
    {/if}
    {#if formError}
      <div class="alert alert-error">{formError}</div>
    {/if}
    {#if formSuccess}
      <div class="alert alert-success">{formSuccess}</div>
    {/if}

    <dl class="dl">
      <div class="dl-item">
        <dt>From</dt>
        <dd>{record.fromAddress}</dd>
      </div>
      <div class="dl-item">
        <dt>Message ID</dt>
        <dd>{record.messageId || "—"}</dd>
      </div>
      <div class="dl-item">
        <dt>Confidence</dt>
        <dd>{typeof record.confidence === "number" ? record.confidence.toFixed(2) : record.confidence}</dd>
      </div>
      <div class="dl-item">
        <dt>Reason</dt>
        <dd>{record.reason || "—"}</dd>
      </div>
      <div class="dl-item">
        <dt>Customer question</dt>
        <dd>{record.customerQuestion || "—"}</dd>
      </div>
      <div class="dl-item">
        <dt>Routed to mailing list</dt>
        <dd>{record.routedToMailingListName || "—"}</dd>
      </div>
      <div class="dl-item">
        <dt>Body</dt>
        <dd>{record.bodyText || "—"}</dd>
      </div>
      <div class="dl-item">
        <dt>Processed</dt>
        <dd>{formatDate(record.processedUtc)}</dd>
      </div>
    </dl>

    <section class="reply-panel">
      <h2>Customer reply</h2>
      {#if isDraft(record)}
        <form
          onsubmit={(event) => {
            event.preventDefault();
          }}
        >
          <label class="field">
            <span>Draft reply</span>
            <textarea
              rows="10"
              bind:value={draftBody}
              disabled={formDisabled}
              placeholder="Write or edit the reply to send to the customer"
            ></textarea>
          </label>
          <div class="actions">
            <button
              type="button"
              class="btn btn-secondary"
              disabled={formDisabled}
              onclick={saveDraft}
              title="Ctrl + S"
            >
              Save draft
            </button>
            <button
              type="button"
              class="btn btn-primary"
              disabled={formDisabled}
              onclick={sendReply}
            >
              Approve &amp; send
            </button>
          </div>
        </form>
      {:else}
        <dl class="dl">
          <div class="dl-item">
            <dt>Reply status</dt>
            <dd>{replyStatusLabel(record.replyStatus)}</dd>
          </div>
          <div class="dl-item">
            <dt>Reply body</dt>
            <dd>{record.replyBody || "—"}</dd>
          </div>
        </dl>
      {/if}
    </section>
  {/if}
</div>

<style>
  .heading-badges {
    display: flex;
    gap: 0.5rem;
    align-items: center;
  }

  .reply-panel {
    margin-top: 1.5rem;
    padding-top: 1rem;
    border-top: 1px solid color-mix(in srgb, CanvasText 12%, transparent);
  }

  .reply-panel h2 {
    margin: 0 0 0.75rem;
    font-size: 1.1rem;
  }

  .field {
    display: grid;
    gap: 0.4rem;
    margin-bottom: 0.85rem;
  }

  .field span {
    font-weight: 600;
    font-size: 0.9rem;
  }

  textarea {
    width: 100%;
    resize: vertical;
    padding: 0.75rem;
    border-radius: 0.5rem;
    border: 1px solid color-mix(in srgb, CanvasText 18%, transparent);
    font: inherit;
    line-height: 1.45;
  }

  .actions {
    display: flex;
    gap: 0.6rem;
    flex-wrap: wrap;
  }

  .alert-success {
    background: color-mix(in srgb, #1f7a4d 16%, transparent);
    border: 1px solid color-mix(in srgb, #1f7a4d 35%, transparent);
    color: inherit;
    padding: 0.75rem 0.9rem;
    border-radius: 0.5rem;
    margin-bottom: 0.85rem;
  }
</style>
