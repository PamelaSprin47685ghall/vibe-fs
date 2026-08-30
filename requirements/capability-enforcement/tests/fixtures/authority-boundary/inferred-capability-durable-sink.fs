namespace Fixture

// DSL-AUTHORITY: Capability
type StoredPermit = private StoredPermit of string

type HiddenState = { Permit: StoredPermit }

type DurableSink() =
    member _.Commit value = ignore value

let issuePermit value = StoredPermit value

let forward durableSink state =
    durableSink.Commit state

let persist durableSink permit =
    let state = { Permit = permit }
    forward durableSink state
