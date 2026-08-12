<script lang="ts">
  import { getApiUrl } from "../../helpers/api";
  import {
    getFieldErrors,
    getGeneralError,
    parseResponse,
    type MyErrorResponse,
  } from "../../helpers/parseResponse";
  import { href, navigate } from "../../helpers/router";

  type MailingList = {
    id: string;
    name: string;
    address: string;
    description?: string | null;
  };

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
      const response = await fetch(`${getApiUrl()}/mailingLists/create`, {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          name: form.name.trim(),
          address: form.address.trim(),
          description: form.description.trim() || null,
        }),
      });

      const parsed = await parseResponse<MailingList | MyErrorResponse>(response);

      if (!parsed.ok) {
        applyBackendErrors(parsed.data as MyErrorResponse);
        return;
      }

      const created = parsed.data as MailingList;
      navigate(`/mailing-lists/${created.id}`);
    } catch {
      formError = "Unable to create mailing list.";
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
    <a href={href("/")}>Mailing lists</a>
    <span>/</span>
    <span>Create</span>
  </div>

  <div class="page-heading">
    <div>
      <h1>Create mailing list</h1>
      <p>Add a destination address EnquirySort can route mail to.</p>
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
        Save
        <span class="tooltip">Ctrl + S</span>
      </button>
      <a class="btn btn-secondary" href={href("/")}>Cancel</a>
    </div>
  </form>
</div>
