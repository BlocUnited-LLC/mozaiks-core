/**
 * Tests for @mozaiks/workflow-ui-discovery
 * 
 * Run with: node --test tests/discovery.test.js
 */

import { describe, it, beforeEach, afterEach } from 'node:test';
import assert from 'node:assert';
import fs from 'fs';
import path from 'path';
import os from 'os';
import { WorkflowComponentDiscovery } from '../src/discovery.js';

// Create a temporary directory for each test
function createTempDir() {
  return fs.mkdtempSync(path.join(os.tmpdir(), 'workflow-discovery-test-'));
}

// Clean up temporary directory
function cleanupTempDir(dir) {
  fs.rmSync(dir, { recursive: true, force: true });
}

// Create a mock workflow structure
function createMockWorkflow(baseDir, workflowName, componentNames) {
  const workflowDir = path.join(baseDir, workflowName);
  const componentsDir = path.join(workflowDir, 'components');
  
  fs.mkdirSync(componentsDir, { recursive: true });
  
  for (const name of componentNames) {
    const componentPath = path.join(componentsDir, `${name}.jsx`);
    fs.writeFileSync(componentPath, `export default function ${name}() { return null; }\n`);
  }
  
  return componentsDir;
}

describe('WorkflowComponentDiscovery', () => {
  let tempDir;
  
  beforeEach(() => {
    tempDir = createTempDir();
  });
  
  afterEach(() => {
    cleanupTempDir(tempDir);
  });
  
  it('should discover component directories', () => {
    createMockWorkflow(tempDir, 'TestWorkflow', ['ComponentA', 'ComponentB']);
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    const dirs = discovery.discoverComponentDirectories();
    
    assert.strictEqual(dirs.size, 1);
    assert.ok(dirs.has('TestWorkflow'));
  });
  
  it('should scan component files', () => {
    const componentsDir = createMockWorkflow(tempDir, 'TestWorkflow', ['ComponentA', 'ComponentB', 'ComponentC']);
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    const files = discovery.scanComponentFiles(componentsDir);
    
    assert.strictEqual(files.length, 3);
    assert.ok(files.includes('ComponentA.jsx'));
    assert.ok(files.includes('ComponentB.jsx'));
    assert.ok(files.includes('ComponentC.jsx'));
  });
  
  it('should exclude index.js files', () => {
    const componentsDir = createMockWorkflow(tempDir, 'TestWorkflow', ['ComponentA']);
    
    // Add an index.js file
    fs.writeFileSync(path.join(componentsDir, 'index.js'), 'export {};\n');
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    const files = discovery.scanComponentFiles(componentsDir);
    
    assert.strictEqual(files.length, 1);
    assert.ok(!files.includes('index.js'));
  });
  
  it('should exclude test files', () => {
    const componentsDir = createMockWorkflow(tempDir, 'TestWorkflow', ['ComponentA']);
    
    // Add test files
    fs.writeFileSync(path.join(componentsDir, 'ComponentA.test.js'), 'test code');
    fs.writeFileSync(path.join(componentsDir, 'ComponentA.spec.js'), 'spec code');
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    const files = discovery.scanComponentFiles(componentsDir);
    
    assert.strictEqual(files.length, 1);
    assert.ok(!files.some(f => f.includes('.test.')));
    assert.ok(!files.some(f => f.includes('.spec.')));
  });
  
  it('should generate valid ESM index content', () => {
    const discovery = new WorkflowComponentDiscovery(tempDir);
    const content = discovery.generateIndexContent(['ComponentA.jsx', 'ComponentB.jsx']);
    
    assert.ok(content.includes('AUTO-GENERATED'));
    assert.ok(content.includes("export { default as ComponentA } from './ComponentA'"));
    assert.ok(content.includes("export { default as ComponentB } from './ComponentB'"));
  });
  
  it('should run full discovery and generate indexes', () => {
    createMockWorkflow(tempDir, 'WorkflowA', ['CompA1', 'CompA2']);
    createMockWorkflow(tempDir, 'WorkflowB', ['CompB1']);
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    const result = discovery.run();
    
    assert.strictEqual(result.workflowsScanned, 2);
    assert.strictEqual(result.componentsFound, 3);
    assert.strictEqual(result.errors.length, 0);
    
    // Check that index files were created
    const indexA = path.join(tempDir, 'WorkflowA', 'components', 'index.js');
    const indexB = path.join(tempDir, 'WorkflowB', 'components', 'index.js');
    const rootIndex = path.join(tempDir, 'index.js');
    
    assert.ok(fs.existsSync(indexA), 'WorkflowA index should exist');
    assert.ok(fs.existsSync(indexB), 'WorkflowB index should exist');
    assert.ok(fs.existsSync(rootIndex), 'Root index should exist');
  });
  
  it('should not rewrite unchanged files', () => {
    createMockWorkflow(tempDir, 'TestWorkflow', ['ComponentA']);
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    
    // First run - should generate
    const result1 = discovery.run();
    assert.strictEqual(result1.indexFilesGenerated, 2); // workflow + root
    
    // Second run - should skip (unchanged)
    const result2 = discovery.run();
    assert.strictEqual(result2.indexFilesGenerated, 0);
    assert.strictEqual(result2.indexFilesUnchanged, 2);
  });
  
  it('should regenerate single workflow', () => {
    createMockWorkflow(tempDir, 'TestWorkflow', ['ComponentA']);
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    discovery.run();
    
    // Add a new component
    const componentsDir = path.join(tempDir, 'TestWorkflow', 'components');
    fs.writeFileSync(path.join(componentsDir, 'ComponentB.jsx'), 'export default function ComponentB() {}');
    
    // Regenerate just this workflow
    const updated = discovery.regenerateWorkflow('TestWorkflow');
    
    assert.ok(updated, 'Should have updated the index');
    
    // Verify new component is in index
    const indexContent = fs.readFileSync(path.join(componentsDir, 'index.js'), 'utf8');
    assert.ok(indexContent.includes('ComponentB'));
  });
  
  it('should handle _shared components directory', () => {
    createMockWorkflow(tempDir, '_shared', ['StatusBadge', 'ApprovalButtons']);
    createMockWorkflow(tempDir, 'TestWorkflow', ['ComponentA']);
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    const result = discovery.run();
    
    assert.strictEqual(result.workflowsScanned, 2);
    assert.ok(result.workflows['_shared']);
    assert.ok(result.workflows['_shared'].includes('StatusBadge'));
    
    // Check root registry includes shared export
    const rootIndex = fs.readFileSync(path.join(tempDir, 'index.js'), 'utf8');
    assert.ok(rootIndex.includes("export * as shared from './_shared/components/index.js'"));
  });
  
  it('should handle empty components directory', () => {
    const workflowDir = path.join(tempDir, 'EmptyWorkflow', 'components');
    fs.mkdirSync(workflowDir, { recursive: true });
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    const result = discovery.run();
    
    assert.strictEqual(result.workflowsScanned, 1);
    assert.strictEqual(result.componentsFound, 0);
    
    // Index should still be created with empty export
    const indexContent = fs.readFileSync(path.join(workflowDir, 'index.js'), 'utf8');
    assert.ok(indexContent.includes('No components found'));
    assert.ok(indexContent.includes('export {}'));
  });
});

