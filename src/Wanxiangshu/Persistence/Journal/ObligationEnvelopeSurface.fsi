namespace Wanxiangshu.Persistence.Journal

open System

/// Legacy-read boundary for the retired obligation-ledger envelope family.
///
/// obligation-ledger-007 keeps old envelopes recognisable for audit and migration.
/// There is no decoder that returns a value: the domain types that would give a
/// decoded fact meaning are gone, so the only honest answer names the retirement.
[<RequireQualifiedAccess>]
module ObligationEnvelopeSurface =
    /// The family an old envelope carries, so a migration reader can tell what it
    /// holds without a decoder that pretends to understand it.
    val legacyFamilyName: string

    val deserializeLegacyEnvelope: encoded: string -> obj
