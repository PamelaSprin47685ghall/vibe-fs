module Wanxiangshu.Hosts.Mux.CompactionTransform

open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Runtime.SessionEventWriter
open Wanxiangshu.Runtime.JsArrayMutate
open Wanxiangshu.Runtime.MuxHookInputCodec
open Wanxiangshu.Hosts.Mux.MessagingCodec
open Wanxiangshu.Runtime.BacklogSession
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure

let private sanitizeMuxMessages (sessionID: string) (messagesArr: obj array) = decodeMessages sessionID messagesArr

let compactMessageBatch (input: obj) (output: obj) =
    let fromInput = Dyn.get input "messages"

    if not (Dyn.isNullish fromInput) && Dyn.isArray fromInput then
        fromInput :?> obj array
    else
        let fromOutput = Dyn.get output "messages"

        if not (Dyn.isNullish fromOutput) && Dyn.isArray fromOutput then
            fromOutput :?> obj array
        else
            [||]

let buildGuid (deps: obj) =
    let rg = get deps "RandomGen"

    if not (isNullish rg) then
        fun () -> string (rg $ ())
    else
        fun () -> System.Guid.NewGuid().ToString()

let readCompactionMetadata (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope) (sessionID: string) =
    let fallbackRuntime =
        match runtimeScope.TryFindKey("fallbackRuntime") with
        | Some obj -> Some(unbox<FallbackRuntimeStore> obj)
        | None -> None

    let gen =
        match fallbackRuntime with
        | Some fr -> (fr.GetSession sessionID).SessionGeneration
        | None -> 0

    let turnId =
        match fallbackRuntime with
        | Some fr -> (fr.GetSession sessionID).HumanTurnId
        | None -> ""

    let cancelGen =
        match fallbackRuntime with
        | Some fr -> (fr.GetSession sessionID).CancelGeneration
        | None -> 0

    let compactionOrdinal =
        match fallbackRuntime with
        | Some fr -> fr.UpdateSessionReturning(sessionID, incrementCompactionOrdinal)
        | None -> 0

    gen, turnId, cancelGen, compactionOrdinal, fallbackRuntime

let buildCompactedResult
    (deps: obj)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (directory: string)
    (sessionID: string)
    (cleaned: Message<obj> list)
    (backlog: _)
    =
    promise {
        let guidGen = buildGuid deps

        let gen, turnId, cancelGen, compactionOrdinal, fallbackRuntime =
            readCompactionMetadata runtimeScope sessionID

        let compactionId = "compact-" + System.Guid.NewGuid().ToString("N")

        do! appendCompactionStartedOrFail directory sessionID compactionId gen turnId compactionOrdinal

        match fallbackRuntime with
        | Some fr ->
            fr.UpdateSession(sessionID, transferOwnership SessionOwner.Compaction)
            fr.UpdateSession(sessionID, setActiveCompactionId compactionId compactionOrdinal turnId cancelGen)
        | None -> ()

        return Wanxiangshu.Runtime.BacklogProjectionBuild.compactingTransform cleaned backlog guidGen
    }

let buildCompactedResultOutput (result: Message<obj> list) (output: obj) =
    let encoded = encodeMessages result
    let promptBody = box {| parts = [| box {| ``type`` = "text"; text = "​" |} |] |}
    output?context <- encoded
    output?prompt <- promptBody

let compactingTransform
    (deps: obj)
    (runtimeScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (backlogSession: BacklogSession)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        let decoded = decodeMuxMessagesTransformInput input deps
        let directory = decoded.Directory
        runtimeScope.TriggerInit(directory)
        do! runtimeScope.WaitInit()
        let messagesArr = compactMessageBatch input output

        if messagesArr.Length > 0 then
            let sessionID = decoded.SessionID
            let cleaned = sanitizeMuxMessages sessionID messagesArr
            let backlog = backlogSession.GetOrRebuildBacklog(sessionID, cleaned)
            let! result = buildCompactedResult deps runtimeScope directory sessionID cleaned backlog
            buildCompactedResultOutput result output
    }
