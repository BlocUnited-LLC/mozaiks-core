// ==============================================================================
// FILE: ChatUI/src/core/ui/index.js  
// DESCRIPTION: Export core/generic UI components
// ==============================================================================

// Import core/generic components
import UserInputRequest from './UserInputRequest';
import UIToolRenderer from './UIToolRenderer';

/**
 * 🎯 CORE UI COMPONENTS
 * 
 * This module exports generic, reusable UI components that work across
 * all workflows. Workflow-specific components belong in their respective
 * workflow directories.
 * 
 * Components exported here:
 * - UserInputRequest: Generic user input component for any workflow
 * - UIToolRenderer: Core system for rendering any workflow's UI tools
 */

const CoreComponents = {
  UserInputRequest,
  UIToolRenderer
};

export default CoreComponents;

// Named exports for convenience
export {
  UserInputRequest,
  UIToolRenderer
};
