terraform {
  required_version = ">= 1.6"
  required_providers {
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 5.0"
    }
  }
}

# Authenticates with CLOUDFLARE_API_TOKEN, which tf.ps1 sets from the signed-in `cf` CLI for the run only.
provider "cloudflare" {}
