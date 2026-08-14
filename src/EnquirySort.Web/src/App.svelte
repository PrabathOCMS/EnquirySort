<script lang="ts">
  import { onMount } from "svelte";
  import { getRoute, href, type Route } from "./helpers/router";
  import MailingListsIndex from "./pages/mailing-lists/Index.svelte";
  import MailingListsCreate from "./pages/mailing-lists/Create.svelte";
  import MailingListsDetail from "./pages/mailing-lists/Detail.svelte";
  import MailingListsUpdate from "./pages/mailing-lists/Update.svelte";
  import KnowledgeArticlesIndex from "./pages/knowledge-articles/Index.svelte";
  import KnowledgeArticlesCreate from "./pages/knowledge-articles/Create.svelte";
  import KnowledgeArticlesDetail from "./pages/knowledge-articles/Detail.svelte";
  import KnowledgeArticlesUpdate from "./pages/knowledge-articles/Update.svelte";
  import EnquiriesIndex from "./pages/enquiries/Index.svelte";
  import EnquiriesDetail from "./pages/enquiries/Detail.svelte";
  import SettingsIndex from "./pages/settings/Index.svelte";

  let route = $state<Route>(getRoute());

  function refreshRoute(): void {
    route = getRoute();
  }

  onMount(() => {
    if (!window.location.hash) {
      window.location.hash = "#/";
    }

    window.addEventListener("hashchange", refreshRoute);
    return () => {
      window.removeEventListener("hashchange", refreshRoute);
    };
  });

  function navClass(names: Route["name"][]): string {
    if (names.includes(route.name)) {
      return "active";
    }
    return "";
  }
</script>

<div class="app-shell">
  <header class="topbar">
    <div class="brand-block">
      <h1 class="brand"><a href={href("/")}>EnquirySort</a></h1>
      <p class="brand-tag">Admin console for mailing lists, knowledge, and inbox routing.</p>
    </div>
    <nav class="nav" aria-label="Primary">
      <a class={navClass(["mailing-lists", "mailing-lists-create", "mailing-lists-detail", "mailing-lists-update"])} href={href("/")}>Mailing lists</a>
      <a class={navClass(["knowledge-articles", "knowledge-articles-create", "knowledge-articles-detail", "knowledge-articles-update"])} href={href("/knowledge-articles")}>Knowledge</a>
      <a class={navClass(["enquiries", "enquiries-detail"])} href={href("/enquiries")}>Enquiries</a>
      <a class={navClass(["settings"])} href={href("/settings")}>Settings</a>
    </nav>
  </header>

  <main class="page">
    {#if route.name === "mailing-lists"}
      <MailingListsIndex query={route.query} />
    {:else if route.name === "mailing-lists-create"}
      <MailingListsCreate />
    {:else if route.name === "mailing-lists-detail"}
      <MailingListsDetail id={route.id} />
    {:else if route.name === "mailing-lists-update"}
      <MailingListsUpdate id={route.id} />
    {:else if route.name === "knowledge-articles"}
      <KnowledgeArticlesIndex query={route.query} />
    {:else if route.name === "knowledge-articles-create"}
      <KnowledgeArticlesCreate />
    {:else if route.name === "knowledge-articles-detail"}
      <KnowledgeArticlesDetail id={route.id} />
    {:else if route.name === "knowledge-articles-update"}
      <KnowledgeArticlesUpdate id={route.id} />
    {:else if route.name === "enquiries"}
      <EnquiriesIndex query={route.query} />
    {:else if route.name === "enquiries-detail"}
      <EnquiriesDetail id={route.id} />
    {:else if route.name === "settings"}
      <SettingsIndex />
    {:else}
      <div class="page-card">
        <h1>Page not found</h1>
        <p class="muted">That admin route does not exist.</p>
        <p><a href={href("/")}>Back to mailing lists</a></p>
      </div>
    {/if}
  </main>
</div>
