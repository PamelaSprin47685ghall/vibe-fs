module Sample

type Connection =
    | Connected
    | Disconnected

type Buffer =
    | Empty
    | Full

/// DSL-state-combination: physical
type ResourceFacts = {
    Connection: Connection
    Buffer: Buffer
}
