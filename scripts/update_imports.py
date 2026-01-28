#!/usr/bin/env python3
"""
Script to update Python imports from old core.* pattern to new mozaiks_* pattern.
"""
import os
import re
from pathlib import Path

# Define the import mappings (order matters - more specific patterns first)
IMPORT_MAPPINGS = [
    # AI Runtime mappings
    (r'from core\.ai_runtime\.', 'from mozaiks_ai.runtime.'),
    (r'import core\.ai_runtime\.', 'import mozaiks_ai.runtime.'),
    (r'from core\.runtime\.manager', 'from mozaiks_ai.core_runtime.manager'),
    (r'from core\.routes\.ai\b', 'from mozaiks_ai.routes.ai'),
    (r'from core\.routes\.status\b', 'from mozaiks_ai.routes.status'),

    # Infrastructure mappings
    (r'from core\.config\.', 'from mozaiks_infra.config.'),
    (r'import core\.config\.', 'import mozaiks_infra.config.'),
    (r'from core\.utils\.', 'from mozaiks_infra.utils.'),
    (r'import core\.utils\.', 'import mozaiks_infra.utils.'),
    (r'from core\.metrics\.', 'from mozaiks_infra.metrics.'),
    (r'import core\.metrics\.', 'import mozaiks_infra.metrics.'),
    (r'from core\.http_utils\.', 'from mozaiks_infra.http_utils.'),
    (r'import core\.http_utils\.', 'import mozaiks_infra.http_utils.'),
    (r'from core\.ops\.', 'from mozaiks_infra.ops.'),
    (r'import core\.ops\.', 'import mozaiks_infra.ops.'),
    (r'from core\.event_bus\b', 'from mozaiks_infra.event_bus'),
    (r'import core\.event_bus\b', 'import mozaiks_infra.event_bus'),
    (r'from core\.websocket_manager\b', 'from mozaiks_infra.websocket_manager'),
    (r'import core\.websocket_manager\b', 'import mozaiks_infra.websocket_manager'),
    (r'from core\.state_manager\b', 'from mozaiks_infra.state_manager'),
    (r'import core\.state_manager\b', 'import mozaiks_infra.state_manager'),
    (r'from core\.routes\.events\b', 'from mozaiks_infra.routes.events'),

    # Platform mappings
    (r'from core\.billing\.', 'from mozaiks_platform.billing.'),
    (r'import core\.billing\.', 'import mozaiks_platform.billing.'),
    (r'from core\.billing\b', 'from mozaiks_platform.billing'),
    (r'import core\.billing\b', 'import mozaiks_platform.billing'),
    (r'from core\.entitlements\.', 'from mozaiks_platform.entitlements.'),
    (r'import core\.entitlements\.', 'import mozaiks_platform.entitlements.'),
    (r'from core\.entitlements\b', 'from mozaiks_platform.entitlements'),
    (r'import core\.entitlements\b', 'import mozaiks_platform.entitlements'),
    (r'from core\.analytics\.', 'from mozaiks_platform.analytics.'),
    (r'import core\.analytics\.', 'import mozaiks_platform.analytics.'),
    (r'from core\.analytics\b', 'from mozaiks_platform.analytics'),
    (r'import core\.analytics\b', 'import mozaiks_platform.analytics'),
    (r'from core\.insights\.', 'from mozaiks_platform.insights.'),
    (r'import core\.insights\.', 'import mozaiks_platform.insights.'),
    (r'from core\.insights\b', 'from mozaiks_platform.insights'),
    (r'import core\.insights\b', 'import mozaiks_platform.insights'),
    (r'from core\.notifications\.', 'from mozaiks_platform.notifications.'),
    (r'import core\.notifications\.', 'import mozaiks_platform.notifications.'),
    (r'from core\.notifications\b', 'from mozaiks_platform.notifications'),
    (r'import core\.notifications\b', 'import mozaiks_platform.notifications'),
    (r'from core\.subscription_manager\b', 'from mozaiks_platform.subscription_manager'),
    (r'import core\.subscription_manager\b', 'import mozaiks_platform.subscription_manager'),
    (r'from core\.subscription_stub\b', 'from mozaiks_platform.subscription_stub'),
    (r'import core\.subscription_stub\b', 'import mozaiks_platform.subscription_stub'),
    (r'from core\.plugin_manager\b', 'from mozaiks_platform.plugin_manager'),
    (r'import core\.plugin_manager\b', 'import mozaiks_platform.plugin_manager'),
    (r'from core\.settings_manager\b', 'from mozaiks_platform.settings_manager'),
    (r'import core\.settings_manager\b', 'import mozaiks_platform.settings_manager'),
    (r'from core\.notifications_manager\b', 'from mozaiks_platform.notifications_manager'),
    (r'import core\.notifications_manager\b', 'import mozaiks_platform.notifications_manager'),
    (r'from core\.hosting_operator\b', 'from mozaiks_platform.hosting_operator'),
    (r'import core\.hosting_operator\b', 'import mozaiks_platform.hosting_operator'),
    (r'from core\.director\b', 'from mozaiks_platform.director'),
    (r'import core\.director\b', 'import mozaiks_platform.director'),

    # Platform routes
    (r'from core\.routes\.billing\b', 'from mozaiks_platform.routes.billing'),
    (r'from core\.routes\.subscription_sync\b', 'from mozaiks_platform.routes.subscription_sync'),
    (r'from core\.routes\.notifications\b', 'from mozaiks_platform.routes.notifications'),
    (r'from core\.routes\.notifications_admin\b', 'from mozaiks_platform.routes.notifications_admin'),
    (r'from core\.routes\.analytics\b', 'from mozaiks_platform.routes.analytics'),
    (r'from core\.routes\.admin_users\b', 'from mozaiks_platform.routes.admin_users'),
    (r'from core\.routes\.app_metadata\b', 'from mozaiks_platform.routes.app_metadata'),
    (r'from core\.routes\.push_subscriptions\b', 'from mozaiks_platform.routes.push_subscriptions'),
]

