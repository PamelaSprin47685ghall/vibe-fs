namespace LocalityDependencyFixture

type PublicValue = { Text: string }

type PublicChoice =
    | PublicChoice of string

module Provider =
    let make text = { Text = text }

    let preserve value = value
