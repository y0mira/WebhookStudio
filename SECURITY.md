# Security policy

## Reporting

Do not open a public issue for a vulnerability. Use GitHub's private vulnerability reporting feature for this repository. If it is unavailable, contact the repository owner privately through the profile contact method. Include the affected version, reproduction, impact, and suggested mitigation; remove real credentials and captured payloads.

Version `0.1.x` receives security fixes while it is the latest published minor version. Pre-1.0 releases may change APIs with release-note documentation.

Webhook Studio stores raw request bodies and headers that may contain secrets. Protect the database and exports as sensitive files. The application has no authentication and defaults to loopback listening. Private-network replay is blocked by default; enabling it intentionally expands SSRF reach. Do not expose the service directly to the public internet.
