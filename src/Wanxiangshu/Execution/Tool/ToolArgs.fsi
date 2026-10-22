namespace Wanxiangshu.Execution.Tool

open Wanxiangshu.Host.Ports

/// The product's own accessors over the canonical JSON arguments of one tool
/// call. A host adapter turns its decoded arguments into canonical JSON at the
/// boundary; from there the product never sees a host payload object again.
///
/// Every accessor is total: a malformed or absent argument reads as absent,
/// never as a thrown error, because a tool body that throws on a missing
/// argument would answer the host with an exception instead of a refusal.
module ToolArgs =

    /// The raw value of one argument, or `None` when it is absent.
    val raw: args: CanonicalJsonText -> name: string -> obj option

    /// The text of one argument; an absent or non-text argument reads as empty.
    val text: args: CanonicalJsonText -> name: string -> string

    /// The text of one argument, when it is present and not blank.
    val optionalText: args: CanonicalJsonText -> name: string -> string option

    /// The number of one argument, when it is a whole number.
    val optionalNumber: args: CanonicalJsonText -> name: string -> int option

    /// The non-negative whole number of one argument.
    val optionalNonNegativeInteger: args: CanonicalJsonText -> name: string -> Result<int option, unit>

    /// The texts of one array argument; an absent or non-array argument is empty.
    val texts: args: CanonicalJsonText -> name: string -> string list
