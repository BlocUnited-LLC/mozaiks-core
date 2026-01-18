// ==============================================================================
// FILE: ChatUI/src/workflows/AgentGenerator/components/index.js
// DESCRIPTION: Export AgentGenerator workflow-specific UI components
// ==============================================================================

// Import AgentGenerator-specific components
import AgentAPIKeysBundleInput from './AgentAPIKeysBundleInput';
import FileDownloadCenter from './FileDownloadCenter';
import ActionPlan from './ActionPlan';
import MermaidSequenceDiagram from './MermaidSequenceDiagram';

/**
 * 🎯 AGENT GENERATOR WORKFLOW COMPONENTS
 * 
 * This module exports ONLY AgentGenerator workflow-specific UI components.
 * 
 * Components exported here:
 * - AgentAPIKeysBundleInput: Handles consolidated API key requests for multiple services
 * - FileDownloadCenter: Handles file downloads for AgentGenerator workflow
 * - ActionPlan: Visualizes workflow steps and status
 * - MermaidSequenceDiagram: Presents the post-approval sequence diagram artifact
 */

const AgentGeneratorComponents = {
  AgentAPIKeysBundleInput,
  FileDownloadCenter,
  ActionPlan,
  MermaidSequenceDiagram
};

export default AgentGeneratorComponents;

// Named exports for convenience
export {
  AgentAPIKeysBundleInput,
  FileDownloadCenter,
  ActionPlan,
  MermaidSequenceDiagram
};
