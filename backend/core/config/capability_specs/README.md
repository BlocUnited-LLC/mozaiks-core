# Declarative AI Capability Specs

This folder is for app-owned, declarative AI capability specs generated upstream (e.g., by AppGenerator).

- MozaiksCore loads `*.json` specs from this folder by default.
- Set `MOZAIKS_AI_CAPABILITY_SPECS_DIR` to point to a different directory.
- Files ending in `.example.json` or `.template.json` are ignored.

See `docs/AIRuntimeIntegration.md` for the spec shape and launch flow.
