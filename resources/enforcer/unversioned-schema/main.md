# unversioned-schema — Main

## What To Do Now
Add a schema version field or equivalent. Define compatibility (backward/forward) and migration/upcast rules. Version bump on breaking changes.

## Repair Strategy
Inventory durable shapes. Introduce versions. Write upcasters for old data. Reject unknown future versions fail-closed or per policy.

## Decision Branches
If dual-writing during migration, bound the window and remove it. If hashes identify format, document the mapping as versioning.

## Wrong Fixes
Silently changing field meaning. Relying on "try parse old else new" without tests. Shipping cache blobs that cannot be distinguished across versions.

## Verification
Old payloads still load under the policy; breaking changes require a new version and tested migration.

## Done When
Durable contracts carry explicit versions and a documented compatibility policy.

## Scope and Authority
Persisted and cross-process contracts.
