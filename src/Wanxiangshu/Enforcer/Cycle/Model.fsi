namespace Wanxiangshu.Enforcer.Cycle

open Wanxiangshu.Enforcer
open Wanxiangshu.Participant.Provider

module EnforcerCycle =

    type ContentBounds =
        { TextBytes: int
          TipSectionsBytes: int
          EvidenceBytes: int }

    [<RequireQualifiedAccess>]
    type ContentBoundsRejection =
        | TextTooLarge
        | TipSectionsTooLarge
        | EvidenceTooLarge

    type CanonicalCycle =
        { MergedText: string
          TipName: string
          TipSectionsText: string
          MergedEvidence: string }

    [<Literal>]
    val MaxBlogTextBytes: int = 512 * 1024

    [<Literal>]
    val MaxEvidenceBytes: int = 128 * 1024

    val renderTipSections: language: ProviderLanguage -> sections: string list -> string
    val ofCall: Wanxiangshu.Enforcer.EnforcerCodec.CanonicalBlogCall -> CanonicalCycle
    val isValidCycle: CanonicalCycle -> bool
    val contentBoundsError: ContentBoundsRejection -> string

    val validateContentBounds:
        (string -> int) -> string -> string -> string -> Result<ContentBounds, ContentBoundsRejection>
