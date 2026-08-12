<script lang="ts">
  import { onDestroy } from "svelte";
  import { getApiUrl } from "../../helpers/api";
  import {
    getFieldErrors,
    getGeneralError,
    parseResponse,
    type MyErrorResponse,
  } from "../../helpers/parseResponse";
  import { href, navigate } from "../../helpers/router";

  type Props = {
    id: string;
  };

  let { id }: Props = $props();

  type KnowledgeArticle = {
    id: string;
    title: string;
    slug: string;
    content: string;
    insertDateUtc: string;
    updatedDateUtc: string;
    concurrencyKey: string;
  };

  let pageLoading = $state<"loading" | "done" | "error">("loading");
  let loadError = $state("");
  let pageTitle = $state("Update knowledge article");
  let concurrencyKey = $state("");
  let abortController: AbortController | null = null;
  let lastLoadedId = $state("");

  let form = $state({
    title: "",
    slug: "",
    content: "",
  });

  let validations = $state({
    title: { touched: false, valid: true, errorMessage: "" },
    slug: { touched: false, valid: true, errorMessage: "" },
    content: { touched: false, valid: true, errorMessage: "" },
  });

  let formDisabled = $state(false);
  let formError = $state("");
  let concurrencyAlert = $state("");
  let concurrencyAdditionalData = $state("");
  let concurrencyCurrent = $state<KnowledgeArticle | null>(null);

  function buildBreadCrumbsAndPageTitle(entity: KnowledgeArticle): void {
    pageTitle = `Update ${entity.title}`;
  }

  function clearErrors(): void {
    validations.title = { touched: false, valid: true, errorMessage: "" };
    validations.slug = { touched: false, valid: true, errorMessage: "" };
    validations.content = { touched: false, valid: true, errorMessage: "" };
    formError = "";
  }

  function validate(setTouched: boolean): boolean {
    let ok = true;

    const title = form.title.trim();
    if (!title) {
      validations.title = {
        touched: setTouched || validations.title.touched,
        valid: false,
        errorMessage: "Title is required.",
      };
      ok = false;
    } else {
      validations.title = {
        touched: setTouched || validations.title.touched,
        valid: true,
        errorMessage: "",
      };
    }

    const slug = form.slug.trim();
    if (!slug) {
      validations.slug = {
        touched: setTouched || validations.slug.touched,
        valid: false,
        errorMessage: "Slug is required.",
      };
      ok = false;
    } else {
      validations.slug = {
        touched: setTouched || validations.slug.touched,
        valid: true,
        errorMessage: "",
      };
    }

    const content = form.content.trim();
    if (!content) {
      validations.content = {
        touched: setTouched || validations.content.touched,
        valid: false,
        errorMessage: "Content is required.",
      };
      ok = false;
    } else {
      validations.content = {
        touched: setTouched || validations.content.touched,
        valid: true,
        errorMessage: "",
      };
    }

    return ok;
  }

  function applyBackendErrors(error: MyErrorResponse): void {
    const titleError = getFieldErrors(error, "title");
    if (titleError) {
      validations.title = { touched: true, valid: false, errorMessage: titleError };
    }

    const slugError = getFieldErrors(error, "slug");
    if (slugError) {
      validations.slug = { touched: true, valid: false, errorMessage: slugError };
    }

    const contentError = getFieldErrors(error, "content");
    if (contentError) {
      validations.content = { touched: true, valid: false, errorMessage: contentError };
    }

    formError = getGeneralError(error);
  }

  async function loadData(): Promise<void> {
    if (abortController) {
      abortController.abort();
    }
    abortController = new AbortController();
    const currentController = abortController;

    pageLoading = "loading";
    loadError = "";
    concurrencyAlert = "";
    concurrencyAdditionalData = "";
    concurrencyCurrent = null;

    try {
      const response = await fetch(`${getApiUrl()}/knowledgeArticles/get/${id}`, {
        method: "GET",
        headers: { Accept: "application/json" },
        signal: currentController.signal,
      });

      const parsed = await parseResponse<KnowledgeArticle | MyErrorResponse>(response);

      if (!parsed.ok) {
        pageLoading = "error";
        loadError = getGeneralError(parsed.data as MyErrorResponse);
        return;
      }

      const entity = parsed.data as KnowledgeArticle;
      form = {
        title: entity.title ?? "",
        slug: entity.slug ?? "",
        content: entity.content ?? "",
      };
      concurrencyKey = entity.concurrencyKey;
      buildBreadCrumbsAndPageTitle(entity);
      pageLoading = "done";
      lastLoadedId = id;
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        return;
      }
      pageLoading = "error";
      loadError = "Unable to load knowledge article.";
    }
  }

  async function handleSubmit(): Promise<void> {
    if (formDisabled || pageLoading !== "done") {
      return;
    }

    clearErrors();
    if (!validate(true)) {
      return;
    }

    formDisabled = true;
    formError = "";

    try {
      const response = await fetch(`${getApiUrl()}/knowledgeArticles/update`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          id,
          title: form.title.trim(),
          slug: form.slug.trim(),
          content: form.content.trim(),
          concurrencyKey,
        }),
      });

      const parsed = await parseResponse<KnowledgeArticle | MyErrorResponse>(response);

      if (!parsed.ok) {
        const error = parsed.data as MyErrorResponse;
        if (error?.concurrencyKeyInvalid) {
          concurrencyAlert = getGeneralError(error);
          concurrencyAdditionalData = error.additionalData ?? "";
          concurrencyCurrent = null;
          if (error.additionalData) {
            try {
              const current = JSON.parse(error.additionalData) as KnowledgeArticle;
              concurrencyCurrent = current;
              if (current.concurrencyKey) {
                concurrencyKey = current.concurrencyKey;
              }
            } catch {
              // keep key
            }
          }
          return;
        }

        applyBackendErrors(error);
        return;
      }

      navigate(`/knowledge-articles/${id}`);
    } catch {
      formError = "Unable to update knowledge article.";
    } finally {
      formDisabled = false;
    }
  }

  function handleKeyDown(event: KeyboardEvent): void {
    if (event.repeat) {
      return;
    }
    if (event.altKey || event.metaKey || !event.ctrlKey) {
      return;
    }
    if (event.code !== "KeyS") {
      return;
    }
    event.preventDefault();
    void handleSubmit();
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
    <a href={href("/knowledge-articles")}>Knowledge articles</a>
    <span>/</span>
    <a href={href(`/knowledge-articles/${id}`)}>{pageTitle.replace(/^Update\s+/, "") || "Detail"}</a>
    <span>/</span>
    <span>Update</span>
  </div>

  {#if pageLoading === "loading"}
    <p class="muted">Loading…</p>
  {:else if pageLoading === "error"}
    <div class="alert alert-error">{loadError}</div>
    <button type="button" class="btn btn-secondary" onclick={loadData}>Retry</button>
  {:else}
    <div class="page-heading">
      <div>
        <h1>{pageTitle}</h1>
        <p>Update article fields and save with Ctrl + S.</p>
      </div>
    </div>

    {#if concurrencyAlert}
      <div class="alert alert-warn">
        {concurrencyAlert}
        {#if concurrencyCurrent}
          <dl class="dl" style="margin-top: 0.75rem;">
            <div class="dl-item">
              <dt>Current title</dt>
              <dd class={concurrencyCurrent.title !== form.title ? "changed" : ""}>{concurrencyCurrent.title}</dd>
            </div>
            <div class="dl-item">
              <dt>Current slug</dt>
              <dd class={concurrencyCurrent.slug !== form.slug ? "changed" : ""}>{concurrencyCurrent.slug}</dd>
            </div>
            <div class="dl-item">
              <dt>Current content</dt>
              <dd class={concurrencyCurrent.content !== form.content ? "changed" : ""}>{concurrencyCurrent.content}</dd>
            </div>
          </dl>
        {:else if concurrencyAdditionalData}
          <pre>{concurrencyAdditionalData}</pre>
        {/if}
        <p>The concurrency key was refreshed. Submit again to overwrite with your values.</p>
      </div>
    {/if}

    {#if formError}
      <div class="alert alert-error">{formError}</div>
    {/if}

    <form
      onsubmit={(event) => {
        event.preventDefault();
      }}
    >
      <div class={"field" + (!validations.title.valid && validations.title.touched ? " error" : "")}>
        <label for="title">Title</label>
        <input id="title" type="text" bind:value={form.title} disabled={formDisabled} />
        {#if !validations.title.valid && validations.title.touched}
          <div class="field-error">{validations.title.errorMessage}</div>
        {/if}
      </div>

      <div class={"field" + (!validations.slug.valid && validations.slug.touched ? " error" : "")}>
        <label for="slug">Slug</label>
        <input id="slug" type="text" bind:value={form.slug} disabled={formDisabled} />
        {#if !validations.slug.valid && validations.slug.touched}
          <div class="field-error">{validations.slug.errorMessage}</div>
        {/if}
      </div>

      <div class={"field" + (!validations.content.valid && validations.content.touched ? " error" : "")}>
        <label for="content">Content</label>
        <textarea id="content" bind:value={form.content} disabled={formDisabled} style="min-height: 14rem;"></textarea>
        {#if !validations.content.valid && validations.content.touched}
          <div class="field-error">{validations.content.errorMessage}</div>
        {/if}
      </div>

      <div class="btn-row">
        <button type="button" class="btn btn-primary btn-with-tooltip" disabled={formDisabled} onclick={handleSubmit}>
          {concurrencyAlert ? "Retry save" : "Save"}
          <span class="tooltip">Ctrl + S</span>
        </button>
        <a class="btn btn-secondary" href={href(`/knowledge-articles/${id}`)}>Cancel</a>
      </div>
    </form>
  {/if}
</div>
