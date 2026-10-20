namespace Wanxiangshu.Participant.Provider.Projection

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module ProjectionPlanner =

    /// 意图规划规则的唯一实现：wanxiangli/projection `plan`（CUT-17）。宿主只把意图翻成
    /// `{ kind, key, anchor, rows, fingerprint }`，再按库给出的规范顺序取回原意图。
    [<AllowNullLiteral>]
    type private ProjectionPlans =
        abstract plan: intents: obj -> obj
        abstract layout: intents: obj * baseLength: int -> obj

    [<Import("Projection", "wanxiangli/projection")>]
    let private createProjection: unit -> ProjectionPlans = jsNative

    let private projection = createProjection ()

    [<Emit("JSON.stringify($0)")>]
    let private json (value: obj) : string = jsNative

    let private partValue (part: ProviderProjection.WirePart) : obj =
        match part with
        | ProviderProjection.WireText text -> box [| box "text"; box text |]
        | ProviderProjection.WireReasoning text -> box [| box "reasoning"; box text |]
        | ProviderProjection.WireToolCall(callId, name, args) ->
            box [| box "call"; box (ToolCallId.value callId); box name; box args |]
        | ProviderProjection.WireToolResult(callId, result) ->
            box [| box "result"; box (ToolCallId.value callId); box result |]
        | ProviderProjection.WireMedia(mediaType, digest) ->
            box [| box "media"; Option.toObj mediaType |> box; box digest |]

    let private rowValue (row: ProjectionMessageRow) : obj =
        box
            [| box row.Message.Role
               box (row.Message.Parts |> List.map partValue |> List.toArray)
               Option.toObj row.HostMessageId |> box
               box row.HostIsPhysical |]

    /// 行内容的规范指纹：结构相等当且仅当指纹相等（嵌套数组的 JSON，字符串逐字转义）。
    let private fingerprint (rows: ProjectionMessageRow list) : string =
        json (box (rows |> List.map rowValue |> List.toArray))

    /// 规划只比较锚点之间的先后与相等，整体平移不改变答案；库只收非负锚点，
    /// 所以把最小锚点移到 0（宿主允许任意 int 锚点，越界由渲染器拒绝）。
    let private anchorOffset (intents: ProjectionIntent list) : float =
        intents
        |> List.choose (function
            | ProjectionIntent.InsertMessageRows { Anchor = ProjectionMessageAnchor.BeforeMessageIndex index } ->
                Some(float index)
            | _ -> None)
        |> function
            | [] -> 0.0
            | anchors -> min 0.0 (List.min anchors)

    let private layoutIntent (offset: float) (intent: ProjectionIntent) : obj =
        match intent with
        | ProjectionIntent.ReplaceMessageBase replacement ->
            createObj
                [ "kind" ==> "ReplaceMessageBase"
                  "key" ==> replacement.Key
                  "rows" ==> List.length replacement.Rows
                  "fingerprint" ==> fingerprint replacement.Rows ]
        | ProjectionIntent.InsertMessageRows insertion ->
            createObj
                [ "kind" ==> "InsertMessageRows"
                  "key" ==> insertion.Key
                  "anchor"
                  ==> (match insertion.Anchor with
                       | ProjectionMessageAnchor.Append -> null
                       | ProjectionMessageAnchor.BeforeMessageIndex index -> box (float index - offset))
                  "rows" ==> List.length insertion.Rows
                  "fingerprint" ==> fingerprint insertion.Rows ]

    /// Dedupe owner keys, reject conflicts, and return base then canonically sorted insertions.
    /// The result is invariant under registration order for every conflict-free multiset.
    let plan (intents: ProjectionIntent list) : Result<ProjectionIntent list, ProjectionConflict> =
        let source = List.toArray intents
        let offset = anchorOffset intents

        let answer = projection.plan (box (source |> Array.map (layoutIntent offset)))

        if unbox<bool> answer?ok then
            unbox<int array> answer?order
            |> Array.map (fun position -> source[position])
            |> Array.toList
            |> Ok
        else
            match unbox<string> answer?conflict with
            | "ConflictingMessageBase" -> Error ProjectionConflict.ConflictingMessageBase
            | "ConflictingMessageRows" -> Error(ProjectionConflict.ConflictingMessageRows(unbox<string> answer?key))
            | other -> invalidOp ("wanxiangli/projection plan returned an unknown conflict: " + other)

    /// 布局规则的唯一实现：wanxiangli/projection `layout`（CUT-17）。只对无冲突的意图集调用；
    /// 锚点原样交库，负锚点或超出（替换后）基底长度的锚点由库拒绝。返回每一输出行的来源：
    /// `None, row` 是原始基底第 row 行，`Some intent, row` 是 `intents[intent]` 的第 row 行。
    let layout (intents: ProjectionIntent list) (baseLength: int) : (int option * int) list =
        let answer =
            projection.layout (box (intents |> List.toArray |> Array.map (layoutIntent 0.0)), baseLength)

        if unbox<bool> answer?ok then
            unbox<obj array> answer?rows
            |> Array.map (fun row ->
                let intent: obj = row?intent
                (if isNull intent then None else Some(unbox<int> intent)), unbox<int> row?row)
            |> Array.toList
        else
            invalidOp (
                "ProjectionPlanner.layout requires a conflict-free intent set: "
                + unbox<string> answer?conflict
            )
