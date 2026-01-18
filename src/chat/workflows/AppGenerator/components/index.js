// ==============================================================================
// FILE: ChatUI/src/workflows/AppGenerator/components/index.js
// DESCRIPTION: Export AppGenerator workflow-specific UI components
// ==============================================================================

import AppWorkbench from './AppWorkbench';

// Re-export shared UI tools for compatibility (some backends may still emit these)
import FileDownloadCenter from '../../AgentGenerator/components/FileDownloadCenter';

const AppGeneratorComponents = {
  AppWorkbench,
  FileDownloadCenter,
};

export default AppGeneratorComponents;

export { AppWorkbench, FileDownloadCenter };

