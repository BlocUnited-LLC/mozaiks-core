# backend/app/__init__.py
"""MozaiksCore SDK surface area.

This package intentionally contains no managed-only secrets or proprietary logic.
It provides an "empty shell" connector layer that can operate in:
- self-hosted mode (mock connectors)
- managed mode (HTTP connectors to Mozaiks Platform)
"""

