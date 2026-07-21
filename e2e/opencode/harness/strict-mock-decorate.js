/**
 * strict-mock-decorate.js — Args decoration (follow-tdd-and-kolmogorov-principles /
 * impossible-via-other-tools / not-suitable-via-continue-tool) used by StrictMockProvider.
 */

const DEFAULT_WARN_TDD = 'i-am-sure-i-have-followed-tdd-and-kolmogorov-principles-and-kept-todo-updated';
const DEFAULT_WARN = 'MUST acknowledge that this task cannot be done with other tools and only run tests when static analysis cannot handle it';
const DEFAULT_WARN_REUSE = 'this-task-is-not-suitable-to-be-completed-via-continue-tool';

const WARN_TDD_KEY = 'follow-tdd-and-kolmogorov-principles';
const WARN_KEY = 'impossible-via-other-tools';
const WARN_REUSE_KEY = 'not-suitable-via-continue-tool';

function setOrDelete(args, key, defaultValue) {
  if (args[key] === null) {
    delete args[key];
    return;
  }
  if (!(key in args)) args[key] = defaultValue;
}

export function decorateLegacyArgs(args) {
  setOrDelete(args, WARN_TDD_KEY, DEFAULT_WARN_TDD);
  setOrDelete(args, WARN_KEY, DEFAULT_WARN);
  setOrDelete(args, WARN_REUSE_KEY, DEFAULT_WARN_REUSE);
}
