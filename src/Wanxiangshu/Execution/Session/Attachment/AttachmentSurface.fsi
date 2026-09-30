namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks

module AttachmentSurface =

    val createOwner: unit -> obj

    val getOrCreate:
        runtime: obj ->
        owner: string ->
        role: string ->
        agent: string ->
        createChild: (string -> string -> string -> string -> Task<string>) ->
        bindChild: (string -> string -> string -> unit) ->
            Task<obj>

    val tryFind: runtime: obj -> owner: string -> role: string -> string option
    val remove: runtime: obj -> owner: string -> role: string -> bool
    val clear: runtime: obj -> unit

    val scenario:
        owner: string -> role: string -> firstAgent: string -> secondAgent: string -> retainBinding: bool -> Task<obj>

    /// managed-session-lifecycle-001 (GAP-133): every AttachmentKind established
    /// through the ONE lifecycle owner and read back from the shared registry.
    val everyKindScenario: owner: string -> Task<obj>
