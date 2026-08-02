namespace Wanxiangshu.Next.Journal

type EffectId = private EffectId of string

module EffectId =
    let create value = EffectId value
    let value (EffectId value) = value

type EffectStatus =
    | Requested of target: string * payload: string
    | Accepted of target: string * payload: string * result: string

type DurableEffectProjection =
    { Current: (EffectId * EffectStatus) option }

/// Single in-flight durable side effect. Accepted facts preserve request data.
module EffectProjection =

    let empty = { Current = None }

    let request effectId target payload _current =
        { Current = Some(EffectId.create effectId, Requested(target, payload)) }

    let accept effectId result current =
        let id = EffectId.create effectId

        match current with
        | Some existing ->
            match existing.Current with
            | Some(existingId, Requested(target, payload)) when existingId = id ->
                { Current = Some(id, Accepted(target, payload, result)) }
            | _ -> existing
        | None -> empty
