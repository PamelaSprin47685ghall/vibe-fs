# Provider semantic resources (PROMPT-017 / HOST-026)

Each localizable asset has a **stable semantic path** under `resources/provider/`.
Language is the **leaf filename**: `en.md` and `zh-CN.md`.

```text
resources/provider/
  role/manager/en.md
  role/manager/zh-CN.md
  role/coder/en.md
  ...
```

Invariant identifiers (tool names, argument names, wire fields, enum literals, paths,
commands, `exit_code`) stay untranslated inside localized prose.

Gate C (`language-parity-gate.mjs`) requires both locale files for every semantic directory.
