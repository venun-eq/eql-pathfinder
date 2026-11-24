# Cloudflare: domain, update address, caching

Terraform for the Cloudflare pieces that aren't Worker configuration. The website is `site/` (its own Worker); the API Worker, its database bindings and its
`api.eqlpathfinder.com` address are in `server/cloudflare.config.ts` (deployed with `cf`); code signing is Azure
(`infra/azure`). Each tool owns one area.

| What | Resource |
|---|---|
| `updates.eqlpathfinder.com` → the `eql-pathfinder-updates` R2 bucket (installer, update feed, community numbers) | `cloudflare_r2_custom_domain.updates` |
| Never cache the files a release or the community job overwrites (`*.json`, `*.exe`, `*.zip`, `RELEASES*`) | `cloudflare_ruleset.cache` |
| DNSSEC (Cloudflare is the registrar, so the DS record reaches the registry by itself) | `cloudflare_zone_dnssec.this` |
| `www.eqlpathfinder.com` → `eqlpathfinder.com`, same path and query (301) | `cloudflare_ruleset.redirects` |
| `http://` → `https://` on every address | `cloudflare_zone_setting.always_use_https` |

The domain itself was bought in the Cloudflare dashboard (registrations can't be done through the API) on 11-17-2026:
auto-renew on, contact details redacted, transfer lock on, expires 11-17-2027.

## Running it

```powershell
.\infra\cloudflare\tf.ps1 init      # once per PC
.\infra\cloudflare\tf.ps1 plan
.\infra\cloudflare\tf.ps1 apply
```

`tf.ps1` signs in as whoever is signed in to the `cf` CLI (`npx cf auth login` in `server/`): it refreshes that login and
passes its token to Terraform for the one run, never printing or storing it. No separate API key exists.

The account's, the zone's and the two rulesets' IDs aren't in this repository. They're four variables with no default
(`main.tf`), and `imports.tf` builds its ids from them. `tf.ps1` loads the private settings (`tools\private.example.env`
has the names; docs/pipeline.md, "Private settings"), stops before Terraform starts, naming it, when an `EQLP_CF_*` one
is missing, and sets `TF_VAR_account_id`, `TF_VAR_zone_id`, `TF_VAR_cache_ruleset_id` and `TF_VAR_redirects_ruleset_id`
for the one run. A CI service sets the same four `EQLP_CF_*` names as secret variables and needs no file.

State is local (`terraform.tfstate`, not in git). It doesn't go in the update bucket because that bucket is public.
`imports.tf` lets a fresh state adopt the rulesets, the HTTPS setting and DNSSEC from Cloudflare; the R2 custom domain can't be
imported, so after a lost state remove it in the dashboard (R2 → the bucket → Settings → Custom domains) and apply.

DNSSEC shows "pending" for a while after it is turned on, until the registry publishes the key; until then `plan` offers
to set it to active, which changes nothing.
