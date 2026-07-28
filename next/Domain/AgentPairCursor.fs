namespace Wanxiangshu.Next.Domain

open System
open Wanxiangshu.Next.Kernel.Identity

/// Pure A/A/B/B fallback cursor. No Dead state; no Fable/Host/Journal dependencies.
[<RequireQualifiedAccess>]
module AgentPairCursor =

    type ModelSide =
        | SideA
        | SideB

    type FallbackCursor =
        { Offset: byte
          LastProviderAttempt: int64 option }

    type AuthorityAgentPair =
        { SelectedAgent: string
          PeerAgent: string }

    type FallbackAttemptIdentity =
        { LogicalRunId: string
          AuthorityRootUserMessageId: string
          ProviderAttempt: string }

    let initial: FallbackCursor =
        { Offset = 0uy
          LastProviderAttempt = None }

    let side (offset: byte) : ModelSide =
        match offset with
        | 0uy
        | 1uy -> SideA
        | 2uy
        | 3uy -> SideB
        | _ -> invalidOp "Fallback offset must be in range 0..3"

    let advance (offset: byte) : byte = byte ((int offset + 1) % 4)

    let advanceCursor (cursor: FallbackCursor) (providerAttempt: int64) : FallbackCursor =
        { Offset = advance cursor.Offset
          LastProviderAttempt = Some providerAttempt }

    let effectiveAgent (authority: AuthorityAgentPair) (cursor: FallbackCursor) : string =
        match side cursor.Offset with
        | SideA -> authority.SelectedAgent
        | SideB -> authority.PeerAgent

    let attemptIdentity
        (logicalRunId: string)
        (authorityRootUserMessageId: string)
        (providerAttempt: string)
        : FallbackAttemptIdentity =
        { LogicalRunId = logicalRunId
          AuthorityRootUserMessageId = authorityRootUserMessageId
          ProviderAttempt = providerAttempt }

    let failureIdentity (identity: FallbackAttemptIdentity) : string =
        String.Concat(
            [| identity.LogicalRunId
               "|"
               identity.AuthorityRootUserMessageId
               "|"
               identity.ProviderAttempt |]
        )

    /// A/A/B/B infinite sequence starting at offset 0 for n steps (0-based index).
    let sideSequence (count: int) : ModelSide list =
        if count < 0 then
            invalidOp "count must be non-negative"
        else
            [ 0 .. count - 1 ] |> List.map (fun i -> side (byte (i % 4)))

    let atOffset (offset: byte) : FallbackCursor =
        { Offset = offset
          LastProviderAttempt = None }
