namespace Fixture

// DSL-AUTHORITY: Capability
type StoredPermit = private StoredPermit of string

type StoredState = {
    Permit: StoredPermit
}

let issuePermit value = StoredPermit value

let persist (journal: GenericJournal) (state: StoredState) =
    journal.Append state
