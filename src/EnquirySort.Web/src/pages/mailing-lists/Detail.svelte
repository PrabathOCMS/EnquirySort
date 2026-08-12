<script lang="ts">
  import { onDestroy } from "svelte";
  import { getApiUrl } from "../../helpers/api";
  import {
    getGeneralError,
    parseResponse,
    type MyErrorResponse,
  } from "../../helpers/parseResponse";
  import { formatDate, href, navigate } from "../../helpers/router";

  type Props = {
    id: string;
  };

  let { id }: Props = $props();

  type MailingList = {
    id: string;
    name: string;
    address: string;
    description?: string | null;
    insertDateUtc: string;
    updatedDateUtc: string;
    concurrencyKey: string;
  };

  let pageLoading = $state<"loading" | "done" | "error">("loading");
  let loadError = $state("");
  let record = $state<MailingList | null>(null);
  let concurrencyKey = $state("");
  let pageTitle = $state("Mailing list");
  let abortController: AbortController | null = null;
  let lastLoadedId = $state("");

  let deleteOpen = $state(false);
  let deleteBusy = $state(false);
  let deleteError = $state("");
  let deleteConcurrencyAlert = $state("");
  let deleteAdditionalData = $state("");

  function buildBreadCrumbsAndPageTitle(entity: MailingList): void {
    pageTitle = entity.name;
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
      const response = await fetch(`${getApiUrl()}/mailingLists/get/${id}`, {
        method: "GET",
        headers: { Accept: "application/json" },
        signal: currentController.signal,
      });

      const parsed = await parseResponse<MailingList | MyErrorResponse>(response);

      if (!parsed.ok) {
        pageLoading = "error";
        loadError = getGeneralError(parsed.data as MyErrorResponse);
        return;
      }

      record = parsed.data as MailingList;
      concurrencyKey = record.concurrencyKey;
      buildBreadCrumbsAndPageTitle(record);
      pageLoading = "done";
      lastLoadedId = id;
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        return;
      }
      pageLoading = "error";
      loadError = "Unable to load mailing list.";
    }
  }

  function openDelete(): void {
    deleteError = "";
    deleteConcurrencyAlert = "";
    deleteAdditionalData = "";
    deleteOpen = true;
  }

  function closeDelete(): void {
    if (deleteBusy) {
      return;
    }
    deleteOpen = false;
  }

  async function confirmDelete(): Promise<void> {
    if (!record) {
      return;
    }

    deleteBusy = true;
    deleteError = "";
    deleteConcurrencyAlert = "";
    deleteAdditionalData = "";

    try {
      const response = await fetch(`${getApiUrl()}/mailingLists/delete`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          id: record.id,
          concurrencyKey,
        }),
      });

      const parsed = await parseResponse<MyErrorResponse>(response);

      if (parsed.ok) {
        navigate("/");
        return;
      }

      const error = parsed.data as MyErrorResponse;
      if (error?.concurrencyKeyInvalid) {
        deleteConcurrencyAlert = getGeneralError(error);
        deleteAdditionalData = error.additionalData ?? "";
        if (error.additionalData) {
          try {
            const current = JSON.parse(error.additionalData) as MailingList;
            if (current.concurrencyKey) {
              concurrencyKey = current.concurrencyKey;
            }
          } catch {
            // ignore parse failures
          }
        }
        return;
      }

      deleteError = getGeneralError(error);
    } catch {
      deleteError = "Unable to delete mailing list.";
    } finally {
      deleteBusy = false;
    }
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
    <a href={href("/")}>Mailing lists</a>
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
        <h1>{record.name}</h1>
        <p>Mailing list details</p>
      </div>
      <div class="actions">
        <a class="btn btn-primary" href={href(`/mailing-lists/${record.id}/update`)}>Edit</a>
        <button type="button" class="btn btn-danger" onclick={openDelete}>Delete</button>
      </div>
    </div>

    <dl class="dl">
      <div class="dl-item">
        <dt>Name</dt>
        <dd>{record.name}</dd>
      </div>
      <div class="dl-item">
        <dt>Address</dt>
        <dd>{record.address}</dd>
      </div>
      <div class="dl-item">
        <dt>Description</dt>
        <dd>{record.description || "—"}</dd>
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

{#if deleteOpen && record}
  <div class="modal-backdrop" role="presentation">
    <div class="modal" role="dialog" aria-modal="true" aria-labelledby="delete-title">
      <h2 id="delete-title">Delete mailing list</h2>
      <p>Delete <strong>{record.name}</strong>?</p>

      {#if deleteConcurrencyAlert}
        <div class="alert alert-warn">
          {deleteConcurrencyAlert}
          {#if deleteAdditionalData}
            <pre>{deleteAdditionalData}</pre>
          {/if}
          <p>Review the current data, then retry delete if you still want to remove it.</p>
        </div>
      {/if}

      {#if deleteError}
        <div class="alert alert-error">{deleteError}</div>
      {/if}

      <div class="btn-row">
        <button type="button" class="btn btn-danger" disabled={deleteBusy} onclick={confirmDelete}>
          {deleteConcurrencyAlert ? "Retry delete" : "Delete"}
        </button>
        <button type="button" class="btn btn-secondary" disabled={deleteBusy} onclick={closeDelete}>Cancel</button>
      </div>
    </div>
  </div>
{/if}
