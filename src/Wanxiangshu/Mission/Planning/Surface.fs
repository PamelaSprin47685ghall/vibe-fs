namespace Wanxiangshu.Mission.Planning

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Mission.Lifecycle
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.EventStore

module PlanningSurface =

    let private sequenceOfJs (value: obj) : int64 =
        if not (emitJsExpr value "typeof $0 === 'bigint' && $0 >= 0n") then
            invalidArg (nameof value) "Plan cursor must be a nonnegative bigint"

        unbox<int64> value

    let assembleTenureMessages
        (messages: obj)
        (tenureObj: obj)
        (materializePrev: obj -> string)
        (messageCursors: obj)
        : obj =
        let previousRange =
            if isNull tenureObj?previousRange then
                None
            else
                let range = unbox<obj array> tenureObj?previousRange
                Some(sequenceOfJs range.[0], sequenceOfJs range.[1])

        let tenure =
            { WorkId = unbox<string> tenureObj?workId
              IncumbencyId = unbox<string> tenureObj?incumbencyId
              Stage = unbox<string> tenureObj?stage
              OpeningCursor = sequenceOfJs tenureObj?openingCursor
              PreviousRange = previousRange
              IsFreshHandover = unbox<bool> tenureObj?isFreshHandover }

        let cursors =
            emitJsExpr messageCursors "Object.entries($0)"
            |> unbox<obj array array>
            |> Array.map (fun entry -> unbox<string> entry.[0], sequenceOfJs entry.[1])
            |> Map.ofArray

        let previousRecord =
            previousRange
            |> Option.map (fun (startCursor, endCursor) ->
                materializePrev (
                    box
                        {| start = startCursor
                           ``end`` = endCursor |}
                ))

        let assembled =
            TenureIsolation.assemble
                (unbox<obj array> messages
                 |> Array.toList
                 |> List.choose (fun raw ->
                     ProviderWireCapture.decodeCapturedMessage raw
                     |> Option.map (fun message ->
                         { Id = ProviderWireDecode.hostMessageId raw
                           Role = message.Role
                           IsSynthetic = ProviderWireCapture.isSyntheticMessage raw
                           HasSemanticParts = not (List.isEmpty message.Parts)
                           Raw = raw })))
                tenure
                cursors
                previousRecord

        box
            {| messages = assembled.Messages |> List.toArray
               reanchorRequested = assembled.ReanchorRequested |}

    let validateWorkId (workId: string) : obj =
        match PlanPath.validateWorkId workId with
        | Ok key -> box {| ok = true; key = key; error = null |}
        | Error err ->
            box
                {| ok = false
                   key = null
                   error = err |}

    let canonicalWorkKey (workId: string) : obj =
        match PlanPath.canonicalWorkKey workId with
        | Ok key -> box {| ok = true; key = key; error = null |}
        | Error err ->
            box
                {| ok = false
                   key = null
                   error = err |}

    let resolvePlanPath (root: string) (workId: string) : obj =
        match PlanPath.resolvePlanPath root workId with
        | Ok path ->
            box
                {| ok = true
                   path = path
                   error = null |}
        | Error err ->
            box
                {| ok = false
                   path = null
                   error = err |}

    let readAndValidatePlan (root: string) (workId: string) : obj =
        match PlanTools.readAndValidatePlan root workId with
        | Ok(_, digest, path) -> box {| ok = true; digest = digest; path = path; error = null |}
        | Error err -> box {| ok = false; digest = null; path = null; error = err |}

    let readPlan (root: string) (workId: string) : obj =
        match PlanPath.readPlan root workId with
        | Ok content ->
            box
                {| ok = true
                   content = content
                   error = null |}
        | Error err ->
            box
                {| ok = false
                   content = null
                   error = err |}

    let rewritePlanAtomic (root: string) (workId: string) (content: string) : obj =
        match PlanPath.rewritePlanAtomic root workId content with
        | Ok(path, digest) ->
            box
                {| ok = true
                   path = path
                   digest = digest
                   error = null |}
        | Error err ->
            box
                {| ok = false
                   path = null
                   digest = null
                   error = err |}

    let editPlanAtomic (root: string) (workId: string) (patches: obj) : obj =
        let patchList =
            if isNull patches then
                []
            else
                let arr = unbox<obj array> patches

                arr
                |> Array.toList
                |> List.map (fun p -> (unbox<string> p?find, unbox<string> p?put))

        match PlanPath.editPlanAtomic root workId patchList with
        | Ok(path, digest) ->
            box
                {| ok = true
                   path = path
                   digest = digest
                   error = null |}
        | Error err ->
            box
                {| ok = false
                   path = null
                   digest = null
                   error = err |}

    let private extractStringOpt (v: obj) : string option =
        if isNull v then
            None
        elif emitJsExpr v "Array.isArray($0)" then
            let arr = unbox<obj array> v

            if arr.Length = 0 || isNull arr.[0] then
                None
            else
                Some(unbox<string> arr.[0])
        elif emitJsExpr v "typeof $0 === 'string'" then
            Some(unbox<string> v)
        else
            None

    let private extractInt64Opt (v: obj) : int64 option =
        if emitJsExpr v "typeof $0 === 'bigint'" then
            Some(unbox<int64> v)
        else
            None

    let private spawnOfJs (spawnVal: obj) : string -> string -> Task<string> =
        fun (agent: string) (prompt: string) ->
            // The spawn argument arrives as a plain JavaScript function
            // (agent, prompt) => Promise<sessionId>; call it through JS interop
            // so currying never gets in the way.
            unbox<Task<string>> (emitJsExpr (spawnVal, agent, prompt) "$0($1, $2)")

    let ensurePlanWorkOpened
        (store: IEventStore)
        (root: string)
        (sessionId: string)
        (observedHead: obj)
        : Task<obj> =
        task {
            let head = extractInt64Opt observedHead

            match! PlanWorkOpen.ensurePlanWorkOpened store root sessionId head with
            | Ok() -> return box {| ok = true; error = null |}
            | Error err -> return box {| ok = false; error = err |}
        }

    let private acquiredCommonDir (store: IEventStore) : string option =
        let text =
            emitJsExpr (box store) "$0 && typeof $0.gitCommonDir === 'string' ? $0.gitCommonDir : ''"
            |> string

        if String.IsNullOrWhiteSpace text then None else Some text

    let executeDeliver
        (store: IEventStore)
        (root: string)
        (workId: string)
        (incumbencyId: string)
        (providerRunId: string)
        (toolCallId: string)
        (hasBlocker: bool)
        (spawn: obj)
        (retirementCursor: obj)
        : Task<obj> =
        let spawnFn = spawnOfJs spawn

        task {
            let! outcome =
                match extractInt64Opt retirementCursor with
                | None -> Task.FromResult(Error "Plan retirement cursor evidence is required")
                | Some cursor ->
                    match acquiredCommonDir store with
                    | None -> Task.FromResult(Error "git common directory is missing")
                    | Some gitCommonDir ->
                        PlanTools.executeDeliver
                            gitCommonDir
                            store
                            root
                            workId
                            incumbencyId
                            providerRunId
                            toolCallId
                            hasBlocker
                            spawnFn
                            cursor

            match outcome with
            | Ok res ->
                return
                    box
                        {| ok = true
                           relayed = res.Relayed
                           delivered = res.Delivered
                           retiredIncumbencyId = res.RetiredIncumbencyId
                           retirementOutcome = res.RetirementOutcome
                           digest = res.Digest
                           path = res.Path
                           execSessionId = res.ExecSessionId
                           nextIncumbencyId = res.NextIncumbencyId
                           message = res.Message
                           error = null |}
            | Error err ->
                return
                    box
                        {| ok = false
                           relayed = false
                           delivered = false
                           retiredIncumbencyId = null
                           retirementOutcome = null
                           digest = null
                           path = null
                           execSessionId = null
                           nextIncumbencyId = null
                           message = null
                           error = err |}
        }

    // The delegate argument arrives as a plain JavaScript function
    // (intent) => Promise<workRecord>; call it through JS interop so currying
    // never gets in the way.
    let private delegateOfJs (delegateVal: obj) : string -> Task<string> =
        fun (intent: string) -> unbox<Task<string>> (emitJsExpr (delegateVal, intent) "$0($1)")

    let executeAsk (store: IEventStore) (workId: string) (intent: string) (delegateTo: obj) : Task<obj> =
        let delegateFn = delegateOfJs delegateTo

        task {
            match acquiredCommonDir store with
            | None ->
                return
                    box
                        {| ok = false
                           kind = null
                           workRecord = null
                           status = null
                           error = "git common directory is missing" |}
            | Some gitCommonDir ->
                let! outcome = PlanTools.executeAsk gitCommonDir store workId intent delegateFn

                return
                    match outcome with
                    | Error error ->
                        box
                            {| ok = false
                               kind = null
                               workRecord = null
                               status = null
                               error = error |}
                    | Ok asked ->
                        box
                            {| ok = true
                               kind = asked.Kind
                               workRecord = asked.WorkRecord
                               status = asked.Status
                               error = null |}
        }

    let executeJsPlan (root: string) (workId: string) (action: string) (content: obj) (patches: obj) : obj =
        let contentOpt = extractStringOpt content

        let patchList =
            if isNull patches then
                None
            else
                let arr = unbox<obj array> patches

                Some(
                    arr
                    |> Array.toList
                    |> List.map (fun p -> (unbox<string> p?find, unbox<string> p?put))
                )

        match PlanTools.executeJsPlan root workId action contentOpt patchList with
        | Ok res ->
            box
                {| ok = true
                   output = res
                   error = null |}
        | Error err ->
            box
                {| ok = false
                   output = null
                   error = err |}

    let reviewTargetFor (stage: string) : string = ReviewPlanBackend.reviewTargetFor stage

    let private parseFindings (findingsVal: obj) : Result<(string * string) list, string> =
        if isNull findingsVal then
            Error "findings must be an array of {acceptance_criteria, work_plan} pairs"
        elif emitJsExpr findingsVal "Array.isArray($0)" then
            let arr = unbox<obj array> findingsVal

            let items =
                arr
                |> Array.toList
                |> List.map (fun (item: obj) ->
                    let acceptance: obj = item?acceptance_criteria
                    let workPlan: obj = item?work_plan

                    if
                        isNull acceptance
                        || isNull workPlan
                        || not (emitJsExpr acceptance "typeof $0 === 'string'")
                        || not (emitJsExpr workPlan "typeof $0 === 'string'")
                        || String.IsNullOrEmpty(unbox<string> acceptance)
                        || String.IsNullOrWhiteSpace(unbox<string> workPlan)
                    then
                        None
                    else
                        Some(unbox<string> acceptance, unbox<string> workPlan))

            let valid = items |> List.choose id

            if valid.Length <> items.Length then
                Error "each finding requires non-empty acceptance_criteria and work_plan strings"
            else
                Ok valid
        else
            Error "findings must be an array of {acceptance_criteria, work_plan} pairs"

    let private gapsToJs (gaps: (string * string) list) : obj array =
        gaps
        |> List.map (fun (acceptance, workPlan) ->
            box
                {| acceptance_criteria = acceptance
                   work_plan = workPlan |})
        |> Array.ofList

    let executeReview
        (store: IEventStore)
        (workId: string)
        (incumbencyId: string)
        (findings: obj)
        (note: obj)
        : Task<obj> =
        // note is accepted for writing and never returned (relay-assessment-001).
        task {
            match parseFindings findings with
            | Error err ->
                return
                    box
                        {| ok = false
                           outcome = null
                           gaps = null
                           error = err |}
            | Ok gaps ->
                match acquiredCommonDir store with
                | None ->
                    return
                        box
                            {| ok = false
                               outcome = null
                               gaps = null
                               error = "git common directory is missing" |}
                | Some gitCommonDir ->
                match! ReviewPlanBackend.executeReview gitCommonDir store workId incumbencyId gaps with
                | Error err ->
                    return
                        box
                            {| ok = false
                               outcome = null
                               gaps = null
                               error = PlanReviewError.render err |}
                | Ok review ->
                    return
                        box
                            {| ok = true
                               outcome = ReviewOutcome.render review.Outcome
                               gaps = gapsToJs review.Gaps
                               error = Unchecked.defaultof<string> |}
        }

    let PlanningSurface: obj =
        box
            {| assembleTenureMessages = assembleTenureMessages
               validateWorkId = validateWorkId
               canonicalWorkKey = canonicalWorkKey
               resolvePlanPath = resolvePlanPath
               readPlan = readPlan
               readAndValidatePlan = readAndValidatePlan
               rewritePlanAtomic = rewritePlanAtomic
               editPlanAtomic = editPlanAtomic
               ensurePlanWorkOpened = ensurePlanWorkOpened
               executeDeliver = executeDeliver
               executeAsk = executeAsk
               executeJsPlan = executeJsPlan
               executeReview = executeReview
               reviewTargetFor = reviewTargetFor |}
