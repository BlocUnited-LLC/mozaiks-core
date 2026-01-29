/**
 * Core Components Registration
 *
 * Registers all core Shell components in the component registry.
 * These components are available by default and can be referenced in navigation.json.
 *
 * @module @mozaiks/shell/registry/coreComponents
 */

import { registerComponent } from './componentRegistry';

// Core pages
import ChatPage from '../pages/ChatPage';
import MyWorkflowsPage from '../pages/MyWorkflowsPage';
import ArtifactPage from '../pages/ArtifactPage';

// Register core components
registerComponent('ChatPage', ChatPage, {
  core: true,
  description: 'Main chat interface page'
});

registerComponent('MyWorkflowsPage', MyWorkflowsPage, {
  core: true,
  description: 'User workflows listing page'
});

registerComponent('ArtifactPage', ArtifactPage, {
  core: true,
  description: 'Artifact viewer page'
});

// Export for potential programmatic access
export const CORE_COMPONENTS = [
  'ChatPage',
  'MyWorkflowsPage',
  'ArtifactPage'
];

console.log('[CoreComponents] Registered core Shell components:', CORE_COMPONENTS);
