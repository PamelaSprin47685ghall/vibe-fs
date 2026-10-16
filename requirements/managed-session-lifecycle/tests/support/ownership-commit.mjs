import assert from 'node:assert/strict'

export async function loseOwnershipCommit(Database, command, afterCommit, action) {
  const prepare = Database.prototype.prepare
  const execute = Database.prototype.exec
  let matching = false
  let injected = false
  Database.prototype.prepare = function (sql) {
    const statement = prepare.call(this, sql)
    if (!sql.startsWith('INSERT INTO journal_entries')) return statement
    const run = statement.run
    statement.run = function (...parameters) {
      matching = JSON.parse(new TextDecoder().decode(parameters[3])).message?.type === command
      return run.apply(this, parameters)
    }
    return statement
  }
  Database.prototype.exec = function (sql) {
    if (!injected && matching && /^COMMIT\b/i.test(sql.trim())) {
      injected = true
      if (afterCommit) execute.call(this, sql)
      throw new Error(`${command} commit acknowledgement was lost`)
    }
    return execute.call(this, sql)
  }
  try {
    const result = await action()
    assert.equal(injected, true, `the production ${command} transaction must reach COMMIT`)
    return { result, injected }
  } finally {
    Database.prototype.prepare = prepare
    Database.prototype.exec = execute
  }
}

export async function crashOwnershipCommit(Database, command, afterCommit, action) {
  const prepare = Database.prototype.prepare
  const execute = Database.prototype.exec
  let matching = false
  Database.prototype.prepare = function (sql) {
    const statement = prepare.call(this, sql)
    if (!sql.startsWith('INSERT INTO journal_entries')) return statement
    const run = statement.run
    statement.run = function (...parameters) {
      matching = JSON.parse(new TextDecoder().decode(parameters[3])).message?.type === command
      return run.apply(this, parameters)
    }
    return statement
  }
  Database.prototype.exec = function (sql) {
    if (matching && /^COMMIT\b/i.test(sql.trim())) {
      if (afterCommit) execute.call(this, sql)
      process.kill(process.pid, 'SIGKILL')
      throw new Error('The crash fixture must not return after SIGKILL')
    }
    return execute.call(this, sql)
  }
  try { return await action() }
  finally {
    Database.prototype.prepare = prepare
    Database.prototype.exec = execute
  }
}
