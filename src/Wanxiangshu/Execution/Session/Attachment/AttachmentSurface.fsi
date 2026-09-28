namespace Wanxiangshu.Execution.Session.Attachment

open System.Threading.Tasks

module AttachmentSurface =
    val classifyObservation: observation: string -> obj

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

    val reconciliationScenario: observation: string -> Task<obj>
