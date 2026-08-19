# Changelog

All notable changes follow [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and semantic versioning.

## [Unreleased]

## [0.1.0] - 2026-08-18

### Added

- Local webhook endpoints, SQLite capture, live SignalR updates, inspection, filtering, comparison, replay, import, and export.
- Configurable responses and retention limits.
- English and Simplified Chinese interface.
- SSRF-safe replay defaults, health endpoints, Docker configuration, and self-contained release packaging.

### Security

- Private, loopback, link-local, multicast, unspecified, credential-bearing, and non-HTTP replay targets are blocked by default.
- Sensitive headers are excluded from replay and exports by default.
