<script lang="ts">
  type Props = {
    value: string;
    disabled?: boolean;
    onChange: (html: string) => void;
  };

  let { value, disabled = false, onChange }: Props = $props();

  let editorEl = $state<HTMLDivElement | null>(null);
  let lastApplied = $state("");

  function emitChange(): void {
    if (!editorEl) {
      return;
    }
    const html = editorEl.innerHTML;
    lastApplied = html;
    onChange(html);
  }

  function exec(command: string, commandValue?: string): void {
    if (disabled) {
      return;
    }
    editorEl?.focus();
    document.execCommand(command, false, commandValue);
    emitChange();
  }

  function insertImageFromFile(file: File): void {
    if (disabled) {
      return;
    }
    if (!file.type.startsWith("image/")) {
      return;
    }
    if (file.size > 1_200_000) {
      window.alert("Image is too large. Please use an image under ~1.2 MB.");
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = typeof reader.result === "string" ? reader.result : "";
      if (!dataUrl) {
        return;
      }
      editorEl?.focus();
      document.execCommand(
        "insertHTML",
        false,
        `<img src="${dataUrl}" alt="" style="max-width: 320px; height: auto;" />`,
      );
      emitChange();
    };
    reader.readAsDataURL(file);
  }

  function handlePaste(event: ClipboardEvent): void {
    if (disabled) {
      return;
    }
    const items = event.clipboardData?.items;
    if (!items) {
      return;
    }

    for (const item of items) {
      if (item.type.startsWith("image/")) {
        event.preventDefault();
        const file = item.getAsFile();
        if (file) {
          insertImageFromFile(file);
        }
        return;
      }
    }
  }

  function handleDrop(event: DragEvent): void {
    if (disabled) {
      return;
    }
    const file = event.dataTransfer?.files?.[0];
    if (!file || !file.type.startsWith("image/")) {
      return;
    }
    event.preventDefault();
    insertImageFromFile(file);
  }

  function pickImage(): void {
    if (disabled) {
      return;
    }
    const input = document.createElement("input");
    input.type = "file";
    input.accept = "image/*";
    input.onchange = () => {
      const file = input.files?.[0];
      if (file) {
        insertImageFromFile(file);
      }
    };
    input.click();
  }

  $effect(() => {
    if (!editorEl) {
      return;
    }
    if (value === lastApplied) {
      return;
    }
    if (editorEl.innerHTML !== value) {
      editorEl.innerHTML = value || "";
    }
    lastApplied = value || "";
  });
</script>

<div class="signature-editor" class:disabled>
  <div class="toolbar" role="toolbar" aria-label="Signature formatting">
    <button type="button" class="tool" disabled={disabled} onclick={() => exec("bold")} title="Bold">
      B
    </button>
    <button type="button" class="tool italic" disabled={disabled} onclick={() => exec("italic")} title="Italic">
      I
    </button>
    <button type="button" class="tool" disabled={disabled} onclick={() => exec("underline")} title="Underline">
      U
    </button>
    <button
      type="button"
      class="tool"
      disabled={disabled}
      onclick={() => {
        const url = window.prompt("Link URL");
        if (url) {
          exec("createLink", url);
        }
      }}
      title="Insert link"
    >
      Link
    </button>
    <button type="button" class="tool" disabled={disabled} onclick={pickImage} title="Insert image">
      Image
    </button>
    <button
      type="button"
      class="tool"
      disabled={disabled}
      onclick={() => exec("removeFormat")}
      title="Clear formatting"
    >
      Clear
    </button>
  </div>

  <div
    class="canvas"
    bind:this={editorEl}
    contenteditable={!disabled}
    role="textbox"
    tabindex="0"
    aria-multiline="true"
    aria-label="Email signature"
    data-placeholder="Write your email signature. Paste or drop images here."
    oninput={emitChange}
    onpaste={handlePaste}
    ondrop={handleDrop}
    ondragover={(event) => event.preventDefault()}
  ></div>

  <p class="hint">
    Tip: paste screenshots or logo images directly into the signature. Images are embedded in the
    outgoing email.
  </p>
</div>

<style>
  .signature-editor {
    border: 1px solid color-mix(in srgb, CanvasText 18%, transparent);
    border-radius: 0.65rem;
    overflow: hidden;
    background: Canvas;
  }

  .signature-editor.disabled {
    opacity: 0.7;
  }

  .toolbar {
    display: flex;
    flex-wrap: wrap;
    gap: 0.35rem;
    padding: 0.55rem;
    border-bottom: 1px solid color-mix(in srgb, CanvasText 12%, transparent);
    background: color-mix(in srgb, CanvasText 4%, Canvas);
  }

  .tool {
    border: 1px solid color-mix(in srgb, CanvasText 16%, transparent);
    background: Canvas;
    color: inherit;
    border-radius: 0.4rem;
    padding: 0.25rem 0.55rem;
    font: inherit;
    font-size: 0.85rem;
    font-weight: 700;
    cursor: pointer;
  }

  .tool.italic {
    font-style: italic;
  }

  .tool:disabled {
    cursor: not-allowed;
    opacity: 0.55;
  }

  .canvas {
    min-height: 180px;
    max-height: 420px;
    overflow: auto;
    padding: 0.9rem 1rem;
    line-height: 1.5;
    outline: none;
  }

  .canvas:empty::before {
    content: attr(data-placeholder);
    color: color-mix(in srgb, CanvasText 45%, transparent);
  }

  .canvas :global(img) {
    max-width: min(320px, 100%);
    height: auto;
    display: inline-block;
    vertical-align: middle;
  }

  .hint {
    margin: 0;
    padding: 0.55rem 0.9rem 0.75rem;
    font-size: 0.85rem;
    color: color-mix(in srgb, CanvasText 65%, transparent);
    border-top: 1px solid color-mix(in srgb, CanvasText 10%, transparent);
  }
</style>
