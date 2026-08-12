# EnquirySort.Web

Svelte (Vite) admin frontend for EnquirySort.

## Setup

```bash
cp .env.example .env
npm install
npm run dev
```

Default API base URL: `http://localhost:5180` (`VITE_API_URL`).

## Scripts

- `npm run dev` — local development server
- `npm run build` — production build
- `npm run preview` — preview production build

## Routes (hash)

- `#/` — mailing lists index
- `#/mailing-lists/create`
- `#/mailing-lists/:id`
- `#/mailing-lists/:id/update`
- `#/knowledge-articles`
- `#/knowledge-articles/create`
- `#/knowledge-articles/:id`
- `#/knowledge-articles/:id/update`
- `#/enquiries`
- `#/enquiries/:id`
