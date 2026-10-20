namespace Wanxiangshu.Mission.Planning

type ActiveTenureInfo =
    { WorkId: string
      IncumbencyId: string
      Stage: string
      OpeningCursor: int64
      IsFreshHandover: bool }

type TenureMessage =
    { Id: string option
      Role: string
      IsSynthetic: bool
      HasSemanticParts: bool
      Raw: obj }

type TenureAssemblyResult =
    { Messages: obj list
      ReanchorRequested: bool }

module TenureIsolation =

    open Fable.Core
    open Fable.Core.JsInterop

    /// 任期过滤规则的唯一实现：wanxiangli/projection（CUT-17）。
    [<AllowNullLiteral>]
    type private ProjectionMessages =
        abstract tenure: messages: obj * openingCursor: obj -> obj

    [<Import("Projection", "wanxiangli/projection")>]
    let private createProjection: unit -> ProjectionMessages = jsNative

    let private projection = createProjection ()

    let private physicalCursor (messageCursors: Map<string, int64>) (message: TenureMessage) =
        message.Id
        |> Option.bind (fun messageId -> Map.tryFind messageId messageCursors)

    let private wireMessage (messageCursors: Map<string, int64>) (message: TenureMessage) : obj =
        createObj
            [ "id" ==> Option.toObj message.Id
              "role" ==> message.Role
              "synthetic" ==> message.IsSynthetic
              "semantic" ==> message.HasSemanticParts
              "cursor"
              ==> (match physicalCursor messageCursors message with
                   | Some cursor -> box cursor
                   | None -> null) ]

    /// Assembles the physical message set for an active or post-S3 Plan tenure:
    /// verbatim user messages `U` (role user, non-synthetic, in original order)
    /// plus current-tenure physical messages whose first XTrace observation
    /// cursor is `>= tenure.OpeningCursor` (or uncaptured with a stable Host
    /// identity). All prior-tenure assistant/tool messages and synthetic rows
    /// are stripped; handover is carried by the predecessor's ANCHOR view
    /// declaration rather than a machine-materialized record.
    let assemble
        (messages: TenureMessage list)
        (tenure: ActiveTenureInfo)
        (messageCursors: Map<string, int64>)
        : TenureAssemblyResult =
        let rows = List.toArray messages

        let kept =
            projection.tenure (rows |> Array.map (wireMessage messageCursors) |> box, box tenure.OpeningCursor)

        { Messages = unbox<int array> kept |> Array.map (fun index -> rows.[index].Raw) |> Array.toList
          ReanchorRequested = tenure.IsFreshHandover }
