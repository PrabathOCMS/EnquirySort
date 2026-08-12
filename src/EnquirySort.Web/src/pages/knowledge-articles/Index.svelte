<script lang="ts">
  import { onDestroy, onMount } from "svelte";
  import { getApiUrl } from "../../helpers/api";
  import { SORT, type SortOrder, type SortValue } from "../../helpers/constants";
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

  type KnowledgeArticleRow = {
    id: string;
    title: string;
    slug: string;
    content: string;
    insertDateUtc: string;
    updatedDateUtc: string;
    concurrencyKey: string;
  };

  type DataTableResponse = {
    requestCounter?: number | null;
    records: KnowledgeArticleRow[];
    pageNumber: number;
    pageSize: number;
    totalCount: number;
  };

  let pageNumber = $state(1);
  let pageSize = $state(30);
  let search = $state("");
  let searchDraft = $state("");
  let sortValue = $state<SortValue>(SORT.NAME);
  let sortOrder = $state<SortOrder>("asc");
  let requestCounter = $state(0);
  let records = $state<KnowledgeArticleRow[]>([]);
  let totalCount = $state(0);
  let loading = $state(true);
  let listError = $state("");
  let abortController: AbortController | null = null;
  let initialized = $state(false);
  let lastQueryKey = $state("");

  let deleteOpen = $state(false);
  let deleteBusy = $state(false);
  let deleteError = $state("");
  let deleteConcurrencyAlert = $state("");
  let deleteAdditionalData = $state("");
  let deleteTarget = $state<KnowledgeArticleRow | null>(null);
  let deleteConcurrencyKey = $state("");

  function restoreFromQuery(params: URLSearchParams): void {
    const page = Number(params.get("pageNumber") ?? "1");
    pageNumber = Number.isFinite(page) && page > 0 ? page : 1;

    const size = Number(params.get("pageSize") ?? "30");
    pageSize = Number.isFinite(size) && size > 0 ? size : 30;

    search = params.get("search") ?? "";
    searchDraft = search;

    const sort = (params.get("sort") ?? SORT.NAME) as SortValue;
    applySort(sort, false);
  }

  function syncUrl(): void {
    const params = new URLSearchParams();
    params.set("pageNumber", String(pageNumber));
    params.set("pageSize", String(pageSize));
    params.set("sort", sortValue);
    params.set("sortOrder", sortOrder);
    if (search) {
      params.set("search", search);
    }
    navigate(`/knowledge-articles?${params.toString()}`);
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
        sortValue = SORT.NAME;
        sortOrder = "asc";
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
      if (search) {
        params.set("search", search);
      }

      const response = await fetch(
        `${getApiUrl()}/knowledgeArticles/listForDataTable?${params.toString()}`,
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
      listError = "Unable to load knowledge articles.";
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

  function openDelete(row: KnowledgeArticleRow): void {
    deleteTarget = row;
    deleteConcurrencyKey = row.concurrencyKey;
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
    deleteTarget = null;
  }

  async function confirmDelete(): Promise<void> {
    if (!deleteTarget) {
      return;
    }

    deleteBusy = true;
    deleteError = "";
    deleteConcurrencyAlert = "";
    deleteAdditionalData = "";

    try {
      const response = await fetch(`${getApiUrl()}/knowledgeArticles/delete`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          id: deleteTarget.id,
          concurrencyKey: deleteConcurrencyKey,
        }),
      });

      const parsed = await parseResponse<MyErrorResponse>(response);

      if (parsed.ok) {
        deleteOpen = false;
        deleteTarget = null;
        await loadData();
        return;
      }

      const error = parsed.data as MyErrorResponse;
      if (error?.concurrencyKeyInvalid) {
        deleteConcurrencyAlert = getGeneralError(error);
        deleteAdditionalData = error.additionalData ?? "";
        if (error.additionalData) {
          try {
            const current = JSON.parse(error.additionalData) as KnowledgeArticleRow;
            if (current.concurrencyKey) {
              deleteConcurrencyKey = current.concurrencyKey;
            }
          } catch {
            // keep existing key
          }
        }
        return;
      }

      deleteError = getGeneralError(error);
    } catch {
      deleteError = "Unable to delete knowledge article.";
    } finally {
      deleteBusy = false;
    }
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
</script>

<div class="page-card">
  <div class="page-heading">
    <div>
      <h1>Knowledge articles</h1>
      <p>Content EnquirySort uses when drafting customer replies.</p>
    </div>
    <a class="btn btn-primary" href={href("/knowledge-articles/create")}>Create article</a>
  </div>

  <div class="toolbar">
    <input
      class="search-input"
      type="search"
      placeholder="Search title or slug"
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
            Title
          </th>
          <th>Slug</th>
          <th
            class={"sortable" + (sortValue === SORT.UPDATED ? " sorted" : "")}
            onclick={() => handleSort(SORT.UPDATED)}
          >
            Updated
          </th>
          <th
            class={"sortable" + (sortValue === SORT.CREATED ? " sorted" : "")}
            onclick={() => handleSort(SORT.CREATED)}
          >
            Created
          </th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        {#if loading}
          <tr><td colspan="5" class="empty">Loading…</td></tr>
        {:else if records.length === 0}
          <tr><td colspan="5" class="empty">No knowledge articles found.</td></tr>
        {:else}
          {#each records as row (row.id)}
            <tr>
              <td><a href={href(`/knowledge-articles/${row.id}`)}>{row.title}</a></td>
              <td>{row.slug}</td>
              <td>{formatDate(row.updatedDateUtc)}</td>
              <td>{formatDate(row.insertDateUtc)}</td>
              <td>
                <div class="actions">
                  <a class="btn btn-secondary" href={href(`/knowledge-articles/${row.id}/update`)}>Edit</a>
                  <button type="button" class="btn btn-danger" onclick={() => openDelete(row)}>Delete</button>
                </div>
              </td>
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

{#if deleteOpen && deleteTarget}
  <div class="modal-backdrop" role="presentation">
    <div class="modal" role="dialog" aria-modal="true" aria-labelledby="delete-title">
      <h2 id="delete-title">Delete knowledge article</h2>
      <p>Delete <strong>{deleteTarget.title}</strong>?</p>

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
