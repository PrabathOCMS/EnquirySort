<script lang="ts">
  import { onDestroy, onMount } from "svelte";
  import { getApiUrl } from "../../helpers/api";
  import {
    ENQUIRY_FILTER,
    ENQUIRY_FILTER_OPTIONS,
    enquiryActionLabel,
    parseEnquiryFilter,
    replyStatusLabel,
    SORT,
    type EnquiryFilterValue,
    type SortOrder,
    type SortValue,
  } from "../../helpers/constants";
  import {
    getGeneralError,
    parseResponse,
    type MyErrorResponse,
  } from "../../helpers/parseResponse";
  import { formatDate, href, navigate } from "../../helpers/router";

  type Props = {
    query: URLSearchParams;
  };

  let { query }: Props = $props();

  type EnquiryRow = {
    id: string;
    fromAddress: string;
    subject: string;
    action: number | string;
    confidence: number;
    replyStatus?: number | string | null;
    routedToMailingListName?: string | null;
    processedUtc: string;
    insertDateUtc: string;
    updatedDateUtc: string;
  };

  type DataTableResponse = {
    requestCounter?: number | null;
    records: EnquiryRow[];
    pageNumber: number;
    pageSize: number;
    totalCount: number;
  };

  let pageNumber = $state(1);
  let pageSize = $state(30);
  let search = $state("");
  let searchDraft = $state("");
  let filter = $state<EnquiryFilterValue>(ENQUIRY_FILTER.OPEN);
  let sortValue = $state<SortValue>(SORT.UPDATED);
  let sortOrder = $state<SortOrder>("desc");
  let requestCounter = $state(0);
  let records = $state<EnquiryRow[]>([]);
  let totalCount = $state(0);
  let loading = $state(true);
  let listError = $state("");
  let processBusy = $state(false);
  let processMessage = $state("");
  let processError = $state("");
  let abortController: AbortController | null = null;
  let initialized = $state(false);
  let lastQueryKey = $state("");

  function restoreFromQuery(params: URLSearchParams): void {
    const page = Number(params.get("pageNumber") ?? "1");
    pageNumber = Number.isFinite(page) && page > 0 ? page : 1;

    const size = Number(params.get("pageSize") ?? "30");
    pageSize = Number.isFinite(size) && size > 0 ? size : 30;

    search = params.get("search") ?? "";
    searchDraft = search;
    filter = parseEnquiryFilter(params.get("filter"));

    const sort = (params.get("sort") ?? SORT.UPDATED) as SortValue;
    applySort(sort, false);
  }

  function syncUrl(): void {
    const params = new URLSearchParams();
    params.set("pageNumber", String(pageNumber));
    params.set("pageSize", String(pageSize));
    params.set("sort", sortValue);
    params.set("sortOrder", sortOrder);
    params.set("filter", filter);
    if (search) {
      params.set("search", search);
    }
    navigate(`/enquiries?${params.toString()}`);
  }

  function applySort(column: SortValue, resetPage: boolean): void {
    switch (column) {
      case SORT.NAME: {
        sortValue = SORT.NAME;
        sortOrder = "asc";
        break;
      }
      case SORT.EMAIL: {
        sortValue = SORT.EMAIL;
        sortOrder = "asc";
        break;
      }
      case SORT.CREATED: {
        sortValue = SORT.CREATED;
        sortOrder = "desc";
        break;
      }
      case SORT.UPDATED: {
        sortValue = SORT.UPDATED;
        sortOrder = "desc";
        break;
      }
      default: {
        sortValue = SORT.UPDATED;
        sortOrder = "desc";
        break;
      }
    }

    if (resetPage) {
      pageNumber = 1;
    }
  }

  function handleSort(column: SortValue): void {
    applySort(column, true);
    syncUrl();
  }

  async function loadData(): Promise<void> {
    if (abortController) {
      abortController.abort();
    }

    abortController = new AbortController();
    const currentController = abortController;
    loading = true;
    listError = "";
    requestCounter += 1;
    const counter = requestCounter;

    try {
      const params = new URLSearchParams();
      params.set("pageNumber", String(pageNumber));
      params.set("pageSize", String(pageSize));
      params.set("sort", sortValue);
      params.set("filter", filter);
      if (search) {
        params.set("search", search);
      }

      const response = await fetch(
        `${getApiUrl()}/enquiries/listForDataTable?${params.toString()}`,
        {
          method: "GET",
          headers: {
            Accept: "application/json",
            "X-Request-Counter": String(counter),
          },
          signal: currentController.signal,
        },
      );

      const parsed = await parseResponse<DataTableResponse | MyErrorResponse>(response);

      if (parsed.data && typeof parsed.data === "object" && "requestCounter" in parsed.data) {
        const body = parsed.data as DataTableResponse;
        if (body.requestCounter != null && body.requestCounter !== counter) {
          return;
        }
      }

      if (!parsed.ok) {
        listError = getGeneralError(parsed.data as MyErrorResponse);
        records = [];
        totalCount = 0;
        return;
      }

      const body = parsed.data as DataTableResponse;
      records = body.records ?? [];
      totalCount = body.totalCount ?? 0;
      pageNumber = body.pageNumber ?? pageNumber;
      pageSize = body.pageSize ?? pageSize;
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        return;
      }
      listError = "Unable to load enquiries.";
      records = [];
      totalCount = 0;
    } finally {
      if (abortController === currentController) {
        loading = false;
      }
    }
  }

  function submitSearch(): void {
    search = searchDraft.trim();
    pageNumber = 1;
    syncUrl();
  }

  function setFilter(next: EnquiryFilterValue): void {
    if (filter === next) {
      return;
    }
    filter = next;
    pageNumber = 1;
    syncUrl();
  }

  function changePage(next: number): void {
    if (next < 1) {
      return;
    }
    const maxPage = Math.max(1, Math.ceil(totalCount / pageSize));
    if (next > maxPage) {
      return;
    }
    pageNumber = next;
    syncUrl();
  }

  async function processInbox(): Promise<void> {
    if (processBusy) {
      return;
    }

    processBusy = true;
    processMessage = "";
    processError = "";

    try {
      const response = await fetch(`${getApiUrl()}/enquiries/processInbox`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({}),
      });

      const parsed = await parseResponse<EnquiryRow[] | MyErrorResponse>(response);

      if (!parsed.ok) {
        processError = getGeneralError(parsed.data as MyErrorResponse);
        return;
      }

      const results = Array.isArray(parsed.data) ? parsed.data : [];
      processMessage = `Processed ${results.length} enquiry${results.length === 1 ? "" : "ies"}.`;
      await loadData();
    } catch {
      processError = "Unable to process inbox.";
    } finally {
      processBusy = false;
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

  function replyStatusClass(status: number | string | null | undefined): string {
    const label = replyStatusLabel(status).toLowerCase();
    if (label === "draft") {
      return "draft";
    }
    if (label === "sent") {
      return "sent";
    }
    return "none";
  }

  onMount(() => {
    restoreFromQuery(query);
    lastQueryKey = query.toString();
    initialized = true;
    void loadData();
  });

  onDestroy(() => {
    if (abortController) {
      abortController.abort();
    }
  });

  $effect(() => {
    const next = query.toString();
    if (!initialized) {
      return;
    }
    if (next === lastQueryKey) {
      return;
    }
    lastQueryKey = next;
    restoreFromQuery(query);
    void loadData();
  });

  const totalPages = $derived(Math.max(1, Math.ceil(totalCount / pageSize) || 1));
  const filterHelp = $derived(
    ENQUIRY_FILTER_OPTIONS.find((option) => option.value === filter)?.help
      ?? "Processed inbox tickets.",
  );
  const emptyMessage = $derived(
    filter === ENQUIRY_FILTER.OPEN
      ? "No open tickets. Process the inbox or switch filters to see history."
      : "No enquiries found for this filter.",
  );
</script>

<div class="page-card">
  <div class="page-heading">
    <div>
      <h1>Enquiries</h1>
      <p>{filterHelp}</p>
    </div>
    <button type="button" class="btn btn-primary" disabled={processBusy} onclick={processInbox}>
      {processBusy ? "Processing…" : "Process inbox"}
    </button>
  </div>

  {#if processMessage}
    <div class="alert alert-ok">{processMessage}</div>
  {/if}
  {#if processError}
    <div class="alert alert-error">{processError}</div>
  {/if}

  <div class="filter-tabs" role="tablist" aria-label="Enquiry filters">
    {#each ENQUIRY_FILTER_OPTIONS as option (option.value)}
      <button
        type="button"
        role="tab"
        class="filter-tab"
        class:active={filter === option.value}
        aria-selected={filter === option.value}
        onclick={() => setFilter(option.value)}
      >
        {option.label}
      </button>
    {/each}
  </div>

  <div class="toolbar">
    <input
      class="search-input"
      type="search"
      placeholder="Search subject or address"
      bind:value={searchDraft}
      onkeydown={(event) => {
        if (event.key === "Enter") {
          submitSearch();
        }
      }}
    />
    <button type="button" class="btn btn-secondary" onclick={submitSearch}>Search</button>
    <div class="spacer"></div>
    <span class="muted">{totalCount} total</span>
  </div>

  {#if listError}
    <div class="alert alert-error">{listError}</div>
  {/if}

  <div class="table-wrap">
    <table class="data-table">
      <thead>
        <tr>
          <th
            class={"sortable" + (sortValue === SORT.NAME ? " sorted" : "")}
            onclick={() => handleSort(SORT.NAME)}
          >
            Subject
          </th>
          <th
            class={"sortable" + (sortValue === SORT.EMAIL ? " sorted" : "")}
            onclick={() => handleSort(SORT.EMAIL)}
          >
            From
          </th>
          <th>Action</th>
          <th>Reply</th>
          <th>Confidence</th>
          <th>Routed to</th>
          <th
            class={"sortable" + (sortValue === SORT.UPDATED ? " sorted" : "")}
            onclick={() => handleSort(SORT.UPDATED)}
          >
            Processed
          </th>
          <th
            class={"sortable" + (sortValue === SORT.CREATED ? " sorted" : "")}
            onclick={() => handleSort(SORT.CREATED)}
          >
            Created
          </th>
        </tr>
      </thead>
      <tbody>
        {#if loading}
          <tr><td colspan="8" class="empty">Loading…</td></tr>
        {:else if records.length === 0}
          <tr><td colspan="8" class="empty">{emptyMessage}</td></tr>
        {:else}
          {#each records as row (row.id)}
            <tr>
              <td><a href={href(`/enquiries/${row.id}`)}>{row.subject || "(no subject)"}</a></td>
              <td>{row.fromAddress}</td>
              <td><span class={"badge " + actionClass(row.action)}>{enquiryActionLabel(row.action)}</span></td>
              <td>
                <span class={"badge " + replyStatusClass(row.replyStatus)}
                  >{replyStatusLabel(row.replyStatus)}</span
                >
              </td>
              <td>{typeof row.confidence === "number" ? row.confidence.toFixed(2) : row.confidence}</td>
              <td>{row.routedToMailingListName || "—"}</td>
              <td>{formatDate(row.processedUtc)}</td>
              <td>{formatDate(row.insertDateUtc)}</td>
            </tr>
          {/each}
        {/if}
      </tbody>
    </table>
  </div>

  <div class="pagination">
    <span class="muted">Page {pageNumber} of {totalPages}</span>
    <div class="actions">
      <button type="button" class="btn btn-secondary" disabled={pageNumber <= 1 || loading} onclick={() => changePage(pageNumber - 1)}>Previous</button>
      <button type="button" class="btn btn-secondary" disabled={pageNumber >= totalPages || loading} onclick={() => changePage(pageNumber + 1)}>Next</button>
    </div>
  </div>
</div>

<style>
  .filter-tabs {
    display: flex;
    flex-wrap: wrap;
    gap: 0.4rem;
    margin-bottom: 0.85rem;
  }

  .filter-tab {
    border: 1px solid color-mix(in srgb, CanvasText 16%, transparent);
    background: color-mix(in srgb, CanvasText 3%, Canvas);
    color: inherit;
    border-radius: 999px;
    padding: 0.4rem 0.85rem;
    font: inherit;
    font-size: 0.9rem;
    font-weight: 600;
    cursor: pointer;
  }

  .filter-tab.active {
    border-color: color-mix(in srgb, #1f6f78 55%, transparent);
    background: color-mix(in srgb, #1f6f78 12%, Canvas);
    color: #16555c;
  }
</style>
