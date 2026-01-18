// ==============================================================================
// FILE: ChatUI/src/workflows/AppGenerator/components/AppWorkbench.js
// DESCRIPTION: AppGenerator artifact canvas (files + Monaco + E2B preview + export)
// ==============================================================================

import React, { useMemo, useState } from 'react';
import { Code, LayoutGrid, Monitor } from 'lucide-react';
import { components as designComponents, typography as designTypography } from '../../../styles/artifactDesignSystem';
import themeConfig from '../theme_config.json';
import { useE2BSandbox } from '../hooks/useE2BSandbox';
import BuildStatusArtifact from './BuildStatusArtifact';
import CodeEditorArtifact from './CodeEditorArtifact';
import E2BPreviewArtifact from './E2BPreviewArtifact';
import ExportActions from './ExportActions';
import FileTreeArtifact from './FileTreeArtifact';

const AppWorkbench = ({
  payload = {},
  onResponse,
  ui_tool_id,
  eventId,
  sourceWorkflowName,
  generatedWorkflowName,
}) => {
  const config = themeConfig;
  const layoutCfg = config?.layout || {};
  const defaultView = layoutCfg.defaultView || 'split';
  const [view, setView] = useState(defaultView);

  const {
    filesMap,
    selectedPath,
    setSelectedPath,
    currentContent,
    updateFileContent,
    previewUrl,
    validationResult,
    validationPassed,
    integrationTestResult,
    integrationPassed,
  } = useE2BSandbox(payload, config);

  const headerText = useMemo(() => payload?.title || 'App Workbench', [payload]);

  const subtitle = useMemo(() => {
    const agentMsg = payload?.agent_message || payload?.description || null;
    if (agentMsg && typeof agentMsg === 'string') return agentMsg;
    return validationPassed
      ? 'Validation passed — review code, preview, and export.'
      : 'Validation needs attention — review errors and retry.';
  }, [payload, validationPassed]);

  const panelClass = [designComponents.panel.artifact, 'p-0 overflow-hidden'].join(' ');

  const toolbarBtn = (active) =>
    [
      'px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors inline-flex items-center gap-2',
      active
        ? 'bg-[rgba(var(--color-primary-rgb),0.25)] border border-[rgba(var(--color-primary-rgb),0.35)] text-white'
        : 'bg-white/5 hover:bg-white/10 border border-white/10 text-[var(--color-text-secondary)]',
    ].join(' ');

  const showSplit = view === 'split';
  const showCode = view === 'code-only';
  const showPreview = view === 'preview-only';

  return (
    <div className={panelClass}>
      <div className="px-4 py-3 border-b border-white/10 bg-black/40">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className={['text-white font-bold', designTypography.heading].join(' ')}>{headerText}</div>
            <div className="text-xs text-[var(--color-text-muted)] mt-1">{subtitle}</div>
            <div className="text-[10px] text-[var(--color-text-muted)] mt-1">
              {generatedWorkflowName || sourceWorkflowName || 'AppGenerator'} • event {eventId || 'n/a'}
            </div>
          </div>
          <div className="flex items-center gap-2 flex-shrink-0">
            <button type="button" className={toolbarBtn(showSplit)} onClick={() => setView('split')} title="Split view">
              <LayoutGrid className="w-4 h-4" /> Split
            </button>
            <button type="button" className={toolbarBtn(showCode)} onClick={() => setView('code-only')} title="Code view">
              <Code className="w-4 h-4" /> Code
            </button>
            <button type="button" className={toolbarBtn(showPreview)} onClick={() => setView('preview-only')} title="Preview view">
              <Monitor className="w-4 h-4" /> Preview
            </button>
          </div>
        </div>
      </div>

      <div className="p-4 space-y-4">
        <BuildStatusArtifact
          config={config}
          validationResult={validationResult}
          validationPassed={validationPassed}
          integrationTestResult={integrationTestResult}
          integrationPassed={integrationPassed}
        />

        <div className={['grid gap-4', showSplit ? 'grid-cols-12' : 'grid-cols-12'].join(' ')}>
          {(showSplit || showCode) && (
            <div className={showSplit ? 'col-span-3' : 'col-span-4'}>
              <FileTreeArtifact
                filesMap={filesMap}
                config={config}
                selectedPath={selectedPath}
                onSelectFile={setSelectedPath}
              />
            </div>
          )}

          {(showSplit || showCode) && (
            <div className={showSplit ? 'col-span-5' : 'col-span-8'}>
              <CodeEditorArtifact
                config={config}
                filePath={selectedPath}
                content={currentContent}
                onChange={(val) => updateFileContent(selectedPath, val)}
              />
              <div className="text-[10px] text-[var(--color-text-muted)] mt-2">
                Editor changes are local to your browser session (not yet synced back to the runtime).
              </div>
            </div>
          )}

          {(showSplit || showPreview) && (
            <div className={showSplit ? 'col-span-4' : 'col-span-12'}>
              <E2BPreviewArtifact previewUrl={previewUrl} config={config} />
            </div>
          )}
        </div>

        <div className="pt-2">
          <ExportActions
            payload={payload}
            onResponse={onResponse}
            ui_tool_id={ui_tool_id}
            eventId={eventId}
            sourceWorkflowName={sourceWorkflowName}
            generatedWorkflowName={generatedWorkflowName}
          />
        </div>
      </div>
    </div>
  );
};

export default AppWorkbench;
