namespace Wanxiangshu.Mission.Review.Assurance

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Resources
open Wanxiangshu.OpenCode

/// Review-assurance owner boundary for semantic tests and host adapters.
///
/// The production review state remains typed and durable. This boundary exposes
/// only strings, arrays, records, and opaque projection handles so callers never
/// need Fable DU/list/result mechanics or internal module paths.

[<RequireQualifiedAccess>]
module ReviewAssuranceSurface =

    type GuardHandle(current: ReviewGuardProjection) =
        member _.Current = current

        static member Create(current: ReviewGuardProjection) = GuardHandle(current)

    type RequirementsHandle(current: ReviewRequirementProjection) =
        member _.Current = current

        static member Create(current: ReviewRequirementProjection) = RequirementsHandle(current)

    type ConfirmedReviewWitnessHandle(witness: ConfirmedReviewWitness) =
        member _.Witness = witness

        static member Create(witness: ConfirmedReviewWitness) = ConfirmedReviewWitnessHandle(witness)

    let private text (value: obj) =
        if isNull value then "" else string value

    let private field (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private firstField (value: obj) (names: string array) : obj =
        names
        |> Array.tryPick (fun name ->
            let candidate = field value name
            if isNull candidate then None else Some candidate)
        |> Option.defaultValue null

    let private physicalOf value =
        PhysicalUserMessageId.create (text value)

    let private sessionOf value = SessionId.create (text value)
    let private runOf value = ProviderRunIdentity.create (text value)
    let private callOf value = ToolCallId.create (text value)
    let private barrierOf value = ReviewBarrierId.create (text value)
    let private treeOf value = GitTreeHash.create (text value)

    let private witnessOf (value: obj) : VerdictWitness =
        { ProviderRun = runOf (firstField value [| "ProviderRun"; "run" |])
          ToolCallId = callOf (firstField value [| "ToolCallId"; "call" |])
          GitTreeHash = treeOf (firstField value [| "GitTreeHash"; "tree" |])
          ReviewerSessionId = sessionOf (firstField value [| "ReviewerSessionId"; "reviewer" |]) }

    let private witnessView (witness: VerdictWitness) : obj =
        box
            {| ProviderRun = ProviderRunIdentity.value witness.ProviderRun
               ToolCallId = ToolCallId.value witness.ToolCallId
               GitTreeHash = GitTreeHash.value witness.GitTreeHash
               ReviewerSessionId = SessionId.value witness.ReviewerSessionId |}

    let private witnessStateView (witness: ReviewWitness) : obj =
        match witness with
        | ReviewWitness.NoReview -> box {| state = "NoReview" |}
        | ReviewWitness.RevisionWitness revision ->
            box
                {| state = "RevisionWitness"
                   report = revision.Report
                   tree = GitTreeHash.value revision.GitTreeHash |}
        | ReviewWitness.Confirmed confirmed ->
            box
                {| state = "Confirmed"
                   barrier = ReviewBarrierId.value confirmed.BarrierId
                   first = witnessView confirmed.First
                   second = witnessView confirmed.Second
                   tree = GitTreeHash.value confirmed.GitTreeHash
                   firstPhysical = PhysicalUserMessageId.value confirmed.FirstPhysicalUserMessageId
                   secondPhysical = PhysicalUserMessageId.value confirmed.SecondPhysicalUserMessageId |}

    let private reviewWitnessOf (value: obj) : ReviewWitness =
        if isNull value then
            ReviewWitness.NoReview
        else
            match text (field value "state") with
            | "RevisionWitness" ->
                ReviewWitness.RevisionWitness
                    {| Report = text (field value "report")
                       GitTreeHash = treeOf (field value "tree") |}
            | "Confirmed" ->
                ReviewWitness.Confirmed
                    {| BarrierId = barrierOf (field value "barrier")
                       First = witnessOf (field value "first")
                       Second = witnessOf (field value "second")
                       GitTreeHash = treeOf (field value "tree")
                       FirstPhysicalUserMessageId =
                        physicalOf (firstField value [| "FirstPhysicalUserMessageId"; "firstPhysical" |])
                       SecondPhysicalUserMessageId =
                        physicalOf (firstField value [| "SecondPhysicalUserMessageId"; "secondPhysical" |]) |}
            | _ -> ReviewWitness.NoReview

    let private attemptView (attempt: ReviewAttemptIdentity) : obj =
        box
            {| ReviewBarrierId = ReviewBarrierId.value attempt.ReviewBarrierId
               GitTreeHash = GitTreeHash.value attempt.GitTreeHash
               ReviewerSessionId = SessionId.value attempt.ReviewerSessionId
               ProviderRun = ProviderRunIdentity.value attempt.ProviderRun
               ToolCallId = ToolCallId.value attempt.ToolCallId |}

    let private attemptOf (value: obj) : ReviewAttemptIdentity =
        { ReviewBarrierId = barrierOf (firstField value [| "ReviewBarrierId"; "barrier" |])
          GitTreeHash = treeOf (firstField value [| "GitTreeHash"; "tree" |])
          ReviewerSessionId = sessionOf (firstField value [| "ReviewerSessionId"; "reviewer" |])
          ProviderRun = runOf (firstField value [| "ProviderRun"; "run" |])
          ToolCallId = callOf (firstField value [| "ToolCallId"; "call" |]) }

    let private guardProjectionView (guard: ReviewGuardProjection) : obj =
        let barrier =
            guard.CurrentBarrierId |> Option.map ReviewBarrierId.value |> Option.toObj

        let manager =
            guard.CurrentManagerSessionId |> Option.map SessionId.value |> Option.toObj

        let tree = guard.LastGitTreeHash |> Option.map GitTreeHash.value |> Option.toObj

        box
            {| barrier = barrier
               managerSession = manager
               tree = tree
               witness = witnessStateView guard.Witness
               observedAttempts = List.length guard.ObservedAttempts
               closedAttempts = List.length guard.ClosedAttempts |}

    let private rejectionName rejection =
        match rejection with
        | VerdictRejection.DuplicateAttempt -> "DuplicateAttempt"
        | VerdictRejection.NotDistinctAttempt -> "NotDistinctAttempt"

    let private verdictOf value =
        match text value with
        | "REVISE"
        | "Revise" -> ReviewGuardVerdict.Revise
        | "PERFECT"
        | "Perfect" -> ReviewGuardVerdict.Perfect
        | other -> invalidArg "value" $"unknown review verdict '{other}'"

    let challengePath = ReviewChallenge.Path

    let challengeText (language: string) =
        ProviderResources.readText (ProviderLanguage.parse language) challengePath

    let challengePrompt (text: string) = ReviewChallenge.promptOf text

    let challengeObject (language: string) : obj =
        let text = challengeText language
        let prompt = challengePrompt text

        box
            {| path = challengePath
               text = text
               prompt = prompt |}

    /// REVIEW-006 self-contained witness. The raw shape deliberately contains
    /// no authority-root field.
    let verdictWitness (value: obj) : obj = witnessOf value |> witnessView

    let attemptIdentity (barrier: string) (witness: obj) : obj =
        ReviewWitness.attemptIdentity (barrierOf (box barrier)) (witnessOf witness)
        |> attemptView

    let dedupeKey (attempt: obj) =
        ReviewAttemptIdentity.dedupeKey (attemptOf attempt)

    let isDistinctAttempt (barrier: string) (first: obj) (second: obj) =
        ReviewWitness.isDistinctAttempt (barrierOf (box barrier)) (witnessOf first) (witnessOf second)

    let confirmWitness
        (barrier: string)
        (firstPhysical: string)
        (secondPhysical: string)
        (first: obj)
        (second: obj)
        : obj =
        match
            ReviewWitness.confirm
                (barrierOf (box barrier))
                (physicalOf (box firstPhysical))
                (physicalOf (box secondPhysical))
                (witnessOf first)
                (witnessOf second)
        with
        | None -> null
        | Some witness -> witnessStateView witness

    let isConfirmed (witness: obj) : bool =
        match witness with
        | :? GuardHandle as guard -> ReviewWitness.isConfirmed guard.Current.Witness
        | _ -> reviewWitnessOf witness |> ReviewWitness.isConfirmed

    let isRevision (witness: obj) : bool =
        match witness with
        | :? GuardHandle as guard -> ReviewWitness.isRevision guard.Current.Witness
        | _ -> reviewWitnessOf witness |> ReviewWitness.isRevision

    let private witnessPublicView (witness: ReviewWitness) : obj =
        match witness with
        | ReviewWitness.NoReview -> box {| state = "NoReview" |}
        | ReviewWitness.RevisionWitness revision ->
            box
                {| state = "RevisionWitness"
                   report = revision.Report
                   tree = GitTreeHash.value revision.GitTreeHash |}
        | ReviewWitness.Confirmed confirmed ->
            box
                {| state = "Confirmed"
                   barrier = ReviewBarrierId.value confirmed.BarrierId
                   tree = GitTreeHash.value confirmed.GitTreeHash
                   first =
                    box
                        {| run = ProviderRunIdentity.value confirmed.First.ProviderRun
                           call = ToolCallId.value confirmed.First.ToolCallId
                           tree = GitTreeHash.value confirmed.First.GitTreeHash
                           reviewer = SessionId.value confirmed.First.ReviewerSessionId |}
                   second =
                    box
                        {| run = ProviderRunIdentity.value confirmed.Second.ProviderRun
                           call = ToolCallId.value confirmed.Second.ToolCallId
                           tree = GitTreeHash.value confirmed.Second.GitTreeHash
                           reviewer = SessionId.value confirmed.Second.ReviewerSessionId |}
                   firstPhysical = PhysicalUserMessageId.value confirmed.FirstPhysicalUserMessageId
                   secondPhysical = PhysicalUserMessageId.value confirmed.SecondPhysicalUserMessageId |}

    let readWitness (witness: obj) : obj =
        reviewWitnessOf witness |> witnessPublicView

    let confirmedWitnessRecord (witness: obj) : obj =
        match reviewWitnessOf witness with
        | ReviewWitness.Confirmed confirmed ->
            box
                {| BarrierId = ReviewBarrierId.value confirmed.BarrierId
                   First = witnessView confirmed.First
                   Second = witnessView confirmed.Second
                   GitTreeHash = GitTreeHash.value confirmed.GitTreeHash
                   FirstPhysicalUserMessageId = PhysicalUserMessageId.value confirmed.FirstPhysicalUserMessageId
                   SecondPhysicalUserMessageId = PhysicalUserMessageId.value confirmed.SecondPhysicalUserMessageId |}
        | _ -> null

    let noReview: obj = box {| state = "NoReview" |}

    let gitTreeHash (witness: obj) : string =
        reviewWitnessOf witness
        |> ReviewWitness.gitTreeHash
        |> Option.map GitTreeHash.value
        |> Option.toObj

    let confirmedReviewer (witness: obj) : string =
        reviewWitnessOf witness
        |> ReviewWitness.confirmedReviewer
        |> Option.map SessionId.value
        |> Option.toObj

    let isValidForTree (tree: string) (witness: obj) : bool =
        ReviewWitness.isValidForTree (treeOf (box tree)) (reviewWitnessOf witness)

    let projectConfirmedReview (lifeId: string) (requestId: string) (tree: string) (memberWitnesses: obj array) : obj =
        let members =
            if isNull memberWitnesses then
                []
            else
                memberWitnesses
                |> Array.toList
                |> List.map (fun item ->
                    let reviewer = sessionOf (firstField item [| "reviewer"; "ReviewerSessionId" |])
                    let barrier = barrierOf (firstField item [| "barrier"; "BarrierId" |])
                    let witness = reviewWitnessOf (firstField item [| "witness"; "Witness" |])
                    (reviewer, barrier, witness))

        match
            ConfirmedReviewWitness.create
                (ManagerLifeId.create lifeId)
                (FinalityRequestId.create requestId)
                (treeOf (box tree))
                members
        with
        | Ok witness ->
            box
                {| ok = true
                   witness = ConfirmedReviewWitnessHandle.Create witness |}
        | Error error -> box {| ok = false; error = error |}

    let confirmedReviewWitnessTree (witness: ConfirmedReviewWitnessHandle) : string =
        GitTreeHash.value (ConfirmedReviewWitness.gitTreeHash witness.Witness)

    let isConfirmedReviewValidForTree (tree: string) (witness: ConfirmedReviewWitnessHandle) : bool =
        ReviewCandidate.isWitnessValidForTree (treeOf (box tree)) witness.Witness

    let verifyCandidate (candidateTree: string) (witness: ConfirmedReviewWitnessHandle) : obj =
        match ReviewCandidate.verifyCandidate (treeOf (box candidateTree)) witness.Witness with
        | Ok() -> box {| ok = true |}
        | Error(CandidateVerificationFailure.StaleWitness(curr, expected)) ->
            box
                {| ok = false
                   error = "StaleWitness"
                   candidateTree = GitTreeHash.value curr
                   witnessTree = GitTreeHash.value expected |}
        | Error(CandidateVerificationFailure.IncompleteCohort reason) ->
            box
                {| ok = false
                   error = "IncompleteCohort"
                   reason = reason |}

    let emptyGuard () : GuardHandle =
        GuardHandle.Create ReviewProjection.empty

    let startBarrier (manager: string) (barrier: string) (tree: string) (current: GuardHandle) : GuardHandle =
        let next =
            ReviewProjection.startBarrier
                (sessionOf (box manager))
                (barrierOf (box barrier))
                (treeOf (box tree))
                current.Current

        GuardHandle.Create next

    let applyVerdict (attempt: obj) (verdict: string) (current: GuardHandle) : obj =
        match ReviewProjection.applyVerdict (attemptOf attempt) (verdictOf (box verdict)) current.Current with
        | Ok next ->
            box
                {| ok = true
                   value = GuardHandle.Create next |}
        | Error rejection ->
            box
                {| ok = false
                   error = rejectionName rejection |}

    let applyConfirmedWitness
        (barrier: string)
        (firstPhysical: string)
        (secondPhysical: string)
        (first: obj)
        (second: obj)
        (current: GuardHandle)
        : obj =
        match
            ReviewProjection.applyConfirmedWitness
                (barrierOf (box barrier))
                (physicalOf (box firstPhysical))
                (physicalOf (box secondPhysical))
                (witnessOf first)
                (witnessOf second)
                current.Current
        with
        | Ok next ->
            box
                {| ok = true
                   value = GuardHandle.Create next |}
        | Error rejection ->
            box
                {| ok = false
                   error = rejectionName rejection |}

    let guardView (current: GuardHandle) : obj = guardProjectionView current.Current

    let guardWitness (current: GuardHandle) : obj =
        witnessPublicView current.Current.Witness

    let hasObservedAttempt (attempt: obj) (current: GuardHandle) : bool =
        ReviewProjection.hasObservedAttempt (attemptOf attempt) current.Current

    let satisfiesGuard (tree: string) (current: GuardHandle) : bool =
        ReviewProjection.satisfiesGuard (treeOf (box tree)) current.Current

    let requirementsEmpty () : RequirementsHandle =
        RequirementsHandle.Create ReviewRequirementProjection.empty

    let addRequirement (session: string) (authorityRoot: string) (current: RequirementsHandle) : RequirementsHandle =
        ReviewRequirementProjection.addRequirement
            (sessionOf (box session))
            (AuthorityRootUserMessageId.create (box authorityRoot |> text))
            current.Current
        |> RequirementsHandle.Create

    let clearRequirements (providerRun: string) (current: RequirementsHandle) : RequirementsHandle =
        ReviewRequirementProjection.clearOnConfirmation (runOf (box providerRun)) current.Current
        |> RequirementsHandle.Create

    let requirementsView (current: RequirementsHandle) : obj =
        let value = current.Current

        box
            {| humanPromptInputs =
                ReviewRequirementProjection.inputs value
                |> List.map (fun input ->
                    box
                        {| sourceSessionId = SessionId.value input.SourceSessionId
                           authorityRoot = AuthorityRootUserMessageId.value input.AuthorityRootUserMessageId |})
                |> List.toArray
               lastConfirmedProviderRun =
                value.LastConfirmedProviderRun
                |> Option.map ProviderRunIdentity.value
                |> Option.toObj |}

    /// Provider run binding result. The raw Host message is adapted once here;
    /// SessionMessage never crosses the owner boundary.
    let bindableRun (physicalUser: string) (messages: obj array) : obj =
        let typedMessages = SessionSnapshotPort.projectMessages messages

        match ProviderRunBinding.bindableRun physicalUser typedMessages with
        | Ok message ->
            box
                {| ok = true
                   id = message.Id
                   parentId = message.ParentId |> Option.toObj
                   completed = message.Completed |}
        | Error ProviderRunBinding.Rejection.NoBindableRun ->
            box
                {| ok = false
                   error = "NoBindableRun" |}
        | Error(ProviderRunBinding.Rejection.AmbiguousRun count) ->
            box
                {| ok = false
                   error = "AmbiguousRun"
                   count = count |}
        | Error ProviderRunBinding.Rejection.NotLatestRun -> box {| ok = false; error = "NotLatestRun" |}
        | Error ProviderRunBinding.Rejection.InsufficientSequence ->
            box
                {| ok = false
                   error = "InsufficientSequence" |}
