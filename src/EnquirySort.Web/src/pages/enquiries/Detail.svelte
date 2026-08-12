<script lang="ts">
  import { onDestroy } from "svelte";
  import { getApiUrl } from "../../helpers/api";
  import { enquiryActionLabel } from "../../helpers/constants";
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

  function buildBreadCrumbsAndPageTitle(entity: Enquiry): void {
    pageTitle = entity.subject || "(no subject)";
  }

  async function loadData(): Promise<void> {
    if (abortController) {
      abortController.abort();
    }
    abortController = new AbortController();
    const currentController = abortController;

    pageLoading = "loading";
    loadError = "";

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
        <p>Enquiry detail</p>
      </div>
      <span class={"badge " + actionClass(record.action)}>{enquiryActionLabel(record.action)}</span>
    </div>

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
        <dt>Reply sent</dt>
        <dd>{record.replySent ? "Yes" : "No"}</dd>
      </div>
      <div class="dl-item">
        <dt>Reply body</dt>
        <dd>{record.replyBody || "—"}</dd>
      </div>
      <div class="dl-item">
        <dt>Body</dt>
        <dd>{record.bodyText || "—"}</dd>
      </div>
      <div class="dl-item">
        <dt>Processed</dt>
        <dd>{formatDate(record.processedUtc)}</dd>
      </div>
      <div class="dl-item">
        <dt>Updated</dt>
        <dd>{formatDate(record.updatedDateUtc)}</dd>
      </div>
      <div class="dl-item">
        <dt>Created</dt>
        <dd>{formatDate(record.insertDateUtc)}</dd>
      </div>
    </dl>
  {/if}
</div>
