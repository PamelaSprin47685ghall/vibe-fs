namespace Wanxiangshu.Host

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// HNI P-18: the tool-call context, what one product tool call knows about its
/// invocation, independent of the host that decoded it. An OpenCode or omp
/// adapter fills it from its own wire context.
///
/// No user-message field. HOST-011: a host tool context carries the session,
/// the provider run and the call, never a user message id.
type HostToolContext =
    { SessionId: string
      Agent: string option
      ToolCallId: ToolCallId option
      ProviderRunId: ProviderRunIdentity option
      PromptText: string option
      AttachAbort: (unit -> unit) -> (unit -> unit) }

/// The authority a tool call is admitted under. capability-enforcement-006 keeps the two sources
/// apart in the type: an office tool is decided by the public Role the Authority
/// Root established, while an internal leaf tool (Bookkeeper's `todowrite-for-bookkeeper`)
/// is decided by owner-held attachment evidence because its session is
/// HostInternal and holds no public authority profile at all.
[<RequireQualifiedAccess>]
type ToolAdmission =
    | OfficeRole of (HostToolContext -> Role -> bool)
    | PrivateAttachment of (HostToolContext -> bool)

/// ARCH-010 data-only TOML value algebra for tool result bodies. The product
/// owns the result text; a host adapter only bounds and returns it.
module ToolResultText =
    type TomlValue =
        | TString of string
        | TInt of int
        | TInt64 of int64
        | TBool of bool
        | TTable of (string * TomlValue) list

    val tomlObject: fields: (string * TomlValue) list -> string
    val tomlObjectWithInstructions: instructions: string list -> fields: (string * TomlValue) list -> string
    val tomlTable: name: string -> entries: (string * TomlValue) list list -> string
