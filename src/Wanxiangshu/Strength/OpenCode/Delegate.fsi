namespace Wanxiangshu.Strength.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Replica

[<RequireQualifiedAccess>]
module StrengthDelegate =

    type CapturedCall = { CallId: ToolCallId; Budget: int }

    val tailBatchIsComplete: rawMessages: obj list -> (string * CapturedCall list) option

    val tryCapture:
        isPredictorConfigured: (unit -> bool) ->
        journal: AgentJournal option ->
        durability: StrengthDurabilityPort option ->
        sessionIdOpt: SessionId option ->
        rawMessages: obj list ->
            Task<unit>

    val tryApply:
        durability: StrengthDurabilityPort option ->
        replicaRuntimeOpt: StrengthReplicaRuntime option ->
        failFuse: (string -> unit) ->
        sessionIdOpt: SessionId option ->
        rawMessages: obj list ->
            Task<unit>
