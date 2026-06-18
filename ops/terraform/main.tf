terraform {
  required_version = ">= 1.5"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }

  # Remote state. Create the bucket once before `terraform init`:
  #   gsutil mb -l <region> gs://clashup-terraform-state
  backend "gcs" {
    bucket = "clashup-terraform-state"
    prefix = "terraform/state"
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

locals {
  # Artifact Registry image repositories (no tag).
  registry_host   = "${var.region}-docker.pkg.dev"
  registry_base   = "${local.registry_host}/${var.project_id}/${google_artifact_registry_repository.docker.repository_id}"
  gateway_image   = "${local.registry_base}/clashup-gateway:${var.gateway_image_version}"
  services_repo   = "${local.registry_base}/clashup-services"
  gameserver_repo = "${local.registry_base}/clashup-gameserver"
}
