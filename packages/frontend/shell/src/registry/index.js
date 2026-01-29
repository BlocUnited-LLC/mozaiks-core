/**
 * Component Registry - Public API
 *
 * @module @mozaiks/shell/registry
 */

export {
  registerComponent,
  getComponent,
  hasComponent,
  getRegisteredComponents,
  getComponentMeta,
  unregisterComponent,
  clearRegistry
} from './componentRegistry';

// Export core component list
export { CORE_COMPONENTS } from './coreComponents';
