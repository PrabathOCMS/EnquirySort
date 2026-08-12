<script lang="ts">
  import { getApiUrl } from "../../helpers/api";
  import {
    getFieldErrors,
    getGeneralError,
    parseResponse,
    type MyErrorResponse,
  } from "../../helpers/parseResponse";
  import { href, navigate } from "../../helpers/router";

  type KnowledgeArticle = {
    id: string;
    title: string;
    slug: string;
    content: string;
  };

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

  async function handleSubmit(): Promise<void> {
    if (formDisabled) {
      return;
    }

    clearErrors();
    if (!validate(true)) {
      return;
    }

    formDisabled = true;
    formError = "";

    try {
      const response = await fetch(`${getApiUrl()}/knowledgeArticles/create`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          title: form.title.trim(),
          slug: form.slug.trim(),
          content: form.content.trim(),
        }),
      });

      const parsed = await parseResponse<KnowledgeArticle | MyErrorResponse>(response);

      if (!parsed.ok) {
        applyBackendErrors(parsed.data as MyErrorResponse);
        return;
      }

      const created = parsed.data as KnowledgeArticle;
      navigate(`/knowledge-articles/${created.id}`);
    } catch {
      formError = "Unable to create knowledge article.";
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
</script>

<svelte:window onkeydown={handleKeyDown} />

<div class="page-card">
  <div class="breadcrumbs">
    <a href={href("/knowledge-articles")}>Knowledge articles</a>
    <span>/</span>
    <span>Create</span>
  </div>

  <div class="page-heading">
    <div>
      <h1>Create knowledge article</h1>
      <p>Add reusable content for automated replies.</p>
    </div>
  </div>

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
      <div class="hint">URL-safe identifier, for example welcome-hours.</div>
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
        Save
        <span class="tooltip">Ctrl + S</span>
      </button>
      <a class="btn btn-secondary" href={href("/knowledge-articles")}>Cancel</a>
    </div>
  </form>
</div>
