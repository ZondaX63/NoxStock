# Frontend Performance Budget

Target host: Oracle VM, 1 OCPU / 6 GB RAM. These are private-beta budgets, not
claims about the current production result.

| Metric | Warm p75 budget | Cold p75 budget |
|---|---:|---:|
| TTFB | 500 ms | 900 ms |
| FCP | 1.2 s | 2.0 s |
| LCP | 2.0 s | 3.0 s |
| CLS | 0.10 | 0.10 |
| Long tasks | 2 | 5 |
| Page transfer | 700 KB | 1.5 MB |
| Requests per navigation | 35 | 55 |

## Target-VM capture

1. Sign in normally, export an authenticated Netscape-format cookie jar, and run:

   ```bash
   NOXSTOCK_BASE_URL=https://your-host \
   NOXSTOCK_TENANT_PATH=/t/your-tenant \
   NOXSTOCK_COOKIE_FILE=/tmp/noxstock.cookies \
   scripts/performance/measure-navigation-http.sh
   ```

2. In the browser console enable the opt-in Web Vitals capture and reload each of
   Dashboard, Products, POS, Invoices and Reports once cold and five times warm:

   ```js
   localStorage.setItem('noxstock.performance.capture', 'true');
   ```

   The server writes structured `Client performance` events containing TTFB, FCP,
   LCP, CLS, request count, transferred bytes and long-task count. Disable it after
   capture with `localStorage.removeItem('noxstock.performance.capture')`.

3. Pair the browser records with `Server-Timing` and `X-Correlation-Id` headers,
   PostgreSQL slow-query/command-count evidence, and the CSV emitted by the script.

Authenticated HTML and ABP configuration/localization responses must remain private.
Only content-fingerprinted static files receive `public, max-age=31536000, immutable`.