def update_file(filepath: Path) -> tuple[bool, int]:
    """Update imports in a single file. Returns (was_modified, num_changes)."""
    try:
        content = filepath.read_text(encoding='utf-8')
    except Exception as e:
        print(f"  Error reading {filepath}: {e}")
        return False, 0

    original = content
    total_changes = 0

    for pattern, replacement in IMPORT_MAPPINGS:
        content, count = re.subn(pattern, replacement, content)
        total_changes += count

    if content != original:
        try:
            filepath.write_text(content, encoding='utf-8')
            return True, total_changes
        except Exception as e:
            print(f"  Error writing {filepath}: {e}")
            return False, 0

    return False, 0

def main():
    packages_dir = Path(__file__).parent.parent / 'packages' / 'python'

    if not packages_dir.exists():
        print(f"Error: {packages_dir} does not exist")
        return

    print(f"Scanning {packages_dir} for Python files...")

    modified_files = []
    total_changes = 0

    for py_file in packages_dir.rglob('*.py'):
        was_modified, num_changes = update_file(py_file)
        if was_modified:
            modified_files.append((py_file, num_changes))
            total_changes += num_changes
            print(f"  Updated: {py_file.relative_to(packages_dir)} ({num_changes} changes)")

    # Also update .md files in packages/python (documentation)
    for md_file in packages_dir.rglob('*.md'):
        was_modified, num_changes = update_file(md_file)
        if was_modified:
            modified_files.append((md_file, num_changes))
            total_changes += num_changes
            print(f"  Updated: {md_file.relative_to(packages_dir)} ({num_changes} changes)")

    print(f"\nSummary:")
    print(f"  Files modified: {len(modified_files)}")
    print(f"  Total import changes: {total_changes}")

if __name__ == '__main__':
    main()
