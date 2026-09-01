namespace Wanxiangshu.Enforcer.Cycle

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Enforcer.EnforcerCodec

module EnforcerCycle =

    type ContentBounds = { TextBytes: int; EvidenceBytes: int }

    [<RequireQualifiedAccess>]
    type ContentBoundsRejection =
        | TextTooLarge
        | EvidenceTooLarge

    type CanonicalCycle =
        { MergedText: string
          CanonicalTip: EnforcerTip
          MergedEvidence: string }

    [<Literal>]
    val MaxBlogTextBytes: int = 512 * 1024

    [<Literal>]
    val MaxEvidenceBytes: int = 128 * 1024

    val ofCall: Wanxiangshu.Enforcer.EnforcerCodec.CanonicalBlogCall -> CanonicalCycle
    val isValidCycle: CanonicalCycle -> bool
    val contentBoundsError: ContentBoundsRejection -> string
    val validateContentBounds: (string -> int) -> string -> string -> Result<ContentBounds, ContentBoundsRejection>
