module UnifiedStore.SchemaVersionFixture

/// Phase 1 RED fixture (§36): schemaVersion on a durable event/store envelope.
/// Additive vocabulary only — event/store protocol versioning is forbidden.
type EventEnvelope =
    { EventId: string
      EventType: string
      schemaVersion: int
      Parents: string list }
