# Code Style: JavaScript / Node.js
- 4 spaces, unless `.editorconfig` says otherwise. No semicolons. ESM (`import`/`export`, `"type": "module"` in `package.json`), unless the project already uses CommonJS. Relative imports with file extension (`./logger.js`). ESLint (flat config) with `@stylistic` is the benchmark.
  ```js
  // eslint.config.js
  import stylistic from '@stylistic/eslint-plugin'

  export default [
      {
          files: ['**/*.js'],
          languageOptions: { sourceType: 'module' },
          plugins: { '@stylistic': stylistic },
          rules: {
              '@stylistic/semi': ['error', 'never'],
              '@stylistic/indent': ['error', 4],
          },
      },
  ]
  ```
- Small projects: folders by role (`commands/`, `routes/`, `views/`, `data/`, `utils/`). When a project grows across several business domains, it is cut by domain (see `SKILL.md`). Files are named `<kind>.<domain>[.<action>].js`, with the kind even when the folder already names it. When a file bundles several units, the kind is plural (`routes.status.js`, `tools.zfs.js` with all ZFS tools), with one unit per file it is singular (`command.repository.download.js`). Consistent within a folder, even when a file currently holds only one unit (`routes.exec.shutdown.js`). Exception: the collection file `_index.js` and files in `utils/` and `data/`, which are only named `<domain>[.<action>].js` (`host.exec.js`).
  ```
  src/
    commands/
      README.md
      _index.js
      command.account.login.js
      command.repository.download.js
    routes/
      README.md
      routes.status.js
      routes.exec.shutdown.js
    views/
      view.repositories.list.js
    data/
      database.js
    utils/
      logger.js
  ```
- Imports sit at the top in groups: first Node and framework (`fs`, `express`), then own modules, classes last.
  ```js
  import fs from 'node:fs'
  import path from 'node:path'
  import express from 'express'

  import logger from '../utils/logger.js'
  import config from '../utils/config.js'

  import Database from '../data/database.js'
  ```
- **The shelf label is a `README.md`.** JavaScript has no entry point that lists a folder, so every folder with more than one source file gets a `README.md` with one line per file (see `SKILL.md` → "The story").
  ```markdown
  # Commands
  | File | Content |
  | --- | --- |
  | `_index.js` | Collection file, registers every command in this order. |
  | `command.account.login.js` | Logs in and caches the token. |
  | `command.repository.download.js` | Clones every repository of a project. |
  ```
- **A collection file `_index.js` only where the folder is loaded as a whole**, e.g. a command registry that imports every command. It is code, not the label, and does not replace the `README.md`. The underscore sorts it to the top in the explorer, and with ESM Node does not resolve an `index.js` automatically anyway. Folders whose modules are imported one by one (`routes/`, `utils/`) have none.
  ```js
  import accountLogin from './command.account.login.js'
  import repositoryDownload from './command.repository.download.js'

  export default [
      accountLogin,
      repositoryDownload,
  ]
  ```
- **Same structure for units of the same kind.** Every command follows the same skeleton:
  ```js
  const COMMAND_NAME = 'repository.download'
  async function command(options) {

      logger.debug(`Invoked command '${COMMAND_NAME}' with '${JSON.stringify(options)}'`)
      if (null == options) return

      try {
          ...
      } catch (exception) {
          logger.error(exception.stack)
          console.error(exception.message)
          process.exitCode = 1
      }

  }

  export default { name: COMMAND_NAME, run: command }
  ```
- Singletons: the default is the instance as the module's default export. Only for lazy init (expensive constructor, needs config or login) a static getter with a private field.
  ```js
  // Default: the module itself is the singleton.
  class Logger { ... }

  export default new Logger()
  ```
  ```js
  // Lazy: created on first access.
  class ApiClient {
      static #instance = null

      static get instance() {
          return ApiClient.#instance ??= new ApiClient()
      }
  }
  ```
