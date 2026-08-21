namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Context.Trace
open Wanxiangshu.Context.Companion

/// Review-owned journal adapter. JournalHandle remains the only durable
/// capability; facts cross this boundary as plain family/case/payload values.
/// This keeps review tests off AgentFact unions, Fable option/list helpers, and
/// internal AgentJournal exports while preserving the production fold.
[<RequireQualifiedAccess>]
module ReviewJournalSurface =

    let private text (value: obj) =
        if isNull value then "" else string value

    let private field (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            Fable.Core.JsInterop.emitJsExpr (value, name) "$0[$1]"

    let private strField value name = text (field value name)

    let private optionalText value name =
        let candidate = field value name
        if isNull candidate then None else Some(text candidate)

    let private identity fieldName value = text (field value fieldName)

    let private streamOfSession (sessionId: string) =
        StreamId.Session(SessionId.create sessionId)

    let private runOf (value: obj) =
        if isNull value then
            None
        else
            Some(ProviderRunIdentity.create (text value))

    let private verdictOf value =
        match text value with
        | "PERFECT"
        | "Perfect" -> ReviewGuardVerdict.Perfect
        | "REVISE"
        | "Revise" -> ReviewGuardVerdict.Revise
        | other -> failwith $"ReviewJournalSurface: unknown verdict '{other}'"

    let private roleOf value =
        match text value with
        | "Manager" -> Role.Manager
        | "Orchestrator" -> Role.Orchestrator
        | "Coder" -> Role.Coder
        | "Inspector" -> Role.Inspector
        | "Browser" -> Role.Browser
        | "Inquiry" -> Role.Inquiry
        | "Reviewer" -> Role.Reviewer
        | "DevOps" -> Role.DevOps
        | "Distiller" -> Role.Distiller
        | "Blogger" -> Role.Blogger
        | other -> failwith $"ReviewJournalSurface: unknown role '{other}'"

    let private ownershipOf value =
        match text value with
        | "DurableParentHandle" -> HandleOwnership.DurableParentHandle
        | "HostOwnedHidden" -> HandleOwnership.HostOwnedHidden
        | other -> failwith $"ReviewJournalSurface: unknown ownership '{other}'"

    let private completionKindOf value =
        match text value with
        | "Terminal" -> HandleCompletionKind.Terminal
        | "SendFailure" -> HandleCompletionKind.SendFailure
        | "Cancelled" -> HandleCompletionKind.Cancelled
        | other -> failwith $"ReviewJournalSurface: unknown completion kind '{other}'"

    let private reviewFactOf (caseName: string) (payload: obj) : AgentFact =
        match caseName with
        | "ReviewBarrierStarted" ->
            ReviewFact.ReviewBarrierStarted
                {| ReviewerSessionId = SessionId.create (strField payload "ReviewerSessionId")
                   ManagerSessionId = SessionId.create (strField payload "ManagerSessionId")
                   BarrierId = ReviewBarrierId.create (strField payload "BarrierId")
                   GitTreeHash = GitTreeHash.create (strField payload "GitTreeHash") |}
        | "ReviewVerdictRecorded" ->
            ReviewFact.ReviewVerdictRecorded
                {| ReviewerSessionId = SessionId.create (strField payload "ReviewerSessionId")
                   ManagerSessionId = SessionId.create (strField payload "ManagerSessionId")
                   BarrierId = ReviewBarrierId.create (strField payload "BarrierId")
                   GitTreeHash = GitTreeHash.create (strField payload "GitTreeHash")
                   ProviderRun = ProviderRunIdentity.create (strField payload "ProviderRun")
                   ToolCallId = ToolCallId.create (strField payload "ToolCallId")
                   Verdict = verdictOf (field payload "Verdict") |}
        | "ReviewAttemptClosed" ->
            ReviewFact.ReviewAttemptClosed
                {| ReviewerSessionId = SessionId.create (strField payload "ReviewerSessionId")
                   BarrierId = ReviewBarrierId.create (strField payload "BarrierId")
                   GitTreeHash = GitTreeHash.create (strField payload "GitTreeHash")
                   ProviderRun = ProviderRunIdentity.create (strField payload "ProviderRun")
                   ToolCallId = ToolCallId.create (strField payload "ToolCallId")
                   FrozenFrontierSequence = int64 (unbox<int> (field payload "FrozenFrontierSequence")) |}
        | "ConfirmedReviewWitness" ->
            ReviewFact.ConfirmedReviewWitness
                {| ManagerJobId = optionalText payload "ManagerJobId" |> Option.map ManagerJobId.create
                   ManagerSessionId = SessionId.create (strField payload "ManagerSessionId")
                   ReviewerSessionId = SessionId.create (strField payload "ReviewerSessionId")
                   WorktreeIdentity = optionalText payload "WorktreeIdentity" |> Option.map WorktreeIdentity.create
                   BarrierId = ReviewBarrierId.create (strField payload "BarrierId")
                   GitTreeHash = GitTreeHash.create (strField payload "GitTreeHash")
                   FirstProviderRun = ProviderRunIdentity.create (strField payload "FirstProviderRun")
                   FirstToolCallId = ToolCallId.create (strField payload "FirstToolCallId")
                   FirstPhysicalUserMessageId =
                    PhysicalUserMessageId.create (strField payload "FirstPhysicalUserMessageId")
                   SecondProviderRun = ProviderRunIdentity.create (strField payload "SecondProviderRun")
                   SecondToolCallId = ToolCallId.create (strField payload "SecondToolCallId")
                   SecondPhysicalUserMessageId =
                    PhysicalUserMessageId.create (strField payload "SecondPhysicalUserMessageId") |}
        | other -> failwith $"ReviewJournalSurface: unknown Review fact '{other}'"

    let private promptFactOf (caseName: string) (payload: obj) : AgentFact =
        match caseName with
        | "AuthorityRootAccepted" ->
            PromptFact.AuthorityRootAccepted
                {| SessionId = SessionId.create (strField payload "SessionId")
                   LogicalRunId = LogicalRunId.create (strField payload "LogicalRunId")
                   AuthorityRootUserMessageId =
                    AuthorityRootUserMessageId.create (strField payload "AuthorityRootUserMessageId")
                   AuthorityKind = strField payload "AuthorityKind"
                   SelectedAgent = strField payload "SelectedAgent"
                   PeerAgent = strField payload "PeerAgent"
                   CanonicalRole = strField payload "CanonicalRole"
                   SelectedTier = strField payload "SelectedTier" |}
        | other -> failwith $"ReviewJournalSurface: unknown Prompt fact '{other}'"

    let private executionFactOf (caseName: string) (payload: obj) : AgentFact =
        let parent = SessionId.create (strField payload "ParentSessionId")
        let handle = HandleId.Agent(AgentHandleId.create (strField payload "Handle"))

        match caseName with
        | "HandleLinked" ->
            ExecutionFact.HandleLinked
                {| ParentSessionId = parent
                   ChildSessionId = SessionId.create (strField payload "ChildSessionId")
                   Handle = handle
                   TargetAgent = strField payload "TargetAgent"
                   Byname = strField payload "Byname"
                   CanonicalRole = roleOf (field payload "CanonicalRole")
                   Ownership = ownershipOf (field payload "Ownership") |}
        | "HandleCompleted" ->
            ExecutionFact.HandleCompleted
                {| ParentSessionId = parent
                   Handle = handle
                   Kind = completionKindOf (field payload "Kind")
                   CompletionRef = optionalText payload "CompletionRef" |> Option.map BlobRef.create
                   CompletionDigest = optionalText payload "CompletionDigest" |> Option.map BlobDigest.create |}
        | "HandleRetired" ->
            ExecutionFact.HandleRetired
                {| ParentSessionId = parent
                   Handle = handle |}
        | other -> failwith $"ReviewJournalSurface: unknown Execution fact '{other}'"

    let private companionFactOf (caseName: string) (payload: obj) : AgentFact =
        match caseName with
        | "XTracePartAppended" ->
            CompanionFact.XTracePartAppended
                {| SessionId = SessionId.create (strField payload "SessionId")
                   CursorSequence = int64 (unbox<int> (field payload "CursorSequence"))
                   Role = strField payload "Role"
                   Turn = unbox<int> (field payload "Turn")
                   PartIndex = unbox<int> (field payload "PartIndex")
                   Kind = strField payload "Kind"
                   ToolName = optionalText payload "ToolName"
                   TextRef = BlobRef.create (strField payload "TextRef")
                   TextDigest = BlobDigest.create (strField payload "TextDigest")
                   Provenance = strField payload "Provenance"
                   ProviderRun = optionalText payload "ProviderRun" |> Option.map ProviderRunIdentity.create
                   ToolCallId = optionalText payload "ToolCallId" |> Option.map ToolCallId.create
                   HostToolPartId = optionalText payload "HostToolPartId" |> Option.map HostToolPartId.create |}
        | other -> failwith $"ReviewJournalSurface: unknown Companion fact '{other}'"

    let private factOf (family: string) (caseName: string) (payload: obj) : AgentFact =
        match family with
        | "Review" -> reviewFactOf caseName payload
        | "Prompt" -> promptFactOf caseName payload
        | "Execution" -> executionFactOf caseName payload
        | "Companion" -> companionFactOf caseName payload
        | other -> failwith $"ReviewJournalSurface: unsupported fact family '{other}'"

    let private appendResult result =
        match result with
        | Ok _ -> box {| ok = true |}
        | Error failure ->
            box
                {| ok = false
                   error = JournalAppendFailure.describe failure |}

    let appendAgent
        (handle: JournalHandle)
        (sessionId: string)
        (providerRun: obj)
        (family: string)
        (caseName: string)
        (payload: obj)
        : Task<obj> =
        task {
            try
                let! result =
                    AgentJournal.appendAgent
                        (streamOfSession sessionId)
                        (runOf providerRun)
                        (factOf family caseName payload)
                        handle.Journal

                return appendResult result
            with error ->
                return box {| ok = false; error = error.Message |}
        }

    let appendReview
        (handle: JournalHandle)
        (sessionId: string)
        (providerRun: obj)
        (caseName: string)
        (payload: obj)
        : Task<obj> =
        appendAgent handle sessionId providerRun "Review" caseName payload

    let appendAuthorityRoot (handle: JournalHandle) (sessionId: string) (agent: string) : Task<obj> =
        appendAgent
            handle
            sessionId
            null
            "Prompt"
            "AuthorityRootAccepted"
            (box
                {| SessionId = sessionId
                   LogicalRunId = $"run-{sessionId}"
                   AuthorityRootUserMessageId = $"root-{sessionId}"
                   AuthorityKind = "AgentOwnerRoot"
                   SelectedAgent = $"fast-{agent}"
                   PeerAgent = $"deep-{agent}"
                   CanonicalRole = agent
                   SelectedTier = "fast" |})

    let sessionView (handle: JournalHandle) (sessionId: string) : obj =
        let projection = AgentJournal.snapshot handle.Journal

        match AgentProjection.tryFind (SessionId.create sessionId) projection.AgentProjections with
        | None -> null
        | Some session ->
            let witnessName =
                match session.ReviewGuard with
                | None -> "NoGuard"
                | Some guard ->
                    match guard.Witness with
                    | ReviewWitness.NoReview -> "NoReview"
                    | ReviewWitness.RevisionWitness _ -> "RevisionWitness"
                    | ReviewWitness.Confirmed _ -> "Confirmed"

            let xTraceHead =
                session.XTrace |> Option.map XTraceProjection.head |> Option.defaultValue 0L

            let xTracePartKinds =
                session.XTrace
                |> Option.map (fun xTrace ->
                    XTraceProjection.parts xTrace
                    |> List.map (fun part -> part.Kind)
                    |> List.toArray)
                |> Option.defaultValue [||]

            let closedAttempts =
                session.ReviewGuard
                |> Option.map (fun guard ->
                    guard.ClosedAttempts
                    |> List.map (fun closed ->
                        box
                            {| providerRun = ProviderRunIdentity.value closed.Attempt.ProviderRun
                               toolCallId = ToolCallId.value closed.Attempt.ToolCallId
                               frontier = closed.FrozenFrontier.Sequence |})
                    |> List.toArray)
                |> Option.defaultValue [||]

            box
                {| witness = witnessName
                   xTraceHead = xTraceHead
                   xTracePartKinds = xTracePartKinds
                   closedAttempts = closedAttempts |}

    let sessionViewRaw (handle: JournalHandle) (sessionId: string) : obj =
        let projection = AgentJournal.snapshot handle.Journal

        match AgentProjection.tryFind (SessionId.create sessionId) projection.AgentProjections with
        | None -> null
        | Some session ->
            let witness =
                match session.ReviewGuard with
                | None -> "NoGuard"
                | Some guard ->
                    match guard.Witness with
                    | ReviewWitness.NoReview -> "NoReview"
                    | ReviewWitness.RevisionWitness _ -> "RevisionWitness"
                    | ReviewWitness.Confirmed _ -> "Confirmed"

            box
                {| witness = witness
                   barrier =
                    session.ReviewGuard
                    |> Option.bind (fun guard -> guard.CurrentBarrierId)
                    |> Option.map ReviewBarrierId.value
                    |> Option.toObj |}

    let xTraceHead (handle: JournalHandle) (sessionId: string) : int64 =
        let projection = AgentJournal.snapshot handle.Journal

        AgentProjection.tryFind (SessionId.create sessionId) projection.AgentProjections
        |> Option.bind (fun session -> session.XTrace)
        |> Option.map XTraceProjection.head
        |> Option.defaultValue 0L

    let xTracePartKinds (handle: JournalHandle) (sessionId: string) : string array =
        let projection = AgentJournal.snapshot handle.Journal

        AgentProjection.tryFind (SessionId.create sessionId) projection.AgentProjections
        |> Option.bind (fun session -> session.XTrace)
        |> Option.map (fun xTrace ->
            XTraceProjection.parts xTrace
            |> List.map (fun part -> part.Kind)
            |> List.toArray)
        |> Option.defaultValue [||]
