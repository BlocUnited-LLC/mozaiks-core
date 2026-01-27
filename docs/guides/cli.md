# Mozaiks Core CLI (OSS)

Mozaiks-core includes a small developer CLI in `runtime/ai/cli`. It is **optional**: the runtime runs without it, but it helps with scaffolding and basic environment checks.

## Where to run it

The CLI package is designed to be executed from `runtime/ai` so `cli` is importable:

```bash
cd runtime/ai
python -m cli.main --help
```

If you want to run from the repo root, set `PYTHONPATH=runtime/ai` or use your shell to run from `runtime/ai`.

## Commands

```text
python -m cli.main version
python -m cli.main doctor
python -m cli.main db --init-db | --check-db | --seed-test-data | --list-plugins
python -m cli.main new plugin <name> [--with-settings] [--with-entitlements] [--with-frontend]
python -m cli.main new workflow <name>
python -m cli.main init <name> [--template minimal] [--no-git]
```

What each does:

- `version` — Show CLI + contract versions.
- `doctor` — Basic system diagnostics (Python/Node/Docker/Mongo checks).
- `db` — Explicit database init/checks (uses runtime env config).
- `new plugin` — Create a minimal plugin skeleton.
- `new workflow` — Create a minimal workflow skeleton.
- `init` — Create a minimal project layout (mozaiks.toml, docker-compose, runtime folders).

## Where files are created

The CLI uses **canonical locations** inside the runtime tree:

- Plugins: `runtime/ai/plugins`
- Workflows: `runtime/ai/workflows`

The CLI will create these directories if they do not exist.

### Project init

`init` creates a new project with:

```
<project>/
└── runtime/
    └── ai/
        ├── workflows/
        └── plugins/
```

## Environment and DB notes

`doctor` and `db` use the same environment configuration as the runtime. Make sure your `.env` is set and MongoDB is running.

The runtime expects `DATABASE_URI` (fallback: `mongodb://localhost:27017/mozaiks`). See `docs/ai-runtime/configuration-reference.md` for the full list of runtime env vars.

## Related guides

- Plugin development: `docs/guides/creating-plugins.md`
- Workflow development: `docs/guides/creating-workflows.md`
- Troubleshooting: `docs/guides/troubleshooting.md`
