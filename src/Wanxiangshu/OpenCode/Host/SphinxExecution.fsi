namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

type ISphinxEngineerPort =
    abstract Invoke:
        owner: SessionId * charge: string * admitted: (SessionId -> unit) * isCancelled: (unit -> bool) -> Task<Result<string, string>>
    abstract Cancel: child: SessionId -> Task<unit>
    abstract LogicalOwnerOf: present: SessionId -> SessionId

/// One plugin-owned inquiry executor; session ownership remains with SyncDelegate.
type SphinxExecution =
    new: store: IEventStore * engineers: ISphinxEngineerPort -> SphinxExecution
    member Run:
        context: HostToolContext * invocationId: string * question: string * expectTurns: int option -> Task<obj>
    member CancelSession: sessionId: string -> unit
    member DisposeAsync: unit -> Task

module SphinxExecution =
    val parseObservation: text: string -> Result<obj, string>
    val prompt: question: string -> expectTurns: int option -> work: obj -> string
