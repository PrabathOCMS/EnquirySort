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
  let pageTitle = $state("Update mailing list");
  let concurrencyKey = $state("");
  let abortController: AbortController | null = null;
  let lastLoadedId = $state("");

  let form = $state({
    name: "",
    address: "",
    description: "",
  });

  let validations = $state({
    name: { touched: false, valid: true, errorMessage: "" },
    address: { touched: false, valid: true, errorMessage: "" },
    description: { touched: false, valid: true, errorMessage: "" },
  });

  let formDisabled = $state(false);
  let formError = $state("");
  let concurrencyAlert = $state("");
  let concurrencyAdditionalData = $state("");
  let concurrencyCurrent = $state<MailingList | null>(null);

  function buildBreadCrumbsAndPageTitle(entity: MailingList): void {
    pageTitle = `Update ${entity.name}`;
  }

  function clearErrors(): void {
    validations.name = { touched: false, valid: true, errorMessage: "" };
    validations.address = { touched: false, valid: true, errorMessage: "" };
    validations.description = { touched: false, valid: true, errorMessage: "" };
    formError = "";
  }

  function validate(setTouched: boolean): boolean {
    let ok = true;

    const name = form.name.trim();
    if (!name) {
      validations.name = {
        touched: setTouched || validations.name.touched,
        valid: false,
        errorMessage: "Name is required.",
      };
      ok = false;
    } else if (name.length > 100) {
      validations.name = {
        touched: setTouched || validations.name.touched,
        valid: false,
        errorMessage: "Name must be 100 characters or less.",
      };
      ok = false;
    } else {
      validations.name = {
        touched: setTouched || validations.name.touched,
        valid: true,
        errorMessage: "",
      };
    }

    const address = form.address.trim();
    if (!address) {
      validations.address = {
        touched: setTouched || validations.address.touched,
        valid: false,
        errorMessage: "Address is required.",
      };
      ok = false;
    } else if (address.length > 320) {
      validations.address = {
        touched: setTouched || validations.address.touched,
        valid: false,
        errorMessage: "Address must be 320 characters or less.",
      };
      ok = false;
    } else {
      validations.address = {
        touched: setTouched || validations.address.touched,
        valid: true,
        errorMessage: "",
      };
    }

    const description = form.description.trim();
    if (description.length > 500) {
      validations.description = {
        touched: setTouched || validations.description.touched,
        valid: false,
        errorMessage: "Description must be 500 characters or less.",
      };
      ok = false;
    } else {
      validations.description = {
        touched: setTouched || validations.description.touched,
        valid: true,
        errorMessage: "",
      };
    }

    return ok;
  }

  function applyBackendErrors(error: MyErrorResponse): void {
    const nameError = getFieldErrors(error, "name");
    if (nameError) {
      validations.name = { touched: true, valid: false, errorMessage: nameError };
    }

    const addressError = getFieldErrors(error, "address");
    if (addressError) {
      validations.address = { touched: true, valid: false, errorMessage: addressError };
    }

    const descriptionError = getFieldErrors(error, "description");
    if (descriptionError) {
      validations.description = {
        touched: true,
        valid: false,
        errorMessage: descriptionError,
      };
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

      const entity = parsed.data as MailingList;
      form = {
        name: entity.name ?? "",
        address: entity.address ?? "",
        description: entity.description ?? "",
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
      loadError = "Unable to load mailing list.";
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
      const response = await fetch(`${getApiUrl()}/mailingLists/update`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          id,
          name: form.name.trim(),
          address: form.address.trim(),
          description: form.description.trim() || null,
          concurrencyKey,
        }),
      });

      const parsed = await parseResponse<MailingList | MyErrorResponse>(response);

      if (!parsed.ok) {
        const error = parsed.data as MyErrorResponse;
        if (error?.concurrencyKeyInvalid) {
          concurrencyAlert = getGeneralError(error);
          concurrencyAdditionalData = error.additionalData ?? "";
          concurrencyCurrent = null;
          if (error.additionalData) {
            try {
              const current = JSON.parse(error.additionalData) as MailingList;
              concurrencyCurrent = current;
              if (current.concurrencyKey) {
                concurrencyKey = current.concurrencyKey;
              }
            } catch {
              // keep key when payload is not JSON
            }
          }
          return;
        }

        applyBackendErrors(error);
        return;
      }

      navigate(`/mailing-lists/${id}`);
    } catch {
      formError = "Unable to update mailing list.";
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
    <a href={href("/")}>Mailing lists</a>
    <span>/</span>
    <a href={href(`/mailing-lists/${id}`)}>{pageTitle.replace(/^Update\s+/, "") || "Detail"}</a>
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
        <p>Update mailing list fields and save with Ctrl + S.</p>
      </div>
    </div>

    {#if concurrencyAlert}
      <div class="alert alert-warn">
        {concurrencyAlert}
        {#if concurrencyCurrent}
          <dl class="dl" style="margin-top: 0.75rem;">
            <div class="dl-item">
              <dt>Current name</dt>
              <dd class={concurrencyCurrent.name !== form.name ? "changed" : ""}>{concurrencyCurrent.name}</dd>
            </div>
            <div class="dl-item">
              <dt>Current address</dt>
              <dd class={concurrencyCurrent.address !== form.address ? "changed" : ""}>{concurrencyCurrent.address}</dd>
            </div>
            <div class="dl-item">
              <dt>Current description</dt>
              <dd class={(concurrencyCurrent.description || "") !== form.description.trim() ? "changed" : ""}>
                {concurrencyCurrent.description || "—"}
              </dd>
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
      <div class={"field" + (!validations.name.valid && validations.name.touched ? " error" : "")}>
        <label for="name">Name</label>
        <input id="name" type="text" bind:value={form.name} disabled={formDisabled} />
        {#if !validations.name.valid && validations.name.touched}
          <div class="field-error">{validations.name.errorMessage}</div>
        {/if}
      </div>

      <div class={"field" + (!validations.address.valid && validations.address.touched ? " error" : "")}>
        <label for="address">Address</label>
        <input id="address" type="email" bind:value={form.address} disabled={formDisabled} />
        {#if !validations.address.valid && validations.address.touched}
          <div class="field-error">{validations.address.errorMessage}</div>
        {/if}
      </div>

      <div class={"field" + (!validations.description.valid && validations.description.touched ? " error" : "")}>
        <label for="description">Description</label>
        <textarea id="description" bind:value={form.description} disabled={formDisabled}></textarea>
        {#if !validations.description.valid && validations.description.touched}
          <div class="field-error">{validations.description.errorMessage}</div>
        {/if}
      </div>

      <div class="btn-row">
        <button type="button" class="btn btn-primary btn-with-tooltip" disabled={formDisabled} onclick={handleSubmit}>
          {concurrencyAlert ? "Retry save" : "Save"}
          <span class="tooltip">Ctrl + S</span>
        </button>
        <a class="btn btn-secondary" href={href(`/mailing-lists/${id}`)}>Cancel</a>
      </div>
    </form>
  {/if}
</div>
