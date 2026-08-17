namespace Wanxiangshu.Execution.Delegation.Fork

open Fable.Core
open System
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

/// JS-native semantic surface for fork child payload (P3 pilot).
module ForkChildPayloadSurface =

    [<Emit("$0===undefined||$0===null")>]
    let private isUndefined (value: obj) : bool = jsNative

    let private languageOf (lang: string) : ProviderLanguage =
        ProviderLanguage.tryParse lang |> Option.defaultValue ProviderLanguage.English

    let private proseOf (lang: string) : ForkChildInstructions =
        let l = languageOf lang

        { Base = ProviderProse.instructionLines l ForkChildPayload.BasePath Map.empty
          CommissionerRecord = ProviderProse.render l ForkChildPayload.CommissionerRecordPath Map.empty
          Attachment = ProviderProse.render l ForkChildPayload.AttachmentPath Map.empty
          Requirements = ProviderProse.render l ForkChildPayload.RequirementsPath Map.empty }

    let instructions
        (lang: string)
        : {| Base: string array
             CommissionerRecord: string
             Attachment: string
             Requirements: string |}
        =
        let p = proseOf lang

        {| Base = List.toArray p.Base
           CommissionerRecord = p.CommissionerRecord
           Attachment = p.Attachment
           Requirements = p.Requirements |}

    /// Render unknown/unavailable calling prose without exposing binding names.
    let unavailableCalling (lang: string) (orchestrator: bool) : string =
        let language = languageOf lang

        let path =
            if orchestrator then
                "tool/commission/unknown-calling"
            else
                "tool/fork/unknown-calling"

        ProviderProse.render language path Map.empty

    /// Render one fork child payload document from JSON-shaped input.
    let render
        (lang: string)
        (input:
            {| Assignment: string
               CommissionerRecord: string option
               Attachment: string option
               RootRequirements: string array
               Payload: string option |})
        : string =
        let prose = proseOf lang

        let assignment =
            if isNull input.Assignment || isUndefined input.Assignment then
                ""
            else
                input.Assignment

        let requirements =
            if isNull input.RootRequirements || isUndefined input.RootRequirements then
                []
            else
                List.ofArray input.RootRequirements

        ForkChildPayload.render
            prose
            { Assignment = assignment
              CommissionerRecord = input.CommissionerRecord
              Attachment = input.Attachment
              RootRequirements = requirements
              Payload = input.Payload }

    let private nonEmpty value =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(value.Trim())

    let chooseRoad (calling: string) (byname: string) (charge: string) : obj =
        let road = nonEmpty calling
        let name = nonEmpty byname
        let task = nonEmpty charge

        match road, name, task with
        | Some callingName, Some logicalName, Some assignment ->
            box
                {| ok = true
                   road = "Independent"
                   calling = callingName
                   byname = logicalName
                   charge = assignment
                   authorityTransferred = false |}
        | None, Some logicalName, Some assignment ->
            box
                {| ok = true
                   road = "Continuation"
                   calling = null
                   byname = logicalName
                   charge = assignment
                   authorityTransferred = false |}
        | _ ->
            box
                {| ok = false
                   error = "charge, byname and calling (for a new road) must be non-empty" |}

    let reuseBinding
        (byname: string)
        (boundAgent: string)
        (requestedAgent: string)
        (tier: string)
        (charge: string)
        : obj =
        match nonEmpty byname, nonEmpty boundAgent, nonEmpty charge with
        | Some logicalName, Some bound, Some assignment ->
            let handle = HandleId.Agent(AgentHandleId.create "surface-binding")

            let projection =
                HandleProjection.linkNamed
                    handle
                    (SessionId.create "surface-child")
                    bound
                    logicalName
                    Role.Coder
                    HandleOwnership.DurableParentHandle
                    HandleProjection.empty

            match projection with
            | Error error ->
                box
                    {| ok = false
                       error = error.ToString() |}
            | Ok linked ->
                match HandleProjection.tryFindByByname logicalName linked with
                | None ->
                    box
                        {| ok = false
                           error = "continuation binding was not found" |}
                | Some record ->
                    box
                        {| ok = true
                           road = "Continuation"
                           byname = logicalName
                           managedAgent = record.TargetAgent
                           requestedAgent = requestedAgent
                           tier = tier
                           charge = assignment
                           authorityTransferred = false |}
        | _ ->
            box
                {| ok = false
                   error = "continuation requires a bound Byname and managed agent" |}
