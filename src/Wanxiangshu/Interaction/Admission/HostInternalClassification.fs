namespace Wanxiangshu.Interaction.Admission

open System

/// HNI-09: the product's rule for telling a Host-generated message from a
/// human-authored one.
///
/// The OpenCode ingress codec used to hold this rule inline: four raw fields
/// are or-ed into "this is a compaction", and "every part carries the host's
/// synthetic flag" is turned into "this message is Host-internal". Which raw
/// fields count as a compaction source is the product's reading of the host
/// wire, so it lives here; the adapter keeps the decoding and asks.
[<RequireQualifiedAccess>]
module HostInternalClassification =

    /// The raw facts one host message carries, read from the wire.
    ///
    /// A record rather than a tuple, so the facts cannot silently swap at a
    /// call site.
    type HostMessageFacts =
        {
            /// A part declares itself a compaction part.
            HasCompactionPart: bool
            /// The message carries a summary.
            HasSummary: bool
            /// The message's agent is the host's compaction agent.
            AgentIsCompaction: bool
            /// The message's mode is the host's compaction mode.
            ModeIsCompaction: bool
            /// The message has at least one part.
            HasParts: bool
            /// Every part of the message carries the host's synthetic flag.
            AllPartsSynthetic: bool
        }

    /// The facts of a message with no parts.
    let emptyFacts: HostMessageFacts =
        { HasCompactionPart = false
          HasSummary = false
          AgentIsCompaction = false
          ModeIsCompaction = false
          HasParts = false
          AllPartsSynthetic = false }

    /// Whether this message is a host compaction pseudo-run.
    ///
    /// Four sources, or-ed: a compaction part, a summary, the compaction agent
    /// and the compaction mode. No source discrimination — a user's `/compact`
    /// and an unexpected host compaction get identical handling.
    let isCompaction (facts: HostMessageFacts) : bool =
        facts.HasCompactionPart
        || facts.HasSummary
        || facts.AgentIsCompaction
        || facts.ModeIsCompaction

    /// Whether this message is Host-internal: a compaction, or material the
    /// host generated outright.
    ///
    /// Only material the Host generated outright is HostInternal. A real user
    /// message still carries host-injected synthetic echoes (for example the
    /// TUI's @-file read output) next to its own parts; treating that mixture
    /// as synthetic would let user-authored content bypass managed admission
    /// and arrive at the provider with the uncontrolled TUI model selection
    /// still attached. So the synthetic judgement needs *every* part to be
    /// synthetic, and needs at least one part to exist.
    let isHostInternal (facts: HostMessageFacts) : bool =
        isCompaction facts || (facts.HasParts && facts.AllPartsSynthetic)

    /// Whether this message is material the host generated outright.
    ///
    /// The synthetic judgement needs *every* part to be synthetic, and needs
    /// at least one part to exist: a message with no parts carries no
    /// synthetic material at all.
    let isHostSynthetic (facts: HostMessageFacts) : bool =
        facts.HasParts && facts.AllPartsSynthetic

    /// Whether a part is opening content: a text part the host did not
    /// synthesize.
    ///
    /// The opening is the first task prompt; a synthetic part (tool result,
    /// metadata, guidance) is not it.
    let isOpeningPart (isSynthetic: bool) (kind: string option) : bool =
        not isSynthetic
        && (match kind with
            | Some kind -> kind.Equals("text", StringComparison.OrdinalIgnoreCase)
            | None -> false)

    /// The text of a part, when it is opening content.
    let openingTextOfPart (isSynthetic: bool) (kind: string option) (text: string option) : string option =
        if isOpeningPart isSynthetic kind then text else None
