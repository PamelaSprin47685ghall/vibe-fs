namespace Wanxiangshu.OpenCode.Host

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Companion
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Persistence.Journal

module RequirementGroundingSurface =

    type private JournalHandleBox(handle: JournalHandle) =
        member _.Value = handle

    let private journalHandleOf value = (unbox<JournalHandleBox> value).Value
    let private agentJournalOf value = (journalHandleOf value).Journal

    let private isNullish value =
        isNull value || Fable.Core.JsInterop.emitJsExpr value "$0 === undefined"

    let createJournal directory : Task<obj> =
        task {
            let! result =
                JournalSurface.boot directory "requirement-grounding-surface" 0 (DateTimeOffset.UtcNow.ToString("O"))

            if isNullish result?ok || not (unbox<bool> result?ok) then
                return result
            else
                let handle = unbox<JournalHandle> result?journal

                return
                    box
                        {| ok = true
                           journal = (JournalHandleBox handle :> obj) |}
        }

    let disposeJournal journal =
        JournalSurface.dispose (journalHandleOf journal)

    let requestPaths journal workspace sessionId (paths: string array) : Task<obj> =
        task {
            let! result =
                RequirementGroundingRuntime.requestPaths
                    (agentJournalOf journal)
                    workspace
                    (SessionId.create sessionId)
                    (if isNull paths then [] else Array.toList paths)

            return
                match result with
                | Ok decision ->
                    box
                        {| ok = true
                           needsGrounding = decision.NeedsGrounding
                           requested = decision.Requested
                           packages = decision.Packages |> List.toArray |}
                | Error error -> box {| ok = false; error = error |}
        }

    let mutationDecision journal workspace sessionId paths : Task<obj> =
        task {
            let! result =
                RequirementGroundingGate.decideMutation
                    (Some(agentJournalOf journal))
                    workspace
                    sessionId
                    (if isNull paths then [] else Array.toList paths)

            return
                match result with
                | Ok decision ->
                    box
                        {| ok = true
                           allowed = true
                           needsGrounding = decision.NeedsGrounding
                           requested = decision.Requested
                           packages = decision.Packages |> List.toArray |}
                | Error error -> box {| ok = false; error = error |}
        }

    let observationDecision journal workspace sessionId (toolName: string) (args: obj) _output : Task<obj> =
        task {
            let paths =
                if toolName.ToLowerInvariant() <> "read" || isNull args then
                    []
                elif not (isNull args?filePath) then
                    [ string args?filePath ]
                elif not (isNull args?path) then
                    [ string args?path ]
                else
                    []

            let! result =
                RequirementGroundingGate.decideRead
                    (Some(agentJournalOf journal))
                    workspace
                    sessionId
                    paths

            return
                match result with
                | Ok decision ->
                    box
                        {| ok = true
                           needsGrounding = decision.NeedsGrounding
                           requested = decision.Requested
                           packages = decision.Packages |> List.toArray |}
                | Error error -> box {| ok = false; error = error |}
        }

    let projectWithJournal journal sessionId (rawMessages: obj array) : Task<obj> =
        task {
            let! result =
                RequirementGroundingTransform.tryProject
                    (agentJournalOf journal)
                    sessionId
                    (if isNull rawMessages then [] else Array.toList rawMessages)

            return
                match result with
                | Ok messages ->
                    box
                        {| ok = true
                           value = messages |> List.toArray |}
                | Error error -> box {| ok = false; error = error |}
        }

    let groundedIdentities journal sessionId : string array =
        RequirementGroundingRuntime.groundedKeys (agentJournalOf journal) (SessionId.create sessionId)
        |> List.toArray

    let pendingPackages journal sessionId : string array =
        RequirementGroundingRuntime.pending (agentJournalOf journal) (SessionId.create sessionId)
        |> List.map _.PackageName
        |> List.toArray

    let appendContextReanchored journal sessionId (previousEpoch: int64) (nextEpoch: int64) observedRun : Task<obj> =
        task {
            let session = SessionId.create sessionId

            let fact =
                ContextFact.ContextReanchored
                    {| SessionId = session
                       PreviousEpochId = PrefixEpochId.create previousEpoch
                       NextEpochId = PrefixEpochId.create nextEpoch
                       ObservedCompactionRun = ProviderRunIdentity.create observedRun |}

            let! result = AgentJournal.appendAgent (StreamId.Session session) None fact (agentJournalOf journal)

            return
                match result with
                | Ok _ -> box {| ok = true; error = null |}
                | Error failure ->
                    box
                        {| ok = false
                           error = JournalAppendFailure.describe failure |}
        }

    let source = RequirementGroundingTransform.source
    let cursorSeparator = RequirementGroundingTransform.cursorSeparator

    let isGroundingRead raw =
        RequirementGroundingTransform.isGroundingRead raw
