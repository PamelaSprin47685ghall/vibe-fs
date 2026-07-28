namespace Wanxiangshu.Next.OpenCode

/// Pure EffectiveAgent selection from Authority agent pair + fallback cursor.
/// Does not resolve or return model IDs.
module EffectiveAgentResolver =

    [<RequireQualifiedAccess>]
    type ModelSide =
        | SideA
        | SideB

    type FallbackCursor =
        { Offset: byte
          LastProviderAttempt: int64 option }

    type AuthorityAgentPair =
        { SelectedAgent: string
          PeerAgent: string }

    let initialCursor: FallbackCursor =
        { Offset = 0uy
          LastProviderAttempt = None }

    let side (offset: byte) : ModelSide =
        match offset with
        | 0uy
        | 1uy -> ModelSide.SideA
        | 2uy
        | 3uy -> ModelSide.SideB
        | _ -> invalidOp "Fallback offset must be in range 0..3"

    let advance (offset: byte) : byte = byte ((int offset + 1) % 4)

    let advanceCursor (cursor: FallbackCursor) (providerAttempt: int64) : FallbackCursor =
        { Offset = advance cursor.Offset
          LastProviderAttempt = Some providerAttempt }

    let effectiveAgent (authority: AuthorityAgentPair) (cursor: FallbackCursor) : string =
        match side cursor.Offset with
        | ModelSide.SideA -> authority.SelectedAgent
        | ModelSide.SideB -> authority.PeerAgent

    let effectiveAgentFromManaged (selected: ManagedAgent) (cursor: FallbackCursor) : string =
        let peer = ManagedAgent.peer selected

        effectiveAgent
            { SelectedAgent = selected.Name
              PeerAgent = peer.Name }
            cursor

    /// A/A/B/B infinite sequence starting at offset 0 for n steps (0-based index).
    let sideSequence (count: int) : ModelSide list =
        if count < 0 then
            invalidOp "count must be non-negative"
        else
            [ 0 .. count - 1 ] |> List.map (fun i -> side (byte (i % 4)))

    let cursorOfProjection (offset: byte) (lastProviderAttempt: int64 option) : FallbackCursor =
        { Offset = offset
          LastProviderAttempt = lastProviderAttempt }

    let effectiveAgentAtOffset (authority: AuthorityAgentPair) (offset: byte) : string =
        effectiveAgent
            authority
            { Offset = offset
              LastProviderAttempt = None }
