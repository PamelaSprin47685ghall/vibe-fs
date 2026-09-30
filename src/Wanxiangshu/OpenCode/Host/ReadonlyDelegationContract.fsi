namespace Wanxiangshu.OpenCode.Host

open Wanxiangshu.Strength

/// The read-only delegation schema contract and
/// parameter boundary. Owns schema decoration, budget/note field validation,
/// provider argument evidence preservation, and the bilingual investigation outlook
/// prose. It never creates a child session, sends a provider request,
/// computes the batch max, or writes business events.
module ReadonlyDelegationContract =

    /// Idempotently decorates a tool object schema for the read-only
    /// delegation protocol: adds estimated_readonly_rounds (required) and
    /// self_note (optional, never required) to properties of participating tools
    /// (classifyTool = EstimateAfterCall), keeps the original required entries,
    /// additionalProperties and other compatibility constraints, and keeps the
    /// original schema composition structure.
    /// Non-participating and unreviewed tools receive zero increment and
    /// are returned immediately unmodified.
    /// Hosts exposing both parameters and jsonSchema get two consistent
    /// views, including the note's omissibility. Same-name properties are
    /// accepted only when identical to this protocol; a conflicting
    /// property, a non-array required, or a root schema that cannot be
    /// legally extended fails loudly instead of publishing a partial
    /// protocol. The stable bilingual outlook prose 
    /// is appended once to the tool description, selected by the existing
    /// ProviderLanguageBinding preference; it never carries remaining
    /// rounds, random identifiers, prices or timestamps.
    val decorateDefinition: toolInput: obj -> toolOutput: obj -> unit

    /// Hides both protocol fields from the business argument view by saving
    /// their original property descriptors (or undefined) under a private
    /// module Symbol on the args object with enumerable:false,
    /// configurable:true, and deleting the properties. Fails atomically if
    /// the object is not extensible/frozen. Uses its own Symbol key, so the
    /// manager review contract's own saved record is never touched.
    val hide: args: obj -> unit

    /// Restores both protocol fields from the private module Symbol on the
    /// args object. Idempotent (no-op when no record is present, e.g. when
    /// the after hook receives a different object than before). Throws
    /// TypeError if the object is frozen/non-extensible.
    val restore: args: obj -> unit

    /// Validates the estimated_readonly_rounds value at the JS boundary
    /// before constructing the F# budget type: only a native finite integer
    /// within [0, 2147483647] is accepted. Missing, null, boolean, string,
    /// non-finite, fractional and out-of-range values are rejected without
    /// parseInt, string coercion, truthiness, truncation, rounding or
    /// clamping.
    val tryReadonlyRoundBudget: value: obj -> Result<ReadonlyRoundBudget, string>
