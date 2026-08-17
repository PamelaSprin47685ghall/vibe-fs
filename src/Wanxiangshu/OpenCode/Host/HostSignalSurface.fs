namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

/// JS-native host signal boundary. Raw host payloads enter once; snapshots of
/// coarse wake signals leave, with no transport union representation exposed.
module HostSignalSurface =
    let private snapshot signal : obj =
        match signal with
        | HostSignal.SessionIdle sessionId ->
            box
                {| kind = "SessionIdle"
                   sessionId = SessionId.value sessionId |}
        | HostSignal.AttemptAborted sessionId ->
            box
                {| kind = "AttemptAborted"
                   sessionId = SessionId.value sessionId |}
        | HostSignal.SessionDeleted(sessionId, parent) ->
            box
                {| kind = "SessionDeleted"
                   sessionId = SessionId.value sessionId
                   parentSessionId = parent |> Option.map SessionId.value |> Option.defaultValue "" |}
        | HostSignal.ProviderRetry retry ->
            box
                {| kind = "ProviderRetry"
                   sessionId = SessionId.value retry.SessionId
                   attempt = retry.Attempt
                   reason = retry.Reason |}
        | HostSignal.ProviderFailure(sessionId, reason) ->
            box
                {| kind = "ProviderFailure"
                   sessionId = SessionId.value sessionId
                   reason = reason |}

    let tryDecode (raw: obj) : obj =
        HostEventCodec.tryDecode raw |> Option.map snapshot |> Option.defaultValue null

    let tryDecodePhysicalExecutionEnd (raw: obj) : obj =
        HostEventCodec.tryDecodePhysicalExecutionEnd raw
        |> Option.map (fun (sessionId, physicalUserMessageId) ->
            box
                {| sessionId = SessionId.value sessionId
                   physicalUserMessageId = PhysicalUserMessageId.value physicalUserMessageId |})
        |> Option.defaultValue null

    let tryAdapt (owned: string array) (raw: obj) : obj =
        // DSL-MUTABLE: resource — owned signal registry for host signal adaptation
        let registry = HashSet<string>(owned)

        HostSignalAdapter.tryAdapt (fun sessionId -> registry.Contains(SessionId.value sessionId)) raw
        |> Option.map snapshot
        |> Option.defaultValue null
