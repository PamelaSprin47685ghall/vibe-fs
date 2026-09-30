namespace Wanxiangshu.Enforcer

module EnforcerCodec =

    type ChronicleRecord =
        { Charge: string
          Occurrence: string
          Settlement: string
          Consequence: string
          Evidence: string option }

    [<RequireQualifiedAccess>]
    type ChronicleContent =
        | Structured of ChronicleRecord
        | Legacy of text: string * evidence: string option

    type CanonicalBlogCall =
        { Content: ChronicleContent
          Tip: EnforcerTip }

    [<Literal>]
    val MissingTipError: string = "missing required argument: tip"

    [<Literal>]
    val MissingChargeError: string = "missing required argument: charge"

    [<Literal>]
    val MissingOccurrenceError: string = "missing required argument: occurrence"

    [<Literal>]
    val MissingSettlementError: string = "missing required argument: settlement"

    [<Literal>]
    val MissingConsequenceError: string = "missing required argument: consequence"

    [<Literal>]
    val MissingChronicleContentError: string = "missing required chronicle content"

    [<Literal>]
    val MixedChronicleProtocolError: string = "structured chronicle fields cannot be mixed with legacy entry/text"

    [<Literal>]
    val InvalidEvidenceError: string = "optional argument evidence must be a string"

    [<Literal>]
    val EvidenceTooLongError: string = "optional argument evidence exceeds 1024 characters"

    [<Literal>]
    val MaxInlineEvidenceChars: int = 1024

    val decodeCall: Wanxiangshu.Enforcer.EnforcerRule list -> Map<string, obj> -> Result<CanonicalBlogCall, string>
    val hasValidText: CanonicalBlogCall -> bool
