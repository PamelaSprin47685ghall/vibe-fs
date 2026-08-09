module Sample

/// DSL-005/007: copies the forbidden mutable StudentRunCell shape. The
/// `state-product` scanner must see independent state axes and reject this record
/// (field-name independent), even under a `mutable` modifier the old parser
/// could not read. `RunState`/`ReturnInfo`/`FinalInfo` are deliberately in
/// another namespace so lookup cannot hide the product.
type Cell = {
    mutable State: OtherNamespace.RunState
    mutable Return: ReturnInfo option
    mutable Handoff: bool
    mutable Final: FinalInfo option
}
