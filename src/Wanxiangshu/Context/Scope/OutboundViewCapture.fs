namespace Wanxiangshu.Context.Scope

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop

type OutboundModel =
    { ProviderId: string
      ModelId: string
      Variant: string option }

type OutboundSnapshot =
    { SessionId: string
      Messages: obj
      System: string array
      Model: OutboundModel }

[<RequireQualifiedAccess>]
module OutboundViewCapture =
    [<Emit("structuredClone($0)")>]
    let private cloneDeep (x: obj) : obj = jsNative

    [<Emit("Array.isArray($0) ? Array.from($0) : []")>]
    let private copyArray (x: obj) : obj array = jsNative

    [<Emit("JSON.stringify($0)")>]
    let private stringify (x: obj) : string = jsNative

    type private PendingState =
        { mutable Messages: obj option
          mutable System: string array option
          mutable Model: OutboundModel option }

    type private SessionState =
        { mutable Pending: PendingState
          mutable Sealed: OutboundSnapshot option }

    let private sessions = Dictionary<string, SessionState>()

    let private emptyPending () =
        { Messages = None
          System = None
          Model = None }

    let private getOrCreateSession (sessionId: string) =
        match sessions.TryGetValue sessionId with
        | true, s -> s
        | false, _ ->
            let s = { Pending = emptyPending (); Sealed = None }
            sessions.[sessionId] <- s
            s

    let private checkAndSeal (sessionId: string) (st: SessionState) =
        match st.Pending.Messages, st.Pending.System, st.Pending.Model with
        | Some msgs, Some sys, Some mdl when
            sys.Length > 0
            && (copyArray msgs).Length > 0
            && not (String.IsNullOrWhiteSpace mdl.ProviderId)
            && not (String.IsNullOrWhiteSpace mdl.ModelId) ->
            let snapshot =
                { SessionId = sessionId
                  Messages = msgs
                  System = sys
                  Model = mdl }
            st.Sealed <- Some snapshot
            st.Pending <- emptyPending ()
        | _ -> ()

    let private extractSessionIdFromMessages (messages: obj) : string option =
        let arr = copyArray messages
        let first = arr |> Array.tryHead |> Option.defaultValue null
        let sid = if isNull first then null else first?info?sessionID
        let value = if isNull sid then "" else string sid
        if String.IsNullOrWhiteSpace value then None else Some value

    let private recordFor (sessionId: string) (content: obj) (update: SessionState -> unit) =
        if not (String.IsNullOrWhiteSpace sessionId) && not (isNull content) then
            update (getOrCreateSession sessionId)

    let recordMessagesFor (sessionId: string) (messages: obj) : unit =
        recordFor sessionId messages (fun st ->
            st.Pending <- if st.Pending.Messages.IsSome then emptyPending () else st.Pending
            st.Pending.Messages <- Some(cloneDeep messages)
            checkAndSeal sessionId st)

    let recordMessages (messages: obj) : unit =
        extractSessionIdFromMessages messages
        |> Option.iter (fun sessionId -> recordMessagesFor sessionId messages)

    let recordSystem (sessionId: string) (system: obj) : unit =
        recordFor sessionId system (fun st ->
            st.Pending <- if st.Pending.System.IsSome then emptyPending () else st.Pending
            let rawArr = copyArray system
            let strArr =
                rawArr
                |> Array.choose (fun item ->
                    if isNull item then None
                    else Some(unbox<string> item))
            st.Pending.System <- Some strArr
            checkAndSeal sessionId st)

    let recordModel (sessionId: string) (model: obj) : unit =
        recordFor sessionId model (fun st ->
            st.Pending <- if st.Pending.Model.IsSome then emptyPending () else st.Pending
            let providerId =
                if not (isNull model?providerID) then string model?providerID
                elif not (isNull model?provider) then string model?provider
                else ""
            let modelId =
                if not (isNull model?modelID) then string model?modelID
                elif not (isNull model?id) then string model?id
                else ""
            let variant =
                if isNull model?variant then None
                else
                    let v = unbox<string> model?variant
                    if String.IsNullOrEmpty v then None else Some v
            let mdl: OutboundModel =
                { ProviderId = providerId
                  ModelId = modelId
                  Variant = variant }
            st.Pending.Model <- Some mdl
            checkAndSeal sessionId st)

    let tryGetSealed (sessionId: string) : OutboundSnapshot option =
        match sessions.TryGetValue sessionId with
        | true, s when not (String.IsNullOrWhiteSpace sessionId) -> s.Sealed
        | _ -> None

    let clearSession (sessionId: string) : unit =
        if not (String.IsNullOrWhiteSpace sessionId) then
            sessions.Remove sessionId |> ignore

    let serializeSnapshot (snapshot: OutboundSnapshot) : string =
        let modelObj = createEmpty<obj>
        modelObj?providerID <- snapshot.Model.ProviderId
        modelObj?modelID <- snapshot.Model.ModelId
        modelObj?variant <-
            match snapshot.Model.Variant with
            | Some v -> box v
            | None -> null

        let root = createEmpty<obj>
        root?sessionId <- snapshot.SessionId
        root?messages <- snapshot.Messages
        root?system <- snapshot.System
        root?model <- modelObj
        root?toolsCaptured <- false

        stringify root
