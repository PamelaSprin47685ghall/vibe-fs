namespace Wanxiangshu.Sphinx

open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop

/// WHAT[EPI-013,EPI-019]: schema-only generic inquiry registry backing the five
/// sphinx_inquiry_start / sphinx_work_submit / sphinx_inquiry_status /
/// sphinx_inquiry_export / sphinx_inquiry_cancel MCP tools. Accepted
/// transitions are durable facts: GenericDurability appends one envelope per
/// transition and replays them at boot, so the registry is a replaceable
/// projection, never the truth. The host stays schema-only — this registry
/// records revisions, liveness, budgets and submitted results and never
/// produces refiner, stop or answer verdicts.
module GecInquiry =

    let toolGenericStart = "sphinx_inquiry_start"
    let toolGenericSubmit = "sphinx_work_submit"
    let toolGenericStatus = "sphinx_inquiry_status"
    let toolGenericExport = "sphinx_inquiry_export"
    let toolGenericCancel = "sphinx_inquiry_cancel"

    let codeRevisionConflict = "REVISION_CONFLICT"
    let codeUnknownInquiry = "UNKNOWN_HANDLE"
    let codeInquiryCancelled = "inquiry-cancelled"

    type GecInquiryEntry =
        { InquiryId: string
          InquiryQuestion: string
          InquiryProfile: string
          InquiryPlugins: obj
          InquiryExecutionMode: string
          InquiryBudget: obj
          InquiryRevision: int
          InquiryCancelled: bool
          InquiryResults: obj list }

    [<RequireQualifiedAccess>]
    type InquiryFault =
        | UnknownInquiry of inquiryId: string
        | InquiryCancelled of inquiryId: string
        | RevisionConflict of inquiryId: string * current: int

    let faultCode (fault: InquiryFault) : string =
        match fault with
        | InquiryFault.RevisionConflict _ -> codeRevisionConflict
        | InquiryFault.UnknownInquiry _ -> codeUnknownInquiry
        | InquiryFault.InquiryCancelled _ -> codeInquiryCancelled

    let private unknownInquiryHint (inquiryId: string) : string =
        if inquiryId.StartsWith "iq_" then
            sprintf "unknown inquiry: %s" inquiryId
        else
            sprintf
                "unknown inquiry: %s (generic tools need iq_ ids from sphinx_inquiry_start, not legacy handles)"
                inquiryId

    let faultMessage (fault: InquiryFault) : string =
        match fault with
        | InquiryFault.UnknownInquiry inquiryId -> unknownInquiryHint inquiryId
        | InquiryFault.InquiryCancelled inquiryId -> sprintf "inquiry is cancelled: %s" inquiryId
        | InquiryFault.RevisionConflict(_, current) -> sprintf "stale expectedRevision: current revision is %d" current

    module private InquiryIdGen =

        [<Import("randomUUID", "node:crypto")>]
        let randomUUID () : string = jsNative

        let private stripDashes (value: string) : string = emitJsExpr value "$0.replace(/-/g, '')"

        let next () : string =
            let stripped = stripDashes (randomUUID ())

            let short =
                if stripped.Length > 16 then
                    stripped.Substring(0, 16)
                else
                    stripped

            "iq_" + short

    let private submitEntry
        (entry: GecInquiryEntry)
        (expectedRevision: int)
        (results: obj list)
        : Result<GecInquiryEntry, InquiryFault> =
        if entry.InquiryCancelled then
            Error(InquiryFault.InquiryCancelled entry.InquiryId)
        elif expectedRevision <> entry.InquiryRevision then
            Error(InquiryFault.RevisionConflict(entry.InquiryId, entry.InquiryRevision))
        else
            let next: GecInquiryEntry =
                { entry with
                    InquiryRevision = entry.InquiryRevision + 1
                    InquiryResults = entry.InquiryResults @ results }

            Ok next

    let private cancelEntry (entry: GecInquiryEntry) : Result<GecInquiryEntry, InquiryFault> =
        if entry.InquiryCancelled then
            Error(InquiryFault.InquiryCancelled entry.InquiryId)
        else
            Ok { entry with InquiryCancelled = true }

    let BuildStart
        (question: string, profile: string, plugins: obj, executionMode: string, budget: obj)
        : GecInquiryEntry =
        { InquiryId = InquiryIdGen.next ()
          InquiryQuestion = question
          InquiryProfile = profile
          InquiryPlugins = plugins
          InquiryExecutionMode = executionMode
          InquiryBudget = budget
          InquiryRevision = 0
          InquiryCancelled = false
          InquiryResults = [] }

    let DecideSubmit
        (entry: GecInquiryEntry, expectedRevision: int, results: obj list)
        : Result<GecInquiryEntry, InquiryFault> =
        submitEntry entry expectedRevision results

    let DecideCancel (entry: GecInquiryEntry) : Result<GecInquiryEntry, InquiryFault> = cancelEntry entry

    [<Sealed>]
    type Registry() =
        // DSL-MUTABLE: resource — generic inquiry table by iq_ id.
        let table = Dictionary<string, GecInquiryEntry>()

        member _.Restore(entry: GecInquiryEntry) : unit = table[entry.InquiryId] <- entry

        member this.Start
            (question: string, profile: string, plugins: obj, executionMode: string, budget: obj)
            : GecInquiryEntry =
            let entry = BuildStart(question, profile, plugins, executionMode, budget)
            this.Restore entry
            entry

        member _.TryFind(inquiryId: string) : GecInquiryEntry option =
            match table.TryGetValue inquiryId with
            | true, (entry: GecInquiryEntry) -> Some entry
            | false, _ -> None

        member private this.Update
            (inquiryId: string, decide: GecInquiryEntry -> Result<GecInquiryEntry, InquiryFault>)
            : Result<GecInquiryEntry, InquiryFault> =
            match this.TryFind inquiryId with
            | None -> Error(InquiryFault.UnknownInquiry inquiryId)
            | Some(entry: GecInquiryEntry) ->
                decide entry
                |> Result.map (fun (next: GecInquiryEntry) ->
                    table[inquiryId] <- next
                    next)

        member this.Submit
            (inquiryId: string, expectedRevision: int, results: obj list)
            : Result<GecInquiryEntry, InquiryFault> =
            this.Update(inquiryId, (fun entry -> DecideSubmit(entry, expectedRevision, results)))

        member this.Cancel(inquiryId: string) : Result<GecInquiryEntry, InquiryFault> =
            this.Update(inquiryId, DecideCancel)

    let private statusName (entry: GecInquiryEntry) : string =
        if entry.InquiryCancelled then "cancelled" else "active"

    let entryView (entry: GecInquiryEntry) : obj =
        createObj
            [ "inquiryId" ==> entry.InquiryId
              "revision" ==> entry.InquiryRevision
              "status" ==> statusName entry
              "question" ==> entry.InquiryQuestion
              "profile" ==> entry.InquiryProfile
              "executionMode" ==> entry.InquiryExecutionMode
              "budget" ==> entry.InquiryBudget ]

    let submitView (entry: GecInquiryEntry) (accepted: int) : obj =
        createObj
            [ "inquiryId" ==> entry.InquiryId
              "revision" ==> entry.InquiryRevision
              "status" ==> statusName entry
              "accepted" ==> accepted ]

    let exportView (entry: GecInquiryEntry) : obj =
        createObj
            [ "inquiryId" ==> entry.InquiryId
              "revision" ==> entry.InquiryRevision
              "status" ==> statusName entry
              "question" ==> entry.InquiryQuestion
              "profile" ==> entry.InquiryProfile
              "executionMode" ==> entry.InquiryExecutionMode
              "budget" ==> entry.InquiryBudget
              "plugins" ==> entry.InquiryPlugins
              "results" ==> (entry.InquiryResults |> List.toArray) ]

    let cancelView (entry: GecInquiryEntry) : obj =
        createObj
            [ "inquiryId" ==> entry.InquiryId
              "revision" ==> entry.InquiryRevision
              "status" ==> statusName entry ]
