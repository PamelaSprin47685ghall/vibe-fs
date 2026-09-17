namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.OpenCode
open Wanxiangshu.Process

[<RequireQualifiedAccess>]
module DelegationPtyAdapter =
    val ensureLf: prompt: string -> string
    val emptyRead: id: PtyId -> PtyRead
    val mapRead: id: PtyId -> output: string * closed: bool -> PtyRead

    val create:
        tryPtyByName: (string -> PtyId option) ->
        forkPty: (string * ManagedAgent * string option -> Task<Result<PtyId, string>>) ->
        tryBindTerminalName: (string * PtyId -> Result<unit, string>) ->
        untrackPtyRun: (string -> unit) ->
        sendPty: (PtyId * string * PtySignal option -> Task<Result<PtyRead, string>>) ->
        ownsPty: (PtyId -> bool) ->
        tryPty: (string -> PtyId option) ->
        tryTerminalNameByPtyId: (string -> string option) ->
            DelegationPtyCapability
