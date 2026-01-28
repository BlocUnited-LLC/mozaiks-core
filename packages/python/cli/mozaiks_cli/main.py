#!/usr/bin/env python
"""
Mozaiks Core CLI
================

Developer tools for building on mozaiks-core.

Usage:
    python -m cli.main <command> [options]

Commands:
    init        Initialize a new mozaiks project
    db          Database operations (init, check, seed)
    new         Scaffold new plugins or workflows (minimal skeletons)
    doctor      Check system requirements
    version     Show version information

Examples:
    python -m cli.main init my-app
    python -m cli.main db --init-db
    python -m cli.main new plugin todo
    python -m cli.main new workflow assistant
    python -m cli.main doctor

For usage details, see docs/guides/cli.md in the mozaiks-core repo.
"""

import argparse
import sys
from pathlib import Path

__version__ = "1.1.0"

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))


def print_banner():
    """Print Mozaiks banner."""
    print("""
    ╔══════════════════════════════════════════╗
    ║           Mozaiks Core CLI               ║
    ║              Version 1.1.0               ║
    ╚══════════════════════════════════════════╝
    """)


def cmd_version(args):
    """Show version information."""
    print(f"mozaiks-core CLI v{__version__}")
    print(f"Entitlement Contract: v1.0")
    print(f"Plugin Contract: v1.0")
    print(f"Workflow Contract: v1.0")
    print(f"Python: {sys.version}")
    return 0


def cmd_init(args):
    """Initialize a new mozaiks project."""
    from cli.init_project import init_project
    return init_project(
        name=args.name,
        template=args.template,
        no_git=args.no_git,
    )


def cmd_db(args):
    """Database commands - delegates to setup.py."""
    from cli.setup import main as setup_main

    # Build argv for setup.py
    setup_argv = ['setup.py']
    if args.init_db:
        setup_argv.append('--init-db')
    if args.check_db:
        setup_argv.append('--check-db')
    if args.seed_test_data:
        setup_argv.append('--seed-test-data')
    if args.list_plugins:
        setup_argv.append('--list-plugins')

    # Temporarily replace sys.argv
    old_argv = sys.argv
    sys.argv = setup_argv
    try:
        return setup_main()
    finally:
        sys.argv = old_argv


def cmd_new(args):
    """Scaffold new components (minimal skeletons)."""
    if args.type == 'plugin':
        from cli.new_plugin import create_plugin
        return create_plugin(
            name=args.name,
            with_settings=args.with_settings,
            with_entitlements=args.with_entitlements,
            with_frontend=args.with_frontend,
        )
    elif args.type == 'workflow':
        from cli.new_workflow import create_workflow
        return create_workflow(name=args.name)
    else:
        print(f"❌ Unknown type: {args.type}")
        return 1


def cmd_doctor(args):
    """Check system requirements."""
    from cli.doctor import run_doctor
    return run_doctor(fix=args.fix)


def main():
    """Main CLI entry point."""
    parser = argparse.ArgumentParser(
        prog='mozaiks',
        description='Mozaiks Core CLI - Developer tools for building on mozaiks-core',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python -m cli.main init my-app                    Initialize a new project
  python -m cli.main db --init-db                   Initialize database
  python -m cli.main new plugin todo                Create a plugin skeleton
  python -m cli.main new workflow assistant         Create a workflow skeleton
  python -m cli.main doctor                         Check system requirements
  python -m cli.main version                        Show version info

For more help on a specific command:
  python -m cli.main <command> --help
        """
    )

    subparsers = parser.add_subparsers(dest='command', help='Available commands')

    # Version command
    version_parser = subparsers.add_parser('version', help='Show version information')
    version_parser.set_defaults(func=cmd_version)

    # Init command
    init_parser = subparsers.add_parser('init', help='Initialize a new mozaiks project')
    init_parser.add_argument('name', help='Project name')
    init_parser.add_argument('--template', default='minimal',
                            choices=['minimal'],
                            help='Project template (default: minimal)')
    init_parser.add_argument('--no-git', action='store_true',
                            help='Skip git initialization')
    init_parser.set_defaults(func=cmd_init)

    # Database commands
    db_parser = subparsers.add_parser('db', help='Database operations')
    db_parser.add_argument('--init-db', action='store_true', help='Initialize database')
    db_parser.add_argument('--check-db', action='store_true', help='Check database health')
    db_parser.add_argument('--seed-test-data', action='store_true', help='Seed test data')
    db_parser.add_argument('--list-plugins', action='store_true', help='List plugins')
    db_parser.set_defaults(func=cmd_db)

    # New command (scaffolding - minimal skeletons only)
    new_parser = subparsers.add_parser('new', help='Scaffold new plugins or workflows (minimal skeletons)')
    new_parser.add_argument('type', choices=['plugin', 'workflow'], help='Type to create')
    new_parser.add_argument('name', help='Name of the plugin or workflow')
    new_parser.add_argument('--with-settings', action='store_true',
                           help='Include settings support (plugins only)')
    new_parser.add_argument('--with-entitlements', action='store_true',
                           help='Include entitlements.yaml template (plugins only)')
    new_parser.add_argument('--with-frontend', action='store_true',
                           help='Create frontend component stub (plugins only)')
    # Note: Advanced patterns (multi-agent, tool-heavy) available via Platform
    new_parser.set_defaults(func=cmd_new)

    # Doctor command
    doctor_parser = subparsers.add_parser('doctor', help='Check system requirements')
    doctor_parser.add_argument('--fix', action='store_true',
                              help='Attempt to fix issues')
    doctor_parser.set_defaults(func=cmd_doctor)

    # Parse arguments
    args = parser.parse_args()

    # If no command, show help
    if not args.command:
        print_banner()
        parser.print_help()
        return 0

    # Run the command
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