- Private fields and methods with `#`, derived values as getters, nested helper classes as `static` or class field.
  ```js
  class Repository {
      #name
      #project

      static Status = class { ... }

      get fullName() {
          return `${this.#project}/${this.#name}`
      }

      #buildRemoteUrl() { ... }
  }
  ```
- Parallel calls share one request: `this.#loading ??= this.#load().finally(() => this.#loading = null)`.
  ```js
  #loading = null

  // Five callers at once -> one HTTP request, all await the same promise.
  async getProjects() {
      this.#loading ??= this.#load().finally(() => this.#loading = null)
      return this.#loading
  }
  ```
- One options object instead of many positional parameters: `api.get({ path, query, isCached })`.
  ```js
  async get({ path, query = {}, isCached = true }) { ... }

  await api.get({ path: 'repositories', query: { owner } })
  ```
  Not like this: `api.get('repositories', null, { owner }, true)`
- Consistently `?.` and `??` instead of `&&` chains. `null == x` deliberately checks `null` and `undefined` at once, otherwise `===`.
  ```js
  const branch = repository?.defaultBranch ?? 'main'
  if (null == repository) return
  if (status === 'completed') { ... }
  ```
  Not like this: `const branch = repository && repository.defaultBranch || 'main'`
- When rethrowing, errors get context and the original as `cause`. Log the full stack, show the user only the message.
  ```js
  try {
      await pipeline.run()
  } catch (exception) {
      logger.error(exception.stack)
      throw new Error(`Pipeline '${pipeline.name}' failed to start: ${exception.message}`, { cause: exception })
  }
  ```
- Array chains (`filter`/`map`/`reduce`) with one step per line.
  ```js
  const totalSize = repositories
      .filter(repository => !repository.isDisabled)
      .map(repository => repository.size)
      .reduce((sum, size) => sum + size, 0)
  ```
- Small methods of the same kind sit under a group comment (`// Get Index`, `// Has Node by Index`).
  ```js
  // Get Index
  getParentIndex(index) { return Math.floor((index - 1) / 2) }
  getLeftIndex(index)   { return index * 2 + 1 }
  getRightIndex(index)  { return index * 2 + 2 }

  // Has Node by Index
  hasParent(index) { return index > 0 }
  hasLeft(index)   { return this.getLeftIndex(index) < this.size }
  hasRight(index)  { return this.getRightIndex(index) < this.size }
  ```
- Tests with Mocha under `test/unit/`.
  ```js
  import assert from 'node:assert'
  import MinHeap from '../../src/utils/heap.min.js'

  describe('MinHeap', () => {
      it('returns values in ascending order', () => {
          const heap = new MinHeap()
          ;[5, 1, 3].forEach(value => heap.insert(value))

          assert.deepStrictEqual([heap.pop(), heap.pop(), heap.pop()], [1, 3, 5])
      })
  })
  ```
- **Every promise is awaited or returned.** Calling an `async` function without `await` starts it and moves on to the next line right away. Its result arrives later, out of reading order, and when it fails the error escapes the surrounding `try`/`catch` as an unhandled rejection.
  ```js
  async function saveReport({ report }) {
      await fs.promises.writeFile('report.json', JSON.stringify(report))   // finished before the next line
      logger.info('Report saved')

      return api.post({ path: 'reports', body: report })                   // the caller awaits it
  }
  ```
  Not like this:
  ```js
  async function saveReport({ report }) {
      fs.promises.writeFile('report.json', JSON.stringify(report))   // still writing, the log already says saved
      logger.info('Report saved')

      api.post({ path: 'reports', body: report })                    // the caller gets undefined, a failure goes unseen
  }
  ```
- **Verify** per `SKILL.md` → "Errors, logging, tests": `node --check` per changed file, ESLint where the project has a config, the tests with `npm test`.
  ```
  node --check src/commands/command.repository.download.js
  npx eslint src
  npm test
  ```
