module Wanxiangshu.Runtime.MessageTransform.CapsStage

open Fable.Core
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Stack
open Wanxiangshu.Runtime.RuntimeScope

let prependCapsWithState
    (scope: RuntimeScope)
    (sessionID: string)
    (plan: MessageTransformPlan)
    (encoded: obj array)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        let capsSessionID =
            if sessionID <> "" then
                sessionID
            else
                plan.Cleaned
                |> List.tryFind (fun message -> message.source = Native && message.info.role = User)
                |> Option.map (fun message -> "anonymous-" + message.info.id)
                |> Option.defaultValue "anonymous-empty"

        let state = get scope capsSessionID

        match state.Caps with
        | Some capsSlot ->
            match capsSlot.Segment with
            | Some prefix -> return Array.append prefix encoded
            | None ->
                let! capsFiles = loadCaps ()
                let result = buildCaps encoded capsFiles None
                let prefixLen = result.Length - encoded.Length

                if prefixLen > 0 then
                    let prefix = result.[.. prefixLen - 1]
                    let updatedCapsSlot = { capsSlot with Segment = Some prefix }

                    set
                        scope
                        capsSessionID
                        { state with
                            Caps = Some updatedCapsSlot }

                return result
        | None ->
            let! capsFiles = loadCaps ()
            let result = buildCaps encoded capsFiles None
            let prefixLen = result.Length - encoded.Length

            if prefixLen > 0 then
                let prefix = result.[.. prefixLen - 1]

                let capsSlot: CapsSlot =
                    { Segment = Some prefix
                      ScopeId = capsSessionID
                      CapsRevision = state.CapsRevision
                      PolicyVersion = state.PolicyVersion }

                set scope capsSessionID { state with Caps = Some capsSlot }

            return result
    }
