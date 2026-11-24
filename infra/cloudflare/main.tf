# Cloudflare pieces that aren't Worker configuration (that is server/cloudflare.config.ts): the domain's DNS-level
# settings, the update bucket's address, and caching. infra/cloudflare/README.md says how to run it.

# The account's IDs come from the environment, never from this repository (docs/specs/private-settings.md): tf.ps1
# sets each TF_VAR_ from its EQLP_CF_ private setting and stops, naming it, when one is missing. They are IDs, not
# secrets, so they aren't marked sensitive (an import block's id won't take a sensitive value).
variable "account_id" {
  type        = string
  nullable    = false
  description = "The Cloudflare account's ID (EQLP_CF_ACCOUNT_ID)."
}

variable "zone_id" {
  type        = string
  nullable    = false
  description = "The eqlpathfinder.com zone's ID (EQLP_CF_ZONE_ID). Registered 11-17-2026 through Cloudflare Registrar."
}

variable "cache_ruleset_id" {
  type        = string
  nullable    = false
  description = "The zone's cache-settings ruleset, for imports.tf (EQLP_CF_CACHE_RULESET_ID)."
}

variable "redirects_ruleset_id" {
  type        = string
  nullable    = false
  description = "The zone's redirects ruleset, for imports.tf (EQLP_CF_REDIRECTS_RULESET_ID)."
}

locals {
  account_id     = var.account_id
  zone_id        = var.zone_id
  updates_host   = "updates.eqlpathfinder.com"
  updates_bucket = "eql-pathfinder-updates" # tools/publish.ps1 uploads releases here; the NAS job writes community/v1.json
}

# The installer, the update feed and the community numbers, at their own address (the bucket's r2.dev address keeps
# working for copies from before the move).
resource "cloudflare_r2_custom_domain" "updates" {
  account_id  = local.account_id
  bucket_name = local.updates_bucket
  domain      = local.updates_host
  zone_id     = local.zone_id
  enabled     = true
  min_tls     = "1.2"
}

# A release overwrites some files under the same name: the update indexes (releases.*.json, RELEASES*), the installers
# and portable zips, and community/v1.json every 30 minutes. Those must never come from cache, or someone would get
# the last version's installer. Versioned update packages (*.nupkg) never change, so they may be cached.
resource "cloudflare_ruleset" "cache" {
  zone_id = local.zone_id
  name    = "Cache settings"
  kind    = "zone"
  phase   = "http_request_cache_settings"
  rules = [{
    description       = "updates: never cache the files a release or the community job replaces"
    expression        = "(http.host eq \"${local.updates_host}\" and (http.request.uri.path.extension in {\"json\" \"exe\" \"zip\"} or http.request.uri.path in {\"/RELEASES\" \"/RELEASES-beta\"}))"
    action            = "set_cache_settings"
    action_parameters = { cache = false }
    enabled           = true
  }]
}

# Signed DNS: with Cloudflare as registrar, the DS record reaches the .com registry by itself.
resource "cloudflare_zone_dnssec" "this" {
  zone_id = local.zone_id
  status  = "active"
}

# www.eqlpathfinder.com → eqlpathfinder.com, keeping the path and query. The site Worker (site/cloudflare.config.ts) also
# claims www so it has a certificate and DNS record; this rule runs before it, so www only ever redirects.
resource "cloudflare_ruleset" "redirects" {
  zone_id = local.zone_id
  name    = "Redirects"
  kind    = "zone"
  phase   = "http_request_dynamic_redirect"
  rules = [{
    description = "www to the bare domain"
    expression  = "(http.host eq \"www.eqlpathfinder.com\")"
    action      = "redirect"
    action_parameters = {
      from_value = {
        status_code           = 301
        target_url            = { expression = "concat(\"https://eqlpathfinder.com\", http.request.uri.path)" }
        preserve_query_string = true
      }
    }
    enabled = true
  }]
}

# Plain http:// on any eqlpathfinder.com address redirects to https:// (the app only ever uses https).
resource "cloudflare_zone_setting" "always_use_https" {
  zone_id    = local.zone_id
  setting_id = "always_use_https"
  value      = "on"
}
