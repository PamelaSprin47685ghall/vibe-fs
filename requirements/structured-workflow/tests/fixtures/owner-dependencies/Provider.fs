namespace OwnerDependencyFixture

module Provider =
    type Hidden = Hidden of int

    let make value = Hidden value
    let verify value = if value > 0 then Ok(make value) else Error "invalid"
    let combine left right = left, right
    let invoke operation argument = operation argument
    let inner () = 1
    let outer value = value

module Foreign =
    let runA () = "a"
    let runB () = "b"

    type Port =
        abstract Send: string -> unit

module WorktreeCommands =
    let create runner repo job path = runner repo job path
