module NonDecoratorCallbacks

type PhysicalItem = { Payload: string }

let listen (callback: PhysicalItem -> unit) items =
    for item in items do
        callback item

let dispatch (callback: string -> unit) outcome =
    match outcome with
    | Ok accepted -> callback accepted
    | Error rejected -> callback rejected
