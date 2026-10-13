namespace Wanxiangshu.Mission.Planning

open System

type ActiveTenureInfo =
    { WorkId: string
      IncumbencyId: string
      Stage: string
      OpeningCursor: int64
      PreviousRange: (int64 * int64) option
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

    /// A message already observed strictly before this opening is the
    /// previous tenure. A message with no first-observation cursor has not
    /// been captured yet; that absence is not a cursor on the Host message.
    let isCurrentTenurePhysical (openingCursor: int64) (cursor: int64 option) =
        match cursor with
        | None -> true
        | Some sequence -> sequence >= openingCursor

    let insertBeforeCurrentTenure
        (openingCursor: int64)
        (messageCursors: Map<string, int64>)
        (record: obj)
        (messages: obj list)
        (messageId: obj -> string option)
        : obj list =
        let rec place reversed pending rest =
            match pending, rest with
            | None, _ -> List.rev reversed @ rest
            | Some pendingRecord, [] -> List.rev (pendingRecord :: reversed)
            | Some pendingRecord, message :: tail ->
                let cursor =
                    messageId message
                    |> Option.bind (fun id -> Map.tryFind id messageCursors)

                if isCurrentTenurePhysical openingCursor cursor then
                    List.rev reversed @ (pendingRecord :: message :: tail)
                else
                    place (message :: reversed) pending tail

        place [] (Some record) messages

    let private recordMessage (tenure: ActiveTenureInfo) (text: string) : obj =
        box
            {| info =
                {| id = "lwr-prev-" + tenure.IncumbencyId
                   sessionID = tenure.WorkId
                   role = "user"
                   synthetic = true
                   source = "plan-tenure-record" |}
               parts = [| box {| ``type`` = "text"; text = text |} |] |}

    let private physicalCursor (messageCursors: Map<string, int64>) (message: TenureMessage) =
        message.Id
        |> Option.bind (fun messageId -> Map.tryFind messageId messageCursors)

    /// Strip a captured assistant/tool exchange only when its first
    /// observation is strictly earlier than this opening. A Host id that is
    /// not in the first-observation map has not been captured yet, so the
    /// message stays. A semantic exchange with no Host id is still a contract
    /// violation: do not invent a cursor, and do not keep or drop the whole set.
    let private keepsInteraction
        (tenure: ActiveTenureInfo)
        (identity: string option)
        (cursor: int64 option)
        (hasSemanticParts: bool)
        =
        match identity, cursor, hasSemanticParts with
        | _, Some sequence, _ -> sequence >= tenure.OpeningCursor
        | Some _, None, _ -> true
        | None, None, false -> false
        | None, None, true -> invalidOp "Plan interaction has no stable XTrace message identity"

    let private keepMessage (tenure: ActiveTenureInfo) cursor (message: TenureMessage) =
        match message.IsSynthetic, message.Role with
        | true, _ -> false
        | false, "user" -> true
        | false, "assistant"
        | false, "tool" -> keepsInteraction tenure message.Id cursor message.HasSemanticParts
        | false, _ -> false

    let private validateTenure (tenure: ActiveTenureInfo) =
        if tenure.OpeningCursor < 0L then
            invalidArg (nameof tenure) "Plan opening cursor cannot be negative"

        match tenure.PreviousRange with
        | Some(startCursor, endCursor) when startCursor < 0L || endCursor < startCursor ->
            invalidArg (nameof tenure) "Plan previous range is invalid"
        | _ -> ()

    let private recordText (previousRecord: string option) =
        match previousRecord with
        | Some text when not (String.IsNullOrWhiteSpace text) -> text
        | _ -> invalidOp "Plan previous work record is unavailable"

    let private previousRecordMessage (tenure: ActiveTenureInfo) (previousRecord: string option) =
        match tenure.PreviousRange with
        | Some(startCursor, endCursor) when endCursor > startCursor ->
            Some(recordMessage tenure (recordText previousRecord))
        | _ -> None

    let private placeBeforeCurrent
        (tenure: ActiveTenureInfo)
        (reversed: obj list)
        (pendingRecord: obj option)
        (cursor: int64 option)
        (raw: obj)
        : obj list * obj option =
        match pendingRecord, cursor with
        | Some record, Some sequence when isCurrentTenurePhysical tenure.OpeningCursor (Some sequence) -> raw :: record :: reversed, None
        | _ -> raw :: reversed, pendingRecord

    let private step
        (tenure: ActiveTenureInfo)
        (messageCursors: Map<string, int64>)
        (state: obj list * obj option)
        (message: TenureMessage)
        : obj list * obj option =
        let cursor = physicalCursor messageCursors message

        if keepMessage tenure cursor message then
            let reversed, pendingRecord = state
            placeBeforeCurrent tenure reversed pendingRecord cursor message.Raw
        else
            state

    /// A record that never met a current physical message (the tenure made no
    /// interaction yet) lands after the retained historical U.
    let private finish (reversed: obj list) (pendingRecord: obj option) : obj list =
        match pendingRecord with
        | Some record -> List.rev (record :: reversed)
        | None -> List.rev reversed

    let assemble
        (messages: TenureMessage list)
        (tenure: ActiveTenureInfo)
        (messageCursors: Map<string, int64>)
        (previousRecord: string option)
        : TenureAssemblyResult =
        validateTenure tenure

        let reversed, pendingRecord =
            List.fold (step tenure messageCursors) ([], previousRecordMessage tenure previousRecord) messages

        { Messages = finish reversed pendingRecord
          ReanchorRequested = tenure.IsFreshHandover }
