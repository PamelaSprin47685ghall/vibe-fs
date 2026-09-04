namespace Wanxiangshu.Mission.Relay

open System
open Fable.Core.JsInterop
open Wanxiangshu.Host

module ProjectionSurface =
    let maxRisks = 16
    let maxEvidenceRefs = 24

    let private property (value: obj) (name: string) : obj =
        if isNull value then null else value?(name)

    let private stringProperty value name =
        let candidate = property value name
        if isNull candidate then "" else unbox<string> candidate

    let private optionalStringProperty value name =
        let candidate = property value name
        if isNull candidate then None else Some(unbox<string> candidate)

    let private stringArrayProperty value name =
        let candidate = property value name
        let isArray: bool = if isNull candidate then false else emitJsExpr candidate "Array.isArray($0)"
        if isArray then unbox<string array> candidate else [||]

    let private nullableString value =
        match value with
        | None -> null
        | Some text -> box text

    let baton (input: obj) =
        let risks = stringArrayProperty input "risks" |> Array.truncate maxRisks
        let evidenceRefs = stringArrayProperty input "evidenceRefs" |> Array.truncate maxEvidenceRefs
        let fromIncumbency = optionalStringProperty input "fromIncumbency" |> nullableString

        let payload =
            createObj
                [ "schemaVersion" ==> 1
                  "roadId" ==> stringProperty input "roadId"
                  "fromIncumbency" ==> fromIncumbency
                  "source" ==> stringProperty input "source"
                  "authorityRevision" ==> stringProperty input "authorityRevision"
                  "snapshotId" ==> stringProperty input "snapshotId"
                  "risks" ==> risks
                  "evidenceRefs" ==> evidenceRefs ]

        let canonical: string = emitJsExpr payload "JSON.stringify($0)"

        box
            {| source = stringProperty input "source"
               fromIncumbency = fromIncumbency
               risks = risks
               evidenceRefs = evidenceRefs
               canonical = canonical
               digest = HostDigest.sha256Hex canonical |}

    let applyCut (messages: obj array) (cutSequence: int) (staleRunIds: string array) =
        let _ = cutSequence
        let stale = Set.ofArray staleRunIds

        let provider =
            messages
            |> Array.filter (fun message ->
                let run = stringProperty message "run"
                String.IsNullOrEmpty run || not (Set.contains run stale))

        box {| audit = messages; provider = provider |}

    let successorContext
        (rootRequest: string)
        (authorityRevision: string)
        (snapshotId: string)
        (baton: string)
        =
        box
            {| rootRequest = rootRequest
               authorityRevision = authorityRevision
               snapshotId = snapshotId
               baton = baton
               prompt = "此前已有其他同事负责用户的需求。现在由你接手，先独立评审当前完成情况和质量。" |}