describe('Component name extraction', () => {
  let tempDir;
  
  beforeEach(() => {
    tempDir = createTempDir();
  });
  
  afterEach(() => {
    cleanupTempDir(tempDir);
  });
  
  it('should handle various file extensions', () => {
    const componentsDir = path.join(tempDir, 'TestWorkflow', 'components');
    fs.mkdirSync(componentsDir, { recursive: true });
    
    fs.writeFileSync(path.join(componentsDir, 'CompJS.js'), '');
    fs.writeFileSync(path.join(componentsDir, 'CompJSX.jsx'), '');
    fs.writeFileSync(path.join(componentsDir, 'CompTS.ts'), '');
    fs.writeFileSync(path.join(componentsDir, 'CompTSX.tsx'), '');
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    const result = discovery.run();
    
    assert.strictEqual(result.componentsFound, 4);
    
    const indexContent = fs.readFileSync(path.join(componentsDir, 'index.js'), 'utf8');
    assert.ok(indexContent.includes('CompJS'));
    assert.ok(indexContent.includes('CompJSX'));
    assert.ok(indexContent.includes('CompTS'));
    assert.ok(indexContent.includes('CompTSX'));
  });
});

describe('Workflow-level index generation', () => {
  let tempDir;
  
  beforeEach(() => {
    tempDir = createTempDir();
  });
  
  afterEach(() => {
    cleanupTempDir(tempDir);
  });
  
  it('should generate workflow index when theme_config.json exists', () => {
    // Create workflow with theme_config.json
    const workflowDir = path.join(tempDir, 'AppGenerator');
    const componentsDir = path.join(workflowDir, 'components');
    fs.mkdirSync(componentsDir, { recursive: true });
    
    // Add theme_config.json
    const themeConfig = { name: 'AppGenerator', primaryColor: '#007bff' };
    fs.writeFileSync(path.join(workflowDir, 'theme_config.json'), JSON.stringify(themeConfig, null, 2));
    
    // Add a component
    fs.writeFileSync(path.join(componentsDir, 'Preview.jsx'), 'export default function Preview() {}');
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    const result = discovery.run();
    
    // Check workflow-level index was generated
    const workflowIndex = path.join(workflowDir, 'index.js');
    assert.ok(fs.existsSync(workflowIndex), 'Workflow index should exist');
    
    const content = fs.readFileSync(workflowIndex, 'utf8');
    assert.ok(content.includes("import themeConfig from './theme_config.json'"), 'Should import themeConfig');
    assert.ok(content.includes("id: 'AppGenerator'"), 'Should have workflow id');
    assert.ok(content.includes("config: themeConfig"), 'Should have config reference');
    assert.ok(content.includes('export default'), 'Should have default export');
  });
  
  it('should NOT generate workflow index when theme_config.json is missing', () => {
    // Create workflow WITHOUT theme_config.json
    const workflowDir = path.join(tempDir, 'SimpleWorkflow');
    const componentsDir = path.join(workflowDir, 'components');
    fs.mkdirSync(componentsDir, { recursive: true });
    
    fs.writeFileSync(path.join(componentsDir, 'Component.jsx'), 'export default function Component() {}');
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    discovery.run();
    
    // Workflow-level index should NOT exist
    const workflowIndex = path.join(workflowDir, 'index.js');
    assert.ok(!fs.existsSync(workflowIndex), 'Workflow index should NOT exist');
    
    // Component index should still exist
    const componentIndex = path.join(componentsDir, 'index.js');
    assert.ok(fs.existsSync(componentIndex), 'Component index should exist');
  });
  
  it('should support custom workflow config file name', () => {
    const workflowDir = path.join(tempDir, 'CustomConfig');
    const componentsDir = path.join(workflowDir, 'components');
    fs.mkdirSync(componentsDir, { recursive: true });
    
    // Use custom config file name
    fs.writeFileSync(path.join(workflowDir, 'workflow.config.json'), JSON.stringify({ name: 'Custom' }));
    fs.writeFileSync(path.join(componentsDir, 'Component.jsx'), 'export default function Component() {}');
    
    const discovery = new WorkflowComponentDiscovery(tempDir, {
      workflowConfigFile: 'workflow.config.json'
    });
    discovery.run();
    
    const workflowIndex = path.join(workflowDir, 'index.js');
    assert.ok(fs.existsSync(workflowIndex), 'Workflow index should exist with custom config');
    
    const content = fs.readFileSync(workflowIndex, 'utf8');
    assert.ok(content.includes("import themeConfig from './workflow.config.json'"));
  });
  
  it('should skip workflow index generation when disabled', () => {
    const workflowDir = path.join(tempDir, 'DisabledWorkflow');
    const componentsDir = path.join(workflowDir, 'components');
    fs.mkdirSync(componentsDir, { recursive: true });
    
    fs.writeFileSync(path.join(workflowDir, 'theme_config.json'), JSON.stringify({ name: 'Disabled' }));
    fs.writeFileSync(path.join(componentsDir, 'Component.jsx'), 'export default function Component() {}');
    
    const discovery = new WorkflowComponentDiscovery(tempDir, {
      generateWorkflowIndex: false
    });
    discovery.run();
    
    const workflowIndex = path.join(workflowDir, 'index.js');
    assert.ok(!fs.existsSync(workflowIndex), 'Workflow index should NOT exist when disabled');
  });
  
  it('should NOT generate workflow index for _shared', () => {
    const sharedDir = path.join(tempDir, '_shared');
    const componentsDir = path.join(sharedDir, 'components');
    fs.mkdirSync(componentsDir, { recursive: true });
    
    // Even with theme_config.json, _shared should not get workflow index
    fs.writeFileSync(path.join(sharedDir, 'theme_config.json'), JSON.stringify({ name: 'Shared' }));
    fs.writeFileSync(path.join(componentsDir, 'SharedComponent.jsx'), 'export default function SharedComponent() {}');
    
    const discovery = new WorkflowComponentDiscovery(tempDir);
    discovery.run();
    
    const workflowIndex = path.join(sharedDir, 'index.js');
    assert.ok(!fs.existsSync(workflowIndex), '_shared should NOT have workflow index');
  });
});
