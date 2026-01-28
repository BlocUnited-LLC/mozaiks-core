#!/usr/bin/env node
import { promises as fs } from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const COLOR_TOKEN_MAP = {
  cyan: {
    default: 'color-primary',
    map: {
      '300': 'color-primary-light',
      '400': 'color-primary-light',
      '500': 'color-primary',
      '600': 'color-primary-dark',
      '700': 'color-primary-dark',
    },
  },
  violet: {
    default: 'color-secondary',
    map: {
      '300': 'color-secondary-light',
      '400': 'color-secondary-light',
      '500': 'color-secondary',
      '600': 'color-secondary-dark',
      '700': 'color-secondary-dark',
    },
  },
  amber: {
    default: 'color-accent',
    map: {
      '300': 'color-accent-light',
      '400': 'color-accent-light',
      '500': 'color-accent',
      '600': 'color-accent-dark',
      '700': 'color-accent-dark',
    },
  },
  emerald: {
    default: 'color-success',
    map: {
      '400': 'color-success',
      '500': 'color-success',
      '600': 'color-success',
    },
  },
  red: {
    default: 'color-error',
    map: {
      '400': 'color-error',
      '500': 'color-error',
      '600': 'color-error',
      '700': 'color-error',
    },
  },
};

function resolveToken(color, shade) {
  const entry = COLOR_TOKEN_MAP[color];
  if (!entry) return null;
  if (shade && entry.map?.[shade]) return entry.map[shade];
  return entry.default;
}

function alphaToDecimal(alpha) {
  if (!alpha && alpha !== '0') return null;
  const numeric = Number(alpha);
  if (Number.isNaN(numeric)) return null;
  return Math.max(0, Math.min(1, numeric / 100));
}

function replaceColorUtilities(source) {
  let result = source;

  const baseRegex = /(bg|text|border|ring|stroke|fill)-(cyan|violet|amber|emerald|red)-(300|400|500|600|700)(?:\/(\d{1,3}))?/g;
  result = result.replace(baseRegex, (match, prop, color, shade, alpha) => {
    const token = resolveToken(color, shade);
    if (!token) return match;
    const alphaDecimal = alphaToDecimal(alpha);

    if (prop === 'bg') {
      if (alphaDecimal !== null) {
        return `bg-[rgba(var(--${token}-rgb),${alphaDecimal})]`;
      }
      return `bg-[var(--${token})]`;
    }
    if (prop === 'text') {
      if (alphaDecimal !== null) {
        return `text-[rgba(var(--${token}-rgb),${alphaDecimal})]`;
      }
      return `text-[var(--${token})]`;
    }
    if (prop === 'border') {
      if (alphaDecimal !== null) {
        return `border-[rgba(var(--${token}-rgb),${alphaDecimal})]`;
      }
      return `border-[var(--${token})]`;
    }
    if (prop === 'ring') {
      if (alphaDecimal !== null) {
        return `ring-[rgba(var(--${token}-rgb),${alphaDecimal})]`;
      }
      return `ring-[var(--${token})]`;
    }
    if (prop === 'stroke') {
      if (alphaDecimal !== null) {
        return `stroke-[rgba(var(--${token}-rgb),${alphaDecimal})]`;
      }
      return `stroke-[var(--${token})]`;
    }
    if (prop === 'fill') {
      if (alphaDecimal !== null) {
        return `fill-[rgba(var(--${token}-rgb),${alphaDecimal})]`;
      }
      return `fill-[var(--${token})]`;
    }
    return match;
  });

  const gradientRegex = /(from|via|to)-(cyan|violet|amber|emerald|red)-(300|400|500|600|700)(?:\/(\d{1,3}))?/g;
  result = result.replace(gradientRegex, (match, stop, color, shade, alpha) => {
    const token = resolveToken(color, shade);
    if (!token) return match;
    const alphaDecimal = alphaToDecimal(alpha);
    if (alphaDecimal !== null) {
      return `${stop}-[rgba(var(--${token}-rgb),${alphaDecimal})]`;
    }
    return `${stop}-[var(--${token})]`;
  });

  const shadowRegex = /shadow-(cyan|violet|amber|emerald|red)-(300|400|500|600|700)(?:\/(\d{1,3}))?/g;
  result = result.replace(shadowRegex, (match, color, shade, alpha) => {
    const token = resolveToken(color, shade);
    if (!token) return match;
    const alphaDecimal = alphaToDecimal(alpha ?? '30') ?? 0.3;
    return `[box-shadow:0_0_0_rgba(var(--${token}-rgb),${alphaDecimal})]`;
  });

  return result;
}

async function collectFiles(target) {
  const stat = await fs.stat(target);
  if (stat.isDirectory()) {
    const entries = await fs.readdir(target);
    const files = await Promise.all(entries.map((entry) => collectFiles(path.join(target, entry))));
    return files.flat();
  }
  if (stat.isFile()) {
    if (/\.(js|jsx|ts|tsx|css|mjs|cjs)$/.test(target)) {
      return [target];
    }
  }
  return [];
}

function unique(list) {
  return Array.from(new Set(list));
}

function parseArgs(argv) {
  const args = { dryRun: false, targets: [] };
  argv.forEach((arg) => {
    if (arg === '--dry-run') {
      args.dryRun = true;
    } else if (arg.startsWith('--')) {
      console.warn(`Unknown flag: ${arg}`);
    } else {
      args.targets.push(arg);
    }
  });
  return args;
}

function formatBytes(bytes) {
  return `${bytes} bytes`;
}

async function processFile(filePath, dryRun) {
  const original = await fs.readFile(filePath, 'utf8');
  const transformed = replaceColorUtilities(original);
  if (transformed === original) {
    return { changed: false, filePath };
  }
  if (!dryRun) {
    await fs.writeFile(filePath, transformed, 'utf8');
  }
  return {
    changed: true,
    filePath,
    diff: original.length - transformed.length,
  };
}

async function main() {
  const { dryRun, targets } = parseArgs(process.argv.slice(2));
  if (!targets.length) {
    console.error('Usage: node tailwind_to_tokens.mjs [--dry-run] <fileOrDirectory> [...more]');
    process.exit(1);
  }

  const files = unique(
    (
      await Promise.all(
        targets.map(async (target) => {
          const absolute = path.isAbsolute(target) ? target : path.join(__dirname, '..', target);
          try {
            return await collectFiles(absolute);
          } catch (error) {
            console.warn(`Skipping ${target}: ${error.message}`);
            return [];
          }
        })
      )
    ).flat()
  );

  if (!files.length) {
    console.log('No matching files to process.');
    return;
  }

  const summary = [];
  for (const file of files) {
    try {
      const result = await processFile(file, dryRun);
      if (result.changed) {
        summary.push(result);
        console.log(`${dryRun ? '[dry-run] ' : ''}Updated ${file}`);
      }
    } catch (error) {
      console.error(`Failed to process ${file}:`, error.message);
    }
  }

  if (!summary.length) {
    console.log('No Tailwind color utilities required changes.');
    return;
  }

  const totalDiff = summary.reduce((acc, item) => acc + (item.diff ?? 0), 0);
  console.log('\nSummary:');
  summary.forEach((item) => {
    console.log(` - ${item.filePath}${item.diff !== undefined ? ` (${formatBytes(item.diff)})` : ''}`);
  });
  console.log(`\n${dryRun ? 'Would update' : 'Updated'} ${summary.length} file(s). Total character delta: ${totalDiff}`);
}

main().catch((error) => {
  console.error('Codemod failed:', error);
  process.exit(1);
});
