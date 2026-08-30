# Wallets Hub security model

- Organization data is isolated by mandatory `OrganizationId` predicates.
- Platform administrators manage organization lifecycle but do not have receipt-reading endpoints.
- Employee receipt queries additionally require an assigned-wallet record and enforce the employee's history limit.
- Android devices authenticate with random device tokens stored only as SHA-256 hashes server-side.
- Pairing codes expire after ten minutes and are single-use.
- Receipt messages are encrypted with ASP.NET Core Data Protection before persistence.
- Provider references and device fingerprints provide duplicate protection.
- Authentication cookies are Secure, HTTP-only, SameSite Strict, and use sliding one-year expiration.
- Device deactivation immediately invalidates future capture requests.
- Receipt reviews and administrative changes create audit events.

Production must persist Data Protection keys, terminate TLS, keep PostgreSQL private, rotate platform credentials, and back up the dedicated database.
