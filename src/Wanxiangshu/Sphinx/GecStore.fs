// WHAT[EPI-019,EPI-028]: canonical-spine store codec over the durable EventStore.
namespace Wanxiangshu.Sphinx

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.Core

/// WHAT[EPI-019]: pure codec, conflict gate, fold and export over the canonical
/// durable spine. Every durable byte lives in the canonical EventStore (owned by
/// Persistence); this module only translates caller JS events into envelopes,
/// judges appends against a caller-held Current, folds caller-supplied envelope
/// lists, and renders export bundles. No file access, no clock, no private store.
module GecStore =

    let private isUndefined (value: obj) : bool = emitJsExpr value "$0 === undefined"

    let private isNullish (value: obj) : bool = isNull value || isUndefined value

    let private isJsArray (value: obj) : bool = emitJsExpr value "Array.isArray($0)"

    let private isFiniteFloat (value: float) : bool = emitJsExpr value "Number.isFinite($0)"

    let private fieldOf (value: obj) (name: string) : obj =
        if isNullish value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private textOf (value: obj) : string =
        if isNullish value then "" else string value

    let private arrayOf (value: obj) : obj array =
        if isNullish value || not (isJsArray value) then
            [||]
        else
            unbox<obj array> value

    let private keysOf (value: obj) : string array = emitJsExpr value "Object.keys($0)"

    let private nonBlank (value: string) : bool = not (String.IsNullOrWhiteSpace value)

    let private typedError (code: string) (message: string) : obj =
        box
            {| ok = false
               error = box {| code = code; message = message |} |}

    let private intField (fallback: int) (value: obj) : int =
        if isNullish value then
            fallback
        else
            let number: float = emitJsExpr value "$0"

            if isFiniteFloat number then int number else fallback

    let private parentsOf (value: obj) : string array =
        arrayOf value |> Array.map textOf |> Array.filter nonBlank

    let private payloadOf (input: obj) : obj =
        let payload: obj = fieldOf input "payload"

        if isNullish payload then createObj [] else payload

    let private envelopeId (inquiryId: string) (revision: int) (kind: string) : string =
        (CoreHash.sha256Hex (inquiryId + "|" + string revision + "|" + kind))
            .Substring(0, 40)

    let private prefixedEventType (kind: string) : string option =
        let prefixed: string = "sphinx/" + kind

        if SphinxEventTypes.isSphinxEvent prefixed then
            Some prefixed
        else
            None

    let private resolveEventType (kind: string) : string option =
        if SphinxEventTypes.isSphinxEvent kind then
            Some kind
        else
            prefixedEventType kind

    let private attemptKeyOf (workId: string) (attemptRaw: obj) : string =
        let attempt: float = emitJsExpr attemptRaw "$0"

        if isFiniteFloat attempt then
            workId + "|" + string (int attempt)
        else
            ""

    let private seenKeyOf (payload: obj) : string =
        let workId: string = textOf (fieldOf payload "workId")
        let attemptRaw: obj = fieldOf payload "attempt"

        if String.IsNullOrWhiteSpace workId || isNullish attemptRaw then
            ""
        else
            attemptKeyOf workId attemptRaw

    let private payloadHashOf (payload: obj) : string = CoreHash.canonicalSha256 payload

    let private currentRevisionOf (current: obj) : int = intField 0 (fieldOf current "revision")

    let private addSeenHash (seen: obj) (acc: Map<string, string>) (key: string) : Map<string, string> =
        let hash: string = textOf (fieldOf seen key)

        if String.IsNullOrWhiteSpace hash then
            acc
        else
            Map.add key hash acc

    let private seenOf (current: obj) : Map<string, string> =
        let seen: obj = fieldOf current "seen"

        if isNullish seen then
            Map.empty
        else
            keysOf seen
            |> Array.toList
            |> List.fold
                (fun (acc: Map<string, string>) (key: string) -> addSeenHash seen acc key)
                Map.empty<string, string>

    let private seenView (seen: Map<string, string>) : obj =
        seen
        |> Map.toList
        |> List.map (fun (key: string, hash: string) -> key ==> box hash)
        |> createObj

    let private isJsObject (value: obj) : bool =
        emitJsExpr value "typeof $0 === 'object'"

    let private encodeKnownEnvelope (input: obj) (inquiryId: string) (kind: string) : obj =
        match resolveEventType kind with
        | None -> typedError "unknown-kind" (sprintf "sphinx event kind is outside the vocabulary: %s" kind)
        | Some eventType ->
            let revision: int = intField 0 (fieldOf input "revision")
            let parents: string array = parentsOf (fieldOf input "parents")
            let payload: obj = payloadOf input
            let id: string = envelopeId inquiryId revision kind

            let envelope: obj =
                createObj
                    [ "id" ==> box id
                      "stream" ==> box ("sphinx/" + inquiryId)
                      "type" ==> box eventType
                      "parents" ==> box parents
                      "payload" ==> payload
                      "payloadRefs" ==> box Array.empty<string> ]

            box {| ok = true; envelope = envelope |}

    let private encodeSphinxEnvelope (input: obj) : obj =
        let inquiryId: string = textOf (fieldOf input "inquiryId")
        let kind: string = textOf (fieldOf input "kind")

        if isNullish input || not (isJsObject input) then
            typedError "missing-event" "sphinx event must be an object"
        elif String.IsNullOrWhiteSpace inquiryId then
            typedError "missing-inquiry" "sphinx event needs a non-blank inquiryId"
        elif String.IsNullOrWhiteSpace kind then
            typedError "missing-kind" "sphinx event needs a non-blank kind"
        else
            encodeKnownEnvelope input inquiryId kind

    let private appendExpectedRevision (input: obj) (revision: int) : int =
        let expectedRaw: obj = fieldOf input "expectedRevision"

        if isNullish expectedRaw then
            revision
        else
            intField revision expectedRaw

    let private decideAppendDuplicate (revision: int) (seen: Map<string, string>) (envelope: obj) : obj =
        let body: obj = payloadOf envelope
        let key: string = seenKeyOf body
        let hash: string = payloadHashOf body

        match (if key = "" then None else Map.tryFind key seen) with
        | Some known when known = hash ->
            box
                {| ok = true
                   revision = revision
                   duplicate = true |}
        | Some _ -> typedError "DUPLICATE_CONFLICT" (sprintf "work attempt %s already recorded a different payload" key)
        | None ->
            box
                {| ok = true
                   revision = revision + 1
                   duplicate = false |}

    let private checkAppend (input: obj) : obj =
        let current: obj = fieldOf input "current"
        let envelope: obj = fieldOf input "envelope"
        let revision: int = currentRevisionOf current
        let seen: Map<string, string> = seenOf current
        let expected: int = appendExpectedRevision input revision

        if isNullish input || isNullish current || isNullish envelope then
            typedError "missing-input" "checkAppend needs current and envelope"
        elif expected <> revision then
            typedError "REVISION_CONFLICT" (sprintf "expected revision %d but current is %d" expected revision)
        else
            decideAppendDuplicate revision seen envelope

    let private advanceSphinxSeen
        (revision: int)
        (seen: Map<string, string>)
        (envelope: obj)
        : Result<int * Map<string, string>, obj> =
        let body: obj = payloadOf envelope
        let key: string = seenKeyOf body
        let hash: string = payloadHashOf body

        match (if key = "" then None else Map.tryFind key seen) with
        | Some known when known = hash -> Ok(revision, seen)
        | Some _ ->
            Error(typedError "DUPLICATE_CONFLICT" (sprintf "work attempt %s already recorded a different payload" key))
        | None ->
            let next: Map<string, string> = if key = "" then seen else Map.add key hash seen

            Ok(revision + 1, next)

    let private sphinxHeadOf (envelopes: obj array) : obj =
        if envelopes.Length = 0 then
            null
        else
            box (textOf (fieldOf envelopes.[envelopes.Length - 1] "id"))

    let private sphinxCurrentOf (envelopes: obj array) : obj =
        let folder
            (acc: Result<int * Map<string, string>, obj>)
            (envelope: obj)
            : Result<int * Map<string, string>, obj> =
            acc
            |> Result.bind (fun (revision: int, seen: Map<string, string>) -> advanceSphinxSeen revision seen envelope)

        match Array.fold folder (Ok(0, Map.empty<string, string>)) envelopes with
        | Error conflict -> conflict
        | Ok(revision: int, seen: Map<string, string>) ->
            box
                {| ok = true
                   current =
                    {| revision = revision
                       seen = seenView seen |}
                   eventHead = sphinxHeadOf envelopes
                   semanticHash = CoreHash.canonicalSha256 envelopes |}

    let private sphinxCurrent (input: obj) : obj =
        let raw: obj = fieldOf input "envelopes"

        if isNullish input then
            typedError "missing-envelopes" "envelopes are required"
        elif isNullish raw || not (isJsArray raw) then
            typedError "missing-envelopes" "envelopes must be an array"
        else
            sphinxCurrentOf (unbox<obj array> raw)

    let private isAnswerEnvelope (envelope: obj) : bool =
        textOf (fieldOf envelope "type") = SphinxEventTypes.AnswerCommitted

    let private claimTextOf (envelope: obj) : string =
        textOf (fieldOf (fieldOf envelope "payload") "claim")

    let private hasSource (source: obj) : bool =
        if isNullish source then
            false
        else
            let isObject: bool = emitJsExpr source "typeof $0 === 'object'"
            isObject && nonBlank (textOf (fieldOf source "id"))

    let private claimWithSource (claim: string) (source: obj) : obj option =
        let kind, sources =
            if hasSource source then
                "externally-grounded-claim", box [| source |]
            else
                "model-belief", box Array.empty<obj>

        Some(
            box
                {| kind = kind
                   text = claim
                   sources = sources |}
        )

    let private claimOf (envelope: obj) : obj option =
        let payload: obj = fieldOf envelope "payload"
        let claim: string = textOf (fieldOf payload "claim")

        if String.IsNullOrWhiteSpace claim then
            None
        else
            claimWithSource claim (fieldOf payload "source")

    let private answerOf (envelopes: obj array) : obj =
        match envelopes |> Array.tryFindBack isAnswerEnvelope with
        | Some envelope ->
            let answerText: string = textOf (fieldOf (fieldOf envelope "payload") "text")
            box {| text = answerText |}
        | None -> box {| text = "" |}

    let private branchTreeOf (envelopes: obj array) : obj =
        let branches: string array =
            envelopes
            |> Array.map (fun (envelope: obj) -> textOf (fieldOf (fieldOf envelope "payload") "workId"))
            |> Array.filter nonBlank
            |> Array.distinct
            |> Array.sort

        box {| branches = branches |}

    let private firstClaimOf (envelopes: obj array) : string =
        envelopes
        |> Array.tryPick (fun (envelope: obj) ->
            let claim: string = claimTextOf envelope

            if String.IsNullOrWhiteSpace claim then None else Some claim)
        |> Option.defaultValue ""

    let private exportBundleOf (envelopes: obj array) : obj =
        let answer: obj = answerOf envelopes

        let claims: obj array =
            envelopes |> Array.choose (fun (envelope: obj) -> claimOf envelope)

        createObj
            [ "events" ==> box envelopes
              "eventHead" ==> sphinxHeadOf envelopes
              "semanticHash" ==> box (CoreHash.canonicalSha256 envelopes)
              "answerHash" ==> box (CoreHash.canonicalSha256 answer)
              "pluginManifests" ==> box Array.empty<obj>
              "schemaManifests" ==> box Array.empty<obj>
              "modelManifest"
              ==> box
                      {| id = "sphinx-unknown"
                         release = "0.0.0" |}
              "branchTree" ==> branchTreeOf envelopes
              "randomizationMatrix" ==> box {| assignments = Array.empty<obj> |}
              "resourceLedger" ==> box {| entries = Array.empty<obj> |}
              "certificates" ==> createObj []
              "rankingDiagnostics" ==> createObj []
              "framingDiagnostics" ==> createObj []
              "calibrationDiagnostics" ==> createObj []
              "initialDisposition" ==> box {| text = firstClaimOf envelopes |}
              "reflectiveDisposition" ==> box {| text = "" |}
              "minorityModes" ==> box Array.empty<obj>
              "answer" ==> answer
              "claims" ==> box claims ]

    let private exportFromEvents (input: obj) : obj =
        let raw: obj = fieldOf input "events"

        if isNullish input then
            typedError "missing-events" "events are required"
        elif isNullish raw || not (isJsArray raw) then
            typedError "missing-events" "events must be an array"
        else
            exportBundleOf (unbox<obj array> raw)

    let private replayHashesOf (bundle: obj) (events: obj) : obj =
        let answer: obj = fieldOf bundle "answer"
        let answerBody: obj = if isNullish answer then createObj [] else answer

        box
            {| semanticHash = CoreHash.canonicalSha256 events
               answerHash = CoreHash.canonicalSha256 answerBody |}

    let private replayExportBundle (input: obj) : obj =
        let bundle: obj = fieldOf input "bundle"
        let events: obj = fieldOf bundle "events"

        if isNullish input then
            typedError "missing-bundle" "bundle is required"
        elif isNullish bundle then
            typedError "missing-bundle" "bundle is required"
        elif isNullish events || not (isJsArray events) then
            typedError "missing-events" "bundle events must be an array"
        else
            replayHashesOf bundle events

    let methods: (string * obj) list =
        [ "encodeSphinxEnvelope", box encodeSphinxEnvelope
          "checkAppend", box checkAppend
          "sphinxCurrent", box sphinxCurrent
          "exportFromEvents", box exportFromEvents
          "replayExportBundle", box replayExportBundle ]
