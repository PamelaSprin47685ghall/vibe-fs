namespace Wanxiangshu.Enforcer.Guidance

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Companion
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// Opaque JS owner for Main tip-guidance delivery. Journal handles and typed
/// facts stay inside this boundary; tests provide only semantic ids and
/// observation payload data as plain objects.
[<RequireQualifiedAccess>]
module TipSurface =

    type private JournalHandleBox(handle: JournalHandle) =
        member _.Value = handle

    let private journalHandleOf (value: obj) = (unbox<JournalHandleBox> value).Value
    let private agentJournalOf (value: obj) = (journalHandleOf value).Journal

    let private isNullish (value: obj) : bool =
        isNull value || emitJsExpr value "$0 === undefined"

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private optionalText (value: obj) : string option =
        if isNullish value then None else Some(text value)

    let private intValue (value: obj) : int =
        if isNullish value then 0 else int (text value)

    let private int64Value (value: obj) : int64 =
        if isNullish value then 0L else int64 (text value)

    let private stringArray (value: obj) : string array =
        if isNullish value then [||] else unbox<string array> value

    let private appendResult (result: Result<ProjectionSet, JournalAppendFailure>) : obj =
        match result with
        | Ok _ -> box {| ok = true |}
        | Error failure ->
            box
                {| ok = false
                   error = JournalAppendFailure.describe failure |}

    let private appendAgent
        (journal: obj)
        (session: string)
        (providerRun: ProviderRunIdentity option)
        (fact: AgentFact)
        : Task<obj> =
        task {
            if isNullish journal then
                return
                    box
                        {| ok = false
                           error = "journal required" |}
            else
                let! result =
                    AgentJournal.appendAgent
                        (StreamId.Session(SessionId.create session))
                        providerRun
                        fact
                        (agentJournalOf journal)

                return appendResult result
        }

    /// Boot a real EventStore journal behind an opaque capability.
    let createJournal (directory: string) : Task<obj> =
        task {
            let! result = JournalSurface.boot directory "tip-guidance-surface" 0 (DateTimeOffset.UtcNow.ToString("O"))

            if isNullish result?ok || not (unbox<bool> result?ok) then
                return result
            else
                let handle = unbox<JournalHandle> result?journal

                return
                    box
                        {| ok = true
                           journal = (JournalHandleBox handle :> obj) |}
        }

    let disposeJournal (journal: obj) : unit =
        JournalSurface.dispose (journalHandleOf journal)

    /// Link a Main session to its Blogger companion.
    let appendCompanionLink (journal: obj) (value: obj) : Task<obj> =
        let session = text value?session

        let fact =
            CompanionFact.CompanionBloggerLinked
                {| SessionId = SessionId.create session
                   BloggerSessionId = SessionId.create (text value?bloggerSession)
                   BloggerAgent = text value?bloggerAgent |}

        appendAgent journal session None fact

    /// Commit one context observation through the same durable owner path used
    /// by the Blogger cycle. All fields are semantic strings/numbers here; the
    /// typed identities and fact family are translated only inside the owner.
    let appendObservation (journal: obj) (value: obj) : Task<obj> =
        let session = text value?session
        let providerRun = ProviderRunIdentity.create (text value?providerRun)

        let toolCallIds =
            stringArray value?toolCallIds |> Array.map ToolCallId.create |> Array.toList

        let evidenceRef = optionalText value?evidenceRef |> Option.map BlobRef.create

        let fact =
            ContextFact.BlogObservationCommitted
                {| SessionId = SessionId.create session
                   BloggerSessionId = SessionId.create (text value?bloggerSession)
                   RequestId = BloggerRequestId.create (text value?requestId)
                   FrameEpochId = FrameEpochId.create (int64Value value?frameEpoch)
                   PreviousIngestedThroughSequence = int64Value value?previousIngestedThrough
                   NextIngestedThroughSequence = int64Value value?nextIngestedThrough
                   PreviousCoverableTurnCutoffExclusive = intValue value?previousCutoff
                   NextCoverableTurnCutoffExclusive = intValue value?nextCutoff
                   NextCoveredPrefixDigest = text value?nextCoveredPrefixDigest
                   TextRef = BlobRef.create (text value?textRef)
                   TextDigest = BlobDigest.create (text value?textDigest)
                   ProviderRun = providerRun
                   ToolCallIds = toolCallIds
                   TipRuleId = text value?tipRuleId
                   FieldNameAtCommit = optionalText value?fieldNameAtCommit
                   EvidenceRef = evidenceRef
                   ObservedPrefixEpochId = PrefixEpochId.create (int64Value value?observedPrefixEpoch) |}

        appendAgent journal session (Some providerRun) fact

    /// Append the Host compaction/reanchor fact that voids Full tip coverage.
    let appendContextReanchored (journal: obj) (value: obj) : Task<obj> =
        let session = text value?session
        let providerRun = ProviderRunIdentity.create (text value?observedCompactionRun)

        let fact =
            ContextFact.ContextReanchored
                {| SessionId = SessionId.create session
                   PreviousEpochId = PrefixEpochId.create (int64Value value?previousEpoch)
                   NextEpochId = PrefixEpochId.create (int64Value value?nextEpoch)
                   ObservedCompactionRun = providerRun |}

        appendAgent journal session (Some providerRun) fact

    let private guidanceToJs (guidance: TipGuidance) : obj =
        box
            {| tipName = guidance.TipName
               presentation =
                match guidance.Presentation with
                | Wanxiangshu.OpenCode.Host.TipPresentation.Full -> "Full"
                | Wanxiangshu.OpenCode.Host.TipPresentation.IdentityOnly -> "IdentityOnly"
               text = guidance.Text |}

    /// Resolve one Main/Blogger session to the localized Full or Identity
    /// guidance object. `null` means no owner or no recent tip.
    let resolve (journal: obj) (session: string) : Task<obj> =
        task {
            let! guidance = EnforcerTipGuidance.resolveTipGuidance (agentJournalOf journal) (SessionId.create session)

            match guidance with
            | Some value -> return box (guidanceToJs value)
            | None -> return null
        }

    let latest (journal: obj) (session: string) : Task<obj> =
        task {
            let! value = EnforcerTipGuidance.latestTipGuidance (agentJournalOf journal) (SessionId.create session)

            match value with
            | Some text -> return box text
            | None -> return null
        }

    let latestNudge (journal: obj) (session: string) : Task<obj> =
        task {
            let! value = EnforcerTipGuidance.latestTipNudge (agentJournalOf journal) (SessionId.create session)

            match value with
            | Some text -> return box text
            | None -> return null
        }
