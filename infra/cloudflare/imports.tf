# If the local state is ever lost, `tf.ps1 plan` adopts these from Cloudflare instead of trying to create them again.
# (cloudflare_r2_custom_domain can't be imported: after a state loss, remove the custom domain in the dashboard and apply.)
# The ids are built from the variables in main.tf (Terraform 1.6 and later take an expression here).
import {
  to = cloudflare_ruleset.cache
  id = "zones/${var.zone_id}/${var.cache_ruleset_id}"
}

import {
  to = cloudflare_zone_dnssec.this
  id = var.zone_id
}

import {
  to = cloudflare_ruleset.redirects
  id = "zones/${var.zone_id}/${var.redirects_ruleset_id}"
}

import {
  to = cloudflare_zone_setting.always_use_https
  id = "${var.zone_id}/always_use_https"
}
