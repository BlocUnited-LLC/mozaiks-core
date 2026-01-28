#!/usr/bin/env node
/**
 * @fileoverview CLI for workflow UI component discovery.
 * 
 * Usage:
 *   workflow-ui-discover [options] [workflows-path]
 * 
 * Options:
 *   --watch, -w         Watch for changes and auto-regenerate
 *   --config, -c        Path to configuration file
 *   --quiet, -q         Suppress non-error output
 *   --help, -h          Show help
 *   --version, -v       Show version
 * 
 * Examples:
 *   workflow-ui-discover ./frontend/workflows
 *   workflow-ui-discover --watch ./frontend/workflows
 *   workflow-ui-discover -c ./config/ui-discovery.json ./frontend/workflows
 */

import fs from 'fs';
import path from 'path';
import { WorkflowComponentDiscovery } from './discovery.js';
import { ComponentWatcher } from './watcher.js';

const VERSION = '1.0.0';

/**
 * Parses command line arguments.
 * 
 * @param {string[]} args - Process arguments (process.argv.slice(2))
 * @returns {Object} Parsed options and positional arguments
 */
function parseArgs(args) {
  const options = {
    watch: false,
    config: null,
    quiet: false,
    help: false,
    version: false
  };
  const positional = [];

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    
    if (arg === '--watch' || arg === '-w') {
      options.watch = true;
    } else if (arg === '--config' || arg === '-c') {
      options.config = args[++i];
    } else if (arg === '--quiet' || arg === '-q') {
      options.quiet = true;
    } else if (arg === '--help' || arg === '-h') {
      options.help = true;
    } else if (arg === '--version' || arg === '-v') {
      options.version = true;
    } else if (!arg.startsWith('-')) {
      positional.push(arg);
    } else {
      console.error(`Unknown option: ${arg}`);
      process.exit(1);
    }
  }

  return { options, positional };
}

/**
 * Shows help message.
 */
function showHelp() {
  console.log(`
workflow-ui-discover - Auto-generate index.js files for workflow UI components

Usage:
  workflow-ui-discover [options] [workflows-path]

Options:
  --watch, -w         Watch for changes and auto-regenerate indexes
  --config, -c        Path to configuration file (JSON)
  --quiet, -q         Suppress non-error output
  --help, -h          Show this help message
  --version, -v       Show version number

Arguments:
  workflows-path      Path to the workflows directory (default: ./frontend/workflows)

Examples:
  # One-time generation
  workflow-ui-discover ./frontend/workflows

  # Watch mode for development
  workflow-ui-discover --watch ./frontend/workflows

  # With custom configuration
  workflow-ui-discover -c ./config/ui-discovery.json ./frontend/workflows

Configuration File (ui-discovery.json):
  {
    "componentExtensions": [".js", ".jsx", ".ts", ".tsx"],
    "excludeFiles": ["index.js", "*.test.js", "*.stories.js"],
    "generateRootRegistry": true,
    "indexTemplate": "esm"
  }

For more information, see:
  https://github.com/BlocUnited/mozaiks-core/docs/workflow-ui-discovery.md
`);
}

/**
 * Loads configuration from a file.
 * 
 * @param {string} configPath - Path to the configuration file
 * @returns {Object} Configuration object
 */
function loadConfig(configPath) {
  try {
    const content = fs.readFileSync(configPath, 'utf8');
    return JSON.parse(content);
  } catch (error) {
    console.error(`Failed to load config from ${configPath}: ${error.message}`);
    process.exit(1);
  }
}

/**
 * Resolves the workflows path, checking common locations.
 * 
 * @param {string|null} providedPath - Path provided by user, or null for auto-detect
 * @returns {string} Resolved absolute path
 */
function resolveWorkflowsPath(providedPath) {
  if (providedPath) {
    const resolved = path.resolve(providedPath);
    if (!fs.existsSync(resolved)) {
      console.error(`Workflows directory not found: ${resolved}`);
      process.exit(1);
    }
    return resolved;
  }

  // Try common locations
  const commonPaths = [
    './frontend/workflows',
    './src/frontend/workflows',
    './workflows',
    './src/workflows'
  ];

  for (const p of commonPaths) {
    const resolved = path.resolve(p);
    if (fs.existsSync(resolved)) {
      return resolved;
    }
  }

  console.error('Could not find workflows directory. Please specify the path explicitly.');
  console.error('Tried: ' + commonPaths.join(', '));
  process.exit(1);
}

/**
 * Main CLI entry point.
 */
async function main() {
  const { options, positional } = parseArgs(process.argv.slice(2));

  if (options.version) {
    console.log(`workflow-ui-discover v${VERSION}`);
    process.exit(0);
  }

  if (options.help) {
    showHelp();
    process.exit(0);
  }

  // Suppress console output if quiet mode
  if (options.quiet) {
    console.log = () => {};
  }

  // Load configuration
  let config = {};
  if (options.config) {
    config = loadConfig(options.config);
  }

  // Resolve workflows path
  const workflowsPath = resolveWorkflowsPath(positional[0]);

  console.log(`Workflows path: ${workflowsPath}`);

  if (options.watch) {
    // Watch mode
    const watcher = new ComponentWatcher(workflowsPath, {
      discoveryOptions: config,
      onRegenerate: ({ workflow, timestamp }) => {
        console.log(`[${timestamp.toISOString()}] Regenerated: ${workflow}`);
      },
      onError: ({ workflow, error }) => {
        console.error(`[error] ${workflow}: ${error.message}`);
      }
    });

    await watcher.start();

    // Handle graceful shutdown
    process.on('SIGINT', () => {
      console.log('\nReceived SIGINT, shutting down...');
      watcher.stop();
      process.exit(0);
    });

    process.on('SIGTERM', () => {
      console.log('\nReceived SIGTERM, shutting down...');
      watcher.stop();
      process.exit(0);
    });

    console.log('\nWatching for changes. Press Ctrl+C to stop.\n');
    
    // Keep the process alive
    await new Promise(() => {});
  } else {
    // One-time generation
    const discovery = new WorkflowComponentDiscovery(workflowsPath, config);
    const result = discovery.run();

    if (result.errors.length > 0) {
      console.error('\nErrors encountered:');
      for (const err of result.errors) {
        console.error(`  - ${err.workflow}: ${err.error}`);
      }
      process.exit(1);
    }

    console.log(`\nSummary:`);
    console.log(`  Workflows scanned: ${result.workflowsScanned}`);
    console.log(`  Components found: ${result.componentsFound}`);
    console.log(`  Index files generated: ${result.indexFilesGenerated}`);
    console.log(`  Index files unchanged: ${result.indexFilesUnchanged}`);
  }
}

// Run if executed directly
main().catch((error) => {
  console.error('Fatal error:', error);
  process.exit(1);
});
