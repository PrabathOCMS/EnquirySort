# Connecting a Custom Domain

Growth and Enterprise plans support custom domains.

1. In the site dashboard open **Settings → Domains**
2. Add your domain (example.com) and optional www redirect
3. Create the DNS records we show (usually a CNAME to `sites.oraclecms.com`)
4. Wait for DNS propagation (often under an hour, up to 48 hours)
5. Click **Verify** — SSL certificates are issued automatically

Wildcard domains and multi-region CDN require Enterprise.
